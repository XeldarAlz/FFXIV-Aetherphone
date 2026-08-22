namespace Aetherphone.Core.Localization;

internal static class TimeText
{
    private const string Pattern24Hour = "HH:mm";
    private const string Pattern12Hour = "h:mm tt";
    private static bool use24Hour = true;
    private static int formatVersion;

    public static bool Use24Hour => use24Hour;

    public static int FormatVersion => formatVersion;

    public static string ClockPattern => use24Hour ? Pattern24Hour : Pattern12Hour;

    public static void ApplyClockPreference(bool? preference)
    {
        use24Hour = preference ?? CultureUses24Hour();
        formatVersion++;
    }

    private static bool CultureUses24Hour() => Loc.Culture.DateTimeFormat.ShortTimePattern.Contains('H');

    public static string Clock(DateTime moment) => moment.ToString(ClockPattern, Loc.Culture);

    public static string Clock(DateTimeOffset moment) => moment.ToString(ClockPattern, Loc.Culture);

    public static string HourLabel(int hourOfDay)
    {
        if (use24Hour)
        {
            return hourOfDay.ToString("D2", Loc.Culture);
        }

        var hour = hourOfDay % 12;
        return (hour == 0 ? 12 : hour).ToString(Loc.Culture);
    }

    public static string MinuteLabel(int minuteOfHour) => minuteOfHour.ToString("D2", Loc.Culture);

    public static string MeridiemLabel(bool afternoon)
    {
        var designator = afternoon
            ? Loc.Culture.DateTimeFormat.PMDesignator
            : Loc.Culture.DateTimeFormat.AMDesignator;
        return designator.Length > 0 ? designator : afternoon ? "PM" : "AM";
    }

    public static string Ago(DateTime utcMoment)
    {
        if (utcMoment == default)
        {
            return "-";
        }

        var delta = DateTime.UtcNow - utcMoment;
        if (delta < TimeSpan.Zero)
        {
            delta = TimeSpan.Zero;
        }

        if (delta.TotalSeconds < 60)
        {
            return Loc.T(L.Time.JustNow);
        }

        if (delta.TotalMinutes < 60)
        {
            return Loc.T(L.Time.MinutesAgo, (int)delta.TotalMinutes);
        }

        if (delta.TotalHours < 24)
        {
            return Loc.T(L.Time.HoursAgo, (int)delta.TotalHours);
        }

        return Loc.T(L.Time.DaysAgo, (int)delta.TotalDays);
    }

    public static string Ago(DateTimeOffset moment) => Ago(moment.UtcDateTime);

    public static string Ago(long unixSeconds) =>
        unixSeconds <= 0 ? "-" : Ago(DateTimeOffset.FromUnixTimeSeconds(unixSeconds));

    public static string AgoPrecise(DateTime utcMoment)
    {
        if (utcMoment == default)
        {
            return "-";
        }

        var delta = DateTime.UtcNow - utcMoment;
        if (delta < TimeSpan.Zero)
        {
            delta = TimeSpan.Zero;
        }

        if (delta.TotalMinutes < 1)
        {
            return Loc.T(L.Time.SecondsAgo, (int)delta.TotalSeconds);
        }

        if (delta.TotalHours < 1)
        {
            var minutes = (int)delta.TotalMinutes;
            return delta.Seconds > 0
                ? Loc.T(L.Time.MinutesSecondsAgo, minutes, delta.Seconds)
                : Loc.T(L.Time.MinutesAgo, minutes);
        }

        if (delta.TotalHours < 24)
        {
            var hours = (int)delta.TotalHours;
            return delta.Minutes > 0
                ? Loc.T(L.Time.HoursMinutesAgo, hours, delta.Minutes)
                : Loc.T(L.Time.HoursAgo, hours);
        }

        return Loc.T(L.Time.DaysAgo, (int)delta.TotalDays);
    }

    public static string AgoPrecise(DateTimeOffset moment) => AgoPrecise(moment.UtcDateTime);

    public static string Short(long unixSeconds)
    {
        if (unixSeconds <= 0)
        {
            return string.Empty;
        }

        var moment = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
        var span = DateTime.UtcNow - moment;
        if (span.TotalSeconds < 60)
        {
            return Loc.T(L.Time.Now);
        }

        if (span.TotalMinutes < 60)
        {
            return Loc.T(L.Time.MinutesShort, (int)span.TotalMinutes);
        }

        if (span.TotalHours < 24)
        {
            return Loc.T(L.Time.HoursShort, (int)span.TotalHours);
        }

        if (span.TotalDays < 7)
        {
            return Loc.T(L.Time.DaysShort, (int)span.TotalDays);
        }

        return moment.ToString("MMM d", Loc.Culture);
    }

