using System;

namespace Aetherphone.Apps.Games.GemSwap;

internal sealed class GemSwapBoard
{
    public const int Columns = 7;

    public const int Rows = 7;

    public const int CellCount = Columns * Rows;

    public const int ColorCount = 6;

    public const int NoFall = -999;

    private readonly int[] colors = new int[CellCount];

    private readonly GemSpecial[] specials = new GemSpecial[CellCount];

    private readonly bool[] matched = new bool[CellCount];

    private readonly int[] fallFrom = new int[CellCount];

    private readonly GemSpecial[] pendingSpecial = new GemSpecial[CellCount];

    private readonly int[] worklist = new int[CellCount];

    private readonly Random random = new();

    private int worklistCount;

    private int lastSwapA = -1;

    private int lastSwapB = -1;

    public int Score { get; private set; }

    public int LastClearCount { get; private set; }

    public int LastSpecialsCreated { get; private set; }

    public int Color(int index) => colors[index];

    public GemSpecial Special(int index) => specials[index];

    public bool Matched(int index) => matched[index];

    public int FallFrom(int index) => fallFrom[index];

    public void Reset()
    {
        GeneratePlayableBoard();
        ClearFall();
        Array.Clear(matched, 0, CellCount);
        Score = 0;
        lastSwapA = -1;
        lastSwapB = -1;
    }

    public void ReshuffleIfStuck()
    {
        if (HasPossibleMoves())
        {
            return;
        }

        GeneratePlayableBoard();
        ClearFall();
        Array.Clear(matched, 0, CellCount);
    }

    private void GeneratePlayableBoard()
    {
        for (var safety = 0; safety < 2000; safety++)
        {
            for (var index = 0; index < CellCount; index++)
            {
                colors[index] = random.Next(ColorCount);
                specials[index] = GemSpecial.None;
            }

            if (!HasAnyMatch() && HasPossibleMoves())
            {
                return;
            }
        }
    }

    public static bool AreAdjacent(int indexA, int indexB)
    {
        var columnA = indexA % Columns;
        var rowA = indexA / Columns;
        var columnB = indexB % Columns;
        var rowB = indexB / Columns;
        return Math.Abs(columnA - columnB) + Math.Abs(rowA - rowB) == 1;
    }

    public void Swap(int indexA, int indexB)
    {
        (colors[indexA], colors[indexB]) = (colors[indexB], colors[indexA]);
        (specials[indexA], specials[indexB]) = (specials[indexB], specials[indexA]);
        lastSwapA = indexA;
        lastSwapB = indexB;
    }

