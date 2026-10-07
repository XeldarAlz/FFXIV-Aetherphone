namespace Aetherphone.Apps.Games.Crates;

internal enum CratesDirection : byte
{
    Up,
    Right,
    Down,
    Left,
}

internal enum CratesStep : byte
{
    Blocked,
    Walked,
    Pushed,
}

internal enum CratesTile : byte
{
    Void,
    Floor,
    Wall,
}

internal sealed class CratesBoard
{
    public const int MaxColumns = 12;
    public const int MaxRows = 14;
    public const int MaxCrates = 12;
    public const int MaxCells = MaxColumns * MaxRows;
    public const int NoCrate = -1;
    private const int HistoryStart = 256;
    private const byte PushedFlag = 4;
    private const byte DirectionBits = 3;
    private static readonly int[] StepColumn = { 0, 1, 0, -1 };
    private static readonly int[] StepRow = { -1, 0, 1, 0 };

    private readonly CratesTile[] tiles = new CratesTile[MaxCells];
    private readonly bool[] targets = new bool[MaxCells];
    private readonly int[] crateAt = new int[MaxCells];
    private readonly int[] crateCells = new int[MaxCrates];
    private readonly int[] startCrates = new int[MaxCrates];
    private readonly int[] searchQueue = new int[MaxCells];
    private readonly int[] searchParent = new int[MaxCells];
    private readonly int[] searchStamp = new int[MaxCells];
    private byte[] history = new byte[HistoryStart];
    private int historyCount;
    private int startPlayer;
    private int stamp;

    public int Columns { get; private set; }

    public int Rows { get; private set; }

    public int CrateCount { get; private set; }

    public int TargetCount { get; private set; }

    public int CratesOnTargets { get; private set; }

    public int PlayerCell { get; private set; }

    public CratesDirection Facing { get; private set; } = CratesDirection.Down;

    public int Moves { get; private set; }

    public int Pushes { get; private set; }

    public int Undos { get; private set; }

    public int LastCrate { get; private set; } = NoCrate;

    public bool LastCrateLanded { get; private set; }

    public bool LastCrateLeft { get; private set; }

    public bool Solved => CrateCount > 0 && CratesOnTargets == CrateCount;

    public bool CanUndo => historyCount > 0;

    public int PlayerColumn => PlayerCell % Math.Max(1, Columns);

    public int PlayerRow => PlayerCell / Math.Max(1, Columns);

    public static int Stars(int moves, int par)
    {
        if (moves <= par)
        {
            return 3;
        }

        return moves * 2 <= par * 3 ? 2 : 1;
    }

    public static CratesDirection Opposite(CratesDirection direction) => (CratesDirection)(((int)direction + 2) & 3);

    public int Cell(int column, int row) => row * Columns + column;

    public bool InBounds(int column, int row) => column >= 0 && row >= 0 && column < Columns && row < Rows;

    public CratesTile TileAt(int column, int row) => InBounds(column, row) ? tiles[Cell(column, row)] : CratesTile.Void;

    public bool IsTarget(int column, int row) => InBounds(column, row) && targets[Cell(column, row)];

    public int CrateAt(int column, int row) => InBounds(column, row) ? crateAt[Cell(column, row)] : NoCrate;

    public int CrateCell(int crate) => crateCells[crate];

    public bool CrateOnTarget(int crate) => targets[crateCells[crate]];

    public bool Load(in CratesLevel level)
    {
        var rows = level.Rows;
        Columns = 0;
        Rows = rows.Length;
        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            Columns = Math.Max(Columns, rows[rowIndex].Length);
        }

        CrateCount = 0;
        TargetCount = 0;
        startPlayer = -1;
        if (Columns == 0 || Columns > MaxColumns || Rows > MaxRows)
        {
            Columns = 0;
            Rows = 0;
            return false;
        }