    public static string Short(DateTime localMoment)
    {
        if (localMoment == default)
        {
            return string.Empty;
        }

        var delta = DateTime.Now - localMoment;
        if (delta.TotalMinutes < 1)
        {
            return Loc.T(L.Time.Now);
        }

        if (delta.TotalHours < 1)
        {
            return Loc.T(L.Time.MinutesShort, (int)delta.TotalMinutes);
        }

        if (delta.TotalDays < 1)
        {
            return Loc.T(L.Time.HoursShort, (int)delta.TotalHours);
        }

        return Loc.T(L.Time.DaysShort, (int)delta.TotalDays);
    }

    public static string Clock(long unixSeconds)
    {
        if (unixSeconds <= 0)
        {
            return string.Empty;
        }

        return Clock(DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime());
    }

    public static string DayLabel(long unixSeconds)
    {
        if (unixSeconds <= 0)
        {
            return string.Empty;
        }

        var day = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime().Date;
        var today = DateTime.Now.Date;
        if (day == today)
        {
            return Loc.T(L.Time.Today);
        }

        if (day == today.AddDays(-1))
        {
            return Loc.T(L.Time.Yesterday);
        }

        if (day > today.AddDays(-7) && day < today)
        {
            return Loc.Culture.TextInfo.ToTitleCase(day.ToString("dddd", Loc.Culture));
        }

        return day.ToString("d", Loc.Culture);
    }

    public static string FutureDayLabel(long unixSeconds)
    {
        if (unixSeconds <= 0)
        {
            return string.Empty;
        }

        var day = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime().Date;
        var today = DateTime.Now.Date;
        if (day == today)
        {
            return Loc.T(L.Time.Today);
        }

        if (day == today.AddDays(1))
        {
            return Loc.T(L.Time.Tomorrow);
        }

        if (day > today && day < today.AddDays(7))
        {
            return Loc.Culture.TextInfo.ToTitleCase(day.ToString("dddd", Loc.Culture));
        }

        return day.ToString("d", Loc.Culture);
    }

    public static string FutureMoment(long unixSeconds)
    {
        if (unixSeconds <= 0)
        {
            return string.Empty;
        }

        return FutureDayLabel(unixSeconds) + " " + Clock(unixSeconds);
    }

    public static string Until(TimeSpan span)
    {
        if (span <= TimeSpan.Zero)
        {
            return Loc.T(L.Time.Now);
        }

        if (span.TotalHours < 1)
        {
            return Loc.T(L.Time.InMinutes, Math.Max(1, (int)span.TotalMinutes));
        }

        if (span.TotalDays < 1)
        {
            var hours = (int)span.TotalHours;
            return span.Minutes > 0
                ? Loc.T(L.Time.InHoursMinutes, hours, span.Minutes)
                : Loc.T(L.Time.InHours, hours);
        }

        return Loc.T(L.Timers.InDays, (int)span.TotalDays);
    }

    public static string Until(long unixSeconds)
    {
        if (unixSeconds <= 0)
        {
            return string.Empty;
        }

        return Until(DateTimeOffset.FromUnixTimeSeconds(unixSeconds) - DateTimeOffset.UtcNow);
    }

    public static bool SameLocalDay(long firstUnix, long secondUnix) =>
        DateTimeOffset.FromUnixTimeSeconds(firstUnix).ToLocalTime().Date ==
        DateTimeOffset.FromUnixTimeSeconds(secondUnix).ToLocalTime().Date;

    public static string MinutesSeconds(int totalSeconds)
    {
        if (totalSeconds < 0)
        {
            totalSeconds = 0;
        }

        return $"{totalSeconds / 60}:{totalSeconds % 60:D2}";
    }

    public static string Duration(int seconds)
    {
        if (seconds < 0)
        {
            seconds = 0;
        }

        var minutes = seconds / 60;
        if (minutes >= 60)
        {
            return $"{minutes / 60}:{minutes % 60:00}:{seconds % 60:00}";
        }

        return $"{minutes}:{seconds % 60:00}";
    }
}
