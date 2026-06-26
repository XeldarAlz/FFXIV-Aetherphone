using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Games.Breakout;

internal sealed class BreakoutApp : IMiniGame
{
    private const string GameId = "breakout";

    private readonly BreakoutBoard board = new();

    private readonly BreakoutRenderer renderer = new();

    private readonly ParticleSystem particles = new();

    private readonly FeedbackFx fx = new();

    private bool started;

    private bool finished;

    private bool pendingSubmit;

    private bool newBest;

    private int loadedBest;

    private int displayBest;

    private float resultAppear;

    private float lastFieldHeight = 1.6f;

    public string Id => GameId;

    public string Title => Loc.T(L.Games.Breakout);

    public string Genre => Loc.T(L.Games.GenreArcade);

    public Vector4 Accent => new(0.95f, 0.45f, 0.50f, 1f);

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
        var field = new Rect(
            new Vector2(body.Min.X + pad, rowY + 26f * scale),
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
            pendingSubmit = false;
        }

        if (!finished)
        {
            HandleInput(field, factor);
            board.Update(deltaSeconds);
            ReactToEvents(field, factor, scale);
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

        var shake = fx.ShakeOffset(scale);
        var shakenField = new Rect(field.Min + shake, field.Max + shake);

        GameHud.Pill(new Vector2(body.Center.X - 62f * scale, rowY), Loc.T(L.Games.Score), GameNumber.Label(board.Score), Accent, theme);
        GameHud.Pill(new Vector2(body.Center.X + 26f * scale, rowY), Loc.T(L.Games.Best), GameNumber.Label(displayBest), Accent, theme, displayBest > loadedBest);
        if (GameHud.RestartButton(new Vector2(body.Max.X - 20f * scale, rowY), 16f * scale, theme))
        {
            StartNewGame(fieldHeight);
            return;
        }

        DrawLives(body, rowY, scale);

        renderer.Draw(board, shakenField, Accent, scale);

        var drawList = ImGui.GetWindowDrawList();
        fx.DrawFlash(drawList, field, 0f);
        particles.Draw(drawList, scale);
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
            particles.Burst(center, 8, BreakoutRenderer.BrickColorOf(board.BreakColor(index)), 150f * scale, 2.6f, 0.45f, 300f);
        }

        if (board.BreakCount > 0)
        {
            fx.AddTrauma(MathF.Min(0.3f, 0.03f * board.BreakCount));
            if (board.Combo >= 5 && board.Combo % 5 == 0)
            {
                var last = field.Min + board.BreakPosition(board.BreakCount - 1) * factor;
                fx.AddText($"x{board.Combo}", last, Accent, 1.2f);
            }
        }

        if (board.LostLifeThisFrame)
        {
            fx.AddTrauma(0.7f);
            fx.Flash(new Vector4(0.95f, 0.3f, 0.3f, 1f), 0.35f);
        }
    }

    private void DrawLives(Rect body, float rowY, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        for (var index = 0; index < board.Lives; index++)
        {
            var center = new Vector2(body.Min.X + (12f + index * 15f) * scale, rowY);
            drawList.AddCircleFilled(center, 4.5f * scale, ImGui.GetColorU32(Accent));
        }
    }

    private void DrawResult(PhoneTheme theme, Rect body)
    {
        resultAppear = MathF.Min(1f, resultAppear + ImGui.GetIO().DeltaTime * 3.4f);

        var bestValue = board.Score > displayBest ? board.Score : displayBest;
        var secondary = $"{Loc.T(L.Games.Best)} {GameNumber.Label(bestValue)}";
        var result = new GameResult(Loc.T(L.Games.GameOver), theme.TextStrong, Loc.T(L.Games.Score), GameNumber.Label(board.Score), secondary, newBest);

        if (GameOverlay.Draw(body, theme, Accent, resultAppear, result))
        {
            StartNewGame(lastFieldHeight);
        }
    }
}
