using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Delve;

internal enum DelveTile : byte
{
    Empty,
    Dirt,
    Wall,
    Boulder,
    Gem,
    Exit,
    Player,
    Bat,
    Slime,
    Blast,
    GemBlast,
}

internal enum DelveState : byte
{
    Playing,
    Won,
    Dead,
}

internal enum DelveEventKind : byte
{
    Dug,
    Collected,
    Landed,
    Pushed,
    Strained,
    Crushed,
    Touched,
    Exploded,
    ExitOpened,
    Escaped,
    Died,
    TimedOut,
}

internal readonly struct DelveEvent
{
    public readonly DelveEventKind Kind;
    public readonly int Cell;
    public readonly DelveTile Tile;

    public DelveEvent(DelveEventKind kind, int cell, DelveTile tile)
    {
        Kind = kind;
        Cell = cell;
        Tile = tile;
    }
}

internal sealed class DelveBoard
{
    public const int MaxColumns = 48;
    public const int MaxRows = 32;
    public const int MaxCells = MaxColumns * MaxRows;
    public const int NoDirection = -1;
    public const float TickSeconds = 0.14f;
    public const float PushChance = 0.45f;
    public const float TimeStarFraction = 0.4f;
    public const int BlastTicks = 3;
    private const float MaxCatchUpSeconds = 0.3f;
    private const int EventCapacity = 256;
    private static readonly int[] StepColumn = { 0, 1, 0, -1 };
    private static readonly int[] StepRow = { -1, 0, 1, 0 };

    private readonly DelveTile[] tiles = new DelveTile[MaxCells];
    private readonly bool[] falling = new bool[MaxCells];
    private readonly byte[] heading = new byte[MaxCells];
    private readonly byte[] blastLeft = new byte[MaxCells];
    private readonly sbyte[] arrivedFrom = new sbyte[MaxCells];
    private readonly int[] scanned = new int[MaxCells];
    private readonly DelveEvent[] events = new DelveEvent[EventCapacity];
    private FixedStepClock clock = new(TickSeconds, MaxCatchUpSeconds);
    private GameRandom random;
    private int tick;
    private int held = NoDirection;
    private int nudged = NoDirection;
    private int exitCell;

    public int Columns { get; private set; }

    public int Rows { get; private set; }

    public int Quota { get; private set; }

    public int GemsInLevel { get; private set; }

    public int Gems { get; private set; }

    public float TimeLimit { get; private set; }

    public float TimeLeft { get; private set; }

    public float Elapsed { get; private set; }

    public DelveState State { get; private set; }

    public bool ExitOpen { get; private set; }

    public int PlayerCell { get; private set; }

    public int Facing { get; private set; } = 2;

    public int EventCount { get; private set; }

    public int Ticks => tick;

    public float Alpha => clock.Alpha;

    public int ExitCell => exitCell;

    public int BonusGems => Quota + (Math.Max(0, GemsInLevel - Quota) + 1) / 2;

    public int PlayerColumn => PlayerCell % Math.Max(1, Columns);

    public int PlayerRow => PlayerCell / Math.Max(1, Columns);

    public static int Stars(bool won, int gems, int bonusGems, float timeLeft, float timeLimit)
    {
        if (!won)
        {
            return 0;
        }

        var stars = 1;
        if (gems >= bonusGems)
        {
            stars++;
        }

        if (timeLimit > 0f && timeLeft >= timeLimit * TimeStarFraction)
        {
            stars++;
        }

        return stars;
    }

    public static Vector2 Step(int direction) =>
        direction is >= 0 and < 4 ? new Vector2(StepColumn[direction], StepRow[direction]) : Vector2.Zero;

    public int Cell(int column, int row) => row * Columns + column;

    public bool InBounds(int column, int row) => column >= 0 && row >= 0 && column < Columns && row < Rows;

    public DelveTile TileAt(int cell) => tiles[cell];

