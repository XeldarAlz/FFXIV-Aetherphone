using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Invaders;

internal sealed class InvadersApp : IMiniGame
{
    private const string GameId = "invaders";
    private const float WaveBannerSeconds = 1.6f;
    private const float PadInsetX = 12f;
    private const float PadInsetY = 6f;
    private const float PadOpacity = 0.92f;
    private const float LastInvaderSlowFactor = 0.5f;
    private const float LastInvaderSlowSeconds = 0.3f;
    private const ulong IdleSeed = 0x494E5641444552UL;
    private static readonly GameSpec StageSpec = new(GameId, L.Games.Invaders, GameGenre.Action, L.Invaders.Hook,
        Backdrop.Neon, HudStyle.Standard, ScoreKind.Score, clocked: true, countdown: true, keyboard: true);
    private static readonly Vector4 Danger = new(0.95f, 0.30f, 0.30f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4[] CelebrationPalette =
    {
        new(0.98f, 0.95f, 0.90f, 1f), new(0.98f, 0.45f, 0.62f, 1f), new(1f, 0.62f, 0.30f, 1f),
        new(0.40f, 0.70f, 0.98f, 1f), new(0.72f, 0.50f, 0.96f, 1f), new(0.46f, 0.86f, 0.62f, 1f),
    };

    private static readonly ParticleSpec[] ConfettiSpecs = BuildConfetti();

    private readonly InvadersBoard board = new();
    private readonly InvadersBoard idleBoard = new();
    private readonly InvadersRenderer renderer = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private Camera2D camera = Camera2D.Create();
    private LabelSlot waveLabel;
    private LabelSlot bonusLabel;
    private bool finished;
    private bool idleReady;
    private float bannerProgress = 1f;
    private string bannerText = string.Empty;

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        board.StartGame(start.Random);
        particles.Clear();
        particles.Reseed(start.Seed);
        fx.Clear();
        finished = false;
        bannerProgress = 1f;
        ShowWaveBanner();
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
        idleBoard.Update(context.RawDeltaSeconds);
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
            board.Update(simDelta);
        }

        particles.Update(context.RawDeltaSeconds);
        fx.Update(context.RawDeltaSeconds);
        bannerProgress = GameBanner.Advance(bannerProgress, context.DeltaSeconds, WaveBannerSeconds);
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
        GameBanner.Draw(drawList, camera.ToScreen(new Vector2(InvadersBoard.Width * 0.5f, InvadersBoard.Height * 0.62f)),
            bannerText, accent, context.Theme, bannerProgress);
        context.Hud.Score(board.Score);
        context.Hud.Lives(board.Lives, InvadersBoard.StartingLives);
        context.Hud.Level(board.Wave);
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
        camera.Fit(view, InvadersBoard.Width, InvadersBoard.Height, FitMode.Contain);
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

