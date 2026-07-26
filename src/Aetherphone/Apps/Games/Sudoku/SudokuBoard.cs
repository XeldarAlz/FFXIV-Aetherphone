namespace Aetherphone.Apps.Games.Sudoku;

internal enum SudokuDifficulty : byte
{
    Easy,
    Medium,
    Hard,
}

internal sealed class SudokuBoard
{
    public const int Size = 9;
    public const int BoxSize = 3;
    public const int CellCount = 81;
    private const int FullMask = 0x3FE;
    private const int EasyClues = 42;
    private const int MediumClues = 33;
    private const int HardClues = 26;

    private readonly struct Change
    {
        public readonly int Cell;
        public readonly byte Entry;
        public readonly ushort Notes;
        public readonly bool Revealed;

        public Change(int cell, byte entry, ushort notes, bool revealed)
        {
            Cell = cell;
            Entry = entry;
            Notes = notes;
            Revealed = revealed;
        }
    }

    private static readonly byte[] BoxIndex = BuildBoxIndex();

    private readonly byte[] solution = new byte[CellCount];
    private readonly byte[] givens = new byte[CellCount];
    private readonly byte[] entries = new byte[CellCount];
    private readonly ushort[] notes = new ushort[CellCount];
    private readonly bool[] revealed = new bool[CellCount];
    private readonly bool[] conflicts = new bool[CellCount];
    private readonly int[] placedPerDigit = new int[Size + 1];
    private readonly List<Change> changes = new(512);
    private readonly List<int> moveStarts = new(256);
    private readonly Random random = new();
    private readonly byte[] digOrder = new byte[CellCount];
    private readonly byte[] workGrid = new byte[CellCount];
    private readonly int[] rowMask = new int[Size];
    private readonly int[] columnMask = new int[Size];
    private readonly int[] boxMask = new int[Size];
    private int filledCount;
    private int correctCount;
    private int solutionsFound;

    public SudokuDifficulty Difficulty { get; private set; }

    public bool Solved => correctCount == CellCount;

    public bool CanUndo => moveStarts.Count > 0;

    public int RemainingCells => CellCount - filledCount;

    public void Reset(SudokuDifficulty difficulty)
    {
        Difficulty = difficulty;
        Array.Clear(entries, 0, CellCount);
        Array.Clear(notes, 0, CellCount);
        Array.Clear(revealed, 0, CellCount);
        changes.Clear();
        moveStarts.Clear();
        Generate(ClueTarget(difficulty));
        RefreshDerived();
    }

    public byte Given(int cell) => givens[cell];

    public byte Entry(int cell) => entries[cell];

    public byte Value(int cell) => givens[cell] != 0 ? givens[cell] : entries[cell];

    public byte Answer(int cell) => solution[cell];

    public ushort Notes(int cell) => notes[cell];

    public bool IsGiven(int cell) => givens[cell] != 0;

    public bool IsRevealed(int cell) => revealed[cell];

    public bool IsConflicting(int cell) => conflicts[cell];

    public bool IsWrong(int cell) => entries[cell] != 0 && entries[cell] != solution[cell];

    public int PlacedCount(int digit) => placedPerDigit[digit];

    public bool SetEntry(int cell, byte digit)
    {
        if (givens[cell] != 0 || entries[cell] == digit)
        {
            return false;
        }

        BeginMove();
        RecordChange(cell);
        entries[cell] = digit;
        notes[cell] = 0;
        revealed[cell] = false;
        if (digit != 0)
        {
            ClearPeerNotes(cell, digit);
        }

        RefreshDerived();
        return true;
    }

    public bool ClearCell(int cell)
    {
        if (givens[cell] != 0 || (entries[cell] == 0 && notes[cell] == 0))
        {
            return false;
        }

        BeginMove();
        RecordChange(cell);
        entries[cell] = 0;
        notes[cell] = 0;
        revealed[cell] = false;
        RefreshDerived();
        return true;
    }

    public bool ToggleNote(int cell, byte digit)
    {
        if (givens[cell] != 0 || entries[cell] != 0 || digit == 0)
        {
            return false;
        }

        BeginMove();
        RecordChange(cell);
        notes[cell] ^= (ushort)(1 << digit);
        return true;
    }

    public bool Reveal(int cell)
    {
        if (cell < 0 || givens[cell] != 0 || entries[cell] == solution[cell])
        {
            return false;
        }

        BeginMove();
        RecordChange(cell);
        var digit = solution[cell];
        entries[cell] = digit;
        notes[cell] = 0;
        revealed[cell] = true;
        ClearPeerNotes(cell, digit);
        RefreshDerived();
        return true;
    }

