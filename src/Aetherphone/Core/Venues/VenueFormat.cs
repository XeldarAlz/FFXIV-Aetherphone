using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Venues;

internal enum VenueStatusKind : byte
{
    None,
    Upcoming,
    Open,
    Live,
}

internal readonly record struct VenueStatus(VenueStatusKind Kind, string Label);

internal static class VenueFormat
{
    public static VenueStatus Status(VenueEvent venue, DateTime nowUtc) =>
        Describe(venue, venue.StartUtc, venue.EndUtc, venue.IsConfirmedLive(nowUtc), venue.IsScheduledOpen(nowUtc),
            (venue.Sources & VenueSources.Partake) != 0, nowUtc);

    public static VenueStatus EventStatus(VenueEvent venue, DateTime nowUtc)
    {
        var on = venue.IsEventOn(nowUtc);
        return Describe(venue, venue.EventStartUtc, venue.EventEndUtc, on && venue.IsConfirmedLive(nowUtc), on, true,
            nowUtc);
    }

    private static VenueStatus Describe(VenueEvent venue, DateTime? startUtc, DateTime? endUtc, bool live, bool open,
        bool starts, DateTime nowUtc)
    {
        var ends = EndsAt(endUtc, nowUtc);
        if (live)
        {
            var headline = venue.LiveHeadline.Length > 0 ? venue.LiveHeadline : Loc.T(L.Venues.LiveNowLabel);
            return new VenueStatus(VenueStatusKind.Live,
                ends.Length > 0 ? $"{headline} · {Loc.T(L.Venues.UntilTime, ends)}" : headline);
        }

        if (open)
        {
            return new VenueStatus(VenueStatusKind.Open,
                ends.Length > 0 ? Loc.T(L.Venues.OpenUntil, ends) : Loc.T(L.Venues.OpenNow));
        }

        if (startUtc is not { } start || start <= nowUtc)
        {
            return new VenueStatus(VenueStatusKind.None, string.Empty);
        }

        var moment = TimeText.FutureMoment(new DateTimeOffset(start).ToUnixTimeSeconds());
        var label = starts ? Loc.T(L.Venues.StartsAt, moment) : Loc.T(L.Venues.OpensAt, moment);
        return new VenueStatus(VenueStatusKind.Upcoming, label);
    }

    public static string Window(VenueEvent venue)
    {
        if (venue.StartUtc is not { } start)
        {
            return string.Empty;
        }

        var startText = TimeText.FutureMoment(new DateTimeOffset(start).ToUnixTimeSeconds());
        return venue.EndUtc is { } end ? $"{startText} – {TimeText.Clock(end.ToLocalTime())}" : startText;
    }

    public static string Meta(VenueEvent venue)
    {
        var world = venue.World.Length > 0 && venue.DataCenter.Length > 0
            ? $"{venue.World} ({venue.DataCenter})"
            : venue.World.Length > 0 ? venue.World : venue.DataCenter;
        return VenueMapper.BuildPlaceLine(world, venue.LocationLine);
    }

    public static string Viewers(int count) => Loc.T(L.Venues.Watching, count.ToString("N0", Loc.Culture));

    private static string EndsAt(DateTime? endUtc, DateTime nowUtc) =>
        endUtc is { } end && end > nowUtc ? TimeText.Clock(end.ToLocalTime()) : string.Empty;
}
