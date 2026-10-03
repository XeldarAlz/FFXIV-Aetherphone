using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Music.Jam;

internal struct JamTextCache
{
    private string? template;
    private string? argument;
    private int number;
    private string? text;

    public string Format(string format, int value)
    {
        if (text is null || number != value || argument is not null
            || !string.Equals(template, format, StringComparison.Ordinal))
        {
            template = format;
            argument = null;
            number = value;
            text = string.Format(Loc.Culture, format, value);
        }

        return text;
    }

    public string Format(string format, string value)
    {
        if (text is null || !string.Equals(template, format, StringComparison.Ordinal)
            || !string.Equals(argument, value, StringComparison.Ordinal))
        {
            template = format;
            argument = value;
            text = string.Format(Loc.Culture, format, value);
        }

        return text;
    }

    public string Upper(string value)
    {
        if (text is null || !string.Equals(template, value, StringComparison.Ordinal))
        {
            template = value;
            argument = null;
            text = Loc.Culture.TextInfo.ToUpper(value);
        }

        return text;
    }
}
