using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Claim;

internal sealed class ClaimApp : IMiniGame
{
    private const string GameId = "claim";
    private const ulong IdleSeed = 0x434C41494DUL;
    private const ulong PilotSeed = 0x50494C4F54UL;
    private const float RevealSpeed = 230f;
    private const float Settled = 100000f;
    private const float BannerSeconds = 1.6f;
    private const float FieldGap = 8f;
    private const int HugePercent = 12;
    private const float FuseTickSeconds = 0.22f;
    private const float ClearFadeSeconds = 0.9f;
    private const float CapsulePadX = 10f;
    private const float IconSize = 11f;
    private const float IconGap = 5f;
    private const float BarGap = 7f;
    private const float BarWidth = 40f;
    private const float BarHeight = 4f;
    private const float RibbonWidth = 1.3f;
    private const float SparkRate = 36f;
    private const float DrawRate = 70f;
    private const float FuseRate = 60f;
    private static readonly GameSpec StageSpec = new(GameId, L.Claim.Title, GameGenre.Arcade, L.Claim.Hook,
        Backdrop.Neon, HudStyle.Standard, ScoreKind.Score, clocked: true, countdown: true, keyboard: true);
    private static readonly Rect FieldWorld = new(Vector2.Zero, new Vector2(ClaimBoard.Width, ClaimBoard.Height));
    private static readonly Vector4 Smoke = new(0.55f, 0.55f, 0.6f, 0.6f);
    private static readonly ParticleSpec DeathShards = new(ClaimRenderer.Danger, ClaimRenderer.FuseColor with { W = 0f },
        2.4f, 55f, 0.9f, 40f, 1.6f, 9f, shape: ParticleShape.Shard);
    private static readonly ParticleSpec DeathGlow = new(ClaimRenderer.White, ClaimRenderer.Danger with { W = 0f },
        3.6f, 22f, 0.5f, shape: ParticleShape.GlowCircle, additive: true);
    private static readonly ParticleSpec TrailSparks = new(ClaimRenderer.Danger, ClaimRenderer.FuseColor with { W = 0f },
        1.2f, 16f, 0.5f, 10f, 2f, shape: ParticleShape.Spark);
    private static readonly ParticleSpec SparkTrail = new(ClaimRenderer.SparkColor, ClaimRenderer.FuseColor with { W = 0f },
        0.9f, 5f, 0.3f, shape: ParticleShape.Spark, additive: true);
    private static readonly ParticleSpec FuseEmbers = new(ClaimRenderer.SparkColor, ClaimRenderer.FuseColor with { W = 0f },
        1f, 14f, 0.4f, 20f, 2f, shape: ParticleShape.Spark, additive: true);
    private static readonly ParticleSpec SmokePuff = new(Smoke, Smoke with { W = 0f }, 2.2f, 6f, 0.7f,
        curve: SizeCurve.Grow);
    private static readonly ParticleSpec SlowSparks = new(ClaimRenderer.SlowColor, ClaimRenderer.SlowColor with { W = 0f },
        0.9f, 9f, 0.35f, shape: ParticleShape.Spark, additive: true);

    private readonly ClaimBoard board = new();
    private readonly ClaimRenderer renderer = new();
    private readonly ClaimMosaic mosaic = new();
    private readonly ParticleSystem particles = new(640);
    private readonly FeedbackFx fx = new();
    private readonly Ribbon ribbon = new();
    private readonly Emitter[] sparkEmitters = new Emitter[ClaimBoard.MaxSparks];
    private readonly ParticleSpec claimShards;
    private readonly ParticleSpec claimSparkle;
    private readonly ParticleSpec fastSparks;
    private readonly ParticleSpec bossShards;
    private readonly Vector4[] confettiPalette;
    private Camera2D camera = Camera2D.Create();
    private ClaimPilot pilot = ClaimPilot.Create(PilotSeed);
    private Emitter drawEmitter;
    private Emitter fuseEmitter = new(FuseEmbers, FuseRate);
    private LabelSlot percentLabel;
    private LabelSlot levelLabel;
    private LabelSlot bonusLabel;
    private ClaimPadInput padInput;
    private ClaimMove keyMove;
    private string bannerText = string.Empty;
    private Vector4 bannerColor;
    private float bannerProgress = 1f;
    private float revealFront = Settled;
    private float fuseTick;
    private float time;
    private float entrance;
    private bool fastToggle;
    private bool slowToggle;
    private bool finished;