    public DelveTile TileAt(int column, int row) => InBounds(column, row) ? tiles[Cell(column, row)] : DelveTile.Wall;

    public bool FallingAt(int cell) => falling[cell];

    public int HeadingAt(int cell) => heading[cell];

    public int ArrivedFrom(int cell) => arrivedFrom[cell];

    public int BlastLeft(int cell) => blastLeft[cell];

    public ref readonly DelveEvent Event(int index) => ref events[index];

    public bool Load(in DelveLevel level, GameRandom seeded)
    {
        random = seeded;
        var rows = level.Rows;
        Rows = rows.Length;
        Columns = Rows > 0 ? rows[0].Length : 0;
        Quota = level.Quota;
        TimeLimit = level.Seconds;
        TimeLeft = level.Seconds;
        Elapsed = 0f;
        Gems = 0;
        GemsInLevel = 0;
        State = DelveState.Playing;
        ExitOpen = false;
        Facing = 2;
        held = NoDirection;
        nudged = NoDirection;
        tick = 0;
        EventCount = 0;
        clock.Reset();
        PlayerCell = -1;
        exitCell = -1;
        if (Columns == 0 || Columns > MaxColumns || Rows > MaxRows)
        {
            return false;
        }

        Array.Clear(falling);
        Array.Clear(heading);
        Array.Clear(blastLeft);
        Array.Clear(scanned);
        Array.Fill(arrivedFrom, (sbyte)NoDirection);
        var valid = true;
        for (var row = 0; row < Rows; row++)
        {
            var line = rows[row];
            valid &= line.Length == Columns;
            for (var column = 0; column < Columns; column++)
            {
                var symbol = column < line.Length ? line[column] : '#';
                valid &= Read(symbol, Cell(column, row));
            }
        }

        return valid && PlayerCell >= 0 && exitCell >= 0 && Quota > 0 && GemsInLevel >= Quota;
    }

    public void Hold(int direction)
    {
        held = direction is >= 0 and < 4 ? direction : NoDirection;
    }

    public void Nudge(int direction)
    {
        if (direction is >= 0 and < 4)
        {
            nudged = direction;
        }
    }

    public int Step(float deltaSeconds)
    {
        EventCount = 0;
        if (deltaSeconds <= 0f)
        {
            return 0;
        }

        if (State == DelveState.Playing)
        {
            Elapsed += deltaSeconds;
            TimeLeft = MathF.Max(0f, TimeLeft - deltaSeconds);
            if (TimeLeft <= 0f)
            {
                TimeOut();
            }
        }

        var count = clock.Advance(deltaSeconds);
        for (var index = 0; index < count; index++)
        {
            Tick();
        }

        return count;
    }

    public void Tick()
    {
        tick++;
        Array.Fill(arrivedFrom, (sbyte)NoDirection, 0, Columns * Rows);
        MovePlayer();
        for (var cell = 0; cell < Columns * Rows; cell++)
        {
            if (scanned[cell] == tick)
            {
                continue;
            }

            switch (tiles[cell])
            {
                case DelveTile.Boulder:
                case DelveTile.Gem:
                    Fall(cell);
                    break;
                case DelveTile.Bat:
                case DelveTile.Slime:
                    Patrol(cell);
                    break;
                case DelveTile.Blast:
                case DelveTile.GemBlast:
                    Burn(cell);
                    break;
                default:
                    break;
            }
        }

        if (!ExitOpen && Gems >= Quota)
        {
            ExitOpen = true;
            Raise(DelveEventKind.ExitOpened, exitCell, DelveTile.Exit);
        }
    }

