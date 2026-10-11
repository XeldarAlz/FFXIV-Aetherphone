using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Casino.Originals;

internal static class OriginalsText
{
    private const int CacheLimit = 256;

    private static readonly Dictionary<int, string> Percents = new();
    private static readonly Dictionary<int, string> Hundredths = new();
    private static LanguageInfo? language;

    public static string Percent(int basisPoints)
    {
        Validate();
        if (Percents.TryGetValue(basisPoints, out var cached))
        {
            return cached;
        }

        var text = Loc.T(L.Originals.Percent, (basisPoints / 100m).ToString("0.##", Loc.Culture));
        return Remember(Percents, basisPoints, text);
    }

    public static string Decimal(int hundredths)
    {
        Validate();
        if (Hundredths.TryGetValue(hundredths, out var cached))
        {
            return cached;
        }

        return Remember(Hundredths, hundredths, (hundredths / 100m).ToString("0.00", Loc.Culture));
    }

    public static string Multiplier(long tenThousandths) =>
        CasinoMultiples.Label((int)Math.Clamp(tenThousandths / 100, 0, int.MaxValue));

    public static string MultiplierHundredths(long hundredths) =>
        CasinoMultiples.Label((int)Math.Clamp(hundredths, 0, int.MaxValue));

    private static void Validate()
    {
        if (ReferenceEquals(language, Loc.Current))
        {
            return;
        }

        language = Loc.Current;
        Percents.Clear();
        Hundredths.Clear();
    }

    private static string Remember(Dictionary<int, string> cache, int key, string text)
    {
        if (cache.Count >= CacheLimit)
        {
            cache.Clear();
        }

        cache[key] = text;
        return text;
    }
}

internal struct OriginalsLabel
{
    private string? first;
    private string? second;
    private string? key;
    private LanguageInfo? language;
    private string? text;

    public string Get(LocString entry, string value) => Get(entry, value, string.Empty);

    public string Get(LocString entry, string value, string other)
    {
        if (text is not null && ReferenceEquals(first, value) && ReferenceEquals(second, other)
            && ReferenceEquals(language, Loc.Current) && string.Equals(key, entry.Key, StringComparison.Ordinal))
        {
            return text;
        }

        first = value;
        second = other;
        key = entry.Key;
        language = Loc.Current;
        text = other.Length == 0 ? Loc.T(entry, value) : Loc.T(entry, value, other);
        return text;
    }
}
