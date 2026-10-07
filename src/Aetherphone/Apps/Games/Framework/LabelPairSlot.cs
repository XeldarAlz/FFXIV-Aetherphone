using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Games.Framework;

internal struct LabelPairSlot
{
    private int first;
    private int second;
    private LanguageInfo? language;
    private string? text;

    public string Get(LocString entry, int wantedFirst, int wantedSecond)
    {
        if (text is not null && first == wantedFirst && second == wantedSecond &&
            ReferenceEquals(language, Loc.Current))
        {
            return text;
        }

        first = wantedFirst;
        second = wantedSecond;
        language = Loc.Current;
        text = Loc.T(entry, GameNumber.Label(wantedFirst), GameNumber.Label(wantedSecond));
        return text;
    }

    public void Reset()
    {
        text = null;
    }
}