    public int PickHintCell(int preferred)
    {
        if (preferred >= 0 && givens[preferred] == 0 && entries[preferred] != solution[preferred])
        {
            return preferred;
        }

        var openCount = 0;
        for (var cell = 0; cell < CellCount; cell++)
        {
            if (givens[cell] == 0 && entries[cell] != solution[cell])
            {
                openCount++;
            }
        }

        if (openCount == 0)
        {
            return -1;
        }

        var target = random.Next(openCount);
        for (var cell = 0; cell < CellCount; cell++)
        {
            if (givens[cell] != 0 || entries[cell] == solution[cell])
            {
                continue;
            }

            if (target == 0)
            {
                return cell;
            }

            target--;
        }

        return -1;
    }

    public bool Undo()
    {
        if (moveStarts.Count == 0)
        {
            return false;
        }

        var start = moveStarts[moveStarts.Count - 1];
        moveStarts.RemoveAt(moveStarts.Count - 1);
        for (var index = changes.Count - 1; index >= start; index--)
        {
            var change = changes[index];
            entries[change.Cell] = change.Entry;
            notes[change.Cell] = change.Notes;
            revealed[change.Cell] = change.Revealed;
        }

        changes.RemoveRange(start, changes.Count - start);
        RefreshDerived();
        return true;
    }

    public bool IsRowComplete(int row)
    {
        for (var column = 0; column < Size; column++)
        {
            var cell = row * Size + column;
            if (Value(cell) != solution[cell])
            {
                return false;
            }
        }

        return true;
    }

    public bool IsColumnComplete(int column)
    {
        for (var row = 0; row < Size; row++)
        {
            var cell = row * Size + column;
            if (Value(cell) != solution[cell])
            {
                return false;
            }
        }

        return true;
    }

    public bool IsBoxComplete(int box)
    {
        var firstRow = box / BoxSize * BoxSize;
        var firstColumn = box % BoxSize * BoxSize;
        for (var row = firstRow; row < firstRow + BoxSize; row++)
        {
            for (var column = firstColumn; column < firstColumn + BoxSize; column++)
            {
                var cell = row * Size + column;
                if (Value(cell) != solution[cell])
                {
                    return false;
                }
            }
        }

        return true;
    }

    public static int RowOf(int cell) => cell / Size;

    public static int ColumnOf(int cell) => cell % Size;

    public static int BoxOf(int cell) => BoxIndex[cell];

    public static bool ArePeers(int first, int second) =>
        first != second && (RowOf(first) == RowOf(second) || ColumnOf(first) == ColumnOf(second) ||
                            BoxIndex[first] == BoxIndex[second]);

    private void BeginMove()
    {
        moveStarts.Add(changes.Count);
    }

    private void RecordChange(int cell)
    {
        changes.Add(new Change(cell, entries[cell], notes[cell], revealed[cell]));
    }

    private void ClearPeerNotes(int cell, byte digit)
    {
        var bit = (ushort)(1 << digit);
        var row = RowOf(cell);
        var column = ColumnOf(cell);
        var box = BoxIndex[cell];
        for (var peer = 0; peer < CellCount; peer++)
        {
            if (peer == cell || (notes[peer] & bit) == 0)
            {
                continue;
            }

            if (RowOf(peer) != row && ColumnOf(peer) != column && BoxIndex[peer] != box)
            {
                continue;
            }

            RecordChange(peer);
            notes[peer] &= (ushort)~bit;
        }
    }

    private void RefreshDerived()
    {
        Array.Clear(conflicts, 0, CellCount);
        Array.Clear(placedPerDigit, 0, placedPerDigit.Length);
        filledCount = 0;
        correctCount = 0;
        for (var cell = 0; cell < CellCount; cell++)
        {
            var value = Value(cell);
            if (value == 0)
            {
                continue;
            }

            filledCount++;
            placedPerDigit[value]++;
            if (value == solution[cell])
            {
                correctCount++;
            }
        }

        for (var cell = 0; cell < CellCount; cell++)
        {
            var value = Value(cell);
            if (value != 0 && HasDuplicatePeer(cell, value))
            {
                conflicts[cell] = true;
            }
        }
    }

    private bool HasDuplicatePeer(int cell, byte value)
    {
        var row = RowOf(cell);
        var column = ColumnOf(cell);
        var box = BoxIndex[cell];
        for (var peer = 0; peer < CellCount; peer++)
        {
            if (peer == cell || Value(peer) != value)
            {
                continue;
            }

            if (RowOf(peer) == row || ColumnOf(peer) == column || BoxIndex[peer] == box)
            {
                return true;
            }
        }

        return false;
    }

