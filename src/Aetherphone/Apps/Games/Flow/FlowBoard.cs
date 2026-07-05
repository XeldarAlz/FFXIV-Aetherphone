namespace Aetherphone.Apps.Games.Flow;

internal enum FlowEvent
{
    None,
    Extended,
    Completed,
    Backtracked,
}

internal sealed class FlowBoard
{
    public const int MaxSize = 8;

    public const int MaxColors = 8;

    private readonly int[] owner = new int[MaxSize * MaxSize];

    private readonly bool[] endpoint = new bool[MaxSize * MaxSize];

    private readonly int[] endpointA = new int[MaxColors];

    private readonly int[] endpointB = new int[MaxColors];

    private readonly List<int>[] paths = new List<int>[MaxColors];

    private readonly bool[] visited = new bool[MaxSize * MaxSize];

    private readonly int[] order = new int[MaxSize * MaxSize];

    private readonly Random random = new();

    public int Size { get; private set; } = 5;

    public int ColorCount { get; private set; } = 4;

    public int Level { get; private set; } = 1;

    public int Moves { get; private set; }

    public int ActiveColor { get; private set; } = -1;

    public FlowBoard()
    {
        for (var color = 0; color < MaxColors; color++)
        {
            paths[color] = new List<int>(MaxSize * MaxSize);
        }
    }

    public int Owner(int cell) => owner[cell];

    public bool IsEndpoint(int cell) => endpoint[cell];

    public int EndpointA(int color) => endpointA[color];

    public int EndpointB(int color) => endpointB[color];

    public int PathLength(int color) => paths[color].Count;

    public int PathCell(int color, int index) => paths[color][index];

    public int CellCount => Size * Size;

    public int FilledCells()
    {
        var count = 0;
        for (var cell = 0; cell < CellCount; cell++)
        {
            if (owner[cell] >= 0)
            {
                count++;
            }
        }

        return count;
    }

    public int ConnectedColors()
    {
        var count = 0;
        for (var color = 0; color < ColorCount; color++)
        {
            if (IsConnected(color))
            {
                count++;
            }
        }

        return count;
    }

    public bool IsConnected(int color)
    {
        var path = paths[color];
        if (path.Count < 2)
        {
            return false;
        }

        var head = path[path.Count - 1];
        return head == endpointA[color] || head == endpointB[color];
    }

    public bool IsSolved()
    {
        for (var color = 0; color < ColorCount; color++)
        {
            if (!IsConnected(color))
            {
                return false;
            }
        }

        for (var cell = 0; cell < CellCount; cell++)
        {
            if (owner[cell] < 0)
            {
                return false;
            }
        }

        return true;
    }

    public void Reset(int level)
    {
        Level = level;
        Configure(level, out var size, out var colors);
        Size = size;
        ColorCount = colors;
        Moves = 0;
        ActiveColor = -1;

        for (var cell = 0; cell < owner.Length; cell++)
        {
            owner[cell] = -1;
            endpoint[cell] = false;
        }

        for (var color = 0; color < MaxColors; color++)
        {
            paths[color].Clear();
        }

        Generate();
    }

    public bool Press(int cell)
    {
        if (cell < 0)
        {
            ActiveColor = -1;
            return false;
        }

        if (endpoint[cell])
        {
            var color = owner[cell];
            BeginFromEndpoint(color, cell);
            ActiveColor = color;
            Moves++;
            return true;
        }

        var pathColor = owner[cell];
        if (pathColor < 0)
        {
            ActiveColor = -1;
            return false;
        }

        var index = paths[pathColor].IndexOf(cell);
        if (index < 0)
        {
            ActiveColor = -1;
            return false;
        }

        TruncateFrom(pathColor, index + 1);
        ActiveColor = pathColor;
        Moves++;
        return true;
    }

    public FlowEvent Extend(int cell)
    {
        if (ActiveColor < 0 || cell < 0)
        {
            return FlowEvent.None;
        }

        var color = ActiveColor;
        var path = paths[color];
        var head = path[path.Count - 1];
        if (cell == head || !Adjacent(cell, head))
        {
            return FlowEvent.None;
        }

        if (path.Count >= 2 && cell == path[path.Count - 2])
        {
            FreeCell(head);
            path.RemoveAt(path.Count - 1);
            return FlowEvent.Backtracked;
        }

        if (endpoint[head] && path.Count >= 2)
        {
            return FlowEvent.None;
        }

        var targetOwner = owner[cell];

        if (endpoint[cell] && targetOwner == color && cell != path[0])
        {
            path.Add(cell);
            return FlowEvent.Completed;
        }

        if (endpoint[cell])
        {
            return FlowEvent.None;
        }

        if (targetOwner == color)
        {
            return FlowEvent.None;
        }

        if (targetOwner >= 0)
        {
            var otherIndex = paths[targetOwner].IndexOf(cell);
            if (otherIndex >= 0)
            {
                TruncateFrom(targetOwner, otherIndex);
            }
        }

        owner[cell] = color;
        path.Add(cell);
        return FlowEvent.Extended;
    }

