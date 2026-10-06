using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Nonogram;

internal enum CellMark : byte
{
    Empty,
    Filled,
    Marked,
}

internal enum MarkResult : byte
{
    None,
    Changed,
    Mistake,
}

internal sealed class NonogramBoard
{
    public const int MinSize = 5;
    public const int MaxSize = 10;
    public const int MaxCells = MaxSize * MaxSize;
    private const int MaxRunsPerLine = (MaxSize + 1) / 2;
    private const int MaxChangedPerMove = MaxSize * 2 + 1;
    private const int MaxGenerationAttempts = 240;
    private const float FillDensity = 0.56f;
    private const byte Unknown = 0;
    private const byte KnownFilled = 1;
    private const byte KnownCross = 2;
    private readonly bool[] solution = new bool[MaxCells];
    private readonly CellMark[] state = new CellMark[MaxCells];
    private readonly int[] rowClues = new int[MaxSize * MaxRunsPerLine];
    private readonly int[] rowClueCounts = new int[MaxSize];
    private readonly int[] columnClues = new int[MaxSize * MaxRunsPerLine];
    private readonly int[] columnClueCounts = new int[MaxSize];
    private readonly bool[] rowSatisfied = new bool[MaxSize];
    private readonly bool[] columnSatisfied = new bool[MaxSize];
    private readonly int[] changed = new int[MaxChangedPerMove];
    private readonly byte[] known = new byte[MaxCells];
    private readonly byte[] lineState = new byte[MaxSize];
    private readonly byte[] lineTrial = new byte[MaxSize];
    private readonly bool[] canFill = new bool[MaxSize];
    private readonly bool[] canCross = new bool[MaxSize];
    private readonly int[] lineRuns = new int[MaxRunsPerLine];
    private GameRandom random;

    public int Size { get; private set; } = MinSize;

    public int MaxRowClues { get; private set; } = 1;

    public int MaxColumnClues { get; private set; } = 1;

    public int FilledTarget { get; private set; }

    public int FilledCount { get; private set; }

    public int Mistakes { get; private set; }

    public bool Solved { get; private set; }

    public bool Started { get; private set; }

    public bool LineSolvable { get; private set; }

    public int ChangedCount { get; private set; }

    public int CellCount => Size * Size;

    public CellMark MarkAt(int index) => state[index];

    public bool SolutionAt(int index) => solution[index];

    public int RowClueCount(int row) => rowClueCounts[row];

    public int RowClue(int row, int slot) => rowClues[row * MaxRunsPerLine + slot];

    public int ColumnClueCount(int column) => columnClueCounts[column];

    public int ColumnClue(int column, int slot) => columnClues[column * MaxRunsPerLine + slot];

    public bool RowSatisfied(int row) => rowSatisfied[row];

    public bool ColumnSatisfied(int column) => columnSatisfied[column];

    public int ChangedCell(int index) => changed[index];

    public static int SizeFor(int mode)
    {
        return mode switch
        {
            1 => 8,
            2 => MaxSize,
            _ => MinSize,
        };
    }

    public void Reset(int size, GameRandom seededRandom)
    {
        random = seededRandom;
        Size = Math.Clamp(size, MinSize, MaxSize);
        Array.Clear(state, 0, MaxCells);
        Array.Clear(rowSatisfied, 0, MaxSize);
        Array.Clear(columnSatisfied, 0, MaxSize);
        FilledCount = 0;
        Mistakes = 0;
        Solved = false;
        Started = false;
        ChangedCount = 0;
        Generate();
        for (var line = 0; line < Size; line++)
        {
            RefreshLine(line, true);
            RefreshLine(line, false);
        }

        ChangedCount = 0;
    }

    public MarkResult SetMark(int index, CellMark mark)
    {
        ChangedCount = 0;
        if (Solved || state[index] == mark)
        {
            return MarkResult.None;
        }

        Started = true;
        if (mark == CellMark.Filled && !solution[index])
        {
            Mistakes++;
            Apply(index, CellMark.Marked);
            RefreshLinesAround(index);
            return MarkResult.Mistake;
        }

        if (state[index] == CellMark.Filled)
        {
            FilledCount--;
        }

        Apply(index, mark);
        if (mark == CellMark.Filled)
        {
            FilledCount++;
        }

        RefreshLinesAround(index);
        Solved = FilledCount == FilledTarget;
        return MarkResult.Changed;
    }

    private void Apply(int index, CellMark mark)
    {
        state[index] = mark;
        changed[ChangedCount] = index;
        ChangedCount++;
    }

    private void RefreshLinesAround(int index)
    {
        RefreshLine(index / Size, true);
        RefreshLine(index % Size, false);
    }

    private void RefreshLine(int line, bool isRow)
    {
        var satisfied = LineSatisfied(line, isRow);
        if (isRow)
        {
            rowSatisfied[line] = satisfied;
        }
        else
        {
            columnSatisfied[line] = satisfied;
        }

        if (!satisfied)
        {
            return;
        }

        for (var cell = 0; cell < Size; cell++)
        {
            var index = LineIndex(line, cell, isRow);
            if (state[index] == CellMark.Empty)
            {
                Apply(index, CellMark.Marked);
            }
        }
    }

    private bool LineSatisfied(int line, bool isRow)
    {
        Span<int> runs = stackalloc int[MaxRunsPerLine];
        var count = ComputeRuns(runs, line, isRow, false);
        var clueCount = isRow ? rowClueCounts[line] : columnClueCounts[line];
        if (count != clueCount)
        {
            return false;
        }

        var clues = isRow ? rowClues : columnClues;
        for (var slot = 0; slot < count; slot++)
        {
            if (runs[slot] != clues[line * MaxRunsPerLine + slot])
            {
                return false;
            }
        }

        return true;
    }