    public bool HasAnyMatch()
    {
        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                if (RunLengthAt(column, row, 1, 0) >= 3 || RunLengthAt(column, row, 0, 1) >= 3)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public int ResolveMatches(int chain)
    {
        Array.Clear(matched, 0, CellCount);
        Array.Clear(pendingSpecial, 0, CellCount);
        LastSpecialsCreated = 0;

        MarkRuns(1, 0);
        MarkRuns(0, 1);

        var anyMatched = false;
        for (var index = 0; index < CellCount; index++)
        {
            if (matched[index])
            {
                anyMatched = true;
                break;
            }
        }

        if (!anyMatched)
        {
            LastClearCount = 0;
            return 0;
        }

        for (var index = 0; index < CellCount; index++)
        {
            if (pendingSpecial[index] != GemSpecial.None)
            {
                matched[index] = false;
            }
        }

        worklistCount = 0;
        for (var index = 0; index < CellCount; index++)
        {
            if (matched[index] && specials[index] != GemSpecial.None)
            {
                worklist[worklistCount++] = index;
            }
        }

        while (worklistCount > 0)
        {
            var cell = worklist[--worklistCount];
            var kind = specials[cell];
            specials[cell] = GemSpecial.None;
            ActivateSpecial(cell, kind);
        }

        var cleared = 0;
        for (var index = 0; index < CellCount; index++)
        {
            if (matched[index])
            {
                cleared++;
            }
        }

        for (var index = 0; index < CellCount; index++)
        {
            if (pendingSpecial[index] != GemSpecial.None)
            {
                specials[index] = pendingSpecial[index];
                LastSpecialsCreated++;
            }
        }

        Score += (cleared * 12 + LastSpecialsCreated * 50) * chain;
        LastClearCount = cleared;
        return cleared;
    }

    public void RemoveMatched()
    {
        for (var index = 0; index < CellCount; index++)
        {
            if (matched[index])
            {
                colors[index] = -1;
                specials[index] = GemSpecial.None;
            }
        }
    }

    public void ApplyGravity()
    {
        ClearFall();

        for (var column = 0; column < Columns; column++)
        {
            var write = Rows - 1;
            for (var row = Rows - 1; row >= 0; row--)
            {
                var index = row * Columns + column;
                if (colors[index] < 0)
                {
                    continue;
                }

                if (row != write)
                {
                    var target = write * Columns + column;
                    colors[target] = colors[index];
                    specials[target] = specials[index];
                    colors[index] = -1;
                    specials[index] = GemSpecial.None;
                    fallFrom[target] = row;
                }

                write--;
            }

            var spawnCount = write + 1;
            for (var row = write; row >= 0; row--)
            {
                var index = row * Columns + column;
                colors[index] = random.Next(ColorCount);
                specials[index] = GemSpecial.None;
                fallFrom[index] = row - spawnCount;
            }
        }
    }

    public void ClearFall()
    {
        for (var index = 0; index < CellCount; index++)
        {
            fallFrom[index] = NoFall;
        }
    }

    public bool HasPossibleMoves()
    {
        return FindHint(out _, out _);
    }

    public bool FindHint(out int indexA, out int indexB)
    {
        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                var cell = row * Columns + column;
                if (column + 1 < Columns)
                {
                    var right = cell + 1;
                    if (colors[cell] != colors[right] && SwapCreatesMatch(cell, right))
                    {
                        indexA = cell;
                        indexB = right;
                        return true;
                    }
                }

                if (row + 1 < Rows)
                {
                    var down = cell + Columns;
                    if (colors[cell] != colors[down] && SwapCreatesMatch(cell, down))
                    {
                        indexA = cell;
                        indexB = down;
                        return true;
                    }
                }
            }
        }

