using Aetherphone.Core.Localization;
using Aetherphone.Core.Venues;

namespace Aetherphone.Apps.Venues;

internal readonly record struct VenueCardText(VenueStatus Status, string Meta, string Stat, string Initial,
    string PressId);

internal sealed class VenueTextList(string pressPrefix, bool eventTimes = false)
{
    private readonly List<VenueCardText> items = new();

    public int Count => items.Count;

    public VenueCardText this[int index] => items[index];

    public void Fill(IReadOnlyList<VenueEvent> venues, DateTime nowUtc)
    {
        items.Clear();
        for (var index = 0; index < venues.Count; index++)
        {
            items.Add(VenueLabelCache.Build(venues[index], nowUtc, pressPrefix, eventTimes));
        }
    }
}

internal sealed class VenueLabelCache
{
    private const int MaxPlusLabel = 99;

    private static readonly string[] PlusLabels = BuildPlusLabels();

    private LanguageInfo? language;
    private int formatVersion = -1;

    public static string Plus(int count) => PlusLabels[Math.Clamp(count, 1, MaxPlusLabel)];

    public bool LanguageChanged()
    {
        if (ReferenceEquals(language, Loc.Current) && formatVersion == TimeText.FormatVersion)
        {
            return false;
        }

        language = Loc.Current;
        formatVersion = TimeText.FormatVersion;
        return true;
    }

    public static VenueCardText Build(VenueEvent venue, DateTime nowUtc, string pressPrefix, bool eventTimes = false) =>
        new(eventTimes ? VenueFormat.EventStatus(venue, nowUtc) : VenueFormat.Status(venue, nowUtc),
            VenueFormat.Meta(venue), StatOf(venue, nowUtc), InitialOf(venue.Title), pressPrefix + venue.Id);

    public static string InitialOf(string title)
    {
        for (var index = 0; index < title.Length; index++)
        {
            if (char.IsLetterOrDigit(title[index]))
            {
                return char.ToUpperInvariant(title[index]).ToString();
            }
        }

        return "?";
    }

    private static string StatOf(VenueEvent venue, DateTime nowUtc)
    {
        if (venue.LiveViewers > 0 && venue.IsConfirmedLive(nowUtc))
        {
            return venue.LiveViewers.ToString("N0", Loc.Culture);
        }

        return venue.AttendeeCount > 0 ? venue.AttendeeCount.ToString("N0", Loc.Culture) : string.Empty;
    }

    private static string[] BuildPlusLabels()
    {
        var labels = new string[MaxPlusLabel + 1];
        for (var index = 0; index <= MaxPlusLabel; index++)
        {
            labels[index] = "+" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return labels;
    }
}
