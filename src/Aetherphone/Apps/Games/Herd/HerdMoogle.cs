namespace Aetherphone.Apps.Games.Herd;

internal enum HerdSkill : byte
{
    Block,
    Dig,
    Bridge,
    Climb,
    Float,
    Bash,
}

internal enum HerdAction : byte
{
    Falling,
    Walking,
    Blocking,
    Digging,
    Bashing,
    Building,
    Climbing,
    Floating,
    Exiting,
    Splatting,
    Drowning,
    Popping,
    Saved,
    Dead,
}

internal enum HerdEventKind : byte
{
    Spawned,
    Saved,
    Splat,
    Drowned,
    FellOut,
    Popped,
    Assigned,
    Brick,
    BridgeDone,
    Dug,
    Bashed,
    Landed,
    GoalReached,
}

internal struct HerdMoogle
{
    public short Column;
    public short Row;
    public short PreviousColumn;
    public short PreviousRow;
    public sbyte Direction;
    public HerdAction Action;
    public bool Climber;
    public bool Floater;
    public short FallCells;
    public byte Counter;
    public byte Bricks;
    public short Timer;
    public short ActionTicks;
    public byte Variant;

    public readonly bool Gone => Action is HerdAction.Saved or HerdAction.Dead;

    public readonly bool Busy => !Gone && Action != HerdAction.Blocking;

    public readonly bool Leaving =>
        Action is HerdAction.Exiting or HerdAction.Splatting or HerdAction.Drowning or HerdAction.Popping ||
        Gone;
}

internal readonly struct HerdEvent
{
    public readonly HerdEventKind Kind;
    public readonly int Moogle;
    public readonly int Column;
    public readonly int Row;

    public HerdEvent(HerdEventKind kind, int moogle, int column, int row)
    {
        Kind = kind;
        Moogle = moogle;
        Column = column;
        Row = row;
    }
}
