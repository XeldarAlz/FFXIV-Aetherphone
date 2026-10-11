using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Casino.Stage;

internal static class CasinoMultiples
{
    private const int CacheLimit = 256;

    private static readonly Dictionary<int, string> Cache = new();
    private static LanguageInfo? language;

    public static string Label(int hundredths)
    {
        if (!ReferenceEquals(language, Loc.Current))
        {
            language = Loc.Current;
            Cache.Clear();
        }

        if (Cache.TryGetValue(hundredths, out var cached))
        {
            return cached;
        }

        if (Cache.Count >= CacheLimit)
        {
            Cache.Clear();
        }

        var text = Loc.T(L.Strip.Multiple, (hundredths / 100m).ToString("0.##", Loc.Culture));
        Cache[hundredths] = text;
        return text;
    }
}