    private void Generate()
    {
        for (var attempt = 0; attempt < MaxGenerationAttempts; attempt++)
        {
            FillRandomSolution();
            ComputeClues();
            if (IsLineSolvable())
            {
                LineSolvable = true;
                return;
            }
        }

        LineSolvable = false;
    }

    private void FillRandomSolution()
    {
        while (true)
        {
            var filled = 0;
            for (var index = 0; index < CellCount; index++)
            {
                var on = random.NextFloat() < FillDensity;
                solution[index] = on;
                if (on)
                {
                    filled++;
                }
            }

            if (filled > CellCount / 4 && filled < CellCount)
            {
                FilledTarget = filled;
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
            var count = ComputeRuns(runs, row, true, true);
            rowClueCounts[row] = count;
            for (var slot = 0; slot < count; slot++)
            {
                rowClues[row * MaxRunsPerLine + slot] = runs[slot];
            }

            MaxRowClues = Math.Max(MaxRowClues, count);
        }

        for (var column = 0; column < Size; column++)
        {
            var count = ComputeRuns(runs, column, false, true);
            columnClueCounts[column] = count;
            for (var slot = 0; slot < count; slot++)
            {
                columnClues[column * MaxRunsPerLine + slot] = runs[slot];
            }

            MaxColumnClues = Math.Max(MaxColumnClues, count);
        }
    }

    private int ComputeRuns(Span<int> destination, int line, bool isRow, bool fromSolution)
    {
        var count = 0;
        var run = 0;
        for (var cell = 0; cell < Size; cell++)
        {
            var index = LineIndex(line, cell, isRow);
            var filled = fromSolution ? solution[index] : state[index] == CellMark.Filled;
            if (filled)
            {
                run++;
                continue;
            }

            if (run > 0)
            {
                destination[count] = run;
                count++;
                run = 0;
            }
        }

        if (run > 0)
        {
            destination[count] = run;
            count++;
        }

        if (count == 0)
        {
            destination[0] = 0;
            return 1;
        }

        return count;
    }

    private bool IsLineSolvable()
    {
        Array.Clear(known, 0, CellCount);
        var progress = true;
        while (progress)
        {
            progress = false;
            for (var line = 0; line < Size; line++)
            {
                progress |= DeduceLine(line, true);
                progress |= DeduceLine(line, false);
            }
        }

        for (var index = 0; index < CellCount; index++)
        {
            if (known[index] == Unknown)
            {
                return false;
            }
        }

        return true;
    }

    private bool DeduceLine(int line, bool isRow)
    {
        var unknowns = 0;
        for (var cell = 0; cell < Size; cell++)
        {
            lineState[cell] = known[LineIndex(line, cell, isRow)];
            if (lineState[cell] == Unknown)
            {
                unknowns++;
            }
        }

        if (unknowns == 0)
        {
            return false;
        }

        var runCount = CopyClues(line, isRow);
        Array.Clear(canFill, 0, Size);
        Array.Clear(canCross, 0, Size);
        EnumeratePlacements(0, 0, runCount);
        var progress = false;
        for (var cell = 0; cell < Size; cell++)
        {
            if (lineState[cell] != Unknown || canFill[cell] == canCross[cell])
            {
                continue;
            }

            known[LineIndex(line, cell, isRow)] = canFill[cell] ? KnownFilled : KnownCross;
            progress = true;
        }

        return progress;
    }

    private int CopyClues(int line, bool isRow)
    {
        var count = isRow ? rowClueCounts[line] : columnClueCounts[line];
        var clues = isRow ? rowClues : columnClues;
        var baseIndex = line * MaxRunsPerLine;
        if (count == 1 && clues[baseIndex] == 0)
        {
            return 0;
        }

        for (var slot = 0; slot < count; slot++)
        {
            lineRuns[slot] = clues[baseIndex + slot];
        }

        return count;
    }

    private void EnumeratePlacements(int runIndex, int start, int runCount)
    {
        if (runIndex == runCount)
        {
            for (var cell = start; cell < Size; cell++)
            {
                if (lineState[cell] == KnownFilled)
                {
                    return;
                }

                lineTrial[cell] = KnownCross;
            }

            for (var cell = 0; cell < Size; cell++)
            {
                if (lineTrial[cell] == KnownFilled)
                {
                    canFill[cell] = true;
                }
                else
                {
                    canCross[cell] = true;
                }
            }

            return;
        }

        var length = lineRuns[runIndex];
        var remaining = 0;
        for (var later = runIndex + 1; later < runCount; later++)
        {
            remaining += lineRuns[later] + 1;
        }

        var lastStart = Size - remaining - length;
        for (var position = start; position <= lastStart; position++)
        {
            if (position > start && lineState[position - 1] == KnownFilled)
            {
                break;
            }

            if (!RunFits(position, length))
            {
                continue;
            }

            for (var cell = start; cell < position; cell++)
            {
                lineTrial[cell] = KnownCross;
            }

            for (var cell = position; cell < position + length; cell++)
            {
                lineTrial[cell] = KnownFilled;
            }

            if (position + length < Size)
            {
                lineTrial[position + length] = KnownCross;
            }

            EnumeratePlacements(runIndex + 1, position + length + 1, runCount);
        }
    }

    private bool RunFits(int position, int length)
    {
        for (var cell = position; cell < position + length; cell++)
        {
            if (lineState[cell] == KnownCross)
            {
                return false;
            }
        }

        return position + length >= Size || lineState[position + length] != KnownFilled;
    }

    private int LineIndex(int line, int cell, bool isRow) => isRow ? line * Size + cell : cell * Size + line;
}
