using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Breakout;

internal sealed class BreakoutApp : IMiniGame
{
    private const string GameId = "breakout";
    private const float BannerSeconds = 1.4f;
    private const float BannerHeightFraction = 0.58f;
    private const float RibbonWidth = BreakoutBoard.BallRadius * 1.8f;
    private const float BrickPunch = 0.03f;
    private const float ExplosionShake = 0.35f;
    private const float LifeLostShake = 0.6f;
    private const float LastBrickSlowFactor = 0.5f;
    private const float LastBrickSlowSeconds = 0.25f;
    private const float PaddleKeySpeed = 1.6f;
    private const int ConfettiCount = 60;
    private const float ConfettiGravity = 2f;
    private const ulong IdleSeed = 0x425249434B53UL;
    private static readonly GameSpec StageSpec = new(GameId, L.Games.Breakout, GameGenre.Arcade, L.Breakout.Hook,
        Backdrop.Neon, HudStyle.Standard, ScoreKind.Score, clocked: true, countdown: true, keyboard: true);
    private static readonly Vector4 Danger = new(0.95f, 0.30f, 0.30f, 1f);
    private static readonly Vector4 Spark = new(1f, 0.95f, 0.65f, 1f);
    private static readonly Vector4 Ember = new(1f, 0.55f, 0.25f, 1f);
    private static readonly Vector4 Flame = new(1f, 0.82f, 0.45f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Ring = new(1f, 1f, 1f, 0.55f);
    private static readonly Vector4[] CelebrationPalette =
    {
        new(0.95f, 0.45f, 0.50f, 1f), new(0.96f, 0.62f, 0.32f, 1f), new(0.92f, 0.82f, 0.36f, 1f),
        new(0.46f, 0.86f, 0.62f, 1f), new(0.40f, 0.70f, 0.98f, 1f), new(0.72f, 0.50f, 0.96f, 1f),
    };

    private static readonly ParticleSpec Embers = new(Ember, Flame with { W = 0f }, 0.014f, 0.9f, 0.6f, 1.4f, 1.4f, 8f,
        shape: ParticleShape.Shard);
    private static readonly ParticleSpec CatchSparkle = new(Spark, White, 0.012f, 0.5f, 0.7f, 0.5f, 2f, 6f,
        shape: ParticleShape.Star, additive: true);
    private static readonly ParticleSpec DentSparks = new(White, BreakoutRenderer.ArmourColor, 0.008f, 0.6f, 0.35f, 1.2f,
        2f, shape: ParticleShape.Spark);
    private static readonly ParticleSpec LostShards = new(BreakoutRenderer.BallColor, Danger, 0.012f, 0.7f, 0.6f, 1.6f,
        1.2f, 8f, MathF.PI, -MathF.PI * 0.5f, ParticleShape.Shard);
    private readonly BreakoutBoard board = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly Ribbon[] ribbons = new Ribbon[BreakoutBoard.MaxBalls];
    private Camera2D camera = Camera2D.Create();
    private LabelSlot levelLabel;
    private float bannerProgress = 1f;
    private bool finished;

    public BreakoutApp()
    {
        for (var slot = 0; slot < ribbons.Length; slot++)
        {
            ribbons[slot] = new Ribbon();
        }

        board.StartGame(GameRandom.FromSeed(IdleSeed));
    }

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        board.StartGame(start.Random);
        particles.Clear();
        particles.Reseed(start.Seed);
        fx.Clear();
        ClearRibbons();
        camera = Camera2D.Create();
        bannerProgress = 0f;
        finished = false;
    }

    public void Close()
    {
        particles.Clear();
        fx.Clear();
        ClearRibbons();
    }

    public void Dispose()
    {
    }

    public void DrawIdle(in GameContext context)
    {
        PlaceCamera(context);
        DrawWorld(ImGui.GetWindowDrawList(), UiScale.Current);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        particles.Update(context.RawDeltaSeconds);
        fx.Update(context.RawDeltaSeconds);
        bannerProgress = GameBanner.Advance(bannerProgress, context.DeltaSeconds, BannerSeconds);
        PlaceCamera(context);
        var simDelta = fx.ScaleDelta(context.DeltaSeconds);
        if (!finished)
        {
            Step(simDelta, context);
        }

        DrawWorld(drawList, scale);
        GameBanner.Draw(drawList, camera.ToScreen(new Vector2(BreakoutBoard.FieldWidth * 0.5f,
            BreakoutBoard.FieldHeight * BannerHeightFraction)), levelLabel.Get(L.Breakout.LevelNumber, board.Level),
            Accent, context.Theme, bannerProgress);
        context.Hud.Score(board.Score);
        context.Hud.Lives(board.Lives, BreakoutBoard.StartingLives);
        context.Hud.Level(board.Level);
        context.Hud.Combo(board.Combo);
        context.Hud.Best(context.Session.Best);
        context.Session.Report(board.Score);
    }

