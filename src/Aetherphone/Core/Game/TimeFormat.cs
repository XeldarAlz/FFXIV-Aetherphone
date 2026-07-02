using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Game;

internal static class TimeFormat
{
    public static string Relative(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.Zero)
        {
            return Loc.T(L.Time.Now);
        }

        var totalMinutes = (int)remaining.TotalMinutes;
        if (totalMinutes < 60)
        {
            return Loc.T(L.Time.InMinutes, Math.Max(1, totalMinutes));
        }

        var totalHours = totalMinutes / 60;
        if (totalHours < 24)
        {
            var minutes = totalMinutes % 60;
            return minutes == 0 ? Loc.T(L.Time.InHours, totalHours) : Loc.T(L.Time.InHoursMinutes, totalHours, minutes);
        }

        var days = totalHours / 24;
        var hours = totalHours % 24;
        return hours == 0 ? Loc.T(L.Timers.InDays, days) : Loc.T(L.Timers.InDaysHours, days, hours);
    }
}
