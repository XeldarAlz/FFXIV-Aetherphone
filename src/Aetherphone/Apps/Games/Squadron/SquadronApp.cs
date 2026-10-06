using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Squadron;

internal sealed class SquadronApp : IMiniGame
{
    private const string GameId = "squadron";
    private const float PadInsetX = 12f;
    private const float PadInsetY = 6f;
    private const float PadOpacity = 0.92f;
    private const float ExhaustRate = 36f;
    private const float ExhaustWidth = 3f;
    private const float RollPunchDelay = 0.14f;
    private const float JoinPunch = 0.08f;
    private const float JoinRollPunch = 0.04f;
    private const int RescueConfetti = 40;
    private const int StageConfetti = 50;
    private const int PerfectConfetti = 80;
    private const float ConfettiSpeed = 67f;
    private const float ConfettiSize = 1f;
    private const float ConfettiLife = 1.4f;
    private const float ConfettiGravity = 138f;
    private const ulong IdleSeed = 0x535155414452UL;
    private static readonly GameSpec StageSpec = new(GameId, L.Games.Squadron, GameGenre.Action, L.Squadron.Hook,
        Backdrop.Neon, HudStyle.Standard, ScoreKind.Score, clocked: true, countdown: true, keyboard: true);
    private static readonly Vector4 Danger = new(0.95f, 0.30f, 0.30f, 1f);
    private static readonly Vector4 DualLossFlash = new(0.95f, 0.5f, 0.3f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4[] CelebrationPalette =
    {
        new(0.98f, 0.95f, 0.90f, 1f), new(0.98f, 0.45f, 0.62f, 1f), new(1f, 0.62f, 0.30f, 1f),
        new(0.40f, 0.70f, 0.98f, 1f), new(0.72f, 0.50f, 0.96f, 1f), new(0.46f, 0.86f, 0.62f, 1f),
    };

    private readonly SquadronBoard board = new();
    private readonly SquadronBoard idleBoard = new();
    private readonly SquadronRenderer renderer = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly Ribbon leftExhaust = new();
    private readonly Ribbon rightExhaust = new();
    private readonly Vector4 exhaustColor;
    private Emitter leftEmitter;
    private Emitter rightEmitter;
    private Camera2D camera = Camera2D.Create();
    private LabelSlot stageLabel;
    private LabelSlot perfectLabel;
    private LabelSlot bonusLabel;
    private string challengeHitsText = string.Empty;
    private bool finished;
    private bool idleReady;
    private float rollPunchTimer = -1f;
    private float bannerProgress = 1f;
    private float bannerLifetime = 1f;
    private string bannerText = string.Empty;

    public SquadronApp()
    {
        exhaustColor = GamePalette.Lighten(Accent, 0.45f);
        var exhaust = new ParticleSpec(exhaustColor, Accent with { W = 0f }, 0.45f, 18f, 0.35f, 0f, 2.2f, 0f, 0.35f,
            MathF.PI * 0.5f, ParticleShape.Spark, SizeCurve.Shrink, true);
        leftEmitter = new Emitter(in exhaust, ExhaustRate);
        rightEmitter = new Emitter(in exhaust, ExhaustRate);
    }

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        board.StartGame(start.Random);
        particles.Clear();
        particles.Reseed(start.Seed);
        fx.Clear();
        leftExhaust.Clear();
        rightExhaust.Clear();
        leftEmitter.Reset();
        rightEmitter.Reset();
        rollPunchTimer = -1f;
        finished = false;
        bannerProgress = 1f;
        ShowStageBanner();
    }

    public void Close()
    {
    }

    public void Dispose()
    {
    }

