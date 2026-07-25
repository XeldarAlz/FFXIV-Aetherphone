using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Venues;

internal static class VenueFormat
{
    public static string Range(VenueEvent venue)
    {
        var start = venue.StartUtc.ToLocalTime();
        var startText = TimeText.Clock(start);
        if (!IsToday(start))
        {
            startText = start.ToString("dd/MM " + TimeText.ClockPattern, Loc.Culture);
        }

        if (venue.EndUtc is not { } endUtc)
        {
            return startText;
        }

        var end = endUtc.ToLocalTime();
        return $"{startText} – {TimeText.Clock(end)}";
    }

    public static string EndsAt(VenueEvent venue)
    {
        if (venue.EndUtc is not { } endUtc)
        {
            return string.Empty;
        }

        return TimeText.Clock(endUtc.ToLocalTime());
    }

    public static string Starts(VenueEvent venue, DateTime nowUtc)
    {
        var delta = venue.StartUtc - nowUtc;
        if (delta <= TimeSpan.Zero)
        {
            return Range(venue);
        }

        if (delta < TimeSpan.FromHours(1))
        {
            return $"in {Math.Max(1, (int)delta.TotalMinutes)}m";
        }

        if (delta < TimeSpan.FromDays(1))
        {
            return $"in {(int)delta.TotalHours}h";
        }

        return $"in {(int)delta.TotalDays}d";
    }

    private static bool IsToday(DateTime local) => local.Date == DateTime.Now.Date;
}
