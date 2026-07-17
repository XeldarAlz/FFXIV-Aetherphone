using Aetherphone.Core.Home;

namespace Aetherphone.Core.ControlCenter;

internal readonly struct ControlMetrics
{
    public readonly Rect Grid;
    public readonly float Scale;
    public readonly float CellWidth;
    public readonly float CellHeight;
    public readonly float Gap;
    public readonly int Columns;

    private ControlMetrics(Rect grid, float scale, float cellWidth, float cellHeight, float gap, int columns)
    {
        Grid = grid;
        Scale = scale;
        CellWidth = cellWidth;
        CellHeight = cellHeight;
        Gap = gap;
        Columns = columns;
    }

    public static ControlMetrics Compute(Rect grid, int columns, float scale)
    {
        var gap = 11f * scale;
        var cellWidth = (grid.Width - (columns - 1) * gap) / columns;
        return new ControlMetrics(grid, scale, cellWidth, cellWidth, gap, columns);
    }

    public float RowStride => CellHeight + Gap;

    public float HeightForRows(int rows) => rows <= 0 ? 0f : rows * CellHeight + (rows - 1) * Gap;

    public Rect SlotRect(GridCell cell, int columnSpan, int rowSpan)
    {
        var x = Grid.Min.X + cell.Column * (CellWidth + Gap);
        var y = Grid.Min.Y + cell.Row * (CellHeight + Gap);
        var width = columnSpan * CellWidth + (columnSpan - 1) * Gap;
        var height = rowSpan * CellHeight + (rowSpan - 1) * Gap;
        return new Rect(new Vector2(x, y), new Vector2(x + width, y + height));
    }

    public int InsertIndexFromPoint(Vector2 point)
    {
        var column = Math.Clamp((int)MathF.Floor((point.X - Grid.Min.X) / (CellWidth + Gap)), 0, Columns - 1);
        var row = Math.Max(0, (int)MathF.Floor((point.Y - Grid.Min.Y) / (CellHeight + Gap)));
        return row * Columns + column;
    }
}
