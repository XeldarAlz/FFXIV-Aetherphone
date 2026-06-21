using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Games;

internal enum MineState
{
    Playing,
    Won,
    Lost,
}

internal sealed class MinesweeperApp : IPhoneApp
{
    private const int Columns = 9;
    private const int Rows = 9;
    private const int TotalCells = Columns * Rows;
    private const int MineCount = 10;
    private const float FlagPopDuration = 0.10f;

    private static readonly Vector4[] NumberColors =
    {
        new(0.30f, 0.55f, 0.95f, 1f),
        new(0.30f, 0.75f, 0.45f, 1f),
        new(0.95f, 0.35f, 0.35f, 1f),
        new(0.40f, 0.40f, 0.85f, 1f),
        new(0.85f, 0.30f, 0.30f, 1f),
        new(0.30f, 0.65f, 0.70f, 1f),
        new(0.30f, 0.30f, 0.30f, 1f),
        new(0.55f, 0.55f, 0.60f, 1f),
    };

    public string Id => "minesweeper";
    public string DisplayName => "Sweeper";
    public string Glyph => "S";
    public Vector4 Accent => Styling.AccentBlue;
    public int BadgeCount => 0;

    private readonly bool[] mines = new bool[TotalCells];
    private readonly bool[] revealed = new bool[TotalCells];
    private readonly bool[] flagged = new bool[TotalCells];

    private readonly float[] flagAnim = new float[TotalCells];

    private MineState state;
    private bool firstClick;
    private int flagCount;
    private int clickedBombIndex = -1;
    private DateTime startTime;
    private readonly Random rng = new();

    private readonly int[] neighborCache = new int[8];

    private PhoneTheme frameTheme = PhoneTheme.Default;

    public void OnOpened() => ResetGame();

    public void OnClosed()
    {
    }

    public void Draw(in PhoneContext context)
    {
        frameTheme = context.Theme;
        var body = GameCommon.LayoutBelowHeader(context.Content);

        AppHeader.Draw(context, DisplayName);

        using (AppSurface.Begin(body))
        {
            var contentMax = new Vector2(body.Min.X, body.Min.Y) + ImGui.GetContentRegionAvail();
            var surface = new Rect(new Vector2(body.Min.X, body.Min.Y), contentMax);
            var scale = ImGuiHelpers.GlobalScale;

            var statsY = body.Min.Y + 26f * scale;
            DrawStatsBar(surface, statsY, scale);

            var gridMinY = statsY + 30f * scale;
            var gridArea = new Rect(new Vector2(surface.Min.X, gridMinY), surface.Max);
            var grid = GameCommon.LayoutGameGrid(gridArea, Columns, Rows, 0.08f);

            for (var row = 0; row < Rows; row++)
            {
                for (var column = 0; column < Columns; column++)
                {
                    DrawCell(grid, column, row, scale);
                }
            }

            if (state == MineState.Lost)
            {
                if (GameCommon.DrawGameOverOverlay(surface, frameTheme, 0, "Boom"))
                {
                    ResetGame();
                }
            }

            if (state == MineState.Won)
            {
                var elapsed = (int)(DateTime.Now - startTime).TotalSeconds;
                if (GameCommon.DrawWinOverlay(surface, frameTheme, 0, elapsed))
                {
                    ResetGame();
                }
            }
        }
    }

    private void ResetGame()
    {
        Array.Clear(mines, 0, TotalCells);
        Array.Clear(revealed, 0, TotalCells);
        Array.Clear(flagged, 0, TotalCells);
        Array.Clear(flagAnim, 0, TotalCells);

        state = MineState.Playing;
        firstClick = true;
        flagCount = 0;
        clickedBombIndex = -1;
        startTime = DateTime.Now;
    }