    private void PlaceCamera(in GameContext context)
    {
        camera.Fit(context.Safe, BreakoutBoard.FieldWidth, BreakoutBoard.FieldHeight, FitMode.Contain);
        context.Fx.ApplyTo(ref camera);
        camera.Update(context.RawDeltaSeconds, UiScale.Current);
        context.Backdrop.SetCamera(in camera);
    }

    private void Step(float deltaSeconds, in GameContext context)
    {
        HandleInput(deltaSeconds, context);
        board.Update(deltaSeconds);
        PushTrails(deltaSeconds);
        ReactToEvents(context);
        if (!board.GameOver)
        {
            return;
        }

        finished = true;
        context.Session.Finish(new GameOutcome(board.Score, ScoreKind.Score, GameId)
            .WithStat(L.Games.Level, GameNumber.Label(board.Level))
            .WithStat(L.Breakout.Bricks, GameNumber.Label(board.BricksBroken))
            .WithStat(L.Games.Combo, GameNumber.Label(board.BestCombo)));
    }

    private void HandleInput(float deltaSeconds, in GameContext context)
    {
        var state = context.Session.State;
        if (state is not (StageFlow.Playing or StageFlow.Countdown))
        {
            return;
        }

        var full = context.Full;
        var mouse = ImGui.GetMousePos();
        var hovered = UiInteract.Hover(full.Min, full.Max) && !context.ChromeHit(mouse);
        if (hovered)
        {
            board.SetPaddle(camera.ToWorld(mouse).X);
        }

        if (state != StageFlow.Playing)
        {
            return;
        }

        if (GameInput.Held(ImGuiKey.A, ImGuiKey.LeftArrow))
        {
            board.SetPaddle(board.PaddleX - PaddleKeySpeed * deltaSeconds);
        }

        if (GameInput.Held(ImGuiKey.D, ImGuiKey.RightArrow))
        {
            board.SetPaddle(board.PaddleX + PaddleKeySpeed * deltaSeconds);
        }

        if (!board.Attached)
        {
            return;
        }

        var tapped = hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
        if (tapped || GameInput.Pressed(ImGuiKey.Space, ImGuiKey.Enter))
        {
            board.Launch();
            UiFeedback.Play(UiSound.GameShoot);
        }
    }

    private void PushTrails(float deltaSeconds)
    {
        if (board.Attached)
        {
            ClearRibbons();
            return;
        }

        for (var slot = 0; slot < board.BallCount; slot++)
        {
            var ball = board.GetBall(slot);
            ribbons[slot].Claim(ball.Id);
            if (deltaSeconds > 0f)
            {
                ribbons[slot].Push(ball.Position);
            }
        }
    }

    private void ClearRibbons()
    {
        for (var slot = 0; slot < ribbons.Length; slot++)
        {
            ribbons[slot].Release();
        }
    }

