namespace Aetherphone.Apps.Games.GemSwap;

internal sealed class GemSwapBoard
{
    public const int Columns = 7;
    public const int Rows = 7;
    public const int CellCount = Columns * Rows;
    public const int ColorCount = 6;
    public const int PrismColor = ColorCount;
    public const int NoFall = -999;
    private const int RunCapacity = 32;
    private readonly int[] colors = new int[CellCount];
    private readonly GemSpecial[] specials = new GemSpecial[CellCount];
    private readonly bool[] matched = new bool[CellCount];
    private readonly int[] fallFrom = new int[CellCount];
    private readonly GemSpecial[] pendingSpecial = new GemSpecial[CellCount];
    private readonly int[] worklist = new int[CellCount];
    private readonly bool[] shapeUsed = new bool[CellCount];
    private readonly int[] runStart = new int[RunCapacity];
    private readonly int[] runLength = new int[RunCapacity];
    private readonly bool[] runHorizontal = new bool[RunCapacity];
    private readonly int[] clearedByColor = new int[ColorCount];
    private readonly int[] activatedCells = new int[CellCount];
    private readonly GemSpecial[] activatedKinds = new GemSpecial[CellCount];
    private readonly Random random;
    private int worklistCount;
    private int runCount;
    private int activatedCount;
    private int lastSwapA = -1;
    private int lastSwapB = -1;

    public GemSwapBoard() : this(new Random())
    {
    }

    public GemSwapBoard(int seed) : this(new Random(seed))
    {
    }

    private GemSwapBoard(Random random)
    {
        this.random = random;
    }

    public int Score { get; private set; }
    public int LastClearCount { get; private set; }
    public int LastSpecialsCreated { get; private set; }
    public GemCombo LastCombo { get; private set; }
    public int ActivatedCount => activatedCount;
    public int ActivatedCell(int slot) => activatedCells[slot];
    public GemSpecial ActivatedKind(int slot) => activatedKinds[slot];
    public int ClearedOfColor(int color) => clearedByColor[color];
    public int Color(int index) => colors[index];
    public GemSpecial Special(int index) => specials[index];
    public bool Matched(int index) => matched[index];
    public int FallFrom(int index) => fallFrom[index];
    public int NextRandom(int exclusiveMaximum) => random.Next(exclusiveMaximum);

