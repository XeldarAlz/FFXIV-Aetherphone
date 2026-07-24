namespace Aetherphone.Core.Home;

internal readonly record struct GridCell(int Column, int Row);

internal static class HomeGridSolver
{
    public const int MaxCells = 64;

    public static readonly GridCell Unassigned = new(-1, -1);

    public static bool IsAssigned(GridCell cell) => cell.Column >= 0 && cell.Row >= 0;

    public static int Solve(IReadOnlyList<IGridTile> tiles, int columns, int rows, List<GridCell> cells)
    {
        cells.Clear();
        Span<bool> occupied = stackalloc bool[MaxCells];
        occupied.Clear();
        for (var index = 0; index < tiles.Count; index++)
        {
            var tile = tiles[index];
            if (!TryFirstFree(occupied, columns, rows, tile.ColumnSpan, tile.RowSpan, out var cell))
            {
                return index;
            }

            Mark(occupied, columns, cell, tile.ColumnSpan, tile.RowSpan);
            cells.Add(cell);
        }

        return tiles.Count;
    }

    public static bool RegionFree(ReadOnlySpan<bool> occupied, int columns, int rows, GridCell cell, int columnSpan,
        int rowSpan)
    {
        if (cell.Column < 0 || cell.Row < 0 || cell.Column + columnSpan > columns || cell.Row + rowSpan > rows)
        {
            return false;
        }

        for (var rowOffset = 0; rowOffset < rowSpan; rowOffset++)
        {
            var rowStart = (cell.Row + rowOffset) * columns;
            for (var columnOffset = 0; columnOffset < columnSpan; columnOffset++)
            {
                if (occupied[rowStart + cell.Column + columnOffset])
                {
                    return false;
                }
            }
        }

        return true;
    }

    public static void Mark(Span<bool> occupied, int columns, GridCell cell, int columnSpan, int rowSpan)
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

    public static bool TryFindFree(ReadOnlySpan<bool> occupied, int columns, int rows, int columnSpan, int rowSpan,
        GridCell desired, out GridCell cell) =>
        IsAssigned(desired)
            ? TryNearestFree(occupied, columns, rows, columnSpan, rowSpan, desired, out cell)
            : TryFirstFree(occupied, columns, rows, columnSpan, rowSpan, out cell);

    public static bool TryFirstFree(ReadOnlySpan<bool> occupied, int columns, int rows, int columnSpan, int rowSpan,
        out GridCell cell)
    {
        for (var row = 0; row + rowSpan <= rows; row++)
        {
            for (var column = 0; column + columnSpan <= columns; column++)
            {
                var candidate = new GridCell(column, row);
                if (RegionFree(occupied, columns, rows, candidate, columnSpan, rowSpan))
                {
                    cell = candidate;
                    return true;
                }
            }
        }

        cell = Unassigned;
        return false;
    }

    private static bool TryNearestFree(ReadOnlySpan<bool> occupied, int columns, int rows, int columnSpan, int rowSpan,
        GridCell desired, out GridCell cell)
    {
        var found = false;
        var bestDistance = int.MaxValue;
        cell = Unassigned;
        for (var row = 0; row + rowSpan <= rows; row++)
        {
            for (var column = 0; column + columnSpan <= columns; column++)
            {
                var candidate = new GridCell(column, row);
                if (!RegionFree(occupied, columns, rows, candidate, columnSpan, rowSpan))
                {
                    continue;
                }

                var columnDelta = column - desired.Column;
                var rowDelta = row - desired.Row;
                var distance = columnDelta * columnDelta + rowDelta * rowDelta;
                if (found && distance >= bestDistance)
                {
                    continue;
                }

                found = true;
                bestDistance = distance;
                cell = candidate;
            }
        }

        return found;
    }
}
