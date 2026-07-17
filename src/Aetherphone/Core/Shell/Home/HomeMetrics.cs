using Aetherphone.Core.Home;

namespace Aetherphone.Core.Shell.Home;

internal readonly struct HomeMetrics
{
    public const float DockHeightUnits = 82f;
    public const float LabelBandUnits = 20f;
    public const float EditToolbarBandUnits = 40f;
    private const float DotsBandUnits = 24f;
    private const float GridTopPadUnits = 4f;

    public readonly Rect Content;
    public readonly Rect Grid;
    public readonly Rect DockBar;
    public readonly float Scale;
    public readonly float CellWidth;
    public readonly float CellHeight;
    public readonly float IconSize;
    public readonly float DotsCenterY;
    public readonly int Columns;
    public readonly int Rows;

    private HomeMetrics(Rect content, Rect grid, Rect dockBar, float scale, float cellWidth, float cellHeight,
        float iconSize, float dotsCenterY, int columns, int rows)
    {
        Content = content;
        Grid = grid;
        DockBar = dockBar;
        Scale = scale;
        CellWidth = cellWidth;
        CellHeight = cellHeight;
        IconSize = iconSize;
        DotsCenterY = dotsCenterY;
        Columns = columns;
        Rows = rows;
    }

    public static HomeMetrics Compute(Rect content, int columns, int rows, float baseScale, in HomeMotion motion,
        float editReserveUnits = 0f)
    {
        var warped = motion.Warp(content);
        var scale = baseScale * motion.Zoom;
        var dockHeight = DockHeightUnits * scale;
        var dockBottom = warped.Max.Y - 2f * scale;
        var dockBar = new Rect(new Vector2(warped.Min.X + 6f * scale, dockBottom - dockHeight),
            new Vector2(warped.Max.X - 6f * scale, dockBottom));
        var dotsCenterY = dockBar.Min.Y - DotsBandUnits * scale * 0.5f - 2f * scale;
        var gridTop = warped.Min.Y + (GridTopPadUnits + editReserveUnits) * scale;
        var gridBottom = dockBar.Min.Y - DotsBandUnits * scale;
        var grid = new Rect(new Vector2(warped.Min.X, gridTop), new Vector2(warped.Max.X, gridBottom));
        var cellWidth = grid.Width / columns;
        var cellHeight = grid.Height / rows;
        var iconSize = MathF.Max(
            MathF.Min(MathF.Min(cellWidth * 0.60f, cellHeight - LabelBandUnits * scale), 60f * scale), 12f * scale);
        return new HomeMetrics(warped, grid, dockBar, scale, cellWidth, cellHeight, iconSize, dotsCenterY, columns,
            rows);
    }

    public float PageOffsetX(int page, float scrollValue) => (page - scrollValue) * Content.Width;

    public Vector2 CellOrigin(int page, float scrollValue, GridCell cell) =>
        new(Grid.Min.X + PageOffsetX(page, scrollValue) + cell.Column * CellWidth, Grid.Min.Y + cell.Row * CellHeight);

    public Vector2 IconCenter(int page, float scrollValue, GridCell cell)
    {
        var origin = CellOrigin(page, scrollValue, cell);
        return new Vector2(origin.X + CellWidth * 0.5f, origin.Y + IconSize * 0.5f + 3f * Scale);
    }

    public Rect WidgetRect(int page, float scrollValue, GridCell cell, int columnSpan, int rowSpan)
    {
        var origin = CellOrigin(page, scrollValue, cell);
        var inset = (CellWidth - IconSize) * 0.5f;
        var min = new Vector2(origin.X + inset, origin.Y + 3f * Scale);
        var max = new Vector2(origin.X + columnSpan * CellWidth - inset,
            origin.Y + rowSpan * CellHeight - LabelBandUnits * Scale + 6f * Scale);
        return new Rect(min, max);
    }

    public Rect TileRect(int page, float scrollValue, GridCell cell, HomeTile tile)
    {
        if (tile.IsWidget)
        {
            return WidgetRect(page, scrollValue, cell, tile.ColumnSpan, tile.RowSpan);
        }

        var center = IconCenter(page, scrollValue, cell);
        var half = IconSize * 0.5f;
        return new Rect(new Vector2(center.X - half, center.Y - half), new Vector2(center.X + half, center.Y + half));
    }

    public GridCell CellFromPoint(int page, float scrollValue, Vector2 point)
    {
        var pageLeft = Grid.Min.X + PageOffsetX(page, scrollValue);
        var column = Math.Clamp((int)MathF.Floor((point.X - pageLeft) / CellWidth), 0, Columns - 1);
        var row = Math.Clamp((int)MathF.Floor((point.Y - Grid.Min.Y) / CellHeight), 0, Rows - 1);
        return new GridCell(column, row);
    }

    public Rect DockSlotRect(int slotCount, int slot)
    {
        var slotWidth = DockBar.Width / Math.Max(1, slotCount);
        var centerX = DockBar.Min.X + slotWidth * (slot + 0.5f);
        var centerY = DockBar.Center.Y;
        var half = MathF.Min(IconSize, DockBar.Height * 0.70f) * 0.5f;
        return new Rect(new Vector2(centerX - half, centerY - half), new Vector2(centerX + half, centerY + half));
    }
}