    private bool Read(char symbol, int cell)
    {
        switch (symbol)
        {
            case '#':
                tiles[cell] = DelveTile.Wall;
                return true;
            case '.':
                tiles[cell] = DelveTile.Dirt;
                return true;
            case '-':
            case ' ':
                tiles[cell] = DelveTile.Empty;
                return true;
            case 'O':
                tiles[cell] = DelveTile.Boulder;
                return true;
            case '*':
                tiles[cell] = DelveTile.Gem;
                GemsInLevel++;
                return true;
            case 'X':
                tiles[cell] = DelveTile.Exit;
                var firstExit = exitCell < 0;
                exitCell = cell;
                return firstExit;
            case '@':
                tiles[cell] = DelveTile.Player;
                var firstPlayer = PlayerCell < 0;
                PlayerCell = cell;
                return firstPlayer;
            case 'b':
                tiles[cell] = DelveTile.Bat;
                heading[cell] = 3;
                return true;
            case 's':
                tiles[cell] = DelveTile.Slime;
                heading[cell] = 1;
                return true;
            default:
                tiles[cell] = DelveTile.Wall;
                return false;
        }
    }

    private int Neighbour(int cell, int direction)
    {
        var column = cell % Columns + StepColumn[direction];
        var row = cell / Columns + StepRow[direction];
        return InBounds(column, row) ? Cell(column, row) : -1;
    }

    private void MovePlayer()
    {
        var direction = held != NoDirection ? held : nudged;
        nudged = NoDirection;
        if (State != DelveState.Playing || direction == NoDirection || tiles[PlayerCell] != DelveTile.Player)
        {
            return;
        }

        Facing = direction;
        var target = Neighbour(PlayerCell, direction);
        if (target < 0)
        {
            return;
        }

        switch (tiles[target])
        {
            case DelveTile.Empty:
                Walk(target, direction);
                return;
            case DelveTile.Dirt:
                Raise(DelveEventKind.Dug, target, DelveTile.Dirt);
                Walk(target, direction);
                return;
            case DelveTile.Gem:
                Gems++;
                falling[target] = false;
                Raise(DelveEventKind.Collected, target, DelveTile.Gem);
                Walk(target, direction);
                return;
            case DelveTile.Exit when ExitOpen:
                Walk(target, direction);
                tiles[target] = DelveTile.Exit;
                State = DelveState.Won;
                Raise(DelveEventKind.Escaped, target, DelveTile.Player);
                return;
            case DelveTile.Boulder when direction is 1 or 3 && !falling[target]:
                Shove(target, direction);
                return;
            default:
                return;
        }
    }

    private void Walk(int target, int direction)
    {
        tiles[PlayerCell] = DelveTile.Empty;
        tiles[target] = DelveTile.Player;
        falling[target] = false;
        arrivedFrom[target] = (sbyte)direction;
        scanned[target] = tick;
        PlayerCell = target;
    }

    private void Shove(int boulder, int direction)
    {
        var beyond = Neighbour(boulder, direction);
        if (beyond < 0 || tiles[beyond] != DelveTile.Empty)
        {
            return;
        }

        if (!random.Chance(PushChance))
        {
            Raise(DelveEventKind.Strained, boulder, DelveTile.Boulder);
            return;
        }

        tiles[beyond] = DelveTile.Boulder;
        falling[beyond] = false;
        arrivedFrom[beyond] = (sbyte)direction;
        scanned[beyond] = tick;
        Raise(DelveEventKind.Pushed, beyond, DelveTile.Boulder);
        Walk(boulder, direction);
    }

    private void Fall(int cell)
    {
        var kind = tiles[cell];
        var below = Neighbour(cell, 2);
        if (below < 0)
        {
            falling[cell] = false;
            return;
        }

        var under = tiles[below];
        if (under == DelveTile.Empty)
        {
            Shift(cell, below, 2);
            falling[below] = true;
            return;
        }

        if (falling[cell] && under is DelveTile.Player or DelveTile.Bat or DelveTile.Slime)
        {
            Raise(DelveEventKind.Crushed, below, under);
            Explode(below, under == DelveTile.Slime ? DelveTile.GemBlast : DelveTile.Blast);
            return;
        }

        if (under is DelveTile.Boulder or DelveTile.Gem && !falling[below] && (Roll(cell, 3) || Roll(cell, 1)))
        {
            return;
        }

        if (!falling[cell])
        {
            return;
        }

        falling[cell] = false;
        Raise(DelveEventKind.Landed, cell, kind);
    }