    private void PlaceMines(int safeIndex)
    {
        var placed = 0;
        while (placed < MineCount)
        {
            var index = rng.Next(TotalCells);
            if (index == safeIndex || mines[index])
            {
                continue;
            }

            mines[index] = true;
            placed++;
        }
    }

    private int CountAdjacentMines(int centerIndex)
    {
        var centerColumn = centerIndex % Columns;
        var centerRow = centerIndex / Columns;
        var count = 0;

        for (var rowOffset = -1; rowOffset <= 1; rowOffset++)
        {
            var row = centerRow + rowOffset;
            if (row < 0 || row >= Rows)
            {
                continue;
            }

            for (var columnOffset = -1; columnOffset <= 1; columnOffset++)
            {
                var column = centerColumn + columnOffset;
                if (column < 0 || column >= Columns)
                {
                    continue;
                }

                if (rowOffset == 0 && columnOffset == 0)
                {
                    continue;
                }

                if (mines[row * Columns + column])
                {
                    count++;
                }
            }
        }

        return count;
    }

    private int GetNeighbors(int centerIndex)
    {
        var centerColumn = centerIndex % Columns;
        var centerRow = centerIndex / Columns;
        var count = 0;

        for (var rowOffset = -1; rowOffset <= 1; rowOffset++)
        {
            var row = centerRow + rowOffset;
            if (row < 0 || row >= Rows)
            {
                continue;
            }

            for (var columnOffset = -1; columnOffset <= 1; columnOffset++)
            {
                var column = centerColumn + columnOffset;
                if (column < 0 || column >= Columns)
                {
                    continue;
                }

                if (rowOffset == 0 && columnOffset == 0)
                {
                    continue;
                }

                neighborCache[count] = row * Columns + column;
                count++;
            }
        }

        return count;
    }

    private void FloodReveal(int startIndex)
    {
        var queue = new int[TotalCells];
        var queueRead = 0;
        var queueWrite = 1;
        queue[0] = startIndex;
        revealed[startIndex] = true;

        while (queueRead < queueWrite)
        {
            var index = queue[queueRead];
            queueRead++;

            var adjacentCount = CountAdjacentMines(index);

            if (adjacentCount > 0)
            {
                continue;
            }

            var neighborCount = GetNeighbors(index);
            for (var n = 0; n < neighborCount; n++)
            {
                var neighbor = neighborCache[n];
                if (revealed[neighbor] || flagged[neighbor])
                {
                    continue;
                }

                queue[queueWrite] = neighbor;
                queueWrite++;
                revealed[neighbor] = true;
            }
        }
    }

    private void RevealCell(int index)
    {
        if (revealed[index] || flagged[index])
        {
            return;
        }

        if (mines[index])
        {
            TriggerGameOver(index);
            return;
        }

        var adjacentCount = CountAdjacentMines(index);

        if (adjacentCount == 0)
        {
            FloodReveal(index);
        }
        else
        {
            revealed[index] = true;
        }

        CheckWin();
    }

    private void TriggerGameOver(int clickedIndex)
    {
        clickedBombIndex = clickedIndex;
        state = MineState.Lost;

        for (var i = 0; i < TotalCells; i++)
        {
            if (mines[i])
            {
                revealed[i] = true;
            }
        }
    }

    private void ToggleFlag(int index)
    {
        if (revealed[index])
        {
            return;
        }

        if (flagged[index])
        {
            flagged[index] = false;
            flagCount--;
            flagAnim[index] = 0f;
        }
        else
        {
            flagged[index] = true;
            flagCount++;
            flagAnim[index] = 1f;
        }
    }

    private void CheckWin()
    {
        for (var i = 0; i < TotalCells; i++)
        {
            if (!mines[i] && !revealed[i])
            {
                return;
            }
        }

        state = MineState.Won;
    }

