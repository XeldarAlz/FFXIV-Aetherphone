using System.Collections.Concurrent;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Market;

internal static class MarketFormat
{
    private static readonly ConcurrentDictionary<(string, long), string> GilCache = new();
    private static readonly ConcurrentDictionary<(string, int), string> ClipCache = new();

    public static string Gil(long amount)
    {
        var culture = Loc.Culture;
        var key = (culture.Name, amount);
        if (GilCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var formatted = amount.ToString("N0", culture);
        GilCache.TryAdd(key, formatted);
        return formatted;
    }

    public static string Gil(double amount) => Gil((long)Math.Round(amount));

    public static DateTime FromUnix(long value)
    {
        if (value <= 0)
        {
            return default;
        }

        var milliseconds = value > 1_000_000_000_000L ? value : value * 1000L;
        return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime;
    }

    public static string Velocity(double perDay)
    {
        if (perDay <= 0)
        {
            return "—";
        }

        if (perDay < 10)
        {
            return Loc.T(L.Market.PerDay, perDay.ToString("0.0", Loc.Culture));
        }

        return Loc.T(L.Market.PerDay, Gil(perDay));
    }

    public static string Clip(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        var key = (value, maxLength);
        if (ClipCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var clipped = value.Substring(0, maxLength - 1) + "…";
        ClipCache.TryAdd(key, clipped);
        return clipped;
    }
}
