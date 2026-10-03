using System.Globalization;

namespace Aetherphone.Core.Activity;

internal enum ActivityMetric : byte
{
    Levels,
    Duties,
    Gil,
    Play,
}

internal readonly record struct ActivityTrend(bool Available, double Recent, double Previous)
{
    public bool Rising => Recent >= Previous;
}

internal static class ActivityStats
{
    public const string DateFormat = "yyyy-MM-dd";
    public const int TrendWindowDays = 7;
    public const int MetricCount = 4;

    public static string DateKey(DateTime date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);

    public static bool TryParseDate(string dateKey, out DateTime date) =>
        DateTime.TryParseExact(dateKey, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    public static ActivityDay? FindDay(IReadOnlyList<ActivityDay> days, string dateKey)
    {
        for (var index = days.Count - 1; index >= 0; index--)
        {
            if (days[index].Date == dateKey)
            {
                return days[index];
            }
        }

        return null;
    }

    public static bool IsClosed(ActivityDay? day, in ActivityTargets targets) =>
        day is not null && ActivityGoals.AllClosed(targets, day);

    public static int CurrentStreak(IReadOnlyList<ActivityDay> days, in ActivityTargets targets, DateTime today)
    {
        var date = today.Date;
        if (!IsClosed(FindDay(days, DateKey(date)), targets))
        {
            date = date.AddDays(-1);
        }

        var streak = 0;
        while (IsClosed(FindDay(days, DateKey(date)), targets))
        {
            streak++;
            date = date.AddDays(-1);
        }

        return streak;
    }

    public static int BestStreak(IReadOnlyList<ActivityDay> days, in ActivityTargets targets)
    {
        var best = 0;
        var run = 0;
        var previous = DateTime.MinValue;
        for (var index = 0; index < days.Count; index++)
        {
            var day = days[index];
            if (!TryParseDate(day.Date, out var date))
            {
                continue;
            }

            if (!ActivityGoals.AllClosed(targets, day))
            {
                run = 0;
                previous = date;
                continue;
            }

            run = run > 0 && (date - previous).Days == 1 ? run + 1 : 1;
            best = Math.Max(best, run);
            previous = date;
        }

        return best;
    }

    public static double Value(ActivityDay day, ActivityMetric metric) => metric switch
    {
        ActivityMetric.Levels => day.LevelUnitsGained,
        ActivityMetric.Duties => day.DutiesCompleted,
        ActivityMetric.Gil => day.GilEarned,
        _ => day.PlaySeconds,
    };

    public static bool HasTrendHistory(IReadOnlyList<ActivityDay> days, DateTime today)
    {
        var horizon = today.Date.AddDays(-TrendWindowDays * 2);
        for (var index = 0; index < days.Count; index++)
        {
            if (TryParseDate(days[index].Date, out var date))
            {
                return date <= horizon;
            }
        }

        return false;
    }

    public static ActivityTrend Trend(IReadOnlyList<ActivityDay> days, ActivityMetric metric, DateTime today)
    {
        if (!HasTrendHistory(days, today))
        {
            return default;
        }

        var todayDate = today.Date;
        var recent = 0d;
        var previous = 0d;
        for (var index = 0; index < days.Count; index++)
        {
            var day = days[index];
            if (!TryParseDate(day.Date, out var date))
            {
                continue;
            }

            var age = (todayDate - date).Days;
            if (age >= 1 && age <= TrendWindowDays)
            {
                recent += Value(day, metric);
            }
            else if (age > TrendWindowDays && age <= TrendWindowDays * 2)
            {
                previous += Value(day, metric);
            }
        }

        return new ActivityTrend(true, recent / TrendWindowDays, previous / TrendWindowDays);
    }

    public static bool Fold(ActivityRecords records, ActivityDay day)
    {
        var changed = false;
        if (day.ExpGained > records.BestExp)
        {
            records.BestExp = day.ExpGained;
            records.BestExpDate = day.Date;
            changed = true;
        }

        if (day.DutiesCompleted > records.BestDuties)
        {
            records.BestDuties = day.DutiesCompleted;
            records.BestDutiesDate = day.Date;
            changed = true;
        }

        if (day.GilEarned > records.BestGil)
        {
            records.BestGil = day.GilEarned;
            records.BestGilDate = day.Date;
            changed = true;
        }

        if (day.PlaySeconds > records.BestPlaySeconds)
        {
            records.BestPlaySeconds = day.PlaySeconds;
            records.BestPlayDate = day.Date;
            changed = true;
        }

        if (day.LevelsGained > records.BestLevels)
        {
            records.BestLevels = day.LevelsGained;
            records.BestLevelsDate = day.Date;
            changed = true;
        }

        return changed;
    }

    public static bool FoldAll(ActivityRecords records, IReadOnlyList<ActivityDay> days)
    {
        var changed = false;
        for (var index = 0; index < days.Count; index++)
        {
            changed |= Fold(records, days[index]);
        }

        return changed;
    }

    public static int CountFlagged(IReadOnlyList<ActivityDay> days, int flag)
    {
        var count = 0;
        for (var index = 0; index < days.Count; index++)
        {
            if ((days[index].RingsNotified & flag) != 0)
            {
                count++;
            }
        }

        return count;
    }
}
