using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Games.Flow;

internal sealed class FlowApp : IMiniGame
{
    private const string GameId = "flow";
    private const float DifficultyRowY = 22f;
    private const float StatsRowY = 62f;
    private const float ProgressRowY = 92f;
    private const float BoardTop = 106f;
    private readonly FlowBoard board = new();
    private readonly FlowRenderer renderer = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly string[] difficultyLabels = new string[FlowBoard.DifficultyCount];
    private bool statsLoaded;
    private int difficulty;
    private int bestCleared;
    private int currentLevel = 1;
    private bool finished;
    private bool newBest;
    private bool pendingSubmit;
    private int clearedLevel;
    private float resultAppear;
    private float fillNudge;
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Games.Flow);
    public string Genre => Loc.T(L.Games.GenrePuzzle);

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
        board.Reset(level, difficulty);
        particles.Clear();
        fx.Clear();
        finished = false;
        newBest = false;
        pendingSubmit = false;
        resultAppear = 0f;
        fillNudge = 0f;
    }

    private void SelectDifficulty(int target, in GameContext context)
    {
        difficulty = target;
        bestCleared = context.Stats.Get(StatId(difficulty)).BestScore;
        StartLevel(bestCleared + 1);
    }

    public void Draw(in GameContext context)
    {
        var deltaSeconds = context.DeltaSeconds;
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var body = context.Body;
        if (!statsLoaded)
        {
            statsLoaded = true;
            SelectDifficulty(difficulty, context);
        }

        if (pendingSubmit)
        {
            context.Stats.SubmitScore(StatId(difficulty), clearedLevel);
            pendingSubmit = false;
        }

        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);
        GameScene.Ambient(ImGui.GetWindowDrawList(), body, Accent);
        var area = new Rect(new Vector2(body.Min.X + 8f * scale, body.Min.Y + BoardTop * scale),
            new Vector2(body.Max.X - 8f * scale, body.Max.Y - 10f * scale));
        var grid = GameGrid.Centered(area, board.Columns, board.Rows, 0.06f);
        var hovered = ResolveHover(grid);
        if (!finished)
        {
            HandleInput(hovered, grid, scale);
        }

        if (DrawDifficultyRow(context, scale))
        {
            return;
        }

        var filled = board.FilledCells();
        var connected = board.ConnectedColors();
        var awaitingFill = !finished && connected == board.ColorCount && filled < board.CellCount;
        fillNudge = Math.Clamp(fillNudge + deltaSeconds * (awaitingFill ? 4f : -5f), 0f, 1f);
        var statsY = body.Min.Y + StatsRowY * scale;
        GameHud.Pill(new Vector2(body.Center.X - 52f * scale, statsY), Loc.T(L.Games.Level),
            GameNumber.Label(currentLevel), Accent, theme);
        GameHud.Pill(new Vector2(body.Center.X + 52f * scale, statsY), Loc.T(L.Games.Flows),
            $"{connected}/{board.ColorCount}", Accent, theme);
        var progressRow = new Rect(new Vector2(body.Min.X + 12f * scale, body.Min.Y + ProgressRowY * scale),
            new Vector2(body.Max.X - 12f * scale, body.Min.Y + (ProgressRowY + 14f) * scale));
        renderer.DrawProgress(progressRow, theme, Accent, scale, filled, board.CellCount, awaitingFill);
        renderer.Draw(board, grid, theme, scale, fillNudge);
        var drawList = ImGui.GetWindowDrawList();
        fx.DrawFlash(drawList, body, 0f);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        if (finished)
        {
            DrawResult(theme, body, deltaSeconds);
        }
    }

    private bool DrawDifficultyRow(in GameContext context, float scale)
    {
        var body = context.Body;
        var theme = context.Theme;
        difficultyLabels[0] = Loc.T(L.Games.Easy);
        difficultyLabels[1] = Loc.T(L.Games.Medium);
        difficultyLabels[2] = Loc.T(L.Games.Hard);
        var rowY = body.Min.Y + DifficultyRowY * scale;
        var segmentRow = new Rect(new Vector2(body.Min.X + 4f * scale, rowY - 13f * scale),
            new Vector2(body.Max.X - 44f * scale, rowY + 13f * scale));
        var selection = SegmentStrip.Draw("flow.difficulty", segmentRow, difficultyLabels, difficulty, theme);
        if (selection != difficulty)
        {
            SelectDifficulty(selection, context);
            return true;
        }

        if (GameHud.RestartButton(new Vector2(body.Max.X - 20f * scale, rowY), 15f * scale, theme))
        {
            StartLevel(currentLevel);
            return true;
        }

        return false;
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
        if (column < 0 || column >= board.Columns || row < 0 || row >= board.Rows)
        {
            return -1;
        }

        return row * board.Columns + column;
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
            DragTo(hovered, grid, scale);
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            board.Release();
        }
    }

    private void DragTo(int target, GameGrid grid, float scale)
    {
        if (target < 0)
        {
            return;
        }

        for (var guard = 0; guard < FlowBoard.MaxCells; guard++)
        {
            var head = board.Head;
            if (head < 0 || head == target)
            {
                return;
            }

            var result = ExtendToward(head, target);
            if (result == FlowEvent.None)
            {
                return;
            }

            if (result == FlowEvent.Completed)
            {
                OnConnected(grid, scale);
                return;
            }
        }
    }

    private FlowEvent ExtendToward(int head, int target)
    {
        var direct = board.StepToward(head, target);
        if (direct >= 0)
        {
            var result = board.Extend(direct);
            if (result != FlowEvent.None)
            {
                return result;
            }
        }

        var aside = board.StepAside(head, target);
        if (aside < 0)
        {
            return FlowEvent.None;
        }

        return board.Extend(aside);
    }

    private void OnConnected(GameGrid grid, float scale)
    {
        var color = board.ActiveColor;
        if (color < 0)
        {
            return;
        }

        var head = board.PathCell(color, board.PathLength(color) - 1);
        var center = grid.CellCenter(head % board.Columns, head / board.Columns);
        particles.Burst(center, 14, FlowRenderer.ColorOf(color), 150f * scale, 3f, 0.5f, 240f);
        particles.Sparkle(center, 6, GamePalette.Lighten(FlowRenderer.ColorOf(color), 0.4f), 120f * scale, 2.2f, 0.6f);
        fx.Shockwave(center, grid.Pitch * 0.9f, GamePalette.Lighten(FlowRenderer.ColorOf(color), 0.25f), 0.42f, 2.6f);
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
        fx.HitStop(0.06f);
        fx.Flash(Accent, 0.35f);
        fx.Shockwave(grid.Center, grid.Width * 0.6f, GamePalette.Lighten(Accent, 0.3f), 0.6f, 3.2f);
        ReadOnlySpan<Vector4> palette = new[]
        {
            FlowRenderer.ColorOf(0), FlowRenderer.ColorOf(1), FlowRenderer.ColorOf(2), FlowRenderer.ColorOf(3),
        };
        particles.Confetti(new Vector2(grid.Center.X, grid.Bounds.Min.Y), 70, palette, 260f * scale, 4f, 1.3f);
    }

    private void DrawResult(PhoneTheme theme, Rect body, float deltaSeconds)
    {
        resultAppear = MathF.Min(1f, resultAppear + deltaSeconds * 3.4f);
        var secondary = $"{GameNumber.Label(board.Moves)} {Loc.T(L.Games.Moves)}";
        var result = new GameResult(Loc.T(L.Games.YouWin), Accent, Loc.T(L.Games.Level), GameNumber.Label(clearedLevel),
            secondary, newBest, Loc.T(L.Games.NextLevel));
        if (GameOverlay.Draw(body, theme, Accent, resultAppear, result))
        {
            StartLevel(currentLevel + 1);
        }
    }

    private static string StatId(int difficulty)
    {
        return difficulty switch
        {
            1 => "flow.medium",
            2 => "flow.hard",
            _ => "flow.easy",
        };
    }
}
