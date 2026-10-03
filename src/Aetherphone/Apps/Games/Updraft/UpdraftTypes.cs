namespace Aetherphone.Apps.Games.Updraft;

internal enum UpdraftCloudKind : byte
{
    Plain,
    Drifting,
    Fragile,
    Golden,
    Spring,
    Storm,
}

internal enum UpdraftPickupKind : byte
{
    Crystal,
    Feather,
    Shield,
}

internal enum UpdraftEventKind : byte
{
    Bounce,
    Break,
    Zap,
    ShieldBlock,
    Crystal,
    Feather,
    Shield,
    Milestone,
    PassedBest,
    Fell,
}

internal struct UpdraftCloud
{
    public float X;
    public float Y;
    public float HalfWidth;
    public float DriftSpeed;
    public float Squash;
    public float Dissolve;
    public float Cooldown;
    public int Look;
    public UpdraftCloudKind Kind;
    public bool Active;
    public bool OnPath;
    public bool Broken;
}

internal struct UpdraftPickup
{
    public float X;
    public float Y;
    public float Phase;
    public UpdraftPickupKind Kind;
    public bool Active;
}

internal struct UpdraftWind
{
    public float Bottom;
    public float Top;
    public float Speed;
    public bool Active;
}

internal readonly struct UpdraftEvent
{
    public readonly float X;
    public readonly float Y;
    public readonly int Value;
    public readonly int Detail;
    public readonly UpdraftEventKind Kind;
    public readonly UpdraftCloudKind CloudKind;

    public UpdraftEvent(UpdraftEventKind kind, float x, float y, int value, int detail, UpdraftCloudKind cloudKind)
    {
        Kind = kind;
        X = x;
        Y = y;
        Value = value;
        Detail = detail;
        CloudKind = cloudKind;
    }
}

internal readonly struct UpdraftInput
{
    public readonly float Axis;
    public readonly float TargetX;
    public readonly bool HasTarget;

    public UpdraftInput(float axis, bool hasTarget, float targetX)
    {
        Axis = axis;
        HasTarget = hasTarget;
        TargetX = targetX;
    }

    public static UpdraftInput Keys(float axis) => new(axis, false, 0f);

    public static UpdraftInput Pointer(float targetX) => new(0f, true, targetX);
}

internal struct UpdraftRandom
{
    private uint state;

    public UpdraftRandom(int seed)
    {
        state = unchecked((uint)seed * 2654435761u) ^ 0x9E3779B9u;
        if (state == 0u)
        {
            state = 0x6D2B79F5u;
        }
    }

    public uint NextUInt()
    {
        var value = state;
        value ^= value << 13;
        value ^= value >> 17;
        value ^= value << 5;
        state = value;
        return value;
    }

    public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

    public float Range(float minimum, float maximum) => minimum + (maximum - minimum) * NextFloat();

    public bool Chance(float probability) => NextFloat() < probability;

    public float Sign() => (NextUInt() & 1u) == 0u ? -1f : 1f;
}