        Array.Clear(targets);
        Array.Fill(tiles, CratesTile.Void);
        var valid = true;
        for (var row = 0; row < Rows; row++)
        {
            var line = rows[row];
            for (var column = 0; column < Columns; column++)
            {
                var symbol = column < line.Length ? line[column] : ' ';
                valid &= ReadSymbol(symbol, Cell(column, row));
            }
        }

        if (!valid || startPlayer < 0)
        {
            return false;
        }

        valid = MarkInterior();
        Restart();
        Undos = 0;
        return valid && CrateCount > 0 && CrateCount == TargetCount;
    }

    public void Restart()
    {
        Array.Fill(crateAt, NoCrate);
        CratesOnTargets = 0;
        for (var crate = 0; crate < CrateCount; crate++)
        {
            crateCells[crate] = startCrates[crate];
            crateAt[startCrates[crate]] = crate;
            if (targets[startCrates[crate]])
            {
                CratesOnTargets++;
            }
        }

        PlayerCell = startPlayer;
        Facing = CratesDirection.Down;
        Moves = 0;
        Pushes = 0;
        historyCount = 0;
        ClearEvents();
    }

    public CratesStep Move(CratesDirection direction)
    {
        ClearEvents();
        Facing = direction;
        if (Solved)
        {
            return CratesStep.Blocked;
        }

        var next = Neighbour(PlayerCell, direction);
        if (next < 0 || tiles[next] != CratesTile.Floor)
        {
            return CratesStep.Blocked;
        }

        var crate = crateAt[next];
        if (crate != NoCrate)
        {
            var beyond = Neighbour(next, direction);
            if (beyond < 0 || tiles[beyond] != CratesTile.Floor || crateAt[beyond] != NoCrate)
            {
                return CratesStep.Blocked;
            }

            ShiftCrate(crate, next, beyond);
            Pushes++;
        }

        PlayerCell = next;
        Moves++;
        Record((byte)((byte)direction | (crate != NoCrate ? PushedFlag : 0)));
        return crate != NoCrate ? CratesStep.Pushed : CratesStep.Walked;
    }

    public bool Undo(out CratesDirection direction)
    {
        ClearEvents();
        direction = Facing;
        if (historyCount == 0)
        {
            return false;
        }

        var entry = history[--historyCount];
        direction = (CratesDirection)(entry & DirectionBits);
        var previous = Neighbour(PlayerCell, Opposite(direction));
        if ((entry & PushedFlag) != 0)
        {
            var crateCell = Neighbour(PlayerCell, direction);
            ShiftCrate(crateAt[crateCell], crateCell, PlayerCell);
            Pushes--;
        }

        PlayerCell = previous;
        Facing = direction;
        Moves--;
        Undos++;
        return true;
    }

    public int PathTo(int column, int row, Span<CratesDirection> path)
    {
        if (!InBounds(column, row))
        {
            return -1;
        }

        var goal = Cell(column, row);
        if (tiles[goal] != CratesTile.Floor || crateAt[goal] != NoCrate)
        {
            return -1;
        }

        if (goal == PlayerCell)
        {
            return 0;
        }

        stamp++;
        var head = 0;
        var tail = 0;
        searchQueue[tail++] = PlayerCell;
        searchStamp[PlayerCell] = stamp;
        searchParent[PlayerCell] = -1;
        while (head < tail)
        {
            var current = searchQueue[head++];
            if (current == goal)
            {
                return Unwind(goal, path);
            }

            for (var direction = 0; direction < 4; direction++)
            {
                var next = Neighbour(current, (CratesDirection)direction);
                if (next < 0 || searchStamp[next] == stamp || tiles[next] != CratesTile.Floor ||
                    crateAt[next] != NoCrate)
                {
                    continue;
                }

                searchStamp[next] = stamp;
                searchParent[next] = current;
                searchQueue[tail++] = next;
            }
        }

        return -1;
    }

    public int Neighbour(int cell, CratesDirection direction)
    {
        var column = cell % Columns + StepColumn[(int)direction];
        var row = cell / Columns + StepRow[(int)direction];
        return InBounds(column, row) ? Cell(column, row) : -1;
    }

    public static Vector2 Step(CratesDirection direction) =>
        new(StepColumn[(int)direction], StepRow[(int)direction]);

    private int Unwind(int goal, Span<CratesDirection> path)
    {
        var length = 0;
        for (var cell = goal; searchParent[cell] >= 0; cell = searchParent[cell])
        {
            length++;
        }

        if (length > path.Length)
        {
            return -1;
        }

        var index = length - 1;
        for (var cell = goal; searchParent[cell] >= 0; cell = searchParent[cell])
        {
            path[index--] = DirectionBetween(searchParent[cell], cell);
        }

        return length;
    }

    private static CratesDirection DirectionBetween(int from, int to)
    {
        var difference = to - from;
        if (difference == 1)
        {
            return CratesDirection.Right;
        }

        if (difference == -1)
        {
            return CratesDirection.Left;
        }

        return difference > 0 ? CratesDirection.Down : CratesDirection.Up;
    }

    private bool ReadSymbol(char symbol, int cell)
    {
        switch (symbol)
        {
            case '#':
                tiles[cell] = CratesTile.Wall;
                return true;
            case ' ':
            case '-':
            case '_':
                return true;
            case '.':
                return AddTarget(cell);
            case '$':
                return AddCrate(cell);
            case '*':
                return AddTarget(cell) && AddCrate(cell);
            case '@':
                return AddPlayer(cell);
            case '+':
                return AddTarget(cell) && AddPlayer(cell);
            default:
                return false;
        }
    }

    private bool AddTarget(int cell)
    {
        targets[cell] = true;
        TargetCount++;
        return true;
    }

    private bool AddCrate(int cell)
    {
        if (CrateCount >= MaxCrates)
        {
            return false;
        }

        startCrates[CrateCount++] = cell;
        return true;
    }

    private bool AddPlayer(int cell)
    {
        if (startPlayer >= 0)
        {
            return false;
        }

        startPlayer = cell;
        return true;
    }

    private bool MarkInterior()
    {
        stamp++;
        var head = 0;
        var tail = 0;
        searchQueue[tail++] = startPlayer;
        searchStamp[startPlayer] = stamp;
        while (head < tail)
        {
            var current = searchQueue[head++];
            tiles[current] = CratesTile.Floor;
            for (var direction = 0; direction < 4; direction++)
            {
                var next = Neighbour(current, (CratesDirection)direction);
                if (next < 0 || searchStamp[next] == stamp || tiles[next] == CratesTile.Wall)
                {
                    continue;
                }

                searchStamp[next] = stamp;
                searchQueue[tail++] = next;
            }
        }

        for (var cell = 0; cell < Columns * Rows; cell++)
        {
            if (targets[cell] && tiles[cell] != CratesTile.Floor)
            {
                return false;
            }
        }

        for (var crate = 0; crate < CrateCount; crate++)
        {
            if (tiles[startCrates[crate]] != CratesTile.Floor)
            {
                return false;
            }
        }

        return true;
    }

    private void ShiftCrate(int crate, int from, int to)
    {
        crateAt[from] = NoCrate;
        crateAt[to] = crate;
        crateCells[crate] = to;
        LastCrate = crate;
        if (targets[from])
        {
            CratesOnTargets--;
            LastCrateLeft = true;
        }

        if (targets[to])
        {
            CratesOnTargets++;
            LastCrateLanded = true;
        }
    }

    private void Record(byte entry)
    {
        if (historyCount == history.Length)
        {
            Array.Resize(ref history, history.Length * 2);
        }

        history[historyCount++] = entry;
    }

    private void ClearEvents()
    {
        LastCrate = NoCrate;
        LastCrateLanded = false;
        LastCrateLeft = false;
    }
}
