using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Nonogram;

internal readonly struct NonogramLayout
{
    public readonly float CellSize;
    public readonly Vector2 GridOrigin;
    public readonly float LeftBand;
    public readonly float TopBand;

    public NonogramLayout(float cellSize, Vector2 gridOrigin, float leftBand, float topBand)
    {
        CellSize = cellSize;
        GridOrigin = gridOrigin;
        LeftBand = leftBand;
        TopBand = topBand;
    }

    public Vector2 CellMin(int column, int row)
    {
        return new Vector2(GridOrigin.X + column * CellSize, GridOrigin.Y + row * CellSize);
    }

    public int HitTest(Vector2 point, int size)
    {
        var column = (int)MathF.Floor((point.X - GridOrigin.X) / CellSize);
        var row = (int)MathF.Floor((point.Y - GridOrigin.Y) / CellSize);
        if (column < 0 || column >= size || row < 0 || row >= size)
        {
            return -1;
        }

        return row * size + column;
    }
}

internal sealed class NonogramRenderer
{
    public static NonogramLayout Layout(Rect area, NonogramBoard board, float scale)
    {
        var acrossCells = board.MaxRowClues + board.Size;
        var downCells = board.MaxColumnClues + board.Size;
        var cellFromWidth = area.Width / acrossCells;
        var cellFromHeight = area.Height / downCells;
        var cellSize = MathF.Min(cellFromWidth, cellFromHeight);
        var leftBand = board.MaxRowClues * cellSize;
        var topBand = board.MaxColumnClues * cellSize;
        var totalWidth = leftBand + board.Size * cellSize;
        var totalHeight = topBand + board.Size * cellSize;
        var originX = area.Center.X - totalWidth * 0.5f + leftBand;
        var originY = area.Center.Y - totalHeight * 0.5f + topBand;
        return new NonogramLayout(cellSize, new Vector2(originX, originY), leftBand, topBand);
    }

    public void Draw(NonogramBoard board, NonogramLayout layout, int hoveredCell, float[] fillAnimation,
        PhoneTheme theme, Vector4 accent, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var cell = layout.CellSize;
        var gridMin = layout.GridOrigin;
        var gridMax = new Vector2(gridMin.X + board.Size * cell, gridMin.Y + board.Size * cell);
        var rounding = MathF.Max(1.5f, cell * 0.12f);
        var inset = MathF.Max(1f, cell * 0.06f);
        drawList.AddRectFilled(gridMin, gridMax, ImGui.GetColorU32(GamePalette.Board), rounding);
        var clueScale = MathF.Max(0.5f, MathF.Min(0.82f, cell / (32f * scale)));
        DrawColumnClues(drawList, board, layout, accent, theme, clueScale, cell);
        DrawRowClues(drawList, board, layout, accent, theme, clueScale, cell);
        for (var row = 0; row < board.Size; row++)
        {
            for (var column = 0; column < board.Size; column++)
            {
                var index = row * board.Size + column;
                var min = layout.CellMin(column, row);
                var max = min + new Vector2(cell, cell);
                DrawCell(drawList, board.MarkAt(index), min, max, inset, rounding, index == hoveredCell,
                    fillAnimation[index], accent, scale);
            }
        }

        DrawGridLines(drawList, board, gridMin, gridMax, cell, scale);
    }

    private void DrawCell(ImDrawListPtr drawList, CellMark mark, Vector2 min, Vector2 max, float inset, float rounding,
        bool hovered, float fillPop, Vector4 accent, float scale)
    {
        var innerMin = min + new Vector2(inset, inset);
        var innerMax = max - new Vector2(inset, inset);
        if (mark == CellMark.Filled)
        {
            var grow = 1f - 0.2f * fillPop;
            var center = (innerMin + innerMax) * 0.5f;
            var half = (innerMax - innerMin) * 0.5f * grow;
            var fillMin = center - half;
            var fillMax = center + half;
            Squircle.Fill(drawList, fillMin, fillMax, rounding, ImGui.GetColorU32(accent));
            Squircle.Fill(drawList, fillMin, new Vector2(fillMax.X, fillMin.Y + half.Y), rounding,
                ImGui.GetColorU32(GamePalette.Lighten(accent, 0.22f) with { W = 0.5f }));
            return;
        }

        var baseColor = hovered ? GamePalette.CellHover : GamePalette.Cell;
        Squircle.Fill(drawList, innerMin, innerMax, rounding, ImGui.GetColorU32(baseColor));
        if (mark == CellMark.Marked)
        {
            var center = (innerMin + innerMax) * 0.5f;
            var reach = (innerMax.X - innerMin.X) * 0.24f;
            var color = ImGui.GetColorU32(ChromeInk.TextDim);
            var thickness = MathF.Max(1.5f, reach * 0.34f);
            drawList.AddLine(center - new Vector2(reach, reach), center + new Vector2(reach, reach), color, thickness);
            drawList.AddLine(center - new Vector2(reach, -reach), center + new Vector2(reach, -reach), color,
                thickness);
        }
    }

    private void DrawColumnClues(ImDrawListPtr drawList, NonogramBoard board, NonogramLayout layout, Vector4 accent,
        PhoneTheme theme, float clueScale, float cell)
    {
        for (var column = 0; column < board.Size; column++)
        {
            var count = board.ColumnClueCount(column);
            var centerX = layout.GridOrigin.X + column * cell + cell * 0.5f;
            for (var slot = 0; slot < count; slot++)
            {
                var fromBottom = count - slot;
                var centerY = layout.GridOrigin.Y - (fromBottom - 0.5f) * cell;
                Typography.DrawCentered(new Vector2(centerX, centerY), GameNumber.Label(board.ColumnClue(column, slot)),
                    theme.TextStrong, clueScale, FontWeight.SemiBold);
            }
        }
    }

    private void DrawRowClues(ImDrawListPtr drawList, NonogramBoard board, NonogramLayout layout, Vector4 accent,
        PhoneTheme theme, float clueScale, float cell)
    {
        for (var row = 0; row < board.Size; row++)
        {
            var count = board.RowClueCount(row);
            var centerY = layout.GridOrigin.Y + row * cell + cell * 0.5f;
            for (var slot = 0; slot < count; slot++)
            {
                var fromRight = count - slot;
                var centerX = layout.GridOrigin.X - (fromRight - 0.5f) * cell;
                Typography.DrawCentered(new Vector2(centerX, centerY), GameNumber.Label(board.RowClue(row, slot)),
                    theme.TextStrong, clueScale, FontWeight.SemiBold);
            }
        }
    }

    private void DrawGridLines(ImDrawListPtr drawList, NonogramBoard board, Vector2 gridMin, Vector2 gridMax,
        float cell, float scale)
    {
        var thin = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f));
        var bold = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.26f));
        for (var line = 0; line <= board.Size; line++)
        {
            var heavy = line % 5 == 0 || line == board.Size;
            var color = heavy ? bold : thin;
            var thickness = (heavy ? 1.6f : 1f) * scale;
            var x = gridMin.X + line * cell;
            drawList.AddLine(new Vector2(x, gridMin.Y), new Vector2(x, gridMax.Y), color, thickness);
            var y = gridMin.Y + line * cell;
            drawList.AddLine(new Vector2(gridMin.X, y), new Vector2(gridMax.X, y), color, thickness);
        }
    }
}