    public ClaimApp()
    {
        var accent = Accent;
        var light = GamePalette.Lighten(accent, 0.5f);
        claimShards = new ParticleSpec(light, accent with { W = 0f }, 2.2f, 40f, 0.75f, 30f, 1.8f, 8f,
            shape: ParticleShape.Shard);
        claimSparkle = new ParticleSpec(ClaimRenderer.White, accent with { W = 0f }, 1.6f, 26f, 0.8f, 0f, 2.2f, 6f,
            shape: ParticleShape.Star, additive: true);
        fastSparks = new ParticleSpec(light, accent with { W = 0f }, 0.9f, 9f, 0.35f, shape: ParticleShape.Spark,
            additive: true);
        bossShards = new ParticleSpec(ClaimRenderer.White, ClaimRenderer.BossColor(accent, 1f) with { W = 0f }, 2f,
            45f, 1f, 30f, 1.4f, 10f, shape: ParticleShape.Shard);
        confettiPalette = new Vector4[]
        {
            accent, light, ClaimRenderer.SlowColor, ClaimRenderer.White, ClaimRenderer.BossColor(accent, 0f),
            ClaimRenderer.BossColor(accent, 4.5f),
        };
        drawEmitter = new Emitter(fastSparks, DrawRate);
        for (var index = 0; index < sparkEmitters.Length; index++)
        {
            sparkEmitters[index] = new Emitter(SparkTrail, SparkRate);
        }

        BuildIdle();
    }

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        board.Reset(start.Random);
        mosaic.Build(board.MosaicSeed, ClaimBoard.Width, ClaimBoard.Height);
        ResetEffects();
        particles.Reseed(start.Seed);
        fastToggle = false;
        slowToggle = false;
        finished = false;
    }

    public void Close()
    {
        BuildIdle();
    }

    public void Dispose()
    {
    }

    public void OnQuit(GameSession session)
    {
        if (finished || board.Score <= 0)
        {
            return;
        }

        finished = true;
        session.Finish(Outcome());
    }

    public void DrawIdle(in GameContext context)
    {
        var scale = UiScale.Current;
        var deltaSeconds = context.RawDeltaSeconds;
        Animate(deltaSeconds);
        PlaceCamera(context, scale);
        pilot.Decide(board, deltaSeconds, out var move, out var draw);
        board.Step(deltaSeconds, move, draw);
        React(context, false, deltaSeconds);
        if (board.State == ClaimState.Over)
        {
            BuildIdle();
        }

        DrawWorld(ImGui.GetWindowDrawList(), context, scale);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        Animate(context.RawDeltaSeconds);
        bannerProgress = GameBanner.Advance(bannerProgress, context.RawDeltaSeconds, BannerSeconds);
        PlaceCamera(context, scale);
        var simDelta = fx.ScaleDelta(context.DeltaSeconds);
        if (!finished)
        {
            ReadInput(context, out var move, out var draw);
            board.Step(simDelta, move, draw);
            React(context, true, simDelta);
            if (board.State == ClaimState.Over)
            {
                finished = true;
                context.Session.Finish(Outcome());
            }
        }

        DrawWorld(drawList, context, scale);
        var band = StageLayout.PadBand(context.Full, StageLayout.DPadBand, scale);
        padInput = ClaimPad.Draw(drawList, band, Accent, ClaimRenderer.SlowColor, context.Theme, fastToggle, slowToggle,
            context.Session.State == StageFlow.Playing && !finished, Loc.T(L.Claim.Fast), Loc.T(L.Claim.Slow));
        ApplyToggles();
        if (bannerText.Length > 0)
        {
            var field = ClaimRenderer.FieldRect(in camera);
            GameBanner.Draw(drawList, new Vector2(field.Center.X, field.Min.Y + field.Height * 0.34f), bannerText,
                bannerColor, context.Theme, bannerProgress);
        }

        context.Hud.Score(board.Score);
        context.Hud.Lives(board.Lives, Math.Max(ClaimBoard.StartLives, board.Lives));
        context.Hud.Level(board.Level);
        context.Hud.Combo(board.Combo);
        context.Hud.Best(context.Session.Best);
        context.Hud.Custom(PercentCapsuleWidth(scale));
        if (context.Hud.CustomPlaced(0))
        {
            DrawPercentCapsule(drawList, context.Hud.CustomRect(0), context, scale);
        }

        context.Session.Report(board.Score);
    }

    private void BuildIdle()
    {
        board.Reset(GameRandom.FromSeed(IdleSeed));
        mosaic.Build(board.MosaicSeed, ClaimBoard.Width, ClaimBoard.Height);
        pilot = ClaimPilot.Create(PilotSeed);
        ResetEffects();
    }

    private void ResetEffects()
    {
        particles.Clear();
        fx.Clear();
        ribbon.Clear();
        camera = Camera2D.Create();
        drawEmitter.Reset();
        fuseEmitter.Reset();
        for (var index = 0; index < sparkEmitters.Length; index++)
        {
            sparkEmitters[index].Reset();
        }

        revealFront = Settled;
        bannerProgress = 1f;
        bannerText = string.Empty;
        keyMove = ClaimMove.None;
        padInput = default;
        entrance = 0f;
        fuseTick = 0f;
    }

    private void Animate(float deltaSeconds)
    {
        time += deltaSeconds;
        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);
        entrance = GameJuice.Advance(entrance, deltaSeconds, 0.9f);
        if (revealFront < Settled)
        {
            revealFront += RevealSpeed * deltaSeconds;
            if (revealFront > board.RevealDepth + ClaimRenderer.RevealBand)
            {
                revealFront = Settled;
            }
        }
    }

    private void PlaceCamera(in GameContext context, float scale)
    {
        var band = StageLayout.PadBand(context.Full, StageLayout.DPadBand, scale);
        var bottom = MathF.Max(context.Safe.Min.Y + 40f * scale, band.Min.Y - FieldGap * scale);
        var view = new Rect(context.Safe.Min, new Vector2(context.Safe.Max.X, bottom));
        camera.Fit(view, FieldWorld, FitMode.Contain);
        context.Fx.ApplyTo(ref camera);
        camera.Update(context.RawDeltaSeconds, scale);
        context.Backdrop.SetCamera(in camera);
    }

    private void ReadInput(in GameContext context, out ClaimMove move, out ClaimDraw draw)
    {
        move = ClaimMove.None;
        draw = ClaimDraw.None;
        if (context.Session.State != StageFlow.Playing)
        {
            keyMove = ClaimMove.None;
            return;
        }

        TrackKey(ImGuiKey.W, ImGuiKey.UpArrow, ClaimMove.Up);
        TrackKey(ImGuiKey.S, ImGuiKey.DownArrow, ClaimMove.Down);
        TrackKey(ImGuiKey.A, ImGuiKey.LeftArrow, ClaimMove.Left);
        TrackKey(ImGuiKey.D, ImGuiKey.RightArrow, ClaimMove.Right);
        if (keyMove != ClaimMove.None && !KeyHeld(keyMove))
        {
            keyMove = FirstHeld();
        }

        move = padInput.Move != ClaimMove.None ? padInput.Move : keyMove;
        var slowKey = GameInput.Held(ImGuiKey.LeftShift, ImGuiKey.RightShift) || GameInput.Held(ImGuiKey.K);
        var fastKey = GameInput.Held(ImGuiKey.Space, ImGuiKey.J);
        if (slowKey)
        {
            draw = ClaimDraw.Slow;
        }
        else if (fastKey)
        {
            draw = ClaimDraw.Fast;
        }
        else if (slowToggle)
        {
            draw = ClaimDraw.Slow;
        }
        else if (fastToggle)
        {
            draw = ClaimDraw.Fast;
        }
    }

    private void TrackKey(ImGuiKey key, ImGuiKey alternate, ClaimMove direction)
    {
        if (GameInput.Pressed(key, alternate))
        {
            keyMove = direction;
        }
    }

    private static bool KeyHeld(ClaimMove direction) => direction switch
    {
        ClaimMove.Up => GameInput.Held(ImGuiKey.W, ImGuiKey.UpArrow),
        ClaimMove.Down => GameInput.Held(ImGuiKey.S, ImGuiKey.DownArrow),
        ClaimMove.Left => GameInput.Held(ImGuiKey.A, ImGuiKey.LeftArrow),
        ClaimMove.Right => GameInput.Held(ImGuiKey.D, ImGuiKey.RightArrow),
        _ => false,
    };

    private static ClaimMove FirstHeld()
    {
        for (var direction = ClaimMove.Up; direction <= ClaimMove.Left; direction++)
        {
            if (KeyHeld(direction))
            {
                return direction;
            }
        }

        return ClaimMove.None;
    }

    private void ApplyToggles()
    {
        if (padInput.FastTapped)
        {
            fastToggle = !fastToggle;
            slowToggle = false;
        }

        if (!padInput.SlowTapped)
        {
            return;
        }

        slowToggle = !slowToggle;
        fastToggle = false;
    }

    private void React(in GameContext context, bool live, float deltaSeconds)
    {
        var accent = Accent;
        if (board.LevelStartedThisStep)
        {
            mosaic.Build(board.MosaicSeed, ClaimBoard.Width, ClaimBoard.Height);
            revealFront = Settled;
            entrance = 0f;
            ribbon.Clear();
            if (live)
            {
                ShowBanner(levelLabel.Get(L.Claim.LevelBanner, board.Level), accent);
                UiFeedback.Play(UiSound.GameTick);
            }
        }

        if (board.LineStartedThisStep)
        {
            ribbon.Clear();
            if (live)
            {
                UiFeedback.Play(UiSound.GamePiece);
            }
        }

        if (board.ClaimedThisStep > 0)
        {
            OnClaim(context, accent, live);
        }

        if (board.FuseLitThisStep && live)
        {
            UiFeedback.Play(UiSound.GameWrong);
            fuseTick = 0f;
        }

        if (board.FuseLit)
        {
            fuseEmitter.Advance(deltaSeconds, board.FusePosition, particles);
            fuseTick -= deltaSeconds;
            if (live && fuseTick <= 0f)
            {
                fuseTick = FuseTickSeconds;
                UiFeedback.Play(UiSound.GameTick);
                context.Fx.Vignette(ClaimRenderer.FuseColor, 0.22f, FuseTickSeconds);
            }
        }

        if (board.FuseOutThisStep)
        {
            particles.Emit(SmokePuff, board.FusePosition, 8);
        }

        if (board.DeathThisStep != ClaimDeath.None)
        {
            OnDeath(context, live);
        }

        if (board.RespawnedThisStep)
        {
            ribbon.Clear();
            fx.Shockwave(camera.ToScreen(board.PlayerPosition), camera.Px(9f), accent, 0.5f, 2.4f);
            particles.Emit(claimSparkle, board.PlayerPosition, 10);
        }

        if (board.ClearedThisStep)
        {
            OnCleared(context, accent, live);
        }

        if (board.LifeGainedThisStep && live)
        {
            UiFeedback.Play(UiSound.GamePowerUp);
            fx.AddText(Loc.T(L.Claim.ExtraLife),
                camera.ToScreen(board.PlayerPosition) - new Vector2(0f, 30f * UiScale.Current), ClaimRenderer.SlowColor,
                1.1f);
        }

        if (board.Drawing && board.Moving)
        {
            drawEmitter.Spec = board.LineSlow ? SlowSparks : fastSparks;
            drawEmitter.Advance(deltaSeconds, board.PlayerPosition, particles);
        }
        else if (!board.Drawing && board.State == ClaimState.Playing && deltaSeconds > 0f)
        {
            ribbon.Push(board.PlayerPosition);
        }

        if (board.State == ClaimState.Playing)
        {
            for (var index = 0; index < board.SparkCount; index++)
            {
                sparkEmitters[index].Advance(deltaSeconds, board.SparkPosition(index), particles);
            }
        }
    }

    private void OnClaim(in GameContext context, Vector4 accent, bool live)
    {
        var percent = ClaimBoard.PercentOf(board.ClaimedThisStep);
        var center = board.ClaimCenterThisStep;
        var screen = camera.ToScreen(center);
        revealFront = 0f;
        ribbon.Clear();
        var shards = Math.Clamp(8 + percent * 2, 8, 44);
        particles.Emit(claimShards, center, shards);
        particles.Emit(claimSparkle, center, shards / 2);
        fx.Shockwave(screen, camera.Px(14f + percent * 2.4f), GamePalette.Lighten(accent, 0.4f), 0.55f, 3f);
        if (!live)
        {
            return;
        }

        var color = board.ClaimSlowThisStep ? ClaimRenderer.SlowColor : GamePalette.Lighten(accent, 0.3f);
        fx.AddText(GameNumber.Signed(board.PointsThisStep), screen, color, 1.1f + MathF.Min(0.6f, percent * 0.03f));
        if (board.ClaimSlowThisStep)
        {
            fx.AddText(Loc.T(L.Claim.Double), screen + new Vector2(0f, 22f * UiScale.Current), ClaimRenderer.SlowColor,
                0.9f);
        }

        UiFeedback.Play(percent >= HugePercent ? UiSound.GameClear : UiSound.GameMatch);
        camera.Punch(0.03f + MathF.Min(0.06f, percent * 0.004f));
        if (board.ComboTierUpThisStep)
        {
            GameSfx.ComboTierUp();
        }

        if (percent < HugePercent || board.ClearedThisStep)
        {
            return;
        }

        ShowBanner(Loc.T(L.Claim.Huge), GamePalette.Lighten(accent, 0.3f));
        context.Fx.Sweep();
        context.Fx.Flash(GamePalette.Lighten(accent, 0.5f), 0.14f);
    }

    private void OnDeath(in GameContext context, bool live)
    {
        var position = board.DeathPosition;
        particles.Emit(DeathShards, position, 26);
        particles.Emit(DeathGlow, position, 8);
        var trail = board.Trail;
        for (var index = 0; index < trail.Length; index += 5)
        {
            particles.Emit(TrailSparks, ClaimBoard.CellCenter(trail[index]), 1);
        }

        fx.Shockwave(camera.ToScreen(position), camera.Px(16f), ClaimRenderer.Danger, 0.6f, 3.4f);
        camera.Shake(0.6f);
        ribbon.Clear();
        if (!live)
        {
            return;
        }

        UiFeedback.Play(UiSound.GameExplosion);
        context.Fx.Flash(ClaimRenderer.Danger, 0.35f);
        context.Fx.SlowMo(0.45f, 0.4f);
        var reason = board.DeathThisStep switch
        {
            ClaimDeath.Boss => L.Claim.CaughtBoss,
            ClaimDeath.Fuse => L.Claim.CaughtFuse,
            _ => L.Claim.CaughtSpark,
        };
        ShowBanner(Loc.T(board.Lives == 1 ? L.Claim.LastLife : reason), ClaimRenderer.Danger);
        if (board.Lives == 1)
        {
            context.Fx.Vignette(ClaimRenderer.Danger, 0.45f, 1.4f);
        }
    }

    private void OnCleared(in GameContext context, Vector4 accent, bool live)
    {
        particles.Emit(bossShards, board.BossHead, 18);
        particles.Emit(bossShards, board.BossTail, 18);
        particles.Confetti(new Vector2(ClaimBoard.Width * 0.5f, ClaimBoard.Height * 0.3f), 70, confettiPalette, 70f,
            1.8f, 1.5f, 70f);
        if (!live)
        {
            return;
        }

        GameSfx.LevelClear();
        context.Fx.Sweep();
        context.Fx.Flash(GamePalette.Lighten(accent, 0.5f), 0.2f);
        camera.Punch(0.08f);
        ShowBanner(Loc.T(L.Claim.Cleared), GamePalette.Lighten(accent, 0.3f));
        var field = ClaimRenderer.FieldRect(in camera);
        fx.AddText(bonusLabel.Get(L.Stage.Plus, board.ClearBonusThisStep),
            new Vector2(field.Center.X, field.Min.Y + field.Height * 0.46f), ClaimRenderer.SlowColor, 1.5f);
    }

    private void ShowBanner(string text, Vector4 color)
    {
        bannerText = text;
        bannerColor = color;
        bannerProgress = 0f;
    }

    private void DrawWorld(ImDrawListPtr drawList, in GameContext context, float scale)
    {
        var accent = Accent;
        var field = ClaimRenderer.FieldRect(in camera);
        BoardPlate.Draw(drawList, BoardPlate.Around(field, scale), BoardPlate.Radius * scale, scale, accent,
            context.Backdrop.Ink);
        drawList.PushClipRect(field.Min, field.Max, true);
        ClaimRenderer.DrawPicture(drawList, mosaic, in camera, accent, time, scale);
        var clearFade = board.State == ClaimState.Cleared
            ? Math.Clamp((ClaimBoard.ClearSeconds - board.StateLeft) / ClearFadeSeconds, 0f, 1f)
            : 0f;
        renderer.DrawCover(drawList, board, in camera, accent, revealFront, 1f - clearFade);
        renderer.DrawWave(drawList, board, in camera, accent, revealFront);
        drawList.PopClipRect();
        var edgeAlpha = Easing.Clamp01(entrance) * (1f - clearFade * 0.6f);
        ClaimRenderer.DrawEdges(drawList, board, in camera, accent, edgeAlpha, scale);
        renderer.DrawTrail(drawList, board, in camera, accent, board.State == ClaimState.Dying, scale);
        ClaimRenderer.DrawBoss(drawList, board, in camera, accent, 1f - clearFade);
        if (board.State != ClaimState.Cleared)
        {
            ClaimRenderer.DrawSparks(drawList, board, in camera, time, scale);
        }

        ribbon.Draw(drawList, in camera, accent with { W = 0.55f }, camera.Px(RibbonWidth), additive: true);
        ClaimRenderer.DrawPlayer(drawList, board, in camera, accent, time, scale);
        particles.Draw(drawList, in camera);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
    }

    private float PercentCapsuleWidth(float scale)
    {
        var label = percentLabel.Get(L.Claim.Percent, board.Percent);
        var text = Typography.Measure(label, TextStyles.FootnoteEmphasized).X / scale;
        return CapsulePadX * 2f + IconSize + IconGap + text + BarGap + BarWidth;
    }

    private void DrawPercentCapsule(ImDrawListPtr drawList, Rect rect, in GameContext context, float scale)
    {
        if (rect.Width <= 0f)
        {
            return;
        }

        var accent = Accent;
        var fraction = Math.Clamp(board.PercentExact / ClaimBoard.TargetPercent, 0f, 1f);
        var close = fraction >= 0.8f;
        StageHud.Capsule(drawList, rect, scale);
        var iconSize = IconSize * scale;
        var left = rect.Min.X + CapsulePadX * scale;
        var iconColor = close ? ClaimRenderer.SlowColor : accent;
        ProgressRing.CenterIcon(drawList, new Vector2(left + iconSize * 0.5f, rect.Center.Y), FontAwesomeIcon.Gem,
            iconColor, iconSize);
        var label = percentLabel.Get(L.Claim.Percent, board.Percent);
        var style = TextStyles.FootnoteEmphasized;
        var textX = left + iconSize + IconGap * scale;
        Typography.Draw(drawList, new Vector2(textX, rect.Center.Y - Typography.LineHeight(style) * 0.5f), label,
            StageInks.Strong, style);
        var barRight = rect.Max.X - CapsulePadX * scale;
        var barLeft = barRight - BarWidth * scale;
        var barHalf = BarHeight * scale * 0.5f;
        var barMin = new Vector2(barLeft, rect.Center.Y - barHalf);
        var barMax = new Vector2(barRight, rect.Center.Y + barHalf);
        drawList.AddRectFilled(barMin, barMax, ImGui.GetColorU32(accent with { W = 0.2f }), barHalf);
        if (fraction > 0f)
        {
            var fill = close ? ClaimRenderer.SlowColor : accent;
            drawList.AddRectFilled(barMin, new Vector2(barLeft + (barRight - barLeft) * fraction, barMax.Y),
                ImGui.GetColorU32(fill), barHalf);
        }

        if (close && board.State == ClaimState.Playing)
        {
            ProgressRing.Glow(new Vector2(barRight, rect.Center.Y), barHalf * 4f, ClaimRenderer.SlowColor,
                0.4f + 0.4f * Pulse.Wave(Pulse.Fast));
        }
    }

    private GameOutcome Outcome() =>
        new GameOutcome(board.Score, ScoreKind.Score, GameId)
            .WithStat(L.Games.Level, GameNumber.Label(board.Level))
            .WithStat(L.Claim.BestClaim,
                Loc.T(L.Claim.Percent, GameNumber.Label(ClaimBoard.PercentOf(board.BestClaimCells))))
            .WithStat(L.Claim.SlowLines, GameNumber.Label(board.SlowLines))
            .WithStat(L.Games.Time, TimeText.MinutesSeconds((int)board.PlaySeconds));
}
