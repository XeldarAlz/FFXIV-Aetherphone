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
    public const int MaxColumns = 7;
    public const int MaxRows = 14;
    public const int MaxCells = MaxColumns * MaxRows;
    public const int MaxColors = 9;
    public const int DifficultyCount = 3;
    private const int MinSegmentLength = 3;
    private const int GenerationAttempts = 12;
    private const int SearchBudget = 40000;
    private readonly int[] owner = new int[MaxCells];
    private readonly bool[] endpoint = new bool[MaxCells];
    private readonly int[] endpointA = new int[MaxColors];
    private readonly int[] endpointB = new int[MaxColors];
    private readonly int[] fallbackA = new int[MaxColors];
    private readonly int[] fallbackB = new int[MaxColors];
    private readonly List<int>[] paths = new List<int>[MaxColors];
    private readonly bool[] visited = new bool[MaxCells];
    private readonly int[] order = new int[MaxCells];
    private readonly bool[] reached = new bool[MaxCells];
    private readonly int[] frontier = new int[MaxCells];
    private Random random = new(1);
    public int Columns { get; private set; } = 5;
    public int Rows { get; private set; } = 8;
    public int ColorCount { get; private set; } = 4;
    public int Level { get; private set; } = 1;
    public int Difficulty { get; private set; }
    public int Moves { get; private set; }
    public int ActiveColor { get; private set; } = -1;

    public FlowBoard()
    {
        for (var color = 0; color < MaxColors; color++)
        {
            paths[color] = new List<int>(MaxCells);
        }
    }

    public int Owner(int cell) => owner[cell];
    public bool IsEndpoint(int cell) => endpoint[cell];
    public int EndpointA(int color) => endpointA[color];
    public int EndpointB(int color) => endpointB[color];
    public int PathLength(int color) => paths[color].Count;
    public int PathCell(int color, int index) => paths[color][index];
    public int CellCount => Columns * Rows;
    public int Head => ActiveColor < 0 || paths[ActiveColor].Count == 0
        ? -1
        : paths[ActiveColor][paths[ActiveColor].Count - 1];

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

    public void Reset(int level, int difficulty)
    {
        Level = level;
        Difficulty = Math.Clamp(difficulty, 0, DifficultyCount - 1);
        Configure(level, Difficulty, out var columns, out var rows, out var colors);
        Columns = columns;
        Rows = rows;
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

    public int StepToward(int from, int target)
    {
        var fromColumn = from % Columns;
        var fromRow = from / Columns;
        var columnDelta = target % Columns - fromColumn;
        var rowDelta = target / Columns - fromRow;
        if (columnDelta == 0 && rowDelta == 0)
        {
            return -1;
        }

        if (rowDelta == 0 || (columnDelta != 0 && Math.Abs(columnDelta) >= Math.Abs(rowDelta)))
        {
            return fromRow * Columns + fromColumn + Math.Sign(columnDelta);
        }

        return (fromRow + Math.Sign(rowDelta)) * Columns + fromColumn;
    }

    public int StepAside(int from, int target)
    {
        var fromColumn = from % Columns;
        var fromRow = from / Columns;
        var columnDelta = target % Columns - fromColumn;
        var rowDelta = target / Columns - fromRow;
        if (columnDelta == 0 || rowDelta == 0)
        {
            return -1;
        }

        if (Math.Abs(columnDelta) >= Math.Abs(rowDelta))
        {
            return (fromRow + Math.Sign(rowDelta)) * Columns + fromColumn;
        }

        return fromRow * Columns + fromColumn + Math.Sign(columnDelta);
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
        var columnDelta = Math.Abs(first % Columns - second % Columns);
        var rowDelta = Math.Abs(first / Columns - second / Columns);
        return columnDelta + rowDelta == 1;
    }

    private static void Configure(int level, int difficulty, out int columns, out int rows, out int colors)
    {
        var step = Math.Max(0, level - 1);
        var growth = Math.Min(step / 5, 2);
        var ramp = step / 3;
        switch (difficulty)
        {
            case 1:
                columns = 6;
                rows = 10 + growth;
                colors = Math.Min(5 + ramp, 8);
                break;
            case 2:
                columns = 7;
                rows = 12 + growth;
                colors = Math.Min(7 + ramp, MaxColors);
                break;
            default:
                columns = 5;
                rows = 8 + growth;
                colors = Math.Min(4 + ramp, 6);
                break;
        }
    }

    private static int SeedFor(int level, int difficulty, int attempt)
    {
        var hash = (uint)level * 2654435761u + (uint)difficulty * 2246822519u + (uint)attempt * 40503u;
        hash ^= hash >> 15;
        hash *= 2246822519u;
        hash ^= hash >> 13;
        hash *= 3266489917u;
        hash ^= hash >> 16;
        return (int)(hash & 0x7FFFFFFF);
    }

    private void Generate()
    {
        var hasCandidate = false;
        for (var attempt = 0; attempt < GenerationAttempts; attempt++)
        {
            random = new Random(SeedFor(Level, Difficulty, attempt));
            if (!BuildHamiltonianPath())
            {
                BuildSnakePath();
            }

            if (!CutIntoSegments())
            {
                continue;
            }

            if (EndpointsSeparated())
            {
                ApplyLayout();
                return;
            }

            if (!hasCandidate)
            {
                hasCandidate = true;
                for (var color = 0; color < ColorCount; color++)
                {
                    fallbackA[color] = endpointA[color];
                    fallbackB[color] = endpointB[color];
                }
            }
        }

        if (hasCandidate)
        {
            for (var color = 0; color < ColorCount; color++)
            {
                endpointA[color] = fallbackA[color];
                endpointB[color] = fallbackB[color];
            }
        }
        else
        {
            BuildSnakePath();
            CutEvenSegments();
        }

        ApplyLayout();
    }

    private void ApplyLayout()
    {
        for (var color = 0; color < ColorCount; color++)
        {
            var first = endpointA[color];
            var last = endpointB[color];
            endpoint[first] = true;
            endpoint[last] = true;
            owner[first] = color;
            owner[last] = color;
        }
    }

    private bool EndpointsSeparated()
    {
        for (var color = 0; color < ColorCount; color++)
        {
            if (Adjacent(endpointA[color], endpointB[color]))
            {
                return false;
            }
        }

        return true;
    }

    private bool BuildHamiltonianPath()
    {
        Array.Clear(visited, 0, CellCount);
        var start = PickStart();
        var budget = SearchBudget;
        visited[start] = true;
        order[0] = start;
        return Search(start, 1, ref budget);
    }

    private int PickStart()
    {
        var cell = random.Next(CellCount);
        if (CellCount % 2 == 0)
        {
            return cell;
        }

        for (var offset = 0; offset < CellCount; offset++)
        {
            var candidate = (cell + offset) % CellCount;
            if ((candidate % Columns + candidate / Columns) % 2 == 0)
            {
                return candidate;
            }
        }

        return cell;
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

        if (!RemainderConnected(cell, CellCount - depth))
        {
            return false;
        }

        Span<int> candidates = stackalloc int[4];
        Span<int> ranks = stackalloc int[4];
        var count = 0;
        var column = cell % Columns;
        var row = cell / Columns;
        for (var direction = 0; direction < 4; direction++)
        {
            var next = Step(column, row, direction);
            if (next < 0 || visited[next])
            {
                continue;
            }

            candidates[count] = next;
            ranks[count] = FreeNeighbours(next) * 8 + random.Next(8);
            count++;
        }

        for (var index = 1; index < count; index++)
        {
            var rank = ranks[index];
            var candidate = candidates[index];
            var scan = index - 1;
            while (scan >= 0 && ranks[scan] > rank)
            {
                ranks[scan + 1] = ranks[scan];
                candidates[scan + 1] = candidates[scan];
                scan--;
            }

            ranks[scan + 1] = rank;
            candidates[scan + 1] = candidate;
        }

        for (var index = 0; index < count; index++)
        {
            var next = candidates[index];
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

    private int FreeNeighbours(int cell)
    {
        var column = cell % Columns;
        var row = cell / Columns;
        var count = 0;
        for (var direction = 0; direction < 4; direction++)
        {
            var next = Step(column, row, direction);
            if (next >= 0 && !visited[next])
            {
                count++;
            }
        }

        return count;
    }

    private bool RemainderConnected(int head, int remaining)
    {
        Array.Clear(reached, 0, CellCount);
        var seed = -1;
        var headColumn = head % Columns;
        var headRow = head / Columns;
        for (var direction = 0; direction < 4; direction++)
        {
            var next = Step(headColumn, headRow, direction);
            if (next >= 0 && !visited[next])
            {
                seed = next;
                break;
            }
        }

        if (seed < 0)
        {
            return false;
        }

        var top = 0;
        var count = 0;
        reached[seed] = true;
        frontier[top++] = seed;
        while (top > 0)
        {
            var cell = frontier[--top];
            count++;
            var column = cell % Columns;
            var row = cell / Columns;
            for (var direction = 0; direction < 4; direction++)
            {
                var next = Step(column, row, direction);
                if (next < 0 || visited[next] || reached[next])
                {
                    continue;
                }

                reached[next] = true;
                frontier[top++] = next;
            }
        }

        return count == remaining;
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

        if (targetColumn < 0 || targetColumn >= Columns || targetRow < 0 || targetRow >= Rows)
        {
            return -1;
        }

        return targetRow * Columns + targetColumn;
    }

    private void BuildSnakePath()
    {
        var index = 0;
        for (var row = 0; row < Rows; row++)
        {
            if (row % 2 == 0)
            {
                for (var column = 0; column < Columns; column++)
                {
                    order[index++] = row * Columns + column;
                }
            }
            else
            {
                for (var column = Columns - 1; column >= 0; column--)
                {
                    order[index++] = row * Columns + column;
                }
            }
        }
    }

    private bool CutIntoSegments()
    {
        var minimum = MinSegmentLength;
        if (minimum * ColorCount > CellCount)
        {
            minimum = 2;
        }

        if (minimum * ColorCount > CellCount)
        {
            return false;
        }

        Span<int> lengths = stackalloc int[MaxColors];
        for (var color = 0; color < ColorCount; color++)
        {
            lengths[color] = minimum;
        }

        var maximum = Math.Max(minimum + 2, CellCount / ColorCount * 2);
        var remaining = CellCount - minimum * ColorCount;
        var guard = remaining * 8 + 64;
        while (remaining > 0 && guard-- > 0)
        {
            var pick = random.Next(ColorCount);
            if (lengths[pick] >= maximum)
            {
                continue;
            }

            lengths[pick]++;
            remaining--;
        }

        if (remaining > 0)
        {
            return false;
        }

        var cursor = 0;
        for (var color = 0; color < ColorCount; color++)
        {
            endpointA[color] = order[cursor];
            endpointB[color] = order[cursor + lengths[color] - 1];
            cursor += lengths[color];
        }

        return true;
    }

    private void CutEvenSegments()
    {
        var baseLength = CellCount / ColorCount;
        var extra = CellCount - baseLength * ColorCount;
        var cursor = 0;
        for (var color = 0; color < ColorCount; color++)
        {
            var length = color < extra ? baseLength + 1 : baseLength;
            endpointA[color] = order[cursor];
            endpointB[color] = order[cursor + length - 1];
            cursor += length;
        }
    }
}
