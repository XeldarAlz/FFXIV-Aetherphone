using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Slice;

internal sealed class SliceApp : IMiniGame
{
    internal const string ArcadeStatId = "slice.arcade";
    private const string GameId = "slice";
    private const string BladeSurfaceId = "slice.blade";
    private const int ArcadeMode = 1;
    private const ulong IdleSeed = 0x534C494345UL;
    private const int SplatCapacity = 24;
    private const float ResultDelaySeconds = 0.9f;
    private const float BannerSeconds = 1.3f;
    private const float UrgentSeconds = 10f;
    private const float BladeCoreWidth = 0.08f;
    private const float BladeGlowWidth = 0.22f;
    private const float BladeHeadRadius = 0.07f;
    private const float SplatScale = 0.85f;
    private const float FrostRate = 16f;
    private const float BigComboSize = 5f;
    private static readonly LocString[] Modes = { L.Games.Classic, L.Slice.Arcade };
    private static readonly string[] ModeStatIds = { GameId, ArcadeStatId };
    private static readonly GameSpec StageSpec = new(GameId, L.Slice.Title, GameGenre.Arcade, L.Slice.Hook,
        Backdrop.Nebula, HudStyle.Standard, ScoreKind.Score, Modes, ModeStatIds, clocked: true, countdown: true, landscape: true);
    private static readonly Rect WorldRect = new(Vector2.Zero, new Vector2(SliceBoard.WorldWidth, SliceBoard.WorldHeight));
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 WhiteHot = new(1f, 0.97f, 0.9f, 1f);
    private static readonly Vector4 Danger = new(0.95f, 0.30f, 0.30f, 1f);
    private static readonly Vector4 Flame = new(1f, 0.62f, 0.26f, 1f);
    private static readonly Vector4 Smoke = new(0.32f, 0.30f, 0.36f, 0.75f);
    private static readonly Vector4 Gold = new(1f, 0.84f, 0.36f, 1f);
    private static readonly ParticleSpec Embers = new(Flame, Danger with { W = 0f }, 0.16f, 7.5f, 0.8f, 6f, 1.3f, 9f,
        shape: ParticleShape.Shard);
    private static readonly ParticleSpec BombSmoke = new(Smoke, Smoke with { W = 0f }, 0.5f, 2.6f, 1f, -1.5f, 1.6f,
        curve: SizeCurve.Grow);
    private static readonly ParticleSpec BombSparks = new(SliceRenderer.Spark, Flame with { W = 0f }, 0.1f, 11f, 0.5f,
        4f, 1.4f, shape: ParticleShape.Streak);
    private static readonly ParticleSpec ComboStars = new(White, Gold with { W = 0f }, 0.14f, 3.6f, 0.8f, 1.5f, 2.2f,
        6f, shape: ParticleShape.Star, additive: true);
    private static readonly ParticleSpec MissMark = new(Danger, Danger with { W = 0f }, 0.32f, 1.6f, 0.9f, -2f, 1.2f,
        spread: 0.6f, direction: -MathF.PI * 0.5f, shape: ParticleShape.Glyph, glyph: 'X');
    private static readonly ParticleSpec Frost = new(White, SliceRenderer.FreezeColor with { W = 0f }, 0.09f, 0.6f,
        1.4f, 0.8f, 1f, 3f, shape: ParticleShape.Star);
    private static readonly ParticleSpec SlashFlare = new(White, WhiteHot with { W = 0f }, 0.08f, 14f, 0.16f, 0f, 0f,
        shape: ParticleShape.Streak, additive: true);
    private readonly SliceBoard board = new();
    private readonly ParticleSystem particles = new(512);
    private readonly FeedbackFx fx = new();
    private readonly Ribbon ribbon = new();
    private readonly SliceSplat[] splats = new SliceSplat[SplatCapacity];
    private Camera2D camera = Camera2D.Create();
    private GameRandom effects = GameRandom.Fresh();
    private Emitter frost = new(Frost, FrostRate);
    private LabelSlot comboLabel;
    private LabelSlot penaltyLabel;
    private LocString bannerText = L.Slice.Arcade;
    private Vector4 bannerColor = White;
    private int splatCursor;
    private int lastMultiplier = 1;
    private float bannerProgress = 1f;
    private float resultDelay;
    private float time;
    private bool bladeActive;
    private bool finished;