        indexA = -1;
        indexB = -1;
        return false;
    }

    public bool SwapCreatesMatch(int indexA, int indexB)
    {
        (colors[indexA], colors[indexB]) = (colors[indexB], colors[indexA]);
        var result = MatchAt(indexA) || MatchAt(indexB);
        (colors[indexA], colors[indexB]) = (colors[indexB], colors[indexA]);
        return result;
    }

    private void ActivateSpecial(int cell, GemSpecial kind)
    {
        var column = cell % Columns;
        var row = cell / Columns;

        switch (kind)
        {
            case GemSpecial.LineHorizontal:
                for (var scan = 0; scan < Columns; scan++)
                {
                    MarkActivated(row * Columns + scan);
                }

                break;
            case GemSpecial.LineVertical:
                for (var scan = 0; scan < Rows; scan++)
                {
                    MarkActivated(scan * Columns + column);
                }

                break;
            case GemSpecial.Bomb:
                for (var rowOffset = -1; rowOffset <= 1; rowOffset++)
                {
                    for (var columnOffset = -1; columnOffset <= 1; columnOffset++)
                    {
                        var targetColumn = column + columnOffset;
                        var targetRow = row + rowOffset;
                        if (targetColumn < 0 || targetColumn >= Columns || targetRow < 0 || targetRow >= Rows)
                        {
                            continue;
                        }

                        MarkActivated(targetRow * Columns + targetColumn);
                    }
                }

                break;
        }
    }

    private void MarkActivated(int index)
    {
        if (pendingSpecial[index] != GemSpecial.None || matched[index])
        {
            return;
        }

        matched[index] = true;
        if (specials[index] != GemSpecial.None)
        {
            worklist[worklistCount++] = index;
        }
    }

    private void MarkRuns(int columnStep, int rowStep)
    {
        var lineCount = columnStep == 0 ? Columns : Rows;
        var spanCount = columnStep == 0 ? Rows : Columns;

        for (var line = 0; line < lineCount; line++)
        {
            var span = 0;
            while (span < spanCount)
            {
                var column = columnStep == 0 ? line : span;
                var row = columnStep == 0 ? span : line;
                var startIndex = row * Columns + column;
                var color = colors[startIndex];
                if (color < 0)
                {
                    span++;
                    continue;
                }

                var length = RunLengthAt(column, row, columnStep, rowStep);
                if (length >= 3)
                {
                    for (var offset = 0; offset < length; offset++)
                    {
                        var cellColumn = column + columnStep * offset;
                        var cellRow = row + rowStep * offset;
                        matched[cellRow * Columns + cellColumn] = true;
                    }

                    RegisterSpecial(column, row, length, columnStep, rowStep);
                }

                span += MathMax(length, 1);
            }
        }
    }

    private void RegisterSpecial(int column, int row, int length, int columnStep, int rowStep)
    {
        if (length < 4)
        {
            return;
        }

        var kind = length >= 5
            ? GemSpecial.Bomb
            : columnStep != 0 ? GemSpecial.LineHorizontal : GemSpecial.LineVertical;

        var chosen = -1;
        for (var offset = 0; offset < length; offset++)
        {
            var cell = (row + rowStep * offset) * Columns + (column + columnStep * offset);
            if (cell == lastSwapA || cell == lastSwapB)
            {
                chosen = cell;
                break;
            }
        }

        if (chosen < 0)
        {
            var midOffset = length / 2;
            chosen = (row + rowStep * midOffset) * Columns + (column + columnStep * midOffset);
        }

        pendingSpecial[chosen] = kind;
    }

    private int RunLengthAt(int column, int row, int columnStep, int rowStep)
    {
        var startIndex = row * Columns + column;
        var color = colors[startIndex];
        if (color < 0)
        {
            return 0;
        }

        if (columnStep != 0 && column > 0 && colors[startIndex - 1] == color)
        {
            return 0;
        }

        if (rowStep != 0 && row > 0 && colors[startIndex - Columns] == color)
        {
            return 0;
        }

        var length = 1;
        var nextColumn = column + columnStep;
        var nextRow = row + rowStep;
        while (nextColumn >= 0 && nextColumn < Columns && nextRow >= 0 && nextRow < Rows && colors[nextRow * Columns + nextColumn] == color)
        {
            length++;
            nextColumn += columnStep;
            nextRow += rowStep;
        }

        return length;
    }

    private bool MatchAt(int index)
    {
        var column = index % Columns;
        var row = index / Columns;
        var color = colors[index];
        if (color < 0)
        {
            return false;
        }

        var horizontal = 1;
        var scan = column - 1;
        while (scan >= 0 && colors[row * Columns + scan] == color)
        {
            horizontal++;
            scan--;
        }

        scan = column + 1;
        while (scan < Columns && colors[row * Columns + scan] == color)
        {
            horizontal++;
            scan++;
        }

        if (horizontal >= 3)
        {
            return true;
        }

        var vertical = 1;
        scan = row - 1;
        while (scan >= 0 && colors[scan * Columns + column] == color)
        {
            vertical++;
            scan--;
        }

        scan = row + 1;
        while (scan < Rows && colors[scan * Columns + column] == color)
        {
            vertical++;
            scan++;
        }

        return vertical >= 3;
    }

    private static int MathMax(int a, int b) => a > b ? a : b;
}
