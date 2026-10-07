namespace Aetherphone.Apps.Games.Fuse;

internal enum FuseTile : byte
{
    Floor,
    Pillar,
    Crate,
    Block,
}

internal enum PowerUp : byte
{
    None,
    ExtraBomb,
    Range,
    Speed,
    Kick,
}

internal enum FuseDirection : byte
{
    None,
    Up,
    Right,
    Down,
    Left,
}

internal enum FuseSkill : byte
{
    Easy,
    Hard,
}

internal enum FusePhase : byte
{
    Ready,
    Fighting,
    Ended,
    MatchOver,
}

internal enum FuseVerdict : byte
{
    Ongoing,
    Won,
    Lost,
    Drawn,
}

internal struct Moogle
{
    public Vector2 Position;
    public FuseDirection Facing;
    public bool Moving;
    public bool Alive;
    public bool Bot;
    public int Bombs;
    public int Range;
    public int SpeedLevel;
    public bool Kick;
    public int BombsOut;
    public int Wins;
    public float Knockout;
    public float Stride;
}

internal struct Bomb
{
    public Vector2 Position;
    public int Cell;
    public float Fuse;
    public float Age;
    public float Pulse;
    public int Owner;
    public int Range;
    public FuseDirection Sliding;
    public bool Alive;
}

internal readonly struct MoogleInput
{
    public readonly FuseDirection Direction;
    public readonly bool Bomb;

    public MoogleInput(FuseDirection direction, bool bomb)
    {
        Direction = direction;
        Bomb = bomb;
    }
}

internal readonly struct Blast
{
    public readonly int Cell;
    public readonly int Chain;
    public readonly int Owner;
    public readonly int Up;
    public readonly int Right;
    public readonly int Down;
    public readonly int Left;

    public Blast(int cell, int chain, int owner, int up, int right, int down, int left)
    {
        Cell = cell;
        Chain = chain;
        Owner = owner;
        Up = up;
        Right = right;
        Down = down;
        Left = left;
    }

    public int Length(FuseDirection direction) => direction switch
    {
        FuseDirection.Up => Up,
        FuseDirection.Right => Right,
        FuseDirection.Down => Down,
        FuseDirection.Left => Left,
        _ => 0,
    };
}

internal readonly struct Knockout
{
    public readonly int Moogle;
    public readonly int Killer;
    public readonly Vector2 Position;
    public readonly bool Crushed;

    public Knockout(int moogle, int killer, Vector2 position, bool crushed)
    {
        Moogle = moogle;
        Killer = killer;
        Position = position;
        Crushed = crushed;
    }
}

internal readonly struct Pickup
{
    public readonly int Moogle;
    public readonly PowerUp Kind;
    public readonly Vector2 Position;

    public Pickup(int moogle, PowerUp kind, Vector2 position)
    {
        Moogle = moogle;
        Kind = kind;
        Position = position;
    }
}

internal sealed class FuseEventList
{
    private readonly int[] cells;
    private readonly int[] owners;

    public FuseEventList(int capacity)
    {
        cells = new int[capacity];
        owners = new int[capacity];
    }

    public int Count { get; private set; }

    public int Cell(int index) => cells[index];

    public int Owner(int index) => owners[index];

    public void Add(int cell, int owner)
    {
        if (Count >= cells.Length)
        {
            return;
        }

        cells[Count] = cell;
        owners[Count] = owner;
        Count++;
    }

    public void Clear()
    {
        Count = 0;
    }
}
