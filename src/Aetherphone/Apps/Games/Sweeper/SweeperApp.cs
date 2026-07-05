using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Games.Sweeper;

internal sealed class SweeperApp : IMiniGame
{
    private const string GameId = "minesweeper";
    private const float FlagPopSpeed = 7f;
    private readonly SweeperBoard board = new();
    private readonly SweeperRenderer renderer = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly float[] flagAnim = new float[SweeperBoard.MaxCells];
    private readonly string[] difficultyLabels = new string[3];
    private Difficulty difficulty = Difficulty.Easy;
    private SweeperState previousState = SweeperState.Playing;
    private float elapsed;
    private float resultAppear;
    private bool pendingResultSubmit;
    private bool newBestTime;
    private int loadedBestTime;
    private string resultTimeText = "0:00";
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Games.Sweeper);
    public string Genre => Loc.T(L.Games.GenreLogic);
    public void Open()
    {
        StartNewGame(difficulty);
    }

    public void Close()
    {
    }

    public void Dispose()
    {
    }

    private void StartNewGame(Difficulty target)
    {
        difficulty = target;
        board.Reset(target);
        particles.Clear();
        fx.Clear();
        Array.Clear(flagAnim, 0, SweeperBoard.MaxCells);
        previousState = SweeperState.Playing;
        elapsed = 0f;
        resultAppear = 0f;
        pendingResultSubmit = false;
        newBestTime = false;
        loadedBestTime = -1;
    }

    public void Draw(in GameContext context)
    {
        var deltaSeconds = context.DeltaSeconds;
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var body = context.Body;
        if (loadedBestTime < 0)
        {
            loadedBestTime = context.Stats.Get(StatId(difficulty)).BestTimeSeconds;
        }

        if (board.State == SweeperState.Playing)
        {
            elapsed += deltaSeconds;
        }

        UpdateFlagAnim(deltaSeconds);
        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);
        if (pendingResultSubmit)
        {
            newBestTime = context.Stats.SubmitTime(StatId(difficulty), (int)elapsed);
            pendingResultSubmit = false;
        }

        DrawDifficultyRow(body, theme, scale);
        DrawStatsRow(body, theme, scale);
        var gridArea = new Rect(new Vector2(body.Min.X, body.Min.Y + 96f * scale),
            new Vector2(body.Max.X, body.Max.Y - 8f * scale));
        var grid = GameGrid.Centered(gridArea, board.Columns, board.Rows, 0.10f);
        var hoveredIndex = ResolveHover(grid);
        if (board.State == SweeperState.Playing)
        {
            HandleInput(hoveredIndex);
        }

        DetectStateChange(grid);
        renderer.Draw(board, grid, hoveredIndex, flagAnim, theme, scale);
        var drawList = ImGui.GetWindowDrawList();
        fx.DrawFlash(drawList, body, 0f);
        particles.Draw(drawList, scale);
        if (board.State != SweeperState.Playing)
        {
            DrawResult(context, theme, body);
        }
    }

    private void DrawDifficultyRow(Rect body, PhoneTheme theme, float scale)
    {
        difficultyLabels[0] = Loc.T(L.Games.Easy);
        difficultyLabels[1] = Loc.T(L.Games.Medium);
        difficultyLabels[2] = Loc.T(L.Games.Hard);
        var rowY = body.Min.Y + 22f * scale;
        var segmentRow = new Rect(new Vector2(body.Min.X + 4f * scale, rowY - 13f * scale),
            new Vector2(body.Max.X - 44f * scale, rowY + 13f * scale));
        var selected = SegmentStrip.Draw("sweeper.difficulty", segmentRow, difficultyLabels, (int)difficulty, theme);
        if (selected != (int)difficulty)
        {
            StartNewGame((Difficulty)selected);
            return;
        }

        if (GameHud.RestartButton(new Vector2(body.Max.X - 20f * scale, rowY), 15f * scale, theme))
        {
            StartNewGame(difficulty);
        }
    }

    private void DrawStatsRow(Rect body, PhoneTheme theme, float scale)
    {
        var rowY = body.Min.Y + 64f * scale;
        GameHud.Pill(new Vector2(body.Center.X - 50f * scale, rowY), Loc.T(L.Games.Mines),
            GameNumber.Label(board.MinesRemaining), Accent, theme);
        GameHud.Pill(new Vector2(body.Center.X + 50f * scale, rowY), Loc.T(L.Games.Time),
            GameNumber.Label((int)elapsed), Accent, theme);
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

        var index = row * board.Columns + column;
        var cell = grid.Cell(column, row);
        return cell.Contains(mouse) ? index : -1;
    }

    private void HandleInput(int hoveredIndex)
    {
        if (hoveredIndex < 0)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            if (board.IsRevealed(hoveredIndex))
            {
                board.Chord(hoveredIndex);
            }
            else
            {
                board.Reveal(hoveredIndex);
            }
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Right) && !board.IsRevealed(hoveredIndex))
        {
            var wasFlagged = board.IsFlagged(hoveredIndex);
            board.ToggleFlag(hoveredIndex);
            if (!wasFlagged && board.IsFlagged(hoveredIndex))
            {
                flagAnim[hoveredIndex] = 1f;
            }
        }
    }

    private void DetectStateChange(GameGrid grid)
    {
        if (board.State == previousState)
        {
            return;
        }

        if (board.State == SweeperState.Lost)
        {
            resultAppear = 0f;
            pendingResultSubmit = false;
            BuildResultTime();
            fx.AddTrauma(0.95f);
            fx.Flash(new Vector4(0.95f, 0.3f, 0.3f, 1f), 0.45f);
            if (board.ClickedBomb >= 0)
            {
                var center = grid.CellCenter(board.ClickedBomb % board.Columns, board.ClickedBomb / board.Columns);
                particles.Burst(center, 40, new Vector4(0.98f, 0.45f, 0.32f, 1f), 360f * ImGuiHelpers.GlobalScale, 4.5f,
                    0.85f, 420f);
            }
        }
        else if (board.State == SweeperState.Won)
        {
            resultAppear = 0f;
            pendingResultSubmit = true;
            BuildResultTime();
            ReadOnlySpan<Vector4> palette = new[]
            {
                Accent, Styling.AccentMint, Styling.AccentAmber, Styling.AccentPink,
            };
            particles.Confetti(new Vector2(grid.Center.X, grid.Bounds.Min.Y), 70, palette,
                260f * ImGuiHelpers.GlobalScale, 4f, 1.3f);
        }

        previousState = board.State;
    }

    private void BuildResultTime()
    {
        var seconds = (int)elapsed;
        resultTimeText = $"{seconds / 60}:{seconds % 60:D2}";
    }

    private void UpdateFlagAnim(float deltaSeconds)
    {
        for (var index = 0; index < SweeperBoard.MaxCells; index++)
        {
            if (flagAnim[index] > 0f)
            {
                flagAnim[index] = MathF.Max(0f, flagAnim[index] - deltaSeconds * FlagPopSpeed);
            }
        }
    }

    private void DrawResult(in GameContext context, PhoneTheme theme, Rect body)
    {
        resultAppear = MathF.Min(1f, resultAppear + context.DeltaSeconds * 3.4f);
        var won = board.State == SweeperState.Won;
        var title = won ? Loc.T(L.Games.YouWin) : Loc.T(L.Games.Boom);
        var titleColor = won ? Accent : theme.Danger;
        string? secondary = null;
        if (won && loadedBestTime > 0)
        {
            secondary = $"{Loc.T(L.Games.Best)} {loadedBestTime / 60}:{loadedBestTime % 60:D2}";
        }

        var result = new GameResult(title, titleColor, Loc.T(L.Games.Time), resultTimeText, secondary,
            won && newBestTime);
        if (GameOverlay.Draw(body, theme, Accent, resultAppear, result))
        {
            StartNewGame(difficulty);
        }
    }

    private static string StatId(Difficulty difficulty)
    {
        return difficulty switch
        {
            Difficulty.Medium => "minesweeper.medium",
            Difficulty.Hard => "minesweeper.hard",
            _ => "minesweeper.easy",
        };
    }
}
