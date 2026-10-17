using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Games.Framework;

internal static class GameNumber
{
    private const int CacheLimit = 512;
    private static readonly Dictionary<int, string> Cache = new();
    private static readonly Dictionary<int, string> SignedCache = new();
    private static LanguageInfo? signedLanguage;

    public static string Label(int value)
    {
        if (Cache.TryGetValue(value, out var label))
        {
            return label;
        }

        if (Cache.Count >= CacheLimit)
        {
            Cache.Clear();
        }

        label = value.ToString();
        Cache[value] = label;
        return label;
    }

    public static string Signed(int value)
    {
        if (value < 0)
        {
            return Label(value);
        }

        if (!ReferenceEquals(signedLanguage, Loc.Current))
        {
            signedLanguage = Loc.Current;
            SignedCache.Clear();
        }

        if (SignedCache.TryGetValue(value, out var label))
        {
            return label;
        }

        if (SignedCache.Count >= CacheLimit)
        {
            SignedCache.Clear();
        }

        label = Loc.T(L.Stage.Plus, Label(value));
        SignedCache[value] = label;
        return label;
    }
}
