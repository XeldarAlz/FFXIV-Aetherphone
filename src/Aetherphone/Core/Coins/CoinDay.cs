namespace Aetherphone.Core.Coins;

internal static class CoinDay
{
    public const int ResetHourUtc = 15;

    public static int Index(DateTime utc)
    {
        return (int)((utc.Ticks - (TimeSpan.TicksPerHour * ResetHourUtc)) / TimeSpan.TicksPerDay);
    }
}