    public bool HasSpecials
    {
        get
        {
            for (var index = 0; index < CellCount; index++)
            {
                if (specials[index] != GemSpecial.None && colors[index] >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public void Reset()
    {
        GeneratePlayableBoard();
        ClearFall();
        Array.Clear(matched, 0, CellCount);
        Score = 0;
        lastSwapA = -1;
        lastSwapB = -1;
    }

    public void SetCell(int index, int color, GemSpecial special)
    {
        colors[index] = special == GemSpecial.Prism ? PrismColor : color;
        specials[index] = special;
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

    public bool IsComboSwap(int indexA, int indexB)
    {
        var kindA = specials[indexA];
        var kindB = specials[indexB];
        if (kindA == GemSpecial.Prism || kindB == GemSpecial.Prism)
        {
            return true;
        }

        return kindA != GemSpecial.None && kindB != GemSpecial.None;
    }

    public int ResolveMatches(int chain)
    {
        BeginResolve();
        MarkRuns(1, 0);
        MarkRuns(0, 1);
        if (runCount == 0)
        {
            LastClearCount = 0;
            return 0;
        }

        RegisterSpecials();
        for (var index = 0; index < CellCount; index++)
        {
            if (pendingSpecial[index] == GemSpecial.None)
            {
                continue;
            }

            if (matched[index] && specials[index] != GemSpecial.None)
            {
                worklist[worklistCount++] = index;
            }

            matched[index] = false;
        }

        for (var index = 0; index < CellCount; index++)
        {
            if (matched[index] && specials[index] != GemSpecial.None)
            {
                worklist[worklistCount++] = index;
            }
        }

        return FinishResolve(chain);
    }

    public int ResolveCombo(int origin, int other, int chain)
    {
        BeginResolve();
        var originKind = specials[origin];
        var otherKind = specials[other];
        if (originKind == GemSpecial.Prism && otherKind == GemSpecial.Prism)
        {
            LastCombo = GemCombo.PrismBoard;
            ConsumeSpecial(origin);
            ConsumeSpecial(other);
            for (var index = 0; index < CellCount; index++)
            {
                MarkActivated(index);
            }

            return FinishResolve(chain);
        }

        if (originKind == GemSpecial.Prism || otherKind == GemSpecial.Prism)
        {
            var prismCell = originKind == GemSpecial.Prism ? origin : other;
            var target = prismCell == origin ? other : origin;
            LastCombo = GemCombo.PrismColor;
            ConsumeSpecial(prismCell);
            MarkColor(colors[target]);
            MarkActivated(target);
            return FinishResolve(chain);
        }

        ConsumeSpecial(origin);
        ConsumeSpecial(other);
        var burstCount = (originKind == GemSpecial.Burst ? 1 : 0) + (otherKind == GemSpecial.Burst ? 1 : 0);
        switch (burstCount)
        {
            case 2:
                LastCombo = GemCombo.BigBurst;
                MarkSquare(origin, 2);
                break;
            case 1:
                LastCombo = GemCombo.WideCross;
                MarkCross(origin, 1);
                break;
            default:
                LastCombo = GemCombo.Cross;
                MarkCross(origin, 0);
                break;
        }

        return FinishResolve(chain);
    }

    public int DetonateSpecials(int chain)
    {
        BeginResolve();
        for (var index = 0; index < CellCount; index++)
        {
            if (specials[index] == GemSpecial.None || colors[index] < 0)
            {
                continue;
            }

            matched[index] = true;
            worklist[worklistCount++] = index;
        }

        return FinishResolve(chain);
    }

    public void BeginBlast()
    {
        BeginResolve();
    }

    public void MarkBlast(int index)
    {
        MarkActivated(index);
    }

    public int FinishBlast(int chain)
    {
        return FinishResolve(chain);
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
                if (column + 1 < Columns && IsPlayableSwap(cell, cell + 1))
                {
                    indexA = cell;
                    indexB = cell + 1;
                    return true;
                }

                if (row + 1 < Rows && IsPlayableSwap(cell, cell + Columns))
                {
                    indexA = cell;
                    indexB = cell + Columns;
                    return true;
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

    private bool IsPlayableSwap(int indexA, int indexB)
    {
        if (IsComboSwap(indexA, indexB))
        {
            return true;
        }

        return colors[indexA] != colors[indexB] && SwapCreatesMatch(indexA, indexB);
    }

    private void BeginResolve()
    {
        Array.Clear(matched, 0, CellCount);
        Array.Clear(pendingSpecial, 0, CellCount);
        Array.Clear(clearedByColor, 0, ColorCount);
        LastSpecialsCreated = 0;
        LastCombo = GemCombo.None;
        worklistCount = 0;
        runCount = 0;
        activatedCount = 0;
    }

    private int FinishResolve(int chain)
    {
        while (worklistCount > 0)
        {
            var cell = worklist[--worklistCount];
            var kind = specials[cell];
            specials[cell] = GemSpecial.None;
            RecordActivation(cell, kind);
            ActivateSpecial(cell, kind);
        }

        var cleared = 0;
        for (var index = 0; index < CellCount; index++)
        {
            if (!matched[index])
            {
                continue;
            }

            cleared++;
            var color = colors[index];
            if (color >= 0 && color < ColorCount)
            {
                clearedByColor[color]++;
            }
        }

        for (var index = 0; index < CellCount; index++)
        {
            if (pendingSpecial[index] == GemSpecial.None)
            {
                continue;
            }

            specials[index] = pendingSpecial[index];
            if (pendingSpecial[index] == GemSpecial.Prism)
            {
                colors[index] = PrismColor;
            }

            LastSpecialsCreated++;
        }

        Score += (cleared * 12 + LastSpecialsCreated * 50) * chain;
        LastClearCount = cleared;
        return cleared;
    }

    private void RecordActivation(int cell, GemSpecial kind)
    {
        if (kind == GemSpecial.None || activatedCount >= CellCount)
        {
            return;
        }

        activatedCells[activatedCount] = cell;
        activatedKinds[activatedCount] = kind;
        activatedCount++;
    }

    private void ConsumeSpecial(int cell)
    {
        RecordActivation(cell, specials[cell]);
        specials[cell] = GemSpecial.None;
        matched[cell] = true;
    }

    private void ActivateSpecial(int cell, GemSpecial kind)
    {
        switch (kind)
        {
            case GemSpecial.LineHorizontal:
                MarkRow(cell / Columns);
                break;
            case GemSpecial.LineVertical:
                MarkColumn(cell % Columns);
                break;
            case GemSpecial.Burst:
                MarkSquare(cell, 1);
                break;
            case GemSpecial.Prism:
                MarkColor(MostCommonColor());
                break;
        }
    }

    private void MarkRow(int row)
    {
        if (row < 0 || row >= Rows)
        {
            return;
        }

        for (var column = 0; column < Columns; column++)
        {
            MarkActivated(row * Columns + column);
        }
    }

    private void MarkColumn(int column)
    {
        if (column < 0 || column >= Columns)
        {
            return;
        }

        for (var row = 0; row < Rows; row++)
        {
            MarkActivated(row * Columns + column);
        }
    }

    private void MarkCross(int center, int halfWidth)
    {
        var column = center % Columns;
        var row = center / Columns;
        for (var offset = -halfWidth; offset <= halfWidth; offset++)
        {
            MarkRow(row + offset);
            MarkColumn(column + offset);
        }
    }

    private void MarkSquare(int center, int radius)
    {
        var column = center % Columns;
        var row = center / Columns;
        for (var rowOffset = -radius; rowOffset <= radius; rowOffset++)
        {
            for (var columnOffset = -radius; columnOffset <= radius; columnOffset++)
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
    }

    private void MarkColor(int color)
    {
        if (color < 0)
        {
            return;
        }

        for (var index = 0; index < CellCount; index++)
        {
            if (colors[index] == color)
            {
                MarkActivated(index);
            }
        }
    }

    private int MostCommonColor()
    {
        Span<int> counts = stackalloc int[ColorCount];
        for (var index = 0; index < CellCount; index++)
        {
            var color = colors[index];
            if (color >= 0 && color < ColorCount && !matched[index])
            {
                counts[color]++;
            }
        }

        var best = -1;
        var bestCount = 0;
        for (var color = 0; color < ColorCount; color++)
        {
            if (counts[color] > bestCount)
            {
                bestCount = counts[color];
                best = color;
            }
        }

        return best;
    }

    private void MarkActivated(int index)
    {
        if (pendingSpecial[index] != GemSpecial.None || matched[index] || colors[index] < 0)
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
                var length = RunLengthAt(column, row, columnStep, rowStep);
                if (length >= 3)
                {
                    for (var offset = 0; offset < length; offset++)
                    {
                        var cellColumn = column + columnStep * offset;
                        var cellRow = row + rowStep * offset;
                        matched[cellRow * Columns + cellColumn] = true;
                    }

                    RecordRun(startIndex, length, columnStep != 0);
                }

                span += MathMax(length, 1);
            }
        }
    }

    private void RecordRun(int startIndex, int length, bool horizontal)
    {
        if (runCount >= RunCapacity)
        {
            return;
        }

        runStart[runCount] = startIndex;
        runLength[runCount] = length;
        runHorizontal[runCount] = horizontal;
        runCount++;
    }

    private void RegisterSpecials()
    {
        Array.Clear(shapeUsed, 0, CellCount);
        for (var run = 0; run < runCount; run++)
        {
            if (runLength[run] >= 5 && !RunUsed(run))
            {
                PlaceSpecial(run, GemSpecial.Prism);
            }
        }

        for (var horizontalRun = 0; horizontalRun < runCount; horizontalRun++)
        {
            if (!runHorizontal[horizontalRun])
            {
                continue;
            }

            for (var verticalRun = 0; verticalRun < runCount; verticalRun++)
            {
                if (runHorizontal[verticalRun] || RunUsed(horizontalRun) || RunUsed(verticalRun))
                {
                    continue;
                }

                if (!TryIntersection(horizontalRun, verticalRun, out var cell))
                {
                    continue;
                }

                pendingSpecial[cell] = GemSpecial.Burst;
                MarkRunUsed(horizontalRun);
                MarkRunUsed(verticalRun);
            }
        }

        for (var run = 0; run < runCount; run++)
        {
            if (runLength[run] == 4 && !RunUsed(run))
            {
                PlaceSpecial(run, runHorizontal[run] ? GemSpecial.LineHorizontal : GemSpecial.LineVertical);
            }
        }
    }

    private bool TryIntersection(int horizontalRun, int verticalRun, out int cell)
    {
        var row = runStart[horizontalRun] / Columns;
        var firstColumn = runStart[horizontalRun] % Columns;
        var lastColumn = firstColumn + runLength[horizontalRun] - 1;
        var column = runStart[verticalRun] % Columns;
        var firstRow = runStart[verticalRun] / Columns;
        var lastRow = firstRow + runLength[verticalRun] - 1;
        cell = row * Columns + column;
        return column >= firstColumn && column <= lastColumn && row >= firstRow && row <= lastRow;
    }

    private int RunCell(int run, int offset) => runStart[run] + offset * (runHorizontal[run] ? 1 : Columns);

    private bool RunUsed(int run)
    {
        for (var offset = 0; offset < runLength[run]; offset++)
        {
            if (shapeUsed[RunCell(run, offset)])
            {
                return true;
            }
        }

        return false;
    }

    private void MarkRunUsed(int run)
    {
        for (var offset = 0; offset < runLength[run]; offset++)
        {
            shapeUsed[RunCell(run, offset)] = true;
        }
    }

    private void PlaceSpecial(int run, GemSpecial kind)
    {
        var chosen = -1;
        for (var offset = 0; offset < runLength[run]; offset++)
        {
            var cell = RunCell(run, offset);
            if (cell == lastSwapA || cell == lastSwapB)
            {
                chosen = cell;
                break;
            }
        }

        if (chosen < 0)
        {
            chosen = RunCell(run, runLength[run] / 2);
        }

        pendingSpecial[chosen] = kind;
        MarkRunUsed(run);
    }

    private int RunLengthAt(int column, int row, int columnStep, int rowStep)
    {
        var startIndex = row * Columns + column;
        var color = colors[startIndex];
        if (color < 0 || color == PrismColor)
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
        while (nextColumn >= 0 && nextColumn < Columns && nextRow >= 0 && nextRow < Rows &&
               colors[nextRow * Columns + nextColumn] == color)
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
        if (color < 0 || color == PrismColor)
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

    private static int MathMax(int left, int right) => left > right ? left : right;
}
