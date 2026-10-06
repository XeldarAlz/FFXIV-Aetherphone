using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Games.Framework;

internal static class GameNumber
{
    private static readonly Dictionary<int, string> Cache = new();
    private static readonly Dictionary<int, string> SignedCache = new();
    private static LanguageInfo? signedLanguage;

    public static string Label(int value)
    {
        if (Cache.TryGetValue(value, out var label))
        {
            return label;
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

        label = Loc.T(L.Stage.Plus, Label(value));
        SignedCache[value] = label;
        return label;
    }
}
