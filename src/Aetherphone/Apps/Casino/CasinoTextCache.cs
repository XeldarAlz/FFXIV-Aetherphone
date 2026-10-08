using Aetherphone.Core.Localization;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino;

internal sealed class CasinoTextCache
{
    private const int Limit = 384;
    private const string SignedKey = "casino.text.signed";
    private const string DurationKey = "casino.text.duration";

    private readonly record struct Slot(string Key, long First, long Second);

    private readonly Dictionary<Slot, string> texts = new();
    private LanguageInfo? language;
    private int timeFormat = -1;
    private DateTime day;
    private int checkedFrame = -1;

    public string Number(LocString template, long value)
    {
        Validate();
        var slot = new Slot(template.Key, value, 0);
        if (texts.TryGetValue(slot, out var cached))
        {
            return cached;
        }

        return Remember(slot, Loc.T(template, NumberText.Group(value)));
    }

    public string Numbers(LocString template, long first, long second)
    {
        Validate();
        var slot = new Slot(template.Key, first, second);
        if (texts.TryGetValue(slot, out var cached))
        {
            return cached;
        }

        return Remember(slot, Loc.T(template, NumberText.Group(first), NumberText.Group(second)));
    }

    public string Count(LocString template, int count)
    {
        Validate();
        var slot = new Slot(template.Key, count, 1);
        if (texts.TryGetValue(slot, out var cached))
        {
            return cached;
        }

        return Remember(slot, Loc.T(template, Games.Framework.GameNumber.Label(count)));
    }

    public string Counts(LocString template, int first, int second)
    {
        Validate();
        var slot = new Slot(template.Key, first, ((long)second << 1) | 1);
        if (texts.TryGetValue(slot, out var cached))
        {
            return cached;
        }

        return Remember(slot, Loc.T(template, Games.Framework.GameNumber.Label(first),
            Games.Framework.GameNumber.Label(second)));
    }

    public string Duration(LocString template, int seconds)
    {
        Validate();
        var slot = new Slot(template.Key, seconds, 2);
        if (texts.TryGetValue(slot, out var cached))
        {
            return cached;
        }

        return Remember(slot, Loc.T(template, TimeText.Duration(seconds)));
    }

    public string Moment(LocString template, long unixSeconds)
    {
        Validate();
        var slot = new Slot(template.Key, unixSeconds, 3);
        if (texts.TryGetValue(slot, out var cached))
        {
            return cached;
        }

        return Remember(slot, Loc.T(template, TimeText.FutureMoment(unixSeconds)));
    }

    public string Duration(int seconds)
    {
        Validate();
        var slot = new Slot(DurationKey, seconds, 0);
        if (texts.TryGetValue(slot, out var cached))
        {
            return cached;
        }

        return Remember(slot, TimeText.Duration(seconds));
    }

    public string Signed(long value)
    {
        Validate();
        var slot = new Slot(SignedKey, value, 0);
        if (texts.TryGetValue(slot, out var cached))
        {
            return cached;
        }

        return Remember(slot, SignedText(value));
    }

    internal static string SignedText(long value) => value switch
    {
        > 0 => NumberText.Signed(value),
        < 0 => NumberText.Group(value),
        _ => NumberText.Group(0),
    };

    private string Remember(Slot slot, string text)
    {
        if (texts.Count >= Limit)
        {
            texts.Clear();
        }

        texts[slot] = text;
        return text;
    }

    private void Validate()
    {
        var frame = ImGui.GetFrameCount();
        if (frame == checkedFrame)
        {
            return;
        }

        checkedFrame = frame;
        var today = DateTime.Now.Date;
        if (ReferenceEquals(language, Loc.Current) && timeFormat == TimeText.FormatVersion && today == day)
        {
            return;
        }

        language = Loc.Current;
        timeFormat = TimeText.FormatVersion;
        day = today;
        texts.Clear();
    }
}
