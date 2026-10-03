namespace Aetherphone.Apps.Games.Coil;

internal enum CoilState : byte
{
    Ready,
    Playing,
    Draining,
    StageClear,
    GameOver,
}

internal enum CoilPower : byte
{
    None,
    Freeze,
    Slow,
    Reverse,
    Blast,
    Prism,
    Guide,
}

internal struct CoilMarble
{
    public float Arc;
    public float Lag;
    public float PullSpeed;
    public float Arrive;
    public Vector2 ArriveFrom;
    public byte Colour;
    public CoilPower Power;
}

internal struct CoilShot
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float CrossArc;
    public int Gaps;
    public byte Colour;
    public CoilPower Kind;
    public bool OnTrack;
}

internal readonly struct CoilBurst
{
    public readonly Vector2 Position;
    public readonly byte Colour;
    public readonly CoilPower Power;

    public CoilBurst(Vector2 position, byte colour, CoilPower power)
    {
        Position = position;
        Colour = colour;
        Power = power;
    }
}

internal readonly struct CoilClear
{
    public readonly Vector2 Center;
    public readonly int Count;
    public readonly int Points;
    public readonly int Multiplier;
    public readonly byte Colour;

    public CoilClear(Vector2 center, int count, int points, int multiplier, byte colour)
    {
        Center = center;
        Count = count;
        Points = points;
        Multiplier = multiplier;
        Colour = colour;
    }
}

internal readonly struct CoilGap
{
    public readonly Vector2 Position;
    public readonly int Points;

    public CoilGap(Vector2 position, int points)
    {
        Position = position;
        Points = points;
    }
}

internal readonly struct CoilTrigger
{
    public readonly Vector2 Position;
    public readonly CoilPower Power;

    public CoilTrigger(Vector2 position, CoilPower power)
    {
        Position = position;
        Power = power;
    }
}

internal struct CoilRandom
{
    private uint state;

    public CoilRandom(int seed)
    {
        state = (uint)seed * 2654435761u + 0x9E3779B9u;
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

    public int Next(int exclusiveMax) => (int)(NextUInt() % (uint)exclusiveMax);

    public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);
}