    private bool Roll(int cell, int direction)
    {
        var side = Neighbour(cell, direction);
        if (side < 0 || tiles[side] != DelveTile.Empty)
        {
            return false;
        }

        var sideBelow = Neighbour(side, 2);
        if (sideBelow < 0 || tiles[sideBelow] != DelveTile.Empty)
        {
            return false;
        }

        Shift(cell, side, direction);
        falling[side] = true;
        return true;
    }

    private void Shift(int from, int to, int direction)
    {
        tiles[to] = tiles[from];
        heading[to] = heading[from];
        tiles[from] = DelveTile.Empty;
        falling[from] = false;
        arrivedFrom[to] = (sbyte)direction;
        scanned[to] = tick;
    }

    private void Patrol(int cell)
    {
        var kind = tiles[cell];
        for (var direction = 0; direction < 4; direction++)
        {
            var neighbour = Neighbour(cell, direction);
            if (neighbour >= 0 && tiles[neighbour] == DelveTile.Player)
            {
                Raise(DelveEventKind.Touched, cell, kind);
                Explode(cell, kind == DelveTile.Slime ? DelveTile.GemBlast : DelveTile.Blast);
                return;
            }
        }

        var facing = heading[cell];
        var preferred = kind == DelveTile.Bat ? (facing + 3) & 3 : (facing + 1) & 3;
        var fallback = kind == DelveTile.Bat ? (facing + 1) & 3 : (facing + 3) & 3;
        if (TryPatrol(cell, preferred) || TryPatrol(cell, facing))
        {
            return;
        }

        heading[cell] = (byte)fallback;
    }

    private bool TryPatrol(int cell, int direction)
    {
        var next = Neighbour(cell, direction);
        if (next < 0 || tiles[next] != DelveTile.Empty)
        {
            return false;
        }

        Shift(cell, next, direction);
        heading[next] = (byte)direction;
        return true;
    }

    private void Burn(int cell)
    {
        if (blastLeft[cell] > 1)
        {
            blastLeft[cell]--;
            return;
        }

        var leaves = tiles[cell] == DelveTile.GemBlast ? DelveTile.Gem : DelveTile.Empty;
        tiles[cell] = leaves;
        blastLeft[cell] = 0;
        falling[cell] = false;
    }

    private void Explode(int center, DelveTile blast)
    {
        Raise(DelveEventKind.Exploded, center, blast);
        var centerColumn = center % Columns;
        var centerRow = center / Columns;
        for (var row = centerRow - 1; row <= centerRow + 1; row++)
        {
            for (var column = centerColumn - 1; column <= centerColumn + 1; column++)
            {
                if (!InBounds(column, row))
                {
                    continue;
                }

                var cell = Cell(column, row);
                var tile = tiles[cell];
                if (tile is DelveTile.Wall or DelveTile.Exit)
                {
                    continue;
                }

                if (tile == DelveTile.Player)
                {
                    Kill(cell);
                }

                tiles[cell] = blast;
                blastLeft[cell] = BlastTicks;
                falling[cell] = false;
                scanned[cell] = tick;
            }
        }
    }

    private void Kill(int cell)
    {
        if (State != DelveState.Playing)
        {
            return;
        }

        State = DelveState.Dead;
        Raise(DelveEventKind.Died, cell, DelveTile.Player);
    }

    private void TimeOut()
    {
        Raise(DelveEventKind.TimedOut, PlayerCell, DelveTile.Player);
        Explode(PlayerCell, DelveTile.Blast);
    }

    private void Raise(DelveEventKind kind, int cell, DelveTile tile)
    {
        if (EventCount >= EventCapacity)
        {
            return;
        }

        events[EventCount++] = new DelveEvent(kind, cell, tile);
    }
}
