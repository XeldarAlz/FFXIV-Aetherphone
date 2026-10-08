using System.Collections.Concurrent;

namespace Aetherphone.Core.Localization;

internal static class NumberText
{
    private const int CacheLimit = 512;

    private static readonly ConcurrentDictionary<long, string> GroupCache = new();
    private static readonly ConcurrentDictionary<long, string> CompactCache = new();
    private static readonly ConcurrentDictionary<long, string> SignedCache = new();
    private static readonly long[] CompactUnits = { 1_000_000_000_000L, 1_000_000_000L, 1_000_000L, 1_000L };
    private static readonly string[] CompactSuffixes = { "T", "B", "M", "K" };
    private static LanguageInfo? cachedLanguage;

    public static string Group(long value)
    {
        Validate();
        if (GroupCache.TryGetValue(value, out var cached))
        {
            return cached;
        }

        return Remember(GroupCache, value, value.ToString("N0", Loc.Culture));
    }

    public static string Compact(long value)
    {
        Validate();
        if (CompactCache.TryGetValue(value, out var cached))
        {
            return cached;
        }

        return Remember(CompactCache, value, CompactText(value));
    }

    public static string Signed(long value)
    {
        Validate();
        if (value <= 0)
        {
            return Group(value);
        }

        if (SignedCache.TryGetValue(value, out var cached))
        {
            return cached;
        }

        return Remember(SignedCache, value, Loc.T(L.Stage.Plus, Group(value)));
    }

    internal static string CompactText(long value)
    {
        if (value == long.MinValue)
        {
            return "-" + CompactText(long.MaxValue);
        }

        if (value < 0)
        {
            return "-" + CompactText(-value);
        }

        for (var unitIndex = 0; unitIndex < CompactUnits.Length; unitIndex++)
        {
            var unit = CompactUnits[unitIndex];
            if (value < unit)
            {
                continue;
            }

            var whole = value / unit;
            if (whole >= 100)
            {
                return whole.ToString(Loc.Culture) + CompactSuffixes[unitIndex];
            }

            var tenths = value % unit / (unit / 10);
            var amount = whole + tenths / 10m;
            return amount.ToString(tenths == 0 ? "0" : "0.0", Loc.Culture) + CompactSuffixes[unitIndex];
        }

        return value.ToString(Loc.Culture);
    }

    private static void Validate()
    {
        if (ReferenceEquals(Loc.Current, cachedLanguage))
        {
            return;
        }

        cachedLanguage = Loc.Current;
        GroupCache.Clear();
        CompactCache.Clear();
        SignedCache.Clear();
    }

    private static string Remember(ConcurrentDictionary<long, string> cache, long value, string text)
    {
        if (cache.Count >= CacheLimit)
        {
            cache.Clear();
        }

        cache[value] = text;
        return text;
    }
}
