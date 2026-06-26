using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Games.BubbleShooter;

internal sealed class BubbleShooterApp : IMiniGame
{
    private const string GameId = "bubbles";

    private readonly BubbleBoard board = new();

    private readonly BubbleRenderer renderer = new();

    private readonly ParticleSystem particles = new();

    private readonly FeedbackFx fx = new();

    private bool started;

    private bool finished;

    private bool pendingSubmit;

    private bool newBest;

    private int loadedBest;

    private int displayBest;

    private float resultAppear;

    private float lastFieldHeight = 1.7f;

    public string Id => GameId;

    public string Title => Loc.T(L.Games.Bubbles);

    public string Genre => Loc.T(L.Games.GenreArcade);

    public Vector4 Accent => new(0.30f, 0.82f, 0.74f, 1f);

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
        board.Reset(fieldHeight);
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

        var aim = ComputeAim(field, factor);

        if (!finished)
        {
            if (board.GameOver)
            {
                finished = true;
                resultAppear = 0f;
                newBest = board.Score > loadedBest;
                pendingSubmit = true;
            }
            else
            {
                HandleInput(field, aim);
                board.Update(deltaSeconds);
                ReactToEvents(field, factor, scale);
            }
        }

        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);

        if (board.Score > displayBest)
        {
            displayBest = board.Score;
        }

        GameHud.Pill(new Vector2(body.Center.X - 62f * scale, rowY), Loc.T(L.Games.Score), GameNumber.Label(board.Score), Accent, theme);
        GameHud.Pill(new Vector2(body.Center.X + 26f * scale, rowY), Loc.T(L.Games.Best), GameNumber.Label(displayBest), Accent, theme, displayBest > loadedBest);
        if (GameHud.RestartButton(new Vector2(body.Max.X - 20f * scale, rowY), 16f * scale, theme))
        {
            StartNewGame(fieldHeight);
            return;
        }

        renderer.Draw(board, field, scale, finished ? Vector2.Zero : aim, theme);

        var drawList = ImGui.GetWindowDrawList();
        fx.DrawFlash(drawList, field, 0f);
        particles.Draw(drawList, scale);
        fx.DrawText();

        if (finished)
        {
            DrawResult(theme, body);
        }
    }

    private Vector2 ComputeAim(Rect field, float factor)
    {
        var mouse = ImGui.GetMousePos();
        var mouseNorm = (mouse - field.Min) / factor;
        var direction = mouseNorm - board.LauncherPosition;
        if (direction.LengthSquared() < 0.0001f)
        {
            return new Vector2(0f, -1f);
        }

        if (direction.Y > -0.2f)
        {
            direction.Y = -0.2f;
        }

        return Vector2.Normalize(direction);
    }

    private void HandleInput(Rect field, Vector2 aim)
    {
        var mouse = ImGui.GetMousePos();
        if (field.Contains(mouse))
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                board.Fire(aim);
            }
        }
    }

    private void ReactToEvents(Rect field, float factor, float scale)
    {
        if (board.PopCount == 0)
        {
            return;
        }

        var sum = Vector2.Zero;
        for (var index = 0; index < board.PopCount; index++)
        {
            var center = field.Min + board.PopPosition(index) * factor;
            sum += center;
            particles.Burst(center, 9, BubbleRenderer.ColorOf(board.PopColor(index)), 150f * scale, 2.8f, 0.5f, 280f);
        }

        if (board.PopCount >= 5)
        {
            fx.AddText($"{board.PopCount}!", sum / board.PopCount, Accent, 1.4f);
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