    private void DrawStatsBar(Rect surface, float statsY, float scale)
    {
        GameCommon.DrawScorePill(new Vector2(surface.Center.X - 52f * scale, statsY), "Mines", MineCount - flagCount, frameTheme);

        var resetCenter = new Vector2(surface.Center.X, statsY);
        var resetSize = 32f * scale;
        var resetMin = new Vector2(resetCenter.X - resetSize * 0.5f, statsY - resetSize * 0.5f);
        var resetMax = new Vector2(resetCenter.X + resetSize * 0.5f, statsY + resetSize * 0.5f);
        var resetRounding = resetSize * 0.5f;

        var resetHovered = GameCommon.HitTest(resetMin, resetMax);
        var resetBg = resetHovered ? GameCommon.ButtonHover : GameCommon.ButtonRest;

        GameCommon.FillRect(resetMin, resetMax, resetBg, resetRounding);
        GameCommon.DrawRect(resetMin, resetMax, Styling.BorderDim, resetRounding, 1f);

        if (resetHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var resetSymbol = state == MineState.Lost ? "x" : "o";
        Typography.DrawCentered(resetCenter, resetSymbol, frameTheme.TextStrong, 1.1f);

        if (resetHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            ResetGame();
        }

        var elapsed = (int)(DateTime.Now - startTime).TotalSeconds;
        GameCommon.DrawScorePill(new Vector2(surface.Center.X + 52f * scale, statsY), "Time", elapsed, frameTheme);
    }

    private void DrawCell(Rect grid, int column, int row, float scale)
    {
        var index = row * Columns + column;
        var (cellMin, cellMax) = GameCommon.CellBounds(grid, column, row, Columns, Rows, 0.08f);
        var rounding = 4f * scale;
        var center = (cellMin + cellMax) * 0.5f;

        var cellRevealed = revealed[index];
        var cellFlagged = flagged[index];
        var cellMine = mines[index];

        if (cellRevealed)
        {
            if (cellMine)
            {
                var color = (index == clickedBombIndex && state == MineState.Lost)
                    ? new Vector4(1f, 0.25f, 0.25f, 1f)
                    : Styling.AccentRose;
                GameCommon.FillRect(cellMin, cellMax, color, rounding);
                Typography.DrawCentered(center, "x", frameTheme.TextStrong, 1.2f);
            }
            else
            {
                var adjacent = CountAdjacentMines(index);
                GameCommon.FillRect(cellMin, cellMax, GameCommon.CardFaceUp, rounding);

                if (adjacent > 0)
                {
                    var color = NumberColors[adjacent - 1];
                    Typography.DrawCentered(center, adjacent.ToString(), color, 1.15f);
                }
            }
        }
        else
        {
            var hovered = GameCommon.HitTest(cellMin, cellMax);
            var bg = hovered ? GameCommon.CardFaceDownHover : GameCommon.CardFaceDown;
            GameCommon.FillRect(cellMin, cellMax, bg, rounding);
            GameCommon.DrawRect(cellMin, cellMax, Styling.BorderDim, rounding, 1f);

            if (cellFlagged)
            {
                var pop = flagAnim[index];
                if (pop > 0f)
                {
                    pop = MathF.Max(0f, pop - ImGui.GetIO().DeltaTime / FlagPopDuration);
                    flagAnim[index] = pop;

                    var popScale = 1f + pop * 0.2f;
                    Typography.DrawCentered(center, "F", Styling.AccentRose, 1f * popScale);
                }
                else
                {
                    Typography.DrawCentered(center, "F", Styling.AccentRose, 1f);
                }
            }

            if (state != MineState.Playing)
            {
                return;
            }

            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && hovered)
            {
                HandleClick(index);
            }

            if (ImGui.IsMouseClicked(ImGuiMouseButton.Right) && hovered)
            {
                ToggleFlag(index);
            }
        }
    }

    private void HandleClick(int index)
    {
        if (firstClick)
        {
            firstClick = false;
            PlaceMines(index);
        }

        RevealCell(index);
    }

    public void Dispose()
    {
    }
}
