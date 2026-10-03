using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Hunts;

internal static class HuntsText
{
    public static string Span(TimeSpan span)
    {
        if (span < TimeSpan.Zero)
        {
            span = TimeSpan.Zero;
        }

        if (span.TotalHours < 1d)
        {
            return Loc.T(L.Time.MinutesShort, Math.Max(1, (int)span.TotalMinutes));
        }

        if (span.TotalDays < 1d)
        {
            var hours = (int)span.TotalHours;
            return span.Minutes > 0
                ? Loc.T(L.Hunts.SpanHoursMinutes, hours, span.Minutes)
                : Loc.T(L.Time.HoursShort, hours);
        }

        return Loc.T(L.Time.DaysShort, (int)span.TotalDays);
    }

    public static string Percent(double value) =>
        ((int)Math.Round(value, MidpointRounding.AwayFromZero)).ToString(Loc.Culture) + "%";

    public static string Hours(double hours) => Loc.T(L.Time.HoursShort, hours.ToString("0.#", Loc.Culture));

    public static string Moment(DateTimeOffset moment)
    {
        var unix = moment.ToUnixTimeSeconds();
        return TimeText.SameLocalDay(unix, DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            ? TimeText.Clock(moment.ToLocalTime())
            : TimeText.FutureMoment(unix);
    }
}
