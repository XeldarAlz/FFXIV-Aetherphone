namespace Aetherphone.Core.Home;

internal readonly record struct GridCell(int Column, int Row);

internal static class HomeGridSolver
{
    public const int MaxCells = 64;

    public static int Solve(IReadOnlyList<IGridTile> tiles, int columns, int rows, List<GridCell> cells)
    {
        cells.Clear();
        Span<bool> occupied = stackalloc bool[MaxCells];
        occupied.Clear();
        for (var index = 0; index < tiles.Count; index++)
        {
            var tile = tiles[index];
            if (!TryPlace(occupied, columns, rows, tile.ColumnSpan, tile.RowSpan, out var cell))
            {
                return index;
            }

            Mark(occupied, columns, cell, tile.ColumnSpan, tile.RowSpan);
            cells.Add(cell);
        }

        return tiles.Count;
    }

    public static bool Fits(IReadOnlyList<IGridTile> tiles, int columns, int rows)
    {
        var scratch = new List<GridCell>(tiles.Count);
        return Solve(tiles, columns, rows, scratch) == tiles.Count;
    }

    public static int ScanOrder(GridCell cell, int columns) => cell.Row * columns + cell.Column;

    private static bool TryPlace(Span<bool> occupied, int columns, int rows, int columnSpan, int rowSpan,
        out GridCell cell)
    {
        for (var row = 0; row + rowSpan <= rows; row++)
        {
            for (var column = 0; column + columnSpan <= columns; column++)
            {
                if (RegionFree(occupied, columns, column, row, columnSpan, rowSpan))
                {
                    cell = new GridCell(column, row);
                    return true;
                }
            }
        }

        cell = default;
        return false;
    }

    private static bool RegionFree(Span<bool> occupied, int columns, int column, int row, int columnSpan, int rowSpan)
    {
        for (var rowOffset = 0; rowOffset < rowSpan; rowOffset++)
        {
            var rowStart = (row + rowOffset) * columns;
            for (var columnOffset = 0; columnOffset < columnSpan; columnOffset++)
            {
                if (occupied[rowStart + column + columnOffset])
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void Mark(Span<bool> occupied, int columns, GridCell cell, int columnSpan, int rowSpan)
    {
        for (var rowOffset = 0; rowOffset < rowSpan; rowOffset++)
        {
            var rowStart = (cell.Row + rowOffset) * columns;
            for (var columnOffset = 0; columnOffset < columnSpan; columnOffset++)
            {
                occupied[rowStart + cell.Column + columnOffset] = true;
            }
        }
    }
}
