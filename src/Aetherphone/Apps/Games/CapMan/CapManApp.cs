using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.CapMan;

internal sealed class CapManApp : IMiniGame
{
    private const string GameId = "capman";
    private const float PadInset = 4f;
    private const float PadKeyInset = 4f;
    private const float PadGap = 6f;
    private const float PadOpacity = 0.92f;
    private const float DeathShake = 0.5f;
    private const float DeathSlowFactor = 0.4f;
    private const float DeathSlowSeconds = 0.4f;
    private const float GhostEatPunch = 0.03f;
    private const float FruitPunch = 0.04f;
    private const int ConfettiCount = 60;
    private const float ConfettiSpeed = 10f;
    private const float ConfettiSize = 0.16f;
    private const float ConfettiLife = 1.4f;
    private const float ConfettiGravity = 20f;
    private const ulong IdleSeed = 0x4341504D414EUL;
    private static readonly Vector2 ReadyBannerTile = new(7f, 11f);
    private static readonly GameSpec StageSpec = new(GameId, L.Games.CapMan, GameGenre.Action, L.CapMan.Hook,
        Backdrop.Neon, HudStyle.Standard, ScoreKind.Score, clocked: true, countdown: true, keyboard: true);
    private static readonly Vector4 Danger = new(0.95f, 0.30f, 0.30f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4[] CelebrationPalette =
    {
        new(1f, 0.85f, 0.30f, 1f), new(0.98f, 0.35f, 0.35f, 1f), new(0.98f, 0.55f, 0.85f, 1f),
        new(0.40f, 0.90f, 0.95f, 1f), new(1f, 0.70f, 0.35f, 1f), new(0.98f, 0.98f, 0.9f, 1f),
    };

    private static readonly ParticleSpec DotPuff = new(CapManRenderer.DotColor with { W = 0.7f },
        CapManRenderer.DotColor with { W = 0f }, 0.08f, 1.5f, 0.25f);
    private static readonly ParticleSpec PelletSparkle = new(CapManRenderer.PlayerColor, White, 0.14f, 4f, 0.6f, 1.5f,
        2.4f, 6f, shape: ParticleShape.Star, additive: true);
    private static readonly ParticleSpec GhostShards = new(CapManRenderer.FrightColor,
        CapManRenderer.FrightColor with { W = 0f }, 0.16f, 5f, 0.5f, 8f, 1.6f, 10f, shape: ParticleShape.Shard,
        additive: true);
    private static readonly ParticleSpec DeathBurst = new(CapManRenderer.PlayerColor,
        CapManRenderer.PlayerColor with { W = 0.2f }, 0.18f, 6f, 0.8f, 10f, 1.2f, 8f);
    private static readonly ParticleSpec FruitSparkle = new(White, CapManRenderer.PlayerColor, 0.12f, 3.5f, 0.6f, 4f,
        2.4f, 6f, shape: ParticleShape.Star, additive: true);
    private static readonly ParticleSpec FruitArrival = new(CapManRenderer.PlayerColor,
        CapManRenderer.PlayerColor with { W = 0f }, 0.3f, 0f, 0.5f, shape: ParticleShape.Ring, additive: true);

    private readonly CapManBoard board = new();
    private readonly CapManBoard idleBoard = new();
    private readonly CapManRenderer renderer = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private Camera2D camera = Camera2D.Create();
    private bool finished;
    private bool idleReady;
    private float bannerProgress = 1f;

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        board.StartGame(start.Random);
        particles.Clear();
        particles.Reseed(start.Seed);
        fx.Clear();
        finished = false;
        bannerProgress = 0f;
    }

    public void Close()
    {
    }

    public void Dispose()
    {
    }

    public void DrawIdle(in GameContext context)
    {
        if (!idleReady)
        {
            idleBoard.StartGame(GameRandom.FromSeed(IdleSeed));
            idleReady = true;
        }

        var scale = UiScale.Current;
        PlaceCamera(context, scale);
        renderer.Draw(ImGui.GetWindowDrawList(), idleBoard, in camera, Accent, scale);
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
        }

        particles.Update(context.RawDeltaSeconds);
        fx.Update(context.RawDeltaSeconds);
        bannerProgress = GameBanner.Advance(bannerProgress, simDelta, CapManBoard.ReadySeconds);
        renderer.Draw(drawList, board, in camera, accent, scale);
        var pad = DrawPad(drawList, context, accent, scale);
        if (!finished)
        {
            if (context.Session.State is StageFlow.Playing or StageFlow.Countdown)
            {
                HandleInput(pad);
            }

            ReactToEvents(context);
        }

