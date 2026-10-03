namespace Aetherphone.Apps.Games.Swoop;

internal struct SwoopRandom
{
    private const uint FallbackState = 0x9E3779B9u;
    private uint state;

    public SwoopRandom(uint seed)
    {
        state = seed == 0u ? FallbackState : seed;
        NextUnit();
        NextUnit();
    }

    public float NextUnit()
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return (state >> 8) * (1f / 16777216f);
    }

    public float Range(float minimum, float maximum) => minimum + (maximum - minimum) * NextUnit();

    public int Next(int exclusiveMaximum) => Math.Min(exclusiveMaximum - 1, (int)(NextUnit() * exclusiveMaximum));
}
