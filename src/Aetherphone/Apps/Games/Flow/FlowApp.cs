using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Games.Flow;

internal sealed class FlowApp : IMiniGame
{
    private const string GameId = "flow";

    private readonly FlowBoard board = new();

    private readonly FlowRenderer renderer = new();

    private readonly ParticleSystem particles = new();

    private readonly FeedbackFx fx = new();

    private bool statsLoaded;

    private int bestCleared;

    private int currentLevel = 1;

    private bool finished;

    private bool newBest;

    private bool pendingSubmit;

    private int clearedLevel;

    private float resultAppear;

    public string Id => GameId;

    public string Title => Loc.T(L.Games.Flow);

    public string Genre => Loc.T(L.Games.GenrePuzzle);

    public Vector4 Accent => new(0.72f, 0.46f, 0.96f, 1f);

    public void Open()
    {
        statsLoaded = false;
    }

    public void Close()
    {
    }

    public void Dispose()
    {
    }

    private void StartLevel(int level)
    {
        currentLevel = level;
        board.Reset(level);
        particles.Clear();
        fx.Clear();
        finished = false;
        newBest = false;
        pendingSubmit = false;
        resultAppear = 0f;
    }

    public void Draw(in GameContext context)
    {
        var deltaSeconds = context.DeltaSeconds;
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var body = context.Body;

        if (!statsLoaded)
        {
            bestCleared = context.Stats.Get(GameId).BestScore;
            statsLoaded = true;
            StartLevel(bestCleared + 1);
        }

        if (pendingSubmit)
        {
            context.Stats.SubmitScore(GameId, clearedLevel);
            pendingSubmit = false;
        }

        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);

        var rowY = body.Min.Y + 30f * scale;
        GameHud.Pill(new Vector2(body.Center.X - 52f * scale, rowY), Loc.T(L.Games.Level), GameNumber.Label(currentLevel), Accent, theme);
        GameHud.Pill(new Vector2(body.Center.X + 52f * scale, rowY), Loc.T(L.Games.Flows), $"{board.ConnectedColors()}/{board.ColorCount}", Accent, theme);

        if (GameHud.RestartButton(new Vector2(body.Max.X - 20f * scale, rowY), 16f * scale, theme))
        {
            StartLevel(currentLevel);
            return;
        }

        var area = new Rect(new Vector2(body.Min.X + 8f * scale, rowY + 30f * scale), new Vector2(body.Max.X - 8f * scale, body.Max.Y - 10f * scale));
        var grid = GameGrid.Centered(area, board.Size, board.Size, 0.06f);

        var hovered = ResolveHover(grid);
        if (!finished)
        {
            HandleInput(hovered, grid, scale);
        }

        renderer.Draw(board, grid, theme, scale);

        var drawList = ImGui.GetWindowDrawList();
        fx.DrawFlash(drawList, body, 0f);
        particles.Draw(drawList, scale);

        if (finished)
        {
            DrawResult(theme, body, deltaSeconds);
        }
    }

    private int ResolveHover(GameGrid grid)
    {
        var mouse = ImGui.GetMousePos();
        if (!grid.Bounds.Contains(mouse))
        {
            return -1;
        }

        var local = mouse - grid.Origin;
        var column = (int)(local.X / grid.Pitch);
        var row = (int)(local.Y / grid.Pitch);
        if (column < 0 || column >= board.Size || row < 0 || row >= board.Size)
        {
            return -1;
        }

        return row * board.Size + column;
    }

    private void HandleInput(int hovered, GameGrid grid, float scale)
    {
        if (hovered >= 0 && (board.IsEndpoint(hovered) || board.Owner(hovered) >= 0))
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            board.Press(hovered);
        }
        else if (ImGui.IsMouseDown(ImGuiMouseButton.Left) && board.ActiveColor >= 0)
        {
            var result = board.Extend(hovered);
            if (result == FlowEvent.Completed)
            {
                OnConnected(grid, scale);
            }
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            board.Release();
        }
    }

    private void OnConnected(GameGrid grid, float scale)
    {
        var color = board.ActiveColor;
        if (color < 0)
        {
            return;
        }

        var head = board.PathCell(color, board.PathLength(color) - 1);
        var center = grid.CellCenter(head % board.Size, head / board.Size);
        particles.Burst(center, 14, FlowRenderer.ColorOf(color), 150f * scale, 3f, 0.5f, 240f);
        fx.AddTrauma(0.12f);

        if (board.IsSolved())
        {
            OnSolved(grid, scale);
        }
    }

    private void OnSolved(GameGrid grid, float scale)
    {
        finished = true;
        resultAppear = 0f;
        clearedLevel = currentLevel;
        newBest = clearedLevel > bestCleared;
        if (newBest)
        {
            bestCleared = clearedLevel;
        }

        pendingSubmit = true;
        fx.AddTrauma(0.3f);
        fx.Flash(Accent, 0.35f);

        ReadOnlySpan<Vector4> palette = new[]
        {
            FlowRenderer.ColorOf(0),
            FlowRenderer.ColorOf(1),
            FlowRenderer.ColorOf(2),
            FlowRenderer.ColorOf(3),
        };
        particles.Confetti(new Vector2(grid.Center.X, grid.Bounds.Min.Y), 70, palette, 260f * scale, 4f, 1.3f);
    }

    private void DrawResult(PhoneTheme theme, Rect body, float deltaSeconds)
    {
        resultAppear = MathF.Min(1f, resultAppear + deltaSeconds * 3.4f);

        var secondary = $"{GameNumber.Label(board.Moves)} {Loc.T(L.Games.Moves)}";
        var result = new GameResult(
            Loc.T(L.Games.YouWin),
            Accent,
            Loc.T(L.Games.Level),
            GameNumber.Label(clearedLevel),
            secondary,
            newBest,
            Loc.T(L.Games.NextLevel));

        if (GameOverlay.Draw(body, theme, Accent, resultAppear, result))
        {
            StartLevel(currentLevel + 1);
        }
    }
}
