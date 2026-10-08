namespace Aetherphone.Apps.Casino.Venue;

internal static class VenueTumble
{
    public const float StepsPerSecond = 18f;

    private const uint Mix = 0x9E3779B9u;

    public static long Value(long seq, int step, long bound)
    {
        if (bound <= 1)
        {
            return 1;
        }

        var hash = Hash((uint)seq ^ (uint)(seq >> 32), (uint)step);
        return (long)(hash % (ulong)bound) + 1;
    }

    public static int StepAt(float seconds)
    {
        return (int)MathF.Floor(MathF.Max(0f, seconds) * StepsPerSecond);
    }

    public static uint SeedOf(string hex)
    {
        var value = 2166136261u;
        for (var index = 0; index < hex.Length; index++)
        {
            value = (value ^ hex[index]) * 16777619u;
        }

        return value;
    }

    public static uint Hash(uint first, uint second)
    {
        var value = first * Mix + second;
        value ^= value >> 16;
        value *= 0x7FEB352Du;
        value ^= value >> 15;
        value *= 0x846CA68Bu;
        value ^= value >> 16;
        return value;
    }

    public static float Unit(uint first, uint second)
    {
        return (Hash(first, second) & 0xFFFFFF) / (float)0x1000000;
    }
}
