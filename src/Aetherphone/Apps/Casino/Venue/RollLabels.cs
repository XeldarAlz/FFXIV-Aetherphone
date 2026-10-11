using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Casino.Venue;

internal sealed class RollLabels
{
    private const int Limit = 256;

    private readonly Dictionary<long, string> times = new();
    private readonly Dictionary<long, string> values = new();
    private int formatVersion = -1;
    private LanguageInfo? language;

    public string Time(long seq, long atUnixMs)
    {
        Validate();
        if (times.TryGetValue(seq, out var cached))
        {
            return cached;
        }

        return Remember(times, seq, TimeText.Clock(atUnixMs / 1000));
    }

    public string Value(long value)
    {
        Validate();
        if (values.TryGetValue(value, out var cached))
        {
            return cached;
        }

        return Remember(values, value, NumberText.Group(value));
    }

    private static string Remember(Dictionary<long, string> cache, long key, string text)
    {
        if (cache.Count >= Limit)
        {
            cache.Clear();
        }

        cache[key] = text;
        return text;
    }

    private void Validate()
    {
        if (formatVersion == TimeText.FormatVersion && ReferenceEquals(language, Loc.Current))
        {
            return;
        }

        formatVersion = TimeText.FormatVersion;
        language = Loc.Current;
        times.Clear();
        values.Clear();
    }
}
