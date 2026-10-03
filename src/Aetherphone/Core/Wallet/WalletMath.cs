namespace Aetherphone.Core.Wallet;

internal enum CapLevel : byte
{
    None,
    Room,
    Near,
    Full,
}

internal static class WalletMath
{
    public const float NearFraction = 0.9f;

    public static CapLevel Level(long amount, long cap)
    {
        if (cap <= 0)
        {
            return CapLevel.None;
        }

        if (amount >= cap)
        {
            return CapLevel.Full;
        }

        return amount >= (long)Math.Ceiling(cap * (double)NearFraction) ? CapLevel.Near : CapLevel.Room;
    }

    public static float Fraction(long amount, long cap)
    {
        if (cap <= 0 || amount <= 0)
        {
            return 0f;
        }

        return amount >= cap ? 1f : (float)(amount / (double)cap);
    }

    public static long Remaining(long amount, long cap) => cap <= 0 ? 0 : Math.Max(0, cap - amount);

    public static bool NeedsAttention(CapLevel level) => level is CapLevel.Near or CapLevel.Full;
}
