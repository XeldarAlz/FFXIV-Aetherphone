namespace Aetherphone.Core.Localization;

internal static class TimeText
{
    private static bool use24Hour = true;

    public static bool Use24Hour
    {
        get => use24Hour;
        set => use24Hour = value;
    }

    public static string ClockPattern => use24Hour ? "HH:mm" : "h:mm tt";

    public static string Clock(DateTime moment) => moment.ToString(ClockPattern, Loc.Culture);

    public static string Clock(DateTimeOffset moment) => moment.ToString(ClockPattern, Loc.Culture);

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
