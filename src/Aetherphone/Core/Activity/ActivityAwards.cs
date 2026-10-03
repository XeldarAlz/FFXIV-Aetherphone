namespace Aetherphone.Core.Activity;

internal enum ActivityAward : byte
{
    PerfectDay,
    PerfectWeek,
    PerfectMonth,
    ExperienceRecord,
    DutyRecord,
    FortuneRecord,
    LongestDay,
    LevelRecord,
}

internal static class ActivityAwards
{
    public const int Count = 8;
    public const int WeekStreakDays = 7;
    public const int MonthStreakDays = 30;

    public static bool Earned(ActivityRecords records, ActivityAward award) =>
        Earned(records, award, records.BestStreak);

    public static bool Earned(ActivityRecords records, ActivityAward award, int bestStreak) => award switch
    {
        ActivityAward.PerfectDay => records.PerfectDays > 0,
        ActivityAward.PerfectWeek => Math.Max(records.BestStreak, bestStreak) >= WeekStreakDays,
        ActivityAward.PerfectMonth => Math.Max(records.BestStreak, bestStreak) >= MonthStreakDays,
        ActivityAward.ExperienceRecord => records.BestExp > 0,
        ActivityAward.DutyRecord => records.BestDuties > 0,
        ActivityAward.FortuneRecord => records.BestGil > 0,
        ActivityAward.LongestDay => records.BestPlaySeconds > 0,
        _ => records.BestLevels > 0,
    };

    public static int EarnedCount(ActivityRecords records)
    {
        var count = 0;
        for (var index = 0; index < Count; index++)
        {
            if (Earned(records, (ActivityAward)index))
            {
                count++;
            }
        }

        return count;
    }
}