    public void DrawIdle(in GameContext context)
    {
        if (!idleReady || idleBoard.GameOver)
        {
            idleBoard.StartGame(GameRandom.FromSeed(IdleSeed));
            idleReady = true;
        }

        var scale = UiScale.Current;
        PlaceCamera(context, scale);
        idleBoard.Tick(context.RawDeltaSeconds);
        renderer.Draw(ImGui.GetWindowDrawList(), idleBoard, in camera, context.Full, Accent, scale);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var accent = Accent;
        PlaceCamera(context, scale);
        var simDelta = fx.ScaleDelta(context.DeltaSeconds);
        if (!finished)
        {
            board.Tick(simDelta);
            TrailExhaust(simDelta);
        }

        particles.Update(context.RawDeltaSeconds);
        fx.Update(context.RawDeltaSeconds);
        AdvanceRollPunch(context);
        bannerProgress = GameBanner.Advance(bannerProgress, context.DeltaSeconds, bannerLifetime);
        DrawExhaust(drawList, scale);
        renderer.Draw(drawList, board, in camera, context.Full, accent, scale);
        var pad = DrawPad(drawList, context, accent, scale);
        if (!finished)
        {
            if (context.Session.State == StageFlow.Playing)
            {
                HandleInput(in pad, simDelta);
            }

            ReactToEvents(context, accent);
        }

        particles.Draw(drawList, in camera);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        GameBanner.Draw(drawList, camera.ToScreen(new Vector2(SquadronBoard.Width * 0.5f, SquadronBoard.Height * 0.62f)),
            bannerText, accent, context.Theme, bannerProgress);
        context.Hud.Score(board.Score);
        context.Hud.Lives(board.Lives, SquadronBoard.StartLives);
        context.Hud.Level(board.Stage);
        context.Hud.Best(context.Session.Best);
        context.Session.Report(board.Score);
        if (board.GameOver && !finished)
        {
            finished = true;
            Finish(context);
        }
    }

    private void PlaceCamera(in GameContext context, float scale)
    {
        var full = context.Full;
        var band = StageLayout.PadBand(full, StageLayout.ShooterBand, scale);
        var view = new Rect(new Vector2(full.Min.X, context.Safe.Min.Y), new Vector2(full.Max.X, band.Min.Y));
        camera.Fit(view, SquadronBoard.Width, SquadronBoard.Height, FitMode.Contain);
        context.Fx.ApplyTo(ref camera);
        camera.Update(context.RawDeltaSeconds, scale);
        context.Backdrop.SetCamera(in camera);
    }

    private static ShooterPadInput DrawPad(ImDrawListPtr drawList, in GameContext context, Vector4 accent, float scale)
    {
        var band = StageLayout.PadBand(context.Full, StageLayout.ShooterBand, scale);
        var panel = new Rect(new Vector2(band.Min.X + PadInsetX * scale, band.Min.Y + PadInsetY * scale),
            new Vector2(band.Max.X - PadInsetX * scale, band.Max.Y - PadInsetY * scale));
        Material.Frosted(drawList, panel.Min, panel.Max, Metrics.Radius.Lg * scale, scale, PadOpacity);
        return GamePad.Shooter(panel, accent, context.Theme);
    }

    private void HandleInput(in ShooterPadInput pad, float deltaSeconds)
    {
        var left = pad.Left || GameInput.Held(ImGuiKey.A, ImGuiKey.LeftArrow);
        var right = pad.Right || GameInput.Held(ImGuiKey.D, ImGuiKey.RightArrow);
        var direction = (right ? 1f : 0f) - (left ? 1f : 0f);
        board.Move(direction, deltaSeconds);
        var fire = pad.Fire || GameInput.Pressed(ImGuiKey.Space, ImGuiKey.W) || GameInput.Pressed(ImGuiKey.UpArrow);
        if (fire)
        {
            board.Fire();
        }
    }

