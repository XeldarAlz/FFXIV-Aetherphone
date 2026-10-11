using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Casino.Machines;

internal struct AmountSlot
{
    private long value;
    private string? key;
    private LanguageInfo? language;
    private string? text;

    public string Get(LocString entry, long wanted)
    {
        if (text is not null && value == wanted && ReferenceEquals(language, Loc.Current)
            && string.Equals(key, entry.Key, StringComparison.Ordinal))
        {
            return text;
        }

        value = wanted;
        key = entry.Key;
        language = Loc.Current;
        text = Loc.T(entry, NumberText.Compact(wanted));
        return text;
    }
}
