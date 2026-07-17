using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Animation;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Games.Breakout;

internal sealed class BreakoutApp : IMiniGame
{
    private const string GameId = "breakout";
    private const float TrailInterval = 0.04f;
    private static readonly Vector4[] CelebrationPalette =
    {
        new(0.95f, 0.45f, 0.50f, 1f), new(0.96f, 0.62f, 0.32f, 1f), new(0.92f, 0.82f, 0.36f, 1f),
        new(0.46f, 0.86f, 0.62f, 1f), new(0.40f, 0.70f, 0.98f, 1f), new(0.72f, 0.50f, 0.96f, 1f),
    };

    private readonly BreakoutBoard board = new();
    private readonly BreakoutRenderer renderer = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private RollingValue scoreRoll;
    private bool started;
    private bool finished;
    private bool pendingSubmit;
    private bool newBest;
    private int loadedBest;
    private int displayBest;
    private float resultAppear;
    private float trailTimer;
    private float lastFieldHeight = 1.6f;
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Games.Breakout);
    public string Genre => Loc.T(L.Games.GenreArcade);
    public void Open()
    {
        loadedBest = 0;
        started = false;
    }

    public void Close()
    {
    }

    public void Dispose()
    {
    }

    private void StartNewGame(float fieldHeight)
    {
        board.StartGame(fieldHeight);
        particles.Clear();
        fx.Clear();
        scoreRoll.Snap(0);
        finished = false;
        pendingSubmit = false;
        newBest = false;
        resultAppear = 0f;
        displayBest = loadedBest;
        started = true;
    }

    public void Draw(in GameContext context)
    {
        var deltaSeconds = context.DeltaSeconds;
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var body = context.Body;
        if (loadedBest == 0)
        {
            loadedBest = context.Stats.Get(GameId).BestScore;
            displayBest = loadedBest;
        }

        var rowY = body.Min.Y + 30f * scale;
        var pad = 6f * scale;
        var field = new Rect(new Vector2(body.Min.X + pad, rowY + 26f * scale),
            new Vector2(body.Max.X - pad, body.Max.Y - pad));
        var factor = field.Width;
        var fieldHeight = field.Height / factor;
        lastFieldHeight = fieldHeight;
        if (!started)
        {
            StartNewGame(fieldHeight);
        }

        board.SetFieldHeight(fieldHeight);
        if (pendingSubmit)
        {
            context.Stats.SubmitScore(GameId, board.Score);
            if (board.Score > loadedBest)
            {
                loadedBest = board.Score;
            }

            pendingSubmit = false;
        }

        if (!finished)
        {
            HandleInput(field, factor);
            var simDelta = fx.ScaleDelta(deltaSeconds);
            board.Update(simDelta);
            ReactToEvents(field, factor, scale);
            EmitBallTrails(simDelta, field, factor);
        }

        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);
        if (board.Score > displayBest)
        {
            displayBest = board.Score;
        }

        if (board.GameOver && !finished)
        {
            finished = true;
            resultAppear = 0f;
            newBest = board.Score > loadedBest;
            pendingSubmit = true;
        }

        var drawList = ImGui.GetWindowDrawList();
        GameScene.Ambient(drawList, body, Accent);
        var shake = fx.ShakeOffset(scale);
        var shakenField = new Rect(field.Min + shake, field.Max + shake);
        var beatingBest = board.Score > 0 && board.Score > loadedBest;
        GameHud.ScorePill(new Vector2(body.Center.X - 62f * scale, rowY), Loc.T(L.Games.Score), ref scoreRoll,
            board.Score, Accent, theme, deltaSeconds, beatingBest);
        GameHud.Pill(new Vector2(body.Center.X + 26f * scale, rowY), Loc.T(L.Games.Best), GameNumber.Label(displayBest),
            Accent, theme, displayBest > loadedBest);
        if (GameHud.RestartButton(new Vector2(body.Max.X - 20f * scale, rowY), 16f * scale, theme))
        {
            StartNewGame(fieldHeight);
            return;
        }

        DrawLives(body, rowY, scale);
        GameScene.Arena(drawList, shakenField, 14f * scale, scale, Accent);
        renderer.Draw(board, shakenField, Accent, scale);
        fx.DrawFlash(drawList, field, 0f);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        if (finished)
        {
            DrawResult(theme, body);
        }
    }

    private void HandleInput(Rect field, float factor)
    {
        var mouse = ImGui.GetMousePos();
        board.SetPaddle((mouse.X - field.Min.X) / factor);
        if (board.Attached && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && field.Contains(mouse))
        {
            board.Launch();
        }
    }

    private void ReactToEvents(Rect field, float factor, float scale)
    {
        for (var index = 0; index < board.BreakCount; index++)
        {
            var center = field.Min + board.BreakPosition(index) * factor;
            var color = BreakoutRenderer.BrickColorOf(board.BreakColor(index));
            particles.Burst(center, 8, color, 150f * scale, 2.6f, 0.45f, 300f);
            particles.Burst(center, 4, GamePalette.Lighten(color, 0.3f), 180f * scale, 2f, 0.4f, 260f, MathF.PI * 2f,
                0f, ParticleShape.Square);
        }

        if (board.BreakCount > 0)
        {
            fx.AddTrauma(MathF.Min(0.3f, 0.03f * board.BreakCount));
            var last = field.Min + board.BreakPosition(board.BreakCount - 1) * factor;
            fx.Shockwave(last, 34f * scale, new Vector4(1f, 1f, 1f, 0.55f), 0.32f, 2.2f);
            if (board.BreakCount >= 3)
            {
                fx.HitStop(0.035f);
            }

            if (board.Combo >= 5 && board.Combo % 5 == 0)
            {
                fx.AddText($"x{board.Combo}", last, Accent, 1.2f);
                fx.Shockwave(last, 70f * scale, GamePalette.Lighten(Accent, 0.3f), 0.5f, 3f);
            }
        }

        if (board.CaughtPowerThisFrame)
        {
            var paddle = field.Min + new Vector2(board.PaddleX, board.PaddleY) * factor;
            particles.Sparkle(paddle, 12, new Vector4(1f, 0.95f, 0.6f, 1f), 150f * scale, 2.6f, 0.8f);
            fx.Shockwave(paddle, 56f * scale, GamePalette.Lighten(Accent, 0.4f), 0.45f, 2.6f);
        }

        if (board.LevelCleared)
        {
            var top = new Vector2(field.Center.X, field.Min.Y + field.Height * 0.2f);
            particles.Confetti(top, 70, CelebrationPalette, 280f * scale, 4f, 1.4f);
            fx.Flash(GamePalette.Lighten(Accent, 0.4f), 0.18f);
        }

        if (board.LostLifeThisFrame)
        {
            fx.AddTrauma(0.7f);
            fx.HitStop(0.1f);
            fx.Flash(new Vector4(0.95f, 0.3f, 0.3f, 1f), 0.35f);
        }
    }

    private void EmitBallTrails(float deltaSeconds, Rect field, float factor)
    {
        if (board.Attached || deltaSeconds <= 0f)
        {
            return;
        }

        trailTimer -= deltaSeconds;
        if (trailTimer > 0f)
        {
            return;
        }

        trailTimer = TrailInterval;
        for (var index = 0; index < board.BallCount; index++)
        {
            var ball = board.GetBall(index);
            var center = field.Min + ball.Position * factor;
            particles.Burst(center, 1, GamePalette.Lighten(Accent, 0.3f) with { W = 0.4f }, 8f,
                BreakoutBoard.BallRadius * factor * 0.16f, 0.28f, 0f, MathF.PI * 2f, 0f, ParticleShape.GlowCircle);
        }
    }

    private void DrawLives(Rect body, float rowY, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var lastLife = board.Lives == 1;
        for (var index = 0; index < board.Lives; index++)
        {
            var center = new Vector2(body.Min.X + (12f + index * 15f) * scale, rowY);
            var color = lastLife
                ? new Vector4(0.95f, 0.35f, 0.35f, 0.6f + 0.4f * Pulse.Wave(Pulse.Fast))
                : Accent;
            drawList.AddCircleFilled(center, 4.5f * scale, ImGui.GetColorU32(color));
        }
    }

    private void DrawResult(PhoneTheme theme, Rect body)
    {
        resultAppear = MathF.Min(1f, resultAppear + ImGui.GetIO().DeltaTime * 3.4f);
        var bestValue = board.Score > displayBest ? board.Score : displayBest;
        var secondary = $"{Loc.T(L.Games.Best)} {GameNumber.Label(bestValue)}";
        var result = new GameResult(Loc.T(L.Games.GameOver), theme.TextStrong, Loc.T(L.Games.Score),
            GameNumber.Label(board.Score), secondary, newBest);
        if (GameOverlay.Draw(body, theme, Accent, resultAppear, result))
        {
            StartNewGame(lastFieldHeight);
        }
    }
}
