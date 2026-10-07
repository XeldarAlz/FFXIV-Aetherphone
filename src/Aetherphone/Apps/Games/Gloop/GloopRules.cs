using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Gloop;

internal static class GloopRules
{
    public const int Columns = 6;
    public const int VisibleRows = 12;
    public const int Rows = VisibleRows + 1;
    public const int Cells = Columns * Rows;
    public const int FirstVisibleRow = 1;
    public const int ColorCount = 4;
    public const byte Empty = 0;
    public const byte Rock = ColorCount + 1;
    public const int PopSize = 4;
    public const int SpawnColumn = 2;
    public const int SpawnRow = 1;
    public const int NuisanceRate = 70;
    public const int MaxGarbageDrop = 30;
    public const int AllClearGarbage = 30;
    public const int AllClearBonus = 2100;
    public const int Orientations = 4;

    private static readonly int[] ChainPower =
    {
        0, 8, 16, 32, 64, 96, 128, 160, 192, 224, 256, 288, 320, 352, 384, 416, 448, 480, 512,
    };
    private static readonly int[] ColorBonus = { 0, 0, 3, 6, 12, 24 };
    private static readonly int[] SatelliteColumns = { 0, 1, 0, -1 };
    private static readonly int[] SatelliteRows = { -1, 0, 1, 0 };

    public static int Index(int column, int row) => row * Columns + column;

    public static int ColumnOf(int cell) => cell % Columns;

    public static int RowOf(int cell) => cell / Columns;

    public static bool IsColor(byte value) => value is >= 1 and <= ColorCount;

    public static int SatelliteColumn(int orientation) => SatelliteColumns[orientation & 3];

    public static int SatelliteRow(int orientation) => SatelliteRows[orientation & 3];

    public static int ChainPowerFor(int chain) => ChainPower[Math.Clamp(chain - 1, 0, ChainPower.Length - 1)];

    public static int GroupBonus(int size)
    {
        if (size <= PopSize)
        {
            return 0;
        }

        return size >= 11 ? 10 : size - 3;
    }

    public static int ColorBonusFor(int colors) => ColorBonus[Math.Clamp(colors, 0, ColorBonus.Length - 1)];

    public static int StepScore(int popped, int chain, int colors, int groupBonus)
    {
        var multiplier = Math.Clamp(ChainPowerFor(chain) + ColorBonusFor(colors) + groupBonus, 1, 999);
        return 10 * popped * multiplier;
    }

    public static int CountColors(int colorMask)
    {
        var count = 0;
        for (var color = 1; color <= ColorCount; color++)
        {
            if ((colorMask & (1 << color)) != 0)
            {
                count++;
            }
        }

        return count;
    }

    public static int FindPops(ReadOnlySpan<byte> grid, Span<bool> pop, Span<int> group, Span<bool> seen,
        out int colorMask, out int groupBonus)
    {
        pop.Clear();
        seen.Clear();
        colorMask = 0;
        groupBonus = 0;
        var popped = 0;
        for (var cell = Index(0, FirstVisibleRow); cell < Cells; cell++)
        {
            var color = grid[cell];
            if (seen[cell] || !IsColor(color))
            {
                continue;
            }

            var size = FloodFill(grid, cell, color, group, seen);
            if (size < PopSize)
            {
                continue;
            }

            for (var member = 0; member < size; member++)
            {
                pop[group[member]] = true;
            }

            popped += size;
            colorMask |= 1 << color;
            groupBonus += GroupBonus(size);
        }

        if (popped > 0)
        {
            MarkRocks(grid, pop);
        }

        return popped;
    }

    public static int FloodFill(ReadOnlySpan<byte> grid, int start, byte color, Span<int> group, Span<bool> seen)
    {
        seen[start] = true;
        group[0] = start;
        var head = 0;
        var tail = 1;
        while (head < tail)
        {
            var cell = group[head++];
            var column = ColumnOf(cell);
            var row = RowOf(cell);
            tail = Visit(grid, column - 1, row, color, group, seen, tail);
            tail = Visit(grid, column + 1, row, color, group, seen, tail);
            tail = Visit(grid, column, row - 1, color, group, seen, tail);
            tail = Visit(grid, column, row + 1, color, group, seen, tail);
        }

        return tail;
    }

    public static void ClearPopped(Span<byte> grid, ReadOnlySpan<bool> pop)
    {
        for (var cell = 0; cell < Cells; cell++)
        {
            if (pop[cell])
            {
                grid[cell] = Empty;
            }
        }
    }