    private void ReactToEvents(in GameContext context)
    {
        var scale = UiScale.Current;
        for (var index = 0; index < board.BreakCount; index++)
        {
            var center = board.BreakPosition(index);
            var color = BreakoutRenderer.ColorFor(board.BreakKind(index), board.BreakColor(index));
            particles.Burst(center, 8, color, 0.45f, 0.012f, 0.45f, 1.4f);
            particles.Burst(center, 4, GamePalette.Lighten(color, 0.3f), 0.55f, 0.009f, 0.4f, 1.2f, MathF.PI * 2f, 0f,
                ParticleShape.Square);
            camera.Punch(BrickPunch);
        }

        if (board.BreakCount > 0)
        {
            UiFeedback.Play(UiSound.GameBreak);
            var last = camera.ToScreen(board.BreakPosition(board.BreakCount - 1));
            fx.Shockwave(last, camera.Px(0.09f), Ring, 0.32f, 2.2f);
            if (board.BreakCount >= 3)
            {
                fx.HitStop(0.035f);
            }
        }

        for (var index = 0; index < board.DentCount; index++)
        {
            particles.Emit(DentSparks, board.DentPosition(index), 5);
        }

        if (board.DentCount > 0)
        {
            UiFeedback.Play(UiSound.GameHitWood);
        }

        for (var index = 0; index < board.ExplosionCount; index++)
        {
            var center = board.ExplosionPosition(index);
            particles.Emit(Embers, center, 18);
            fx.Shockwave(camera.ToScreen(center), camera.Px(0.3f), Flame, 0.5f, 3.4f);
        }

        if (board.ExplosionCount > 0)
        {
            UiFeedback.Play(UiSound.GameExplosion);
            camera.Shake(ExplosionShake);
            context.Fx.Flash(Flame, 0.18f);
        }

        if (board.PaddleHitThisFrame)
        {
            UiFeedback.Play(UiSound.GameHitSoft);
        }

        if (board.CaughtPowerThisFrame)
        {
            OnPowerUp(context);
        }

        if (board.LevelCleared)
        {
            OnLevelCleared(context);
        }

        if (board.LevelStarted)
        {
            bannerProgress = 0f;
        }

        if (board.LostLifeThisFrame)
        {
            OnLifeLost(context);
        }

        if (board.Lives == 1 && !board.GameOver)
        {
            context.Fx.Vignette(Danger, 0.10f + 0.10f * Pulse.Wave(Pulse.Fast), 0.3f);
        }
    }

    private void OnPowerUp(in GameContext context)
    {
        UiFeedback.Play(UiSound.GamePowerUp);
        var paddle = new Vector2(board.PaddleX, BreakoutBoard.PaddleY);
        var screen = camera.ToScreen(paddle);
        particles.Emit(CatchSparkle, paddle, 14);
        fx.Shockwave(screen, camera.Px(0.16f), GamePalette.Lighten(Accent, 0.4f), 0.45f, 2.6f);
        var multiBall = board.CaughtKind == PowerUpKind.MultiBall;
        fx.AddText(Loc.T(multiBall ? L.Breakout.MultiBall : L.Breakout.Wide), screen - new Vector2(0f, camera.Px(0.06f)),
            multiBall ? BreakoutRenderer.MultiBallColor : BreakoutRenderer.WideColor, 1.1f);
        context.Fx.Punch(0.04f);
    }

    private void OnLevelCleared(in GameContext context)
    {
        GameSfx.LevelClear();
        context.Fx.SlowMo(LastBrickSlowFactor, LastBrickSlowSeconds);
        context.Fx.Sweep();
        context.Fx.Flash(GamePalette.Lighten(Accent, 0.4f), 0.18f);
        var origin = new Vector2(BreakoutBoard.FieldWidth * 0.5f, BreakoutBoard.FieldHeight * 0.2f);
        particles.Confetti(origin, ConfettiCount, CelebrationPalette, 0.9f, 0.012f, 1.4f, ConfettiGravity);
    }

    private void OnLifeLost(in GameContext context)
    {
        UiFeedback.Play(UiSound.GameHitSoft);
        var lost = board.LostBallPosition with { Y = BreakoutBoard.FieldHeight };
        particles.Emit(LostShards, lost, 16);
        fx.Shockwave(camera.ToScreen(lost), camera.Px(0.2f), Danger, 0.5f, 3f);
        fx.HitStop(0.1f);
        camera.Shake(LifeLostShake);
        context.Fx.Flash(Danger, 0.35f);
        ClearRibbons();
    }

    private void DrawWorld(ImDrawListPtr drawList, float scale)
    {
        var accent = Accent;
        var field = BreakoutRenderer.FieldRect(in camera);
        BreakoutRenderer.DrawField(drawList, in camera, accent, scale);
        drawList.PushClipRect(field.Min, field.Max, true);
        BreakoutRenderer.DrawArena(drawList, board, in camera, accent, scale);
        var trail = GamePalette.Lighten(accent, 0.35f) with { W = 0.6f };
        if (!board.Attached)
        {
            for (var slot = 0; slot < board.BallCount; slot++)
            {
                ribbons[slot].Draw(drawList, in camera, trail, camera.Px(RibbonWidth), additive: true);
            }
        }

        BreakoutRenderer.DrawBalls(drawList, board, in camera, accent);
        drawList.PopClipRect();
        particles.Draw(drawList, in camera);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
    }
}
