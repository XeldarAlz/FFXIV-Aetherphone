using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Drift;

internal sealed class DriftApp : IMiniGame
{
    private const string GameId = "drift";
    private const float PadInsetX = 12f;
    private const float PadInsetY = 6f;
    private const float PadOpacity = 0.92f;
    private const float WaveBannerSeconds = 1.6f;
    private const float ExhaustRate = 40f;
    private const float ExhaustWidth = 0.9f;
    private const float HeatRise = 6f;
    private const float HeatFall = 2.4f;
    private const float WrapJump = 20f;
    private const float IdleAimTolerance = 0.12f;
    private const float IdleFireTolerance = 0.3f;
    private const int IdleWaveLimit = 4;
    private const float SaucerTextMargin = 14f;
    private const ulong IdleSeed = 0x4452494654UL;
    private static readonly GameSpec StageSpec = new(GameId, L.Drift.Title, GameGenre.Action, L.Drift.Hook,
        Backdrop.Neon, HudStyle.Standard, ScoreKind.Score, clocked: true, countdown: true, keyboard: true);
    private static readonly Vector4 Danger = new(0.95f, 0.30f, 0.30f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Gold = new(1f, 0.84f, 0.36f, 1f);
    private static readonly Vector4 AccentColor = AppAccents.For(GameId);
    private static readonly ParticleSpec Exhaust = new(NeonStroke.Core(DriftRenderer.FlameColor),
        AccentColor with { W = 0f }, 0.5f, 16f, 0.32f, 0f, 2.2f, 0f, 0.5f, 0f, ParticleShape.Spark, SizeCurve.Shrink,
        true);
    private static readonly ParticleSpec Sparks = new(White, AccentColor with { W = 0f }, 0.35f, 30f, 0.45f, 0f, 2f,
        shape: ParticleShape.Spark, additive: true);
    private static readonly ParticleSpec Muzzle = new(White, AccentColor, 0.4f, 22f, 0.18f, 0f, 3f, 0f, 0.6f,
        shape: ParticleShape.Streak, additive: true);
    private static readonly ParticleSpec ShipDebris = new(NeonStroke.Core(AccentColor), AccentColor with { W = 0f },
        1.3f, 20f, 1.1f, 0f, 1.1f, 9f, shape: ParticleShape.Shard, additive: true);
    private static readonly ParticleSpec SaucerDebris = new(NeonStroke.Core(DriftRenderer.SaucerColor),
        DriftRenderer.SaucerColor with { W = 0f }, 1.2f, 26f, 0.9f, 0f, 1.2f, 10f, shape: ParticleShape.Shard,
        additive: true);
    private static readonly ParticleSpec WarpRing = new(AccentColor, AccentColor with { W = 0f }, 3.2f, 0f, 0.45f,
        shape: ParticleShape.Ring, curve: SizeCurve.Grow, additive: true);

    private readonly DriftBoard board = new();
    private readonly DriftBoard idleBoard = new();
    private readonly ParticleSystem particles = new(640);
    private readonly FeedbackFx fx = new();
    private readonly Ribbon exhaust = new();
    private Emitter exhaustEmitter = new(Exhaust, ExhaustRate);
    private Camera2D camera = Camera2D.Create();
    private DriftControls controls;
    private LabelSlot waveLabel;
    private Vector2 lastTail;
    private float exhaustHeat;
    private float bannerProgress = 1f;
    private float time;
    private string bannerText = string.Empty;
    private bool finished;
    private bool idleReady;

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AccentColor;

    public void Start(in GameStart start)
    {
        board.Reset(start.Random);
        particles.Clear();
        particles.Reseed(start.Seed);
        fx.Clear();
        exhaust.Clear();
        exhaustEmitter.Reset();
        controls = default;
        exhaustHeat = 0f;
        finished = false;
        ShowWaveBanner();
    }

    public void Close()
    {
        particles.Clear();
        exhaust.Clear();
    }

    public void Dispose()
    {
    }

    public void DrawIdle(in GameContext context)
    {
        if (!idleReady || idleBoard.GameOver || idleBoard.Wave > IdleWaveLimit)
        {
            idleBoard.Reset(GameRandom.FromSeed(IdleSeed));
            idleReady = true;
        }

        var scale = UiScale.Current;
        time += context.RawDeltaSeconds;
        PlaceCamera(context, scale);
        idleBoard.Step(context.RawDeltaSeconds, Autopilot(idleBoard));
        var drawList = ImGui.GetWindowDrawList();
        var world = DriftRenderer.WorldRect(in camera);
        DriftRenderer.DrawFrame(drawList, world, Accent, scale);
        drawList.PushClipRect(world.Min, world.Max, true);
        DriftRenderer.DrawWorld(drawList, idleBoard, in camera, Accent, scale, time);
        drawList.PopClipRect();
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var accent = Accent;
        time += context.RawDeltaSeconds;
        PlaceCamera(context, scale);
        var simDelta = fx.ScaleDelta(context.DeltaSeconds);
        if (!finished)
        {
            board.Step(simDelta, controls);
            TrailExhaust(simDelta);
            ReactToEvents(context, accent);
        }

        particles.Update(context.RawDeltaSeconds);
        fx.Update(context.RawDeltaSeconds);
        bannerProgress = GameBanner.Advance(bannerProgress, context.DeltaSeconds, WaveBannerSeconds);
        var world = DriftRenderer.WorldRect(in camera);
        DriftRenderer.DrawFrame(drawList, world, accent, scale);
        drawList.PushClipRect(world.Min, world.Max, true);
        exhaust.Draw(drawList, in camera, NeonStroke.Core(DriftRenderer.FlameColor) with { W = 0.75f * exhaustHeat },
            camera.Px(ExhaustWidth), true);
        DriftRenderer.DrawWorld(drawList, board, in camera, accent, scale, time);
        particles.Draw(drawList, in camera);
        drawList.PopClipRect();
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        var pad = DrawPad(drawList, context, accent, scale);
        controls = context.Session.State == StageFlow.Playing && !finished ? ReadControls(in pad) : default;
        GameBanner.Draw(drawList, camera.ToScreen(new Vector2(DriftBoard.Width * 0.5f, DriftBoard.Height * 0.38f)),
            bannerText, accent, context.Theme, bannerProgress);
        context.Hud.Score(board.Score);
        context.Hud.Lives(board.Lives, Math.Max(DriftBoard.StartLives, board.Lives));
        context.Hud.Level(board.Wave);
        context.Hud.Combo(board.Combo);
        context.Hud.Best(context.Session.Best);
        context.Session.Report(board.Score);
        if (!board.GameOver || finished)
        {
            return;
        }

        finished = true;
        Finish(context);
    }

    private void PlaceCamera(in GameContext context, float scale)
    {
        var full = context.Full;
        var band = StageLayout.PadBand(full, StageLayout.ShooterBand, scale);
        var view = new Rect(new Vector2(full.Min.X + StageLayout.SafeSide * scale, context.Safe.Min.Y),
            new Vector2(full.Max.X - StageLayout.SafeSide * scale, band.Min.Y));
        camera.Fit(view, DriftBoard.Width, DriftBoard.Height, FitMode.Contain);
        context.Fx.ApplyTo(ref camera);
        camera.Update(context.RawDeltaSeconds, scale);
        context.Backdrop.SetCamera(in camera);
    }

    private static DriftPadInput DrawPad(ImDrawListPtr drawList, in GameContext context, Vector4 accent, float scale)
    {
        var band = StageLayout.PadBand(context.Full, StageLayout.ShooterBand, scale);
        var panel = new Rect(new Vector2(band.Min.X + PadInsetX * scale, band.Min.Y + PadInsetY * scale),
            new Vector2(band.Max.X - PadInsetX * scale, band.Max.Y - PadInsetY * scale));
        Material.Frosted(drawList, panel.Min, panel.Max, Metrics.Radius.Lg * scale, scale, PadOpacity);
        return DriftPad.Draw(panel, accent);
    }

    private static DriftControls ReadControls(in DriftPadInput pad)
    {
        var left = pad.Left || GameInput.Held(ImGuiKey.A, ImGuiKey.LeftArrow);
        var right = pad.Right || GameInput.Held(ImGuiKey.D, ImGuiKey.RightArrow);
        var thrust = pad.Thrust || GameInput.Held(ImGuiKey.W, ImGuiKey.UpArrow);
        var fire = pad.Fire || GameInput.Held(ImGuiKey.Space);
        var warp = pad.Warp || GameInput.Pressed(ImGuiKey.S, ImGuiKey.DownArrow) || GameInput.Pressed(ImGuiKey.LeftShift);
        return new DriftControls((right ? 1f : 0f) - (left ? 1f : 0f), thrust, fire, warp);
    }

    private static DriftControls Autopilot(DriftBoard target)
    {
        if (target.State != ShipState.Flying || target.RockCount == 0)
        {
            return default;
        }

        var nearest = target.RockAt(0).Position;
        var nearestDistance = float.MaxValue;
        for (var index = 0; index < target.RockCount; index++)
        {
            var distance = DriftBoard.WrapDelta(target.ShipPosition, target.RockAt(index).Position).LengthSquared();
            if (distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = distance;
            nearest = target.RockAt(index).Position;
        }

        var toRock = DriftBoard.WrapDelta(target.ShipPosition, nearest);
        var desired = MathF.Atan2(toRock.X, -toRock.Y);
        var difference = DriftBoard.Wrap(desired - target.ShipAngle + MathF.PI, MathF.Tau) - MathF.PI;
        var turn = MathF.Abs(difference) < IdleAimTolerance ? 0f : MathF.Sign(difference);
        var thrust = nearestDistance > 900f && MathF.Abs(difference) < IdleFireTolerance;
        return new DriftControls(turn, thrust, MathF.Abs(difference) < IdleFireTolerance, false);
    }

    private void TrailExhaust(float deltaSeconds)
    {
        var flying = board.State == ShipState.Flying;
        exhaustHeat = board.Thrusting
            ? MathF.Min(1f, exhaustHeat + deltaSeconds * HeatRise)
            : MathF.Max(0f, exhaustHeat - deltaSeconds * HeatFall);
        if (!flying)
        {
            exhaust.Clear();
            return;
        }

        var heading = board.Heading;
        var tail = board.ShipPosition - heading * DriftBoard.ShipSize * 0.9f;
        if (Vector2.Distance(tail, lastTail) > WrapJump)
        {
            exhaust.Clear();
        }

        lastTail = tail;
        if (deltaSeconds > 0f)
        {
            exhaust.Push(tail);
        }

        if (!board.Thrusting)
        {
            return;
        }

        var backAngle = MathF.Atan2(-heading.Y, -heading.X);
        exhaustEmitter.Spec = Exhaust.WithDirection(backAngle, 0.5f);
        exhaustEmitter.Advance(deltaSeconds, tail, particles);
    }

    private void ReactToEvents(in GameContext context, Vector4 accent)
    {
        if (board.FiredCount > 0)
        {
            UiFeedback.Play(UiSound.GameShoot);
            var heading = board.Heading;
            particles.Emit(Muzzle.WithDirection(MathF.Atan2(heading.Y, heading.X), 0.6f),
                board.ShipPosition + heading * DriftBoard.ShipSize * 1.2f, 3);
        }

        for (var index = 0; index < board.BreakCount; index++)
        {
            OnRockBreak(board.BreakAt(index), context, accent);
        }

        if (board.ComboRaisedThisFrame)
        {
            GameSfx.ComboTierUp();
        }

        if (board.SaucerArrivedThisFrame)
        {
            UiFeedback.Play(UiSound.GameTick);
            var warning = new Vector2(Math.Clamp(board.SaucerPosition.X, SaucerTextMargin,
                DriftBoard.Width - SaucerTextMargin), board.SaucerPosition.Y);
            fx.AddText(Loc.T(L.Drift.Saucer), camera.ToScreen(warning), DriftRenderer.SaucerColor, 1f);
        }

        if (board.SaucerFiredThisFrame)
        {
            particles.Emit(Sparks, board.SaucerPosition, 3);
        }

        if (board.SaucerDownedThisFrame)
        {
            OnSaucerDown(context);
        }

        if (board.WarpStartedThisFrame)
        {
            UiFeedback.Play(UiSound.GameJump);
            particles.Emit(WarpRing, board.WarpFrom, 1);
            particles.Emit(Sparks, board.WarpFrom, 10);
            exhaust.Clear();
        }

        if (board.WarpEndedThisFrame)
        {
            particles.Emit(WarpRing, board.ShipPosition, 1);
            particles.Emit(Sparks, board.ShipPosition, 12);
            context.Fx.Flash(accent, 0.12f);
            exhaust.Clear();
        }

        if (board.ShipLostThisFrame)
        {
            OnShipLost(context, accent);
        }

        if (board.SpawnedThisFrame)
        {
            exhaust.Clear();
            particles.Emit(WarpRing, board.ShipPosition, 1);
            if (board.Lives == 1)
            {
                context.Fx.Vignette(Danger, 0.45f, 1.2f);
            }
        }

        if (board.ExtraLifeThisFrame)
        {
            UiFeedback.Play(UiSound.GamePowerUp);
            fx.AddText(Loc.T(L.Drift.ExtraShip), camera.ToScreen(board.ShipPosition), Gold, 1.2f);
            context.Fx.Flash(Gold, 0.14f);
        }

        if (board.WaveClearedThisFrame)
        {
            GameSfx.LevelClear();
            context.Fx.Sweep();
            context.Fx.SlowMo(0.45f, 0.4f);
            context.Fx.Flash(GamePalette.Lighten(accent, 0.4f), 0.14f);
            fx.AddText(Loc.T(L.Drift.WaveClear),
                camera.ToScreen(new Vector2(DriftBoard.Width * 0.5f, DriftBoard.Height * 0.5f)), accent, 1.4f);
        }

        if (board.WaveStartedThisFrame)
        {
            ShowWaveBanner();
        }
    }

    private void OnRockBreak(in RockBreak broken, in GameContext context, Vector4 accent)
    {
        var color = DriftRenderer.RockColor(broken.Size, accent);
        var radius = DriftBoard.RockRadius(broken.Size);
        var debris = new ParticleSpec(NeonStroke.Core(color), color with { W = 0f }, 0.5f + radius * 0.1f,
            14f + radius * 1.6f, 0.75f, 0f, 1.2f, 10f, shape: ParticleShape.Shard, additive: true);
        particles.Emit(debris, broken.Position, 6 + (int)broken.Size * 4);
        particles.Emit(Sparks, broken.Position, 4 + (int)broken.Size * 3);
        particles.Emit(new ParticleSpec(color, color with { W = 0f }, radius * 0.55f, 0f, 0.4f,
            shape: ParticleShape.Ring, curve: SizeCurve.Grow, additive: true), broken.Position, 1);
        var screen = camera.ToScreen(broken.Position);
        if (broken.Points > 0)
        {
            fx.AddText(GameNumber.Signed(broken.Points), screen, color, broken.Size == RockSize.Small ? 1f : 0.85f);
        }

        switch (broken.Size)
        {
            case RockSize.Large:
                UiFeedback.Play(UiSound.GameExplosion);
                camera.Shake(0.22f);
                fx.HitStop(0.035f);
                context.Fx.Punch(0.035f);
                return;
            case RockSize.Medium:
                UiFeedback.Play(UiSound.GameBreak);
                camera.Shake(0.12f);
                fx.HitStop(0.02f);
                return;
            default:
                UiFeedback.Play(UiSound.GamePop);
                camera.Shake(0.06f);
                return;
        }
    }

    private void OnSaucerDown(in GameContext context)
    {
        var position = board.SaucerDownPosition;
        UiFeedback.Play(board.SaucerDownPoints > 0 ? UiSound.GamePowerUp : UiSound.GameExplosion);
        particles.Emit(SaucerDebris, position, 18);
        particles.Emit(Sparks, position, 14);
        particles.Emit(new ParticleSpec(DriftRenderer.SaucerColor, DriftRenderer.SaucerColor with { W = 0f }, 5f, 0f,
            0.5f, shape: ParticleShape.Ring, curve: SizeCurve.Grow, additive: true), position, 1);
        var screen = camera.ToScreen(position);
        fx.Shockwave(screen, camera.Px(14f), DriftRenderer.SaucerColor with { W = 0.7f }, 0.5f, 3f);
        camera.Shake(0.3f);
        if (board.SaucerDownPoints <= 0)
        {
            return;
        }

        fx.AddText(GameNumber.Signed(board.SaucerDownPoints), screen, DriftRenderer.SaucerColor, 1.35f);
        fx.HitStop(0.06f);
        context.Fx.Punch(0.06f);
        context.Fx.Flash(DriftRenderer.SaucerColor, 0.18f);
    }

    private void OnShipLost(in GameContext context, Vector4 accent)
    {
        UiFeedback.Play(UiSound.GameExplosion);
        var position = board.ShipPosition;
        particles.Emit(ShipDebris, position, 16);
        particles.Emit(Sparks, position, 24);
        particles.Emit(new ParticleSpec(accent, accent with { W = 0f }, 6f, 0f, 0.6f, shape: ParticleShape.Ring,
            curve: SizeCurve.Grow, additive: true), position, 1);
        fx.Shockwave(camera.ToScreen(position), camera.Px(18f), Danger with { W = 0.7f }, 0.6f, 3.4f);
        camera.Shake(0.8f);
        fx.HitStop(0.08f);
        context.Fx.Punch(0.07f);
        context.Fx.Flash(Danger, 0.38f);
        context.Fx.SlowMo(0.4f, 0.6f);
        exhaust.Clear();
        exhaustHeat = 0f;
        if (board.Lives <= 0)
        {
            context.Fx.Vignette(Danger, 0.6f, 1.6f);
        }
    }

    private void ShowWaveBanner()
    {
        bannerText = waveLabel.Get(L.Drift.WaveNumber, board.Wave);
        bannerProgress = 0f;
    }

    private void Finish(in GameContext context)
    {
        var outcome = new GameOutcome(board.Score, ScoreKind.Score, GameId)
            .WithStat(L.Drift.WavesCleared, GameNumber.Label(board.WavesCleared))
            .WithStat(L.Drift.Rocks, GameNumber.Label(board.RocksBroken))
            .WithStat(L.Drift.Saucers, GameNumber.Label(board.SaucersDowned));
        if (board.ShotsFired > 0)
        {
            var percent = board.ShotsHit * 100 / board.ShotsFired;
            outcome = outcome.WithStat(L.Drift.Accuracy, Loc.T(L.Drift.Percent, GameNumber.Label(percent)));
        }

        context.Session.Finish(outcome);
    }
}
