namespace Aetherphone.Apps.Games.Nonogram;

internal enum CellMark : byte
{
    Empty,
    Filled,
    Marked,
}

internal sealed class NonogramBoard
{
    public const int MaxSize = 10;
    private const int MaxRunsPerLine = (MaxSize + 1) / 2;
    private readonly bool[] solution = new bool[MaxSize * MaxSize];
    private readonly CellMark[] state = new CellMark[MaxSize * MaxSize];
    private readonly int[] rowClues = new int[MaxSize * MaxRunsPerLine];
    private readonly int[] rowClueCounts = new int[MaxSize];
    private readonly int[] columnClues = new int[MaxSize * MaxRunsPerLine];
    private readonly int[] columnClueCounts = new int[MaxSize];
    private readonly Random random = new();
    public int Size { get; private set; } = 5;
    public int MaxRowClues { get; private set; } = 1;
    public int MaxColumnClues { get; private set; } = 1;
    public bool Solved { get; private set; }
    public int CellCount => Size * Size;
    public CellMark MarkAt(int index) => state[index];
    public bool SolutionAt(int index) => solution[index];
    public int RowClueCount(int row) => rowClueCounts[row];
    public int RowClue(int row, int slot) => rowClues[row * MaxRunsPerLine + slot];
    public int ColumnClueCount(int column) => columnClueCounts[column];
    public int ColumnClue(int column, int slot) => columnClues[column * MaxRunsPerLine + slot];

    public int FilledRemaining()
    {
        var target = 0;
        var painted = 0;
        for (var index = 0; index < CellCount; index++)
        {
            if (solution[index])
            {
                target++;
            }

            if (state[index] == CellMark.Filled)
            {
                painted++;
            }
        }

        return Math.Max(0, target - painted);
    }

    public void Reset(int size)
    {
        Size = Math.Clamp(size, 5, MaxSize);
        Array.Clear(state, 0, state.Length);
        Solved = false;
        Generate();
        ComputeClues();
    }

    public bool SetMark(int index, CellMark mark)
    {
        if (state[index] == mark)
        {
            return false;
        }

        state[index] = mark;
        if (mark != CellMark.Marked)
        {
            Solved = Evaluate();
        }

        return true;
    }

    private void Generate()
    {
        while (true)
        {
            var filled = 0;
            for (var index = 0; index < CellCount; index++)
            {
                var on = random.NextDouble() < 0.56;
                solution[index] = on;
                if (on)
                {
                    filled++;
                }
            }

            if (filled > CellCount / 4 && filled < CellCount)
            {
                return;
            }
        }
    }

    private void ComputeClues()
    {
        MaxRowClues = 1;
        MaxColumnClues = 1;
        Span<int> runs = stackalloc int[MaxRunsPerLine];
        for (var row = 0; row < Size; row++)
        {
            var count = ComputeRowRuns(runs, row, true);
            rowClueCounts[row] = count;
            for (var slot = 0; slot < count; slot++)
            {
                rowClues[row * MaxRunsPerLine + slot] = runs[slot];
            }

            if (count > MaxRowClues)
            {
                MaxRowClues = count;
            }
        }

        for (var column = 0; column < Size; column++)
        {
            var count = ComputeColumnRuns(runs, column, true);
            columnClueCounts[column] = count;
            for (var slot = 0; slot < count; slot++)
            {
                columnClues[column * MaxRunsPerLine + slot] = runs[slot];
            }

            if (count > MaxColumnClues)
            {
                MaxColumnClues = count;
            }
        }
    }

    private bool Evaluate()
    {
        Span<int> runs = stackalloc int[MaxRunsPerLine];
        for (var row = 0; row < Size; row++)
        {
            var count = ComputeRowRuns(runs, row, false);
            if (count != rowClueCounts[row])
            {
                return false;
            }

            for (var slot = 0; slot < count; slot++)
            {
                if (runs[slot] != rowClues[row * MaxRunsPerLine + slot])
                {
                    return false;
                }
            }
        }

        for (var column = 0; column < Size; column++)
        {
            var count = ComputeColumnRuns(runs, column, false);
            if (count != columnClueCounts[column])
            {
                return false;
            }

            for (var slot = 0; slot < count; slot++)
            {
                if (runs[slot] != columnClues[column * MaxRunsPerLine + slot])
                {
                    return false;
                }
            }
        }

        return true;
    }

    private int ComputeRowRuns(Span<int> destination, int row, bool fromSolution)
    {
        var count = 0;
        var run = 0;
        for (var column = 0; column < Size; column++)
        {
            if (IsFilled(row * Size + column, fromSolution))
            {
                run++;
                continue;
            }

            if (run > 0)
            {
                destination[count++] = run;
                run = 0;
            }
        }

        if (run > 0)
        {
            destination[count++] = run;
        }

        if (count == 0)
        {
            destination[0] = 0;
            return 1;
        }

        return count;
    }

    private int ComputeColumnRuns(Span<int> destination, int column, bool fromSolution)
    {
        var count = 0;
        var run = 0;
        for (var row = 0; row < Size; row++)
        {
            if (IsFilled(row * Size + column, fromSolution))
            {
                run++;
                continue;
            }

            if (run > 0)
            {
                destination[count++] = run;
                run = 0;
            }
        }

        if (run > 0)
        {
            destination[count++] = run;
        }

        if (count == 0)
        {
            destination[0] = 0;
            return 1;
        }

        return count;
    }

    private bool IsFilled(int index, bool fromSolution)
    {
        return fromSolution ? solution[index] : state[index] == CellMark.Filled;
    }
}