    private void TrailExhaust(float deltaSeconds)
    {
        if (board.CaptureActive || board.Respawning || board.GameOver)
        {
            leftExhaust.Clear();
            rightExhaust.Clear();
            return;
        }

        var tail = board.PlayerCenter + new Vector2(0f, SquadronBoard.PlayerHeight * 0.5f);
        if (!board.Dual)
        {
            rightExhaust.Clear();
            leftExhaust.Push(tail);
            leftEmitter.Advance(deltaSeconds, tail, particles);
            return;
        }

        var offset = new Vector2(SquadronBoard.PlayerWidth * 0.5f, 0f);
        leftExhaust.Push(tail - offset);
        rightExhaust.Push(tail + offset);
        leftEmitter.Advance(deltaSeconds, tail - offset, particles);
        rightEmitter.Advance(deltaSeconds, tail + offset, particles);
    }

    private void DrawExhaust(ImDrawListPtr drawList, float scale)
    {
        var width = ExhaustWidth * scale;
        leftExhaust.Draw(drawList, in camera, exhaustColor with { W = 0.6f }, width, true);
        rightExhaust.Draw(drawList, in camera, exhaustColor with { W = 0.6f }, width, true);
    }

    private void AdvanceRollPunch(in GameContext context)
    {
        if (rollPunchTimer < 0f)
        {
            return;
        }

        rollPunchTimer -= context.RawDeltaSeconds;
        if (rollPunchTimer > 0f)
        {
            return;
        }

        rollPunchTimer = -1f;
        context.Fx.Punch(JoinRollPunch);
    }

    private void ReactToEvents(in GameContext context, Vector4 accent)
    {
        if (board.ShotFiredThisFrame)
        {
            UiFeedback.Play(UiSound.GameShoot);
            var muzzle = new Vector2(board.PlayerX, SquadronBoard.PlayerRowY - SquadronBoard.PlayerHeight);
            particles.Burst(muzzle, 3, GamePalette.Lighten(accent, 0.4f), 28f, 0.5f, 0.2f, 56f, 0.5f, -MathF.PI * 0.5f,
                ParticleShape.Streak);
        }

        for (var index = 0; index < board.KillCount; index++)
        {
            var center = board.KillPosition(index);
            var color = SquadronRenderer.KindColor(board.KillKind(index), accent);
            particles.Burst(center, 8, color, 38f, 0.62f, 0.5f, 56f, MathF.PI * 2f, 0f, ParticleShape.Square);
            particles.Emit(new ParticleSpec(color, color with { W = 0f }, 0.9f, 35f, 0.6f, 40f, 1.6f, 10f,
                shape: ParticleShape.Shard, additive: true), center, 8);
            particles.Emit(new ParticleSpec(White, White, 0.5f, 26f, 0.4f, 10f, 2.4f, 6f, shape: ParticleShape.Star),
                center, 4);
            var screen = camera.ToScreen(center);
            fx.Shockwave(screen, camera.Px(SquadronBoard.ShipWidth * 1.3f), color with { W = 0.6f }, 0.3f, 2f);
            var points = board.KillPoints(index);
            if (points > 0)
            {
                fx.AddText(GameNumber.Signed(points), screen, color, points >= SquadronBoard.WardenDivingPoints ? 1.2f : 0.9f);
            }
        }

        if (board.KillCount > 0)
        {
            UiFeedback.Play(UiSound.GameExplosion);
            camera.Shake(0.06f);
            fx.HitStop(0.03f);
        }

        if (board.CaptureStartedThisFrame)
        {
            context.Fx.Flash(SquadronRenderer.WardenColor, 0.25f);
            camera.Shake(0.3f);
            leftExhaust.Clear();
            rightExhaust.Clear();
        }

        if (board.RescueStartedThisFrame)
        {
            particles.Emit(new ParticleSpec(GamePalette.Lighten(accent, 0.4f), White, 0.67f, 36f, 0.8f, 10f, 2.4f, 6f,
                shape: ParticleShape.Star), board.RescuePosition, 12);
        }

        if (board.RescueCompletedThisFrame)
        {
            UiFeedback.Play(UiSound.GamePowerUp);
            var center = board.PlayerCenter;
            EmitConfetti(center, RescueConfetti);
            fx.Shockwave(camera.ToScreen(center), camera.Px(SquadronBoard.PlayerWidth * 3f),
                GamePalette.Lighten(accent, 0.4f) with { W = 0.7f }, 0.5f, 3f);
            fx.AddText(bonusLabel.Get(L.Squadron.Bonus, SquadronBoard.RescueBonus), camera.ToScreen(center),
                GamePalette.Lighten(accent, 0.4f), 1.3f);
            fx.HitStop(0.06f);
            context.Fx.Punch(JoinPunch);
            camera.Shake(0.25f);
            context.Fx.SlowMo(0.6f, 0.25f);
            rollPunchTimer = RollPunchDelay;
        }

        if (board.DualLostThisFrame)
        {
            var center = board.PlayerCenter;
            particles.Burst(center, 12, accent, 38f, 0.62f, 0.6f, 67f);
            camera.Shake(0.35f);
            fx.HitStop(0.05f);
            context.Fx.Flash(DualLossFlash, 0.2f);
            rightExhaust.Clear();
        }

        if (board.PlayerHitThisFrame)
        {
            UiFeedback.Play(UiSound.GameHitSoft);
            var center = board.PlayerCenter;
            particles.Burst(center, 16, accent, 44f, 0.67f, 0.7f, 77f);
            camera.Shake(0.7f);
            fx.HitStop(0.1f);
            context.Fx.Punch(0.06f);
            context.Fx.Flash(Danger, 0.35f);
            if (board.Lives == 1)
            {
                context.Fx.Vignette(Danger, 0.5f, 1f);
            }
        }

        if (board.StageClearedThisFrame)
        {
            GameSfx.LevelClear();
            context.Fx.Sweep();
            EmitConfetti(new Vector2(SquadronBoard.Width * 0.5f, SquadronBoard.Height * 0.2f), StageConfetti);
            context.Fx.Flash(GamePalette.Lighten(accent, 0.4f), 0.14f);
        }

        if (board.ChallengeEndedThisFrame)
        {
            ShowChallengeResult();
            if (board.LastChallengeWasPerfect)
            {
                EmitConfetti(new Vector2(SquadronBoard.Width * 0.5f, SquadronBoard.Height * 0.2f), PerfectConfetti);
                context.Fx.Sweep();
            }
        }
        else if (board.StageStartedThisFrame)
        {
            ShowStageBanner();
        }
    }

