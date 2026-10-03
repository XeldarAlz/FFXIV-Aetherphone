using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Music.Radio.Live;

internal sealed class RadioCountLabel
{
    private LanguageInfo? language;
    private string? template;
    private string? argument;
    private long first = long.MinValue;
    private long second = long.MinValue;
    private string text = string.Empty;

    public string Format(LocString entry, long value)
    {
        if (Matches(entry.Key, value, 0))
        {
            return text;
        }

        Remember(entry.Key, value, 0);
        text = Loc.T(entry, value);
        return text;
    }

    public string Format(LocString entry, long value, long other)
    {
        if (Matches(entry.Key, value, other))
        {
            return text;
        }

        Remember(entry.Key, value, other);
        text = Loc.T(entry, value, other);
        return text;
    }

    public string Format(LocString entry, string argument)
    {
        if (Matches(entry.Key, 0, 0) && ReferenceEquals(this.argument, argument))
        {
            return text;
        }

        Remember(entry.Key, 0, 0);
        this.argument = argument;
        text = Loc.T(entry, argument);
        return text;
    }

    public string Prefixed(string prefix, LocString entry, long value)
    {
        if (Matches(entry.Key, value, 0) && ReferenceEquals(argument, prefix))
        {
            return text;
        }

        Remember(entry.Key, value, 0);
        argument = prefix;
        text = string.Concat(prefix, " · ", Loc.T(entry, value));
        return text;
    }

    public string Plural(LocPlural entry, int value)
    {
        if (Matches(entry.KeyBase, value, 0))
        {
            return text;
        }

        Remember(entry.KeyBase, value, 0);
        text = Loc.Plural(entry, value);
        return text;
    }

    public string Number(long value)
    {
        if (Matches(null, value, 0))
        {
            return text;
        }

        Remember(null, value, 0);
        text = value.ToString(Loc.Culture);
        return text;
    }

    private bool Matches(string? entry, long value, long other)
    {
        return ReferenceEquals(language, Loc.Current) && ReferenceEquals(template, entry) && first == value
               && second == other;
    }

    private void Remember(string? entry, long value, long other)
    {
        argument = null;
        language = Loc.Current;
        template = entry;
        first = value;
        second = other;
    }
}
