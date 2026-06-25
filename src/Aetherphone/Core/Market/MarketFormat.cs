using System.Collections.Concurrent;
using System.Globalization;

namespace Aetherphone.Core.Market;

internal static class MarketFormat
{
    private static readonly ConcurrentDictionary<long, string> GilCache = new();
    private static readonly ConcurrentDictionary<(string, int), string> ClipCache = new();

    public static string Gil(long amount)
    {
        if (GilCache.TryGetValue(amount, out var cached))
        {
            return cached;
        }

        var formatted = amount.ToString("N0", CultureInfo.InvariantCulture);
        GilCache.TryAdd(amount, formatted);
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

    public static string Ago(DateTime utc)
    {
        if (utc == default)
        {
            return "—";
        }

        var delta = DateTime.UtcNow - utc;
        if (delta < TimeSpan.Zero)
        {
            delta = TimeSpan.Zero;
        }

        if (delta.TotalSeconds < 60)
        {
            return "just now";
        }

        if (delta.TotalMinutes < 60)
        {
            return $"{(int)delta.TotalMinutes}m ago";
        }

        if (delta.TotalHours < 24)
        {
            return $"{(int)delta.TotalHours}h ago";
        }

        return $"{(int)delta.TotalDays}d ago";
    }

    public static string Velocity(double perDay)
    {
        if (perDay <= 0)
        {
            return "—";
        }

        if (perDay < 10)
        {
            return perDay.ToString("0.0", CultureInfo.InvariantCulture) + "/day";
        }

        return Gil(perDay) + "/day";
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