    private void ReactToEvents(in GameContext context, Vector4 accent)
    {
        if (board.ShotFiredThisFrame)
        {
            UiFeedback.Play(UiSound.GameShoot);
            var muzzle = new Vector2(board.PlayerX, InvadersBoard.PlayerY - InvadersBoard.PlayerHeight);
            particles.Burst(muzzle, 3, GamePalette.Lighten(accent, 0.4f), 28f, 0.5f, 0.2f, 56f, 0.5f, -MathF.PI * 0.5f,
                ParticleShape.Streak);
        }

        for (var index = 0; index < board.KillCount; index++)
        {
            var center = board.KillPosition(index);
            var color = InvadersRenderer.KindColor(board.KillKind(index), accent);
            particles.Burst(center, 10, color, 38f, 0.62f, 0.5f, 56f, MathF.PI * 2f, 0f, ParticleShape.Square);
            particles.Emit(new ParticleSpec(White, White, 0.5f, 26f, 0.4f, 10f, 2.4f, 6f, shape: ParticleShape.Star),
                center, 4);
            var screen = camera.ToScreen(center);
            fx.Shockwave(screen, camera.Px(InvadersBoard.InvaderWidth * 1.3f), color with { W = 0.6f }, 0.3f, 2f);
            fx.AddText(GameNumber.Label(InvadersBoard.RowPoints[RowForKind(board.KillKind(index))]), screen, color, 0.9f);
        }

        if (board.KillCount > 0)
        {
            UiFeedback.Play(UiSound.GameExplosion);
            camera.Shake(0.06f);
            fx.HitStop(0.03f);
        }

        for (var index = 0; index < board.ChipCount; index++)
        {
            particles.Burst(board.ChipPosition(index), 4, GamePalette.Lighten(accent, 0.12f), 23f, 0.4f, 0.35f, 67f,
                MathF.PI * 2f, 0f, ParticleShape.Square);
        }

        if (board.SaucerKilledThisFrame)
        {
            UiFeedback.Play(UiSound.GamePowerUp);
            var center = board.SaucerKillPosition;
            particles.Emit(new ParticleSpec(InvadersRenderer.SaucerColor, White, 0.7f, 44f, 0.8f, 10f, 2.4f, 6f,
                shape: ParticleShape.Star), center, 16);
            var screen = camera.ToScreen(center);
            fx.Shockwave(screen, camera.Px(InvadersBoard.SaucerHalfWidth * 3f),
                InvadersRenderer.SaucerColor with { W = 0.7f }, 0.45f, 2.5f);
            fx.AddText(bonusLabel.Get(L.Invaders.Bonus, InvadersBoard.SaucerPoints), screen, InvadersRenderer.SaucerColor,
                1.2f);
            fx.HitStop(0.05f);
            context.Fx.Punch(0.05f);
        }

        if (board.PlayerHitThisFrame)
        {
            UiFeedback.Play(UiSound.GameHitSoft);
            var center = new Vector2(board.PlayerX, InvadersBoard.PlayerY);
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

        if (board.LandedThisFrame)
        {
            camera.Shake(0.8f);
            context.Fx.Flash(Danger, 0.45f);
        }

        if (board.WaveClearedThisFrame)
        {
            GameSfx.LevelClear();
            context.Fx.Sweep();
            context.Fx.SlowMo(LastInvaderSlowFactor, LastInvaderSlowSeconds);
            EmitConfetti(new Vector2(InvadersBoard.Width * 0.5f, InvadersBoard.Height * 0.2f), 60);
            context.Fx.Flash(GamePalette.Lighten(accent, 0.4f), 0.16f);
        }

        if (board.WaveStartedThisFrame)
        {
            ShowWaveBanner();
        }
    }

    private static int RowForKind(int kind)
    {
        for (var row = 0; row < InvadersBoard.Rows; row++)
        {
            if (InvadersBoard.RowKinds[row] == kind)
            {
                return row;
            }
        }

        return InvadersBoard.Rows - 1;
    }

    private void ShowWaveBanner()
    {
        bannerText = waveLabel.Get(L.Invaders.WaveNumber, board.Wave);
        bannerProgress = 0f;
    }

    private void EmitConfetti(Vector2 origin, int count)
    {
        var perColor = Math.Max(1, count / ConfettiSpecs.Length);
        for (var index = 0; index < ConfettiSpecs.Length; index++)
        {
            particles.Emit(in ConfettiSpecs[index], origin, perColor);
        }
    }

    private void Finish(in GameContext context)
    {
        var outcome = new GameOutcome(board.Score, ScoreKind.Score, GameId)
            .WithStat(L.Invaders.WavesCleared, GameNumber.Label(board.Wave - 1))
            .WithStat(L.Invaders.Saucers, GameNumber.Label(board.SaucersHit));
        if (board.ShotsFired > 0)
        {
            var percent = board.ShotsHit * 100 / board.ShotsFired;
            outcome = outcome.WithStat(L.Invaders.Accuracy, Loc.T(L.Invaders.Percent, GameNumber.Label(percent)));
        }

        context.Session.Finish(outcome);
    }

    private static ParticleSpec[] BuildConfetti()
    {
        var specs = new ParticleSpec[CelebrationPalette.Length];
        for (var index = 0; index < specs.Length; index++)
        {
            specs[index] = new ParticleSpec(CelebrationPalette[index], CelebrationPalette[index], 1f, 67f, 1.4f, 138f,
                0.7f, 16f, 1.4f, -MathF.PI * 0.5f, ParticleShape.Square);
        }

        return specs;
    }
}
