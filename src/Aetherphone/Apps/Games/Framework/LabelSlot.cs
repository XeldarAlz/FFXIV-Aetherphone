using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Games.Framework;

internal struct LabelSlot
{
    private int value;
    private LanguageInfo? language;
    private string? text;

    public string Get(LocString entry, int wanted)
    {
        if (text is not null && value == wanted && ReferenceEquals(language, Loc.Current))
        {
            return text;
        }

        value = wanted;
        language = Loc.Current;
        text = Loc.T(entry, GameNumber.Label(wanted));
        return text;
    }

    public void Reset()
    {
        text = null;
    }
}