    public static bool Collapse(Span<byte> grid, Span<byte> fall)
    {
        fall.Clear();
        var moved = false;
        for (var column = 0; column < Columns; column++)
        {
            var write = Rows - 1;
            for (var row = Rows - 1; row >= 0; row--)
            {
                var cell = Index(column, row);
                var value = grid[cell];
                if (value == Empty)
                {
                    continue;
                }

                if (row != write)
                {
                    var target = Index(column, write);
                    grid[target] = value;
                    grid[cell] = Empty;
                    fall[target] = (byte)(write - row);
                    moved = true;
                }

                write--;
            }
        }

        return moved;
    }

    public static int Height(ReadOnlySpan<byte> grid, int column)
    {
        var height = 0;
        for (var row = Rows - 1; row >= 0; row--)
        {
            if (grid[Index(column, row)] == Empty)
            {
                break;
            }

            height++;
        }

        return height;
    }

    public static int Drop(Span<byte> grid, int column, byte value)
    {
        if (column < 0 || column >= Columns)
        {
            return -1;
        }

        var row = Rows - 1 - Height(grid, column);
        if (row < 0)
        {
            return -1;
        }

        grid[Index(column, row)] = value;
        return row;
    }

    public static void Place(Span<byte> grid, int column, int orientation, byte pivot, byte satellite)
    {
        var satelliteColumn = column + SatelliteColumn(orientation);
        if (orientation == 2)
        {
            Drop(grid, column, satellite);
            Drop(grid, column, pivot);
            return;
        }

        Drop(grid, column, pivot);
        Drop(grid, satelliteColumn, satellite);
    }

    public static bool IsClear(ReadOnlySpan<byte> grid)
    {
        for (var cell = 0; cell < Cells; cell++)
        {
            if (grid[cell] != Empty)
            {
                return false;
            }
        }

        return true;
    }

    public static bool SpawnBlocked(ReadOnlySpan<byte> grid) => grid[Index(SpawnColumn, SpawnRow)] != Empty;

    public static int Resolve(Span<byte> grid, Span<bool> pop, Span<int> group, Span<bool> seen, Span<byte> fall,
        out int chains)
    {
        chains = 0;
        var score = 0;
        while (true)
        {
            var popped = FindPops(grid, pop, group, seen, out var colorMask, out var groupBonus);
            if (popped == 0)
            {
                return score;
            }

            chains++;
            score += StepScore(popped, chains, CountColors(colorMask), groupBonus);
            ClearPopped(grid, pop);
            Collapse(grid, fall);
        }
    }

    public static void PlanGarbage(int count, ref GameRandom random, Span<int> perColumn)
    {
        perColumn.Clear();
        var fullRows = count / Columns;
        var remainder = count % Columns;
        for (var column = 0; column < Columns; column++)
        {
            perColumn[column] = fullRows;
        }

        if (remainder == 0)
        {
            return;
        }

        Span<int> order = stackalloc int[Columns];
        for (var column = 0; column < Columns; column++)
        {
            order[column] = column;
        }

        for (var pick = 0; pick < remainder; pick++)
        {
            var swap = pick + random.Next(Columns - pick);
            (order[pick], order[swap]) = (order[swap], order[pick]);
            perColumn[order[pick]]++;
        }
    }

    private static int Visit(ReadOnlySpan<byte> grid, int column, int row, byte color, Span<int> group,
        Span<bool> seen, int tail)
    {
        if (column < 0 || column >= Columns || row < FirstVisibleRow || row >= Rows)
        {
            return tail;
        }

        var cell = Index(column, row);
        if (seen[cell] || grid[cell] != color)
        {
            return tail;
        }

        seen[cell] = true;
        group[tail] = cell;
        return tail + 1;
    }

    private static void MarkRocks(ReadOnlySpan<byte> grid, Span<bool> pop)
    {
        for (var cell = Index(0, FirstVisibleRow); cell < Cells; cell++)
        {
            if (grid[cell] != Rock || pop[cell])
            {
                continue;
            }

            var column = ColumnOf(cell);
            var row = RowOf(cell);
            if (PoppedColor(grid, pop, column - 1, row) || PoppedColor(grid, pop, column + 1, row) ||
                PoppedColor(grid, pop, column, row - 1) || PoppedColor(grid, pop, column, row + 1))
            {
                pop[cell] = true;
            }
        }
    }

    private static bool PoppedColor(ReadOnlySpan<byte> grid, ReadOnlySpan<bool> pop, int column, int row)
    {
        if (column < 0 || column >= Columns || row < FirstVisibleRow || row >= Rows)
        {
            return false;
        }

        var cell = Index(column, row);
        return pop[cell] && IsColor(grid[cell]);
    }
}