    public void Release()
    {
        ActiveColor = -1;
    }

    private void BeginFromEndpoint(int color, int cell)
    {
        var path = paths[color];
        for (var index = 0; index < path.Count; index++)
        {
            FreeCell(path[index]);
        }

        path.Clear();
        owner[cell] = color;
        path.Add(cell);
    }

    private void TruncateFrom(int color, int fromIndex)
    {
        var path = paths[color];
        for (var index = path.Count - 1; index >= fromIndex; index--)
        {
            FreeCell(path[index]);
            path.RemoveAt(index);
        }
    }

    private void FreeCell(int cell)
    {
        if (!endpoint[cell])
        {
            owner[cell] = -1;
        }
    }

    private bool Adjacent(int first, int second)
    {
        var firstColumn = first % Size;
        var firstRow = first / Size;
        var secondColumn = second % Size;
        var secondRow = second / Size;
        var columnDelta = Math.Abs(firstColumn - secondColumn);
        var rowDelta = Math.Abs(firstRow - secondRow);
        return columnDelta + rowDelta == 1;
    }

    private void Configure(int level, out int size, out int colors)
    {
        size = Math.Clamp(4 + level, 5, MaxSize);
        colors = Math.Clamp(2 + level, 4, MaxColors);
        if (colors > size + 1)
        {
            colors = size + 1;
        }
    }

    private void Generate()
    {
        if (!BuildHamiltonianPath())
        {
            BuildSnakePath();
        }

        CutIntoSegments();
    }

    private bool BuildHamiltonianPath()
    {
        Array.Clear(visited, 0, CellCount);
        var start = random.Next(CellCount);
        var budget = 200000;
        visited[start] = true;
        order[0] = start;
        return Search(start, 1, ref budget);
    }

    private bool Search(int cell, int depth, ref int budget)
    {
        if (depth == CellCount)
        {
            return true;
        }

        if (budget-- <= 0)
        {
            return false;
        }

        Span<int> directions = stackalloc int[4] { 0, 1, 2, 3 };
        for (var index = 3; index > 0; index--)
        {
            var swap = random.Next(index + 1);
            (directions[index], directions[swap]) = (directions[swap], directions[index]);
        }

        var column = cell % Size;
        var row = cell / Size;
        for (var index = 0; index < 4; index++)
        {
            var next = Step(column, row, directions[index]);
            if (next < 0 || visited[next])
            {
                continue;
            }

            visited[next] = true;
            order[depth] = next;
            if (Search(next, depth + 1, ref budget))
            {
                return true;
            }

            visited[next] = false;
        }

        return false;
    }

    private int Step(int column, int row, int direction)
    {
        var targetColumn = column;
        var targetRow = row;
        switch (direction)
        {
            case 0:
                targetRow--;
                break;
            case 1:
                targetColumn++;
                break;
            case 2:
                targetRow++;
                break;
            default:
                targetColumn--;
                break;
        }

        if (targetColumn < 0 || targetColumn >= Size || targetRow < 0 || targetRow >= Size)
        {
            return -1;
        }

        return targetRow * Size + targetColumn;
    }

    private void BuildSnakePath()
    {
        var index = 0;
        for (var row = 0; row < Size; row++)
        {
            if (row % 2 == 0)
            {
                for (var column = 0; column < Size; column++)
                {
                    order[index++] = row * Size + column;
                }
            }
            else
            {
                for (var column = Size - 1; column >= 0; column--)
                {
                    order[index++] = row * Size + column;
                }
            }
        }
    }

    private void CutIntoSegments()
    {
        Span<int> lengths = stackalloc int[MaxColors];
        for (var color = 0; color < ColorCount; color++)
        {
            lengths[color] = 2;
        }

        var remaining = CellCount - 2 * ColorCount;
        while (remaining > 0)
        {
            var pick = random.Next(ColorCount);
            lengths[pick]++;
            remaining--;
        }

        var cursor = 0;
        for (var color = 0; color < ColorCount; color++)
        {
            var first = order[cursor];
            var last = order[cursor + lengths[color] - 1];
            endpointA[color] = first;
            endpointB[color] = last;
            endpoint[first] = true;
            endpoint[last] = true;
            owner[first] = color;
            owner[last] = color;
            cursor += lengths[color];
        }
    }
}
