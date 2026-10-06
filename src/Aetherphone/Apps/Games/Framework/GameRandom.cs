namespace Aetherphone.Apps.Games.Framework;

internal struct GameRandom
{
    private const float UnitScale = 1f / 16777216f;

    private uint state0;
    private uint state1;
    private uint state2;
    private uint state3;

    public static GameRandom FromSeed(ulong seed)
    {
        var random = default(GameRandom);
        var mixer = seed;
        var first = SplitMix(ref mixer);
        var second = SplitMix(ref mixer);
        random.state0 = (uint)first;
        random.state1 = (uint)(first >> 32);
        random.state2 = (uint)second;
        random.state3 = (uint)(second >> 32);
        if ((random.state0 | random.state1 | random.state2 | random.state3) == 0u)
        {
            random.state0 = 1u;
        }

        return random;
    }

    public static GameRandom Fresh() => FromSeed(GameSeed.Fresh());

    public uint NextUInt()
    {
        var result = RotateLeft(state1 * 5u, 7) * 9u;
        var shifted = state1 << 9;
        state2 ^= state0;
        state3 ^= state1;
        state1 ^= state2;
        state0 ^= state3;
        state2 ^= shifted;
        state3 = RotateLeft(state3, 11);
        return result;
    }

    public int Next(int max)
    {
        if (max <= 0)
        {
            return 0;
        }

        return (int)((ulong)NextUInt() * (ulong)max >> 32);
    }

    public int Next(int min, int max) => min + Next(max - min);

    public float NextFloat() => (NextUInt() >> 8) * UnitScale;

    public float Range(float min, float max) => min + (max - min) * NextFloat();

    public bool Chance(float probability) => NextFloat() < probability;

    public int Sign() => (NextUInt() & 1u) == 0u ? -1 : 1;

    private static uint RotateLeft(uint value, int count) => (value << count) | (value >> (32 - count));

    private static ulong SplitMix(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        var value = state;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}
