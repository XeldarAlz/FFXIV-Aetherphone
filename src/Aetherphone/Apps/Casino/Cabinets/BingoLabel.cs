using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Casino.Cabinets;

internal struct BingoLabel
{
    private const int Clock = 1;
    private const int Pair = 2;

    private string? text;
    private string? key;
    private LanguageInfo? language;
    private int first;
    private int second;
    private int mode;

    public string Duration(LocString entry, int seconds)
    {
        if (Current(entry, seconds, 0, Clock))
        {
            return text!;
        }

        text = Loc.T(entry, TimeText.Duration(seconds));
        return text;
    }

    public string Numbers(LocString entry, int left, int right)
    {
        if (Current(entry, left, right, Pair))
        {
            return text!;
        }

        text = Loc.T(entry, GameNumber.Label(left), GameNumber.Label(right));
        return text;
    }

    private bool Current(LocString entry, int left, int right, int wanted)
    {
        if (text is not null && left == first && right == second && mode == wanted
            && ReferenceEquals(language, Loc.Current) && string.Equals(key, entry.Key, StringComparison.Ordinal))
        {
            return true;
        }

        first = left;
        second = right;
        mode = wanted;
        language = Loc.Current;
        key = entry.Key;
        return false;
    }
}