        particles.Draw(drawList, in camera);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        GameBanner.Draw(drawList, camera.ToScreen(ReadyBannerTile + CapManRenderer.Half), Loc.T(L.Games.Ready), accent,
            context.Theme, bannerProgress);
        context.Hud.Score(board.Score);
        context.Hud.Lives(board.Lives, CapManBoard.StartLives);
        context.Hud.Level(board.Level);
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
        var band = StageLayout.PadBand(context.Full, StageLayout.DPadBand, scale);
        var safe = context.Safe;
        var bottom = MathF.Max(safe.Min.Y, band.Min.Y - PadGap * scale);
        var view = new Rect(safe.Min, new Vector2(safe.Max.X, bottom));
        camera.Fit(view, CapManBoard.Columns, CapManBoard.Rows, FitMode.Contain);
        context.Fx.ApplyTo(ref camera);
        camera.Update(context.RawDeltaSeconds, scale);
        context.Backdrop.SetCamera(in camera);
    }

    private static PadDirection DrawPad(ImDrawListPtr drawList, in GameContext context, Vector4 accent, float scale)
    {
        var band = StageLayout.PadBand(context.Full, StageLayout.DPadBand, scale);
        var side = MathF.Max(0f, band.Height - PadInset * 2f * scale);
        var panel = new Rect(new Vector2(band.Center.X - side * 0.5f, band.Min.Y + PadInset * scale),
            new Vector2(band.Center.X + side * 0.5f, band.Min.Y + PadInset * scale + side));
        Material.Frosted(drawList, panel.Min, panel.Max, Metrics.Radius.Lg * scale, scale, PadOpacity);
        return GamePad.DPad(panel.Inset(PadKeyInset * scale), accent, context.Theme);
    }

    private void HandleInput(PadDirection pad)
    {
        if (pad == PadDirection.Up || GameInput.Pressed(ImGuiKey.W, ImGuiKey.UpArrow))
        {
            board.Turn(CapManBoard.Up);
        }
        else if (pad == PadDirection.Down || GameInput.Pressed(ImGuiKey.S, ImGuiKey.DownArrow))
        {
            board.Turn(CapManBoard.Down);
        }
        else if (pad == PadDirection.Left || GameInput.Pressed(ImGuiKey.A, ImGuiKey.LeftArrow))
        {
            board.Turn(CapManBoard.Left);
        }
        else if (pad == PadDirection.Right || GameInput.Pressed(ImGuiKey.D, ImGuiKey.RightArrow))
        {
            board.Turn(CapManBoard.Right);
        }
    }

    private void ReactToEvents(in GameContext context)
    {
        if (board.DotsEatenThisFrame > 0)
        {
            particles.Emit(DotPuff, board.LastDotPosition + CapManRenderer.Half, 2);
        }

        if (board.PelletEatenThisFrame)
        {
            UiFeedback.Play(UiSound.GamePowerUp);
            var world = board.LastDotPosition + CapManRenderer.Half;
            particles.Emit(PelletSparkle, world, 10);
            fx.Shockwave(camera.ToScreen(world), camera.Px(4f), CapManRenderer.FrightColor with { W = 0.7f }, 0.5f, 3f);
            context.Fx.Flash(CapManRenderer.FrightColor, 0.14f);
            camera.Shake(0.12f);
        }

        if (board.GhostsEatenThisFrame > 0)
        {
            UiFeedback.Play(UiSound.GameCollect);
        }

        for (var index = 0; index < board.GhostsEatenThisFrame; index++)
        {
            var world = board.GhostEatPosition(index) + CapManRenderer.Half;
            var screen = camera.ToScreen(world);
            particles.Emit(GhostShards, world, 12);
            fx.AddText(GameNumber.Signed(board.GhostEatPoints(index)), screen, CapManRenderer.DotColor, 1.1f);
            fx.Shockwave(screen, camera.Px(2.5f), CapManRenderer.DotColor with { W = 0.6f }, 0.35f, 2f);
            fx.HitStop(0.06f);
            camera.Shake(0.18f);
            context.Fx.Punch(GhostEatPunch);
        }

        if (board.FruitSpawnedThisFrame)
        {
            particles.Emit(FruitArrival, board.FruitPosition + CapManRenderer.Half, 1);
        }

        if (board.FruitEatenThisFrame)
        {
            UiFeedback.Play(UiSound.GameMatch);
            var world = board.FruitPosition + CapManRenderer.Half;
            var screen = camera.ToScreen(world);
            particles.Emit(FruitSparkle, world, 14);
            fx.AddText(GameNumber.Signed(board.LastFruitPoints), screen, CapManRenderer.PlayerColor, 1.2f);
            fx.Shockwave(screen, camera.Px(2f), CapManRenderer.PlayerColor with { W = 0.7f }, 0.4f, 2.5f);
            context.Fx.Punch(FruitPunch);
        }

        if (board.PlayerDiedThisFrame)
        {
            UiFeedback.Play(UiSound.GameHitSoft);
            particles.Emit(DeathBurst, board.PlayerPosition + CapManRenderer.Half, 18);
            camera.Shake(DeathShake);
            context.Fx.SlowMo(DeathSlowFactor, DeathSlowSeconds);
            context.Fx.Flash(Danger, 0.35f);
            if (board.Lives == 1)
            {
                context.Fx.Vignette(Danger, 0.5f, 1f);
            }
        }

        if (board.LevelClearedThisFrame)
        {
            GameSfx.LevelClear();
            context.Fx.Sweep();
            particles.Confetti(new Vector2(CapManBoard.Columns * 0.5f, CapManBoard.Rows * 0.2f), ConfettiCount,
                CelebrationPalette, ConfettiSpeed, ConfettiSize, ConfettiLife, ConfettiGravity);
            context.Fx.Flash(GamePalette.Lighten(Accent, 0.4f), 0.18f);
        }

        if (board.ReadyStartedThisFrame)
        {
            bannerProgress = 0f;
        }
    }

    private void Finish(in GameContext context)
    {
        context.Session.Finish(new GameOutcome(board.Score, ScoreKind.Score, GameId)
            .WithStat(L.Games.Level, GameNumber.Label(board.Level))
            .WithStat(L.CapMan.GhostsEaten, GameNumber.Label(board.GhostsEaten))
            .WithStat(L.CapMan.Fruit, GameNumber.Label(board.FruitEaten)));
    }
}
