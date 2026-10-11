namespace Aetherphone.Apps.Casino.Machines;

internal readonly record struct OutlineSegment(byte FromColumn, byte FromRow, byte ToColumn, byte ToRow);

internal sealed class MachineCellOutline
{
    public const int MaxSegments = MachineRoundPlayback.MaxCells * 4;

    private readonly bool[] inside = new bool[MachineRoundPlayback.MaxCells];
    private readonly OutlineSegment[] segments = new OutlineSegment[MaxSegments];

    private int count;
    private int reels;
    private int rows;

    public int Count => count;

    public OutlineSegment SegmentAt(int index) => segments[index];

    public int Build(int[]? cells, int reelCount, int rowCount)
    {
        count = 0;
        reels = Math.Clamp(reelCount, 0, MachineRoundPlayback.MaxReels);
        rows = rowCount <= 0 ? 0 : Math.Min(rowCount, MachineRoundPlayback.MaxCells / Math.Max(1, reels));
        Array.Clear(inside);
        if (cells is null || reels == 0 || rows == 0)
        {
            return 0;
        }

        var cellCount = reels * rows;
        for (var index = 0; index < cells.Length; index++)
        {
            var cell = cells[index];
            if (cell >= 0 && cell < cellCount)
            {
                inside[cell] = true;
            }
        }

        for (var line = 0; line <= rows; line++)
        {
            AddHorizontal(line);
        }

        for (var line = 0; line <= reels; line++)
        {
            AddVertical(line);
        }

        return count;
    }

    public static int Anchor(int[]? cells, int reelCount, int rowCount)
    {
        if (cells is null || reelCount <= 0 || rowCount <= 0)
        {
            return -1;
        }

        var cellCount = reelCount * rowCount;
        var sumColumn = 0f;
        var sumRow = 0f;
        var valid = 0;
        for (var index = 0; index < cells.Length; index++)
        {
            var cell = cells[index];
            if (cell < 0 || cell >= cellCount)
            {
                continue;
            }

            sumColumn += cell / rowCount;
            sumRow += cell % rowCount;
            valid++;
        }

        if (valid == 0)
        {
            return -1;
        }

        var centerColumn = sumColumn / valid;
        var centerRow = sumRow / valid;
        var best = -1;
        var bestDistance = float.MaxValue;
        for (var index = 0; index < cells.Length; index++)
        {
            var cell = cells[index];
            if (cell < 0 || cell >= cellCount)
            {
                continue;
            }

            var columnOffset = cell / rowCount - centerColumn;
            var rowOffset = cell % rowCount - centerRow;
            var distance = columnOffset * columnOffset + rowOffset * rowOffset;
            if (distance < bestDistance || (distance == bestDistance && cell < best))
            {
                best = cell;
                bestDistance = distance;
            }
        }

        return best;
    }

    private bool In(int column, int row)
    {
        return column >= 0 && column < reels && row >= 0 && row < rows && inside[column * rows + row];
    }

    private void AddHorizontal(int line)
    {
        var start = -1;
        var startBelow = false;
        for (var column = 0; column <= reels; column++)
        {
            var edge = column < reels && In(column, line - 1) != In(column, line);
            var below = edge && In(column, line);
            if (start >= 0 && (!edge || below != startBelow))
            {
                Push(new OutlineSegment((byte)start, (byte)line, (byte)column, (byte)line));
                start = -1;
            }

            if (edge && start < 0)
            {
                start = column;
                startBelow = below;
            }
        }
    }

    private void AddVertical(int line)
    {
        var start = -1;
        var startRight = false;
        for (var row = 0; row <= rows; row++)
        {
            var edge = row < rows && In(line - 1, row) != In(line, row);
            var right = edge && In(line, row);
            if (start >= 0 && (!edge || right != startRight))
            {
                Push(new OutlineSegment((byte)line, (byte)start, (byte)line, (byte)row));
                start = -1;
            }

            if (edge && start < 0)
            {
                start = row;
                startRight = right;
            }
        }
    }

    private void Push(OutlineSegment segment)
    {
        if (count < MaxSegments)
        {
            segments[count++] = segment;
        }
    }
}