    private void ShowChallengeResult()
    {
        if (board.LastChallengeWasPerfect)
        {
            bannerText = perfectLabel.Get(L.Squadron.PerfectBonus, SquadronBoard.PerfectBonus);
        }
        else
        {
            challengeHitsText = Loc.T(L.Games.HitsOf, board.LastChallengeHits, SquadronBoard.ChallengeShipCount);
            bannerText = challengeHitsText;
        }

        bannerLifetime = SquadronBoard.ResultBannerSeconds;
        bannerProgress = 0f;
    }

    private void ShowStageBanner()
    {
        bannerText = board.IsChallenge
            ? Loc.T(L.Games.ChallengeStage)
            : stageLabel.Get(L.Squadron.StageNumber, board.Stage);
        bannerLifetime = SquadronBoard.StageBannerSeconds;
        bannerProgress = 0f;
    }

    private void EmitConfetti(Vector2 origin, int count)
    {
        particles.Confetti(origin, count, CelebrationPalette, ConfettiSpeed, ConfettiSize, ConfettiLife, ConfettiGravity);
    }

    private void Finish(in GameContext context)
    {
        var outcome = new GameOutcome(board.Score, ScoreKind.Score, GameId)
            .WithStat(L.Squadron.StagesCleared, GameNumber.Label(board.Stage - 1))
            .WithStat(L.Squadron.Rescues, GameNumber.Label(board.Rescues));
        if (board.ShotsFired > 0)
        {
            var percent = board.ShotsHit * 100 / board.ShotsFired;
            outcome = outcome.WithStat(L.Squadron.Accuracy, Loc.T(L.Squadron.Percent, GameNumber.Label(percent)));
        }

        context.Session.Finish(outcome);
    }
}