    public SliceApp()
    {
        BuildIdle();
    }

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        board.Reset(start.Random, start.Mode == ArcadeMode ? SliceMode.Arcade : SliceMode.Classic);
        effects = GameRandom.FromSeed(start.Seed ^ IdleSeed);
        particles.Clear();
        particles.Reseed(start.Seed);
        fx.Clear();
        ribbon.Clear();
        ClearSplats();
        frost.Reset();
        camera = Camera2D.Create();
        bannerProgress = 1f;
        resultDelay = ResultDelaySeconds;
        lastMultiplier = 1;
        bladeActive = false;
        finished = false;
    }

    public void Close()
    {
        BuildIdle();
        particles.Clear();
        fx.Clear();
        ribbon.Clear();
        ClearSplats();
    }

    public void Dispose()
    {
    }

    public void OnQuit(GameSession session)
    {
        if (finished || board.Mode == SliceMode.Preview || board.Score <= 0)
        {
            return;
        }

        finished = true;
        session.Finish(Outcome(session));
    }

    public void DrawIdle(in GameContext context)
    {
        var deltaSeconds = context.RawDeltaSeconds;
        time += deltaSeconds;
        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);
        AgeSplats(deltaSeconds);
        PlaceCamera(context);
        board.BeginFrame();
        var blade = IdleBlade(time);
        board.MoveBlade(blade, true, deltaSeconds);
        if (deltaSeconds > 0f)
        {
            ribbon.Push(blade);
        }

        board.Step(deltaSeconds);
        React(context, false);
        DrawWorld(ImGui.GetWindowDrawList(), UiScale.Current);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        time += context.RawDeltaSeconds;
        particles.Update(context.RawDeltaSeconds);
        fx.Update(context.RawDeltaSeconds);
        AgeSplats(context.RawDeltaSeconds);
        bannerProgress = GameBanner.Advance(bannerProgress, context.RawDeltaSeconds, BannerSeconds);
        PlaceCamera(context);
        board.BeginFrame();
        var simDelta = fx.ScaleDelta(context.DeltaSeconds);
        if (!finished)
        {
            Step(simDelta, context);
        }

        AmbientFx(context);
        DrawWorld(drawList, scale);
        GameBanner.Draw(drawList, camera.ToScreen(new Vector2(SliceBoard.WorldWidth * 0.5f, SliceBoard.WorldHeight * 0.36f)),
            Loc.T(bannerText), bannerColor, context.Theme, bannerProgress);
        FillHud(drawList, context, scale);
        context.Session.Report(board.Score);
    }

    private void BuildIdle()
    {
        board.Reset(GameRandom.FromSeed(IdleSeed), SliceMode.Preview);
        ribbon.Clear();
        bladeActive = false;
        time = 0f;
    }

    private static Vector2 IdleBlade(float seconds) =>
        new(SliceBoard.WorldWidth * 0.5f + 6f * MathF.Sin(seconds * 1.9f),
            4.6f + 1.8f * MathF.Sin(seconds * 2.7f + 0.8f));

    private void PlaceCamera(in GameContext context)
    {
        camera.Fit(context.Full, WorldRect, FitMode.Contain);
        context.Fx.ApplyTo(ref camera);
        camera.Update(context.RawDeltaSeconds, UiScale.Current);
        context.Backdrop.SetCamera(in camera);
    }

    private void Step(float deltaSeconds, in GameContext context)
    {
        HandleBlade(deltaSeconds, context);
        board.Step(deltaSeconds);
        React(context, true);
        if (board.State != SliceState.Over)
        {
            return;
        }

        resultDelay -= context.DeltaSeconds;
        if (resultDelay > 0f)
        {
            return;
        }

        finished = true;
        context.Session.Finish(Outcome(context.Session));
    }

    private GameOutcome Outcome(GameSession session)
    {
        var outcome = new GameOutcome(board.Score, ScoreKind.Score, board.Arcade ? ArcadeStatId : GameId)
            .WithStat(L.Slice.Sliced, GameNumber.Label(board.Sliced))
            .WithStat(L.Slice.BestSwipe, GameNumber.Label(board.BestSwipe))
            .WithStat(L.Games.Combo, GameNumber.Label(board.BestStreak));
        return board.Arcade
            ? outcome.WithStat(L.Slice.Bombs, GameNumber.Label(board.BombsHit))
            : outcome.WithStat(L.Games.Time, TimeText.MinutesSeconds((int)session.PlaySeconds));
    }

    private void HandleBlade(float deltaSeconds, in GameContext context)
    {
        var mouse = ImGui.GetMousePos();
        if (context.Session.State != StageFlow.Playing || board.State != SliceState.Playing)
        {
            ReleaseBlade(mouse, deltaSeconds);
            return;
        }

        var scale = UiScale.Current;
        var full = context.Full;
        var surface = new Rect(new Vector2(full.Min.X, full.Min.Y + StageLayout.ChromeBand * scale), full.Max);
        PressSurface.Claim(BladeSurfaceId, surface, out var activated);
        if (activated && !context.ChromeHit(mouse))
        {
            bladeActive = true;
            ribbon.Clear();
        }

        if (!bladeActive || !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            ReleaseBlade(mouse, deltaSeconds);
            return;
        }

        var point = camera.ToWorld(mouse);
        board.MoveBlade(point, true, deltaSeconds);
        if (context.RawDeltaSeconds > 0f)
        {
            ribbon.Push(point);
        }
    }

    private void ReleaseBlade(Vector2 mouse, float deltaSeconds)
    {
        if (!bladeActive && !board.BladeDown)
        {
            return;
        }

        bladeActive = false;
        board.MoveBlade(camera.ToWorld(mouse), false, deltaSeconds);
        ribbon.Clear();
    }

    private void React(in GameContext context, bool live)
    {
        for (var index = 0; index < board.HitCount; index++)
        {
            OnHit(board.Hit(index), live);
        }

        if (board.HitCount > 0 && live)
        {
            PlayHitSound(board.Hit(0).Kind);
            camera.Punch(0.012f + 0.004f * Math.Min(board.HitCount, 4));
            camera.Shake(0.05f);
            if (board.Combo.Multiplier > lastMultiplier)
            {
                GameSfx.ComboTierUp();
            }
        }

        lastMultiplier = board.Combo.Multiplier;
        if (board.ComboThisFrame > 0)
        {
            OnCombo(context, live);
        }

        if (!live)
        {
            return;
        }

        if (board.PickupActivated)
        {
            OnPickup(context, board.PickupThisFrame);
        }

        for (var index = 0; index < board.MissCount; index++)
        {
            OnMiss(board.Miss(index));
        }

        if (board.LifeLostThisFrame)
        {
            OnLifeLost(context);
        }

        if (board.BombThisFrame)
        {
            OnBomb(context);
        }

        if (board.TimeUpThisFrame)
        {
            UiFeedback.Play(UiSound.GameClear);
            ShowBanner(L.Slice.TimeUp, GamePalette.Lighten(Accent, 0.3f));
            context.Fx.Flash(White, 0.16f);
        }
    }

    private void OnHit(in SliceHit hit, bool live)
    {
        var juice = SliceRenderer.JuiceOf(hit.Kind, hit.Tint);
        var normalAngle = MathF.Atan2(hit.Direction.X, -hit.Direction.Y);
        var drops = new ParticleSpec(juice, juice with { W = 0f }, 0.1f, 5.5f, 0.6f, 12f, 1.1f, spread: 1.3f);
        particles.Emit(drops.WithDirection(normalAngle, 1.3f), hit.Position, 7);
        particles.Emit(drops.WithDirection(normalAngle + MathF.PI, 1.3f), hit.Position, 7);
        var swipeAngle = MathF.Atan2(hit.Direction.Y, hit.Direction.X);
        var mist = new ParticleSpec(WhiteHot, juice with { W = 0f }, 0.07f, 3.6f, 0.35f, 2f, 2.4f,
            shape: ParticleShape.Spark, additive: true);
        particles.Emit(mist.WithDirection(swipeAngle, 0.7f), hit.Position, 5);
        particles.Emit(SlashFlare.WithDirection(swipeAngle, 0.04f), hit.Position, 2);
        switch (hit.Kind)
        {
            case SliceKind.Crystal:
                particles.Emit(new ParticleSpec(GamePalette.Lighten(juice, 0.35f), juice with { W = 0f }, 0.12f, 5f,
                    0.7f, 10f, 1.2f, 10f, shape: ParticleShape.Shard), hit.Position, 8);
                break;
            case SliceKind.Egg:
                particles.Emit(new ParticleSpec(SliceRenderer.EggShell, SliceRenderer.EggShell with { W = 0f }, 0.11f,
                    4.2f, 0.7f, 11f, 1.2f, 9f, shape: ParticleShape.Shard), hit.Position, 7);
                break;
            case SliceKind.Moogle:
                particles.Emit(new ParticleSpec(White, SliceRenderer.MoogleFur with { W = 0f }, 0.14f, 2.6f, 1f, 1.5f,
                    2.4f, shape: ParticleShape.GlowCircle, curve: SizeCurve.Pulse), hit.Position, 9);
                break;
            default:
                particles.Emit(new ParticleSpec(White, juice with { W = 0f }, 0.13f, 3.8f, 0.8f, 2f, 2.2f, 6f,
                    shape: ParticleShape.Star, additive: true), hit.Position, 14);
                break;
        }

        AddSplat(hit.Position, juice, SliceBoard.RadiusOf(hit.Kind) * SplatScale);
        if (!live)
        {
            return;
        }

        var screen = camera.ToScreen(hit.Position);
        fx.AddText(GameNumber.Signed(hit.Points), screen, board.DoubleLeft > 0f ? Gold : GamePalette.Lighten(juice, 0.2f),
            0.95f);
    }

    private static void PlayHitSound(SliceKind kind)
    {
        switch (kind)
        {
            case SliceKind.Crystal:
                UiFeedback.Play(UiSound.GameBreak);
                return;
            case SliceKind.Egg:
                UiFeedback.Play(UiSound.GameHitSoft);
                return;
            case SliceKind.Moogle:
                UiFeedback.Play(UiSound.GamePop);
                return;
            default:
                UiFeedback.Play(UiSound.GameCollect);
                return;
        }
    }

    private void OnCombo(in GameContext context, bool live)
    {
        var count = board.ComboThisFrame;
        particles.Emit(ComboStars, board.ComboPosition, 8 + count * 2);
        if (!live)
        {
            return;
        }

        var scale = UiScale.Current;
        var screen = camera.ToScreen(board.ComboPosition);
        UiFeedback.Play(UiSound.GameMatch);
        fx.AddText(comboLabel.Get(L.Slice.SwipeCombo, count), screen - new Vector2(0f, 30f * scale), Gold, 1.35f);
        fx.AddText(GameNumber.Signed(board.ComboBonusThisFrame), screen - new Vector2(0f, 6f * scale), White, 1.05f);
        fx.Shockwave(screen, camera.Px(1.6f + count * 0.15f), Gold with { W = 0.8f }, 0.45f, 3f);
        context.Fx.Punch(0.025f + 0.008f * count);
        context.Fx.Flash(White, 0.08f);
        if (count < BigComboSize)
        {
            return;
        }

        context.Fx.Sweep();
        context.Fx.SlowMo(0.6f, 0.22f);
    }

    private void OnPickup(in GameContext context, SliceKind kind)
    {
        var color = SliceRenderer.ColorOf(kind, 0);
        UiFeedback.Play(UiSound.GamePowerUp);
        context.Fx.Flash(color, 0.25f);
        context.Fx.Sweep();
        context.Fx.Punch(0.05f);
        ShowBanner(kind switch
        {
            SliceKind.Freeze => L.Slice.Freeze,
            SliceKind.Frenzy => L.Slice.Frenzy,
            _ => L.Slice.Double,
        }, color);
    }

    private void OnMiss(Vector2 position)
    {
        particles.Emit(MissMark, position - new Vector2(0f, 0.8f), 1);
        camera.Shake(0.14f);
        if (board.Classic)
        {
            UiFeedback.Play(UiSound.GameWrong);
        }
    }

    private void OnLifeLost(in GameContext context)
    {
        context.Fx.Vignette(Danger, 0.42f, 0.8f);
        context.Fx.Flash(Danger, 0.18f);
        camera.Shake(0.3f);
        if (board.Lives == 1)
        {
            ShowBanner(L.Slice.LastLife, Danger);
        }
    }

    private void OnBomb(in GameContext context)
    {
        var position = board.BombPosition;
        var screen = camera.ToScreen(position);
        UiFeedback.Play(UiSound.GameExplosion);
        particles.Emit(Embers, position, 26);
        particles.Emit(BombSmoke, position, 12);
        particles.Emit(BombSparks, position, 18);
        fx.Shockwave(screen, camera.Px(4f), Flame, 0.6f, 4f);
        fx.Shockwave(screen, camera.Px(2.2f), White, 0.35f, 3f);
        AddSplat(position, Smoke with { W = 1f }, 1.4f);
        camera.Shake(0.85f);
        context.Fx.Punch(0.08f);
        context.Fx.Flash(White, 0.7f);
        if (board.Classic)
        {
            context.Fx.SlowMo(0.3f, 0.7f);
            fx.HitStop(0.08f);
            return;
        }

        fx.AddText(penaltyLabel.Get(L.Slice.Penalty, SliceBoard.BombPenaltySeconds), screen, Danger, 1.45f);
        context.Fx.Vignette(Danger, 0.38f, 0.7f);
        context.Fx.SlowMo(0.5f, 0.25f);
    }

    private void AmbientFx(in GameContext context)
    {
        if (board.Mode == SliceMode.Preview || finished)
        {
            return;
        }

        if (board.FreezeLeft > 0f)
        {
            frost.Advance(context.RawDeltaSeconds,
                new Vector2(effects.Range(0.5f, SliceBoard.WorldWidth - 0.5f), effects.Range(1.5f, 7f)), particles);
        }

        var pulse = 0.5f + 0.5f * Pulse.Wave(Pulse.Fast);
        if (board.Arcade && board.TimeLeft <= UrgentSeconds && board.State == SliceState.Playing)
        {
            context.Fx.Vignette(Danger, 0.10f + 0.12f * pulse, 0.3f);
            return;
        }

        if (board.Classic && board.Lives == 1 && board.State == SliceState.Playing)
        {
            context.Fx.Vignette(Danger, 0.08f + 0.08f * pulse, 0.3f);
            return;
        }

        if (board.FreezeLeft > 0f)
        {
            context.Fx.Vignette(SliceRenderer.FreezeColor, 0.24f, 0.3f);
            return;
        }

        if (board.FrenzyLeft > 0f)
        {
            context.Fx.Vignette(SliceRenderer.FrenzyColor, 0.18f + 0.06f * pulse, 0.3f);
            return;
        }

        if (board.DoubleLeft > 0f)
        {
            context.Fx.Vignette(SliceRenderer.DoubleColor, 0.16f, 0.3f);
        }
    }

    private void ShowBanner(LocString text, Vector4 color)
    {
        bannerText = text;
        bannerColor = color;
        bannerProgress = 0f;
    }

    private void AddSplat(Vector2 position, Vector4 color, float size)
    {
        ref var splat = ref splats[splatCursor];
        splatCursor = (splatCursor + 1) % SplatCapacity;
        splat.Position = position;
        splat.Color = color;
        splat.Size = size;
        splat.Life = SliceRenderer.SplatSeconds;
        splat.Rotation = effects.Range(0f, MathF.Tau);
    }

    private void AgeSplats(float deltaSeconds)
    {
        for (var index = 0; index < SplatCapacity; index++)
        {
            splats[index].Life -= deltaSeconds;
        }
    }

    private void ClearSplats()
    {
        for (var index = 0; index < SplatCapacity; index++)
        {
            splats[index].Life = 0f;
        }

        splatCursor = 0;
    }

    private void DrawWorld(ImDrawListPtr drawList, float scale)
    {
        SliceRenderer.DrawSplats(drawList, in camera, splats);
        SliceRenderer.DrawHalves(drawList, in camera, board);
        SliceRenderer.DrawObjects(drawList, in camera, board, time);
        particles.Draw(drawList, in camera);
        DrawBlade(drawList);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
    }

    private void DrawBlade(ImDrawListPtr drawList)
    {
        if (ribbon.Count < 2)
        {
            return;
        }

        var sharp = board.BladeSharp ? 1f : 0.45f;
        var glow = Vector4.Lerp(GamePalette.Lighten(Accent, 0.3f), WhiteHot, board.Combo.Heat);
        ribbon.Draw(drawList, in camera, glow with { W = 0.55f * sharp }, camera.Px(BladeGlowWidth), additive: true);
        ribbon.Draw(drawList, in camera, White with { W = 0.95f * sharp }, camera.Px(BladeCoreWidth));
        var head = camera.ToScreen(ribbon.Point(0));
        var radius = camera.Px(BladeHeadRadius);
        ProgressRing.Glow(head, radius * 3f, glow, 0.6f * sharp);
        drawList.AddCircleFilled(head, radius, ImGui.GetColorU32(White with { W = sharp }), 12);
    }

    private void FillHud(ImDrawListPtr drawList, in GameContext context, float scale)
    {
        var hud = context.Hud;
        hud.Score(board.Score);
        if (board.Arcade)
        {
            hud.Timer(board.TimeLeft, SliceBoard.ArcadeSeconds, board.TimeLeft <= UrgentSeconds);
        }
        else
        {
            hud.Lives(board.Lives, SliceBoard.StartLives);
        }

        hud.Combo(board.Combo);
        hud.Best(context.Session.Best);
        if (board.Classic)
        {
            hud.Custom(SliceRenderer.MissMarksWidth);
            if (hud.CustomPlaced(0))
            {
                SliceRenderer.DrawMissMarks(drawList, hud.CustomRect(0), board.Misses, scale);
            }

            return;
        }

        if (!TryActivePower(out var kind, out var fraction))
        {
            return;
        }

        hud.Custom(SliceRenderer.PowerWidth);
        if (hud.CustomPlaced(0))
        {
            SliceRenderer.DrawPower(drawList, hud.CustomRect(0), kind, fraction, scale);
        }
    }

    private bool TryActivePower(out SliceKind kind, out float fraction)
    {
        if (board.FreezeLeft > 0f)
        {
            kind = SliceKind.Freeze;
            fraction = board.FreezeLeft / SliceBoard.FreezeSeconds;
            return true;
        }

        if (board.FrenzyLeft > 0f)
        {
            kind = SliceKind.Frenzy;
            fraction = board.FrenzyLeft / SliceBoard.FrenzySeconds;
            return true;
        }

        kind = SliceKind.Double;
        fraction = board.DoubleLeft / SliceBoard.DoubleSeconds;
        return board.DoubleLeft > 0f;
    }
}