    private void Generate(int targetClues)
    {
        Array.Clear(solution, 0, CellCount);
        ClearMasks();
        FillSolution(0);
        Array.Copy(solution, givens, CellCount);
        ShuffleDigOrder();
        var clues = CellCount;
        for (var index = 0; index < CellCount && clues > targetClues; index++)
        {
            var cell = digOrder[index];
            var digit = givens[cell];
            if (digit == 0)
            {
                continue;
            }

            givens[cell] = 0;
            if (CountSolutions(givens, 2) == 1)
            {
                clues--;
                continue;
            }

            givens[cell] = digit;
        }
    }

    private bool FillSolution(int cell)
    {
        if (cell >= CellCount)
        {
            return true;
        }

        var row = RowOf(cell);
        var column = ColumnOf(cell);
        var box = BoxIndex[cell];
        var used = rowMask[row] | columnMask[column] | boxMask[box];
        Span<byte> bag = stackalloc byte[Size];
        for (var index = 0; index < Size; index++)
        {
            bag[index] = (byte)(index + 1);
        }

        for (var index = Size - 1; index > 0; index--)
        {
            var swap = random.Next(index + 1);
            (bag[index], bag[swap]) = (bag[swap], bag[index]);
        }

        for (var index = 0; index < Size; index++)
        {
            var digit = bag[index];
            var bit = 1 << digit;
            if ((used & bit) != 0)
            {
                continue;
            }

            solution[cell] = digit;
            rowMask[row] |= bit;
            columnMask[column] |= bit;
            boxMask[box] |= bit;
            if (FillSolution(cell + 1))
            {
                return true;
            }

            rowMask[row] &= ~bit;
            columnMask[column] &= ~bit;
            boxMask[box] &= ~bit;
            solution[cell] = 0;
        }

        return false;
    }

    private int CountSolutions(byte[] grid, int limit)
    {
        Array.Copy(grid, workGrid, CellCount);
        ClearMasks();
        for (var cell = 0; cell < CellCount; cell++)
        {
            var digit = workGrid[cell];
            if (digit == 0)
            {
                continue;
            }

            var bit = 1 << digit;
            rowMask[RowOf(cell)] |= bit;
            columnMask[ColumnOf(cell)] |= bit;
            boxMask[BoxIndex[cell]] |= bit;
        }

        solutionsFound = 0;
        SolveStep(limit);
        return solutionsFound;
    }

    private void SolveStep(int limit)
    {
        var bestCell = -1;
        var bestCount = Size + 1;
        var bestCandidates = 0;
        for (var cell = 0; cell < CellCount; cell++)
        {
            if (workGrid[cell] != 0)
            {
                continue;
            }

            var candidates = FullMask & ~(rowMask[RowOf(cell)] | columnMask[ColumnOf(cell)] | boxMask[BoxIndex[cell]]);
            var count = BitOperations.PopCount((uint)candidates);
            if (count == 0)
            {
                return;
            }

            if (count >= bestCount)
            {
                continue;
            }

            bestCell = cell;
            bestCount = count;
            bestCandidates = candidates;
            if (count == 1)
            {
                break;
            }
        }

        if (bestCell < 0)
        {
            solutionsFound++;
            return;
        }

        var row = RowOf(bestCell);
        var column = ColumnOf(bestCell);
        var box = BoxIndex[bestCell];
        for (var digit = 1; digit <= Size; digit++)
        {
            var bit = 1 << digit;
            if ((bestCandidates & bit) == 0)
            {
                continue;
            }

            workGrid[bestCell] = (byte)digit;
            rowMask[row] |= bit;
            columnMask[column] |= bit;
            boxMask[box] |= bit;
            SolveStep(limit);
            rowMask[row] &= ~bit;
            columnMask[column] &= ~bit;
            boxMask[box] &= ~bit;
            workGrid[bestCell] = 0;
            if (solutionsFound >= limit)
            {
                return;
            }
        }
    }

    private void ShuffleDigOrder()
    {
        for (var index = 0; index < CellCount; index++)
        {
            digOrder[index] = (byte)index;
        }

        for (var index = CellCount - 1; index > 0; index--)
        {
            var swap = random.Next(index + 1);
            (digOrder[index], digOrder[swap]) = (digOrder[swap], digOrder[index]);
        }
    }

    private void ClearMasks()
    {
        Array.Clear(rowMask, 0, Size);
        Array.Clear(columnMask, 0, Size);
        Array.Clear(boxMask, 0, Size);
    }

    private static int ClueTarget(SudokuDifficulty difficulty)
    {
        return difficulty switch
        {
            SudokuDifficulty.Medium => MediumClues,
            SudokuDifficulty.Hard => HardClues,
            _ => EasyClues,
        };
    }

    private static byte[] BuildBoxIndex()
    {
        var table = new byte[CellCount];
        for (var cell = 0; cell < CellCount; cell++)
        {
            table[cell] = (byte)(cell / Size / BoxSize * BoxSize + cell % Size / BoxSize);
        }

        return table;
    }
}
