using Aetherphone.Core;
using Aetherphone.Core.Activity;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Activity;

internal sealed class ActivityDigest
{
    public const int WeekLength = 7;
    public const int TodaySlot = WeekLength - 1;

    private const int MinuteSeconds = 60;
    private const string GoalFormat = "0.#";

    public readonly DateTime[] Dates = new DateTime[WeekLength];
    public readonly ActivityDay?[] Days = new ActivityDay?[WeekLength];
    public readonly string[] Letters = new string[WeekLength];
    public readonly string[] Titles = new string[WeekLength];
    public readonly float[] Fractions = new float[WeekLength * ActivityGoals.RingCount];
    public readonly string[] RingValues = new string[WeekLength * ActivityGoals.RingCount];
    public readonly string[] RingDetails = new string[WeekLength * ActivityGoals.RingCount];
    public readonly string[] PlayTexts = new string[WeekLength];
    public readonly string[] CollectibleTexts = new string[WeekLength];
    public readonly string[] Units = new string[ActivityGoals.RingCount];
    public readonly bool[] TrendRising = new bool[ActivityStats.MetricCount];
    public readonly string[] TrendValues = new string[ActivityStats.MetricCount];
    public readonly bool[] AwardEarned = new bool[ActivityAwards.Count];
    public readonly string[] AwardSubs = new string[ActivityAwards.Count];

    private long key;
    private bool built;

    public bool TrendsAvailable { get; private set; }
    public int CurrentStreak { get; private set; }
    public string CurrentStreakText { get; private set; } = string.Empty;
    public string BestStreakText { get; private set; } = string.Empty;
    public string SessionPlay { get; private set; } = string.Empty;
    public string SessionExperience { get; private set; } = string.Empty;
    public string SessionDuties { get; private set; } = string.Empty;
    public string SessionGil { get; private set; } = string.Empty;
    public string SessionSince { get; private set; } = string.Empty;
    public string RetainerValue { get; private set; } = string.Empty;
    public bool RetainersReady { get; private set; }

    public void Invalidate() => built = false;

    public void Refresh(ActivityTracker tracker, in ActivityTargets targets)
    {
        var candidate = KeyFor(tracker, targets);
        if (built && candidate == key)
        {
            return;
        }

        key = candidate;
        built = true;
        tracker.FoldToday();
        Build(tracker, targets);
    }

    private static long KeyFor(ActivityTracker tracker, in ActivityTargets targets)
    {
        var today = tracker.Today;
        var session = tracker.Session;
        var records = tracker.Records;
        var dayHash = HashCode.Combine(today.Date, today.ExpGained, today.LevelsGained, today.DutiesCompleted,
            today.GilEarned, today.PlaySeconds / MinuteSeconds, today.MountsGained + today.MinionsGained);
        var sessionHash = HashCode.Combine(session.ExpGained, session.DutiesCompleted, session.GilEarned,
            session.PlaySeconds / MinuteSeconds, tracker.SessionStartedUnix);
        var contextHash = HashCode.Combine(targets, tracker.Days.Count, records.BestStreak, records.PerfectDays,
            tracker.RetainerCount * 1024 + tracker.VenturesReady * 32 + tracker.VenturesActive, Loc.Culture,
            TimeText.FormatVersion, DateTime.Today);
        return ((long)HashCode.Combine(dayHash, sessionHash) << 32) ^ (uint)contextHash;
    }

    private void Build(ActivityTracker tracker, in ActivityTargets targets)
    {
        var days = tracker.Days;
        var todayDate = DateTime.Today;
        Units[0] = Loc.Upper(Loc.T(L.Character.UnitLevels));
        Units[1] = Loc.Upper(Loc.T(L.Character.UnitDuties));
        Units[2] = Loc.Upper(Loc.T(L.Character.UnitGil));
        for (var slot = 0; slot < WeekLength; slot++)
        {
            var date = todayDate.AddDays(slot - TodaySlot);
            Dates[slot] = date;
            var day = slot == TodaySlot ? tracker.Today : ActivityStats.FindDay(days, ActivityStats.DateKey(date));
            Days[slot] = day;
            Letters[slot] = Loc.Culture.DateTimeFormat.GetShortestDayName(date.DayOfWeek);
            Titles[slot] = DayTitle(date, todayDate);
            BuildSlot(slot, day, targets);
        }

        var best = Math.Max(tracker.Records.BestStreak, ActivityStats.BestStreak(days, targets));
        CurrentStreak = ActivityStats.CurrentStreak(days, targets, todayDate);
        CurrentStreakText = Loc.Plural(L.Character.StreakDays, CurrentStreak);
        BestStreakText = Loc.Plural(L.Character.StreakDays, Math.Max(best, CurrentStreak));
        BuildTrends(days, todayDate);
        BuildSession(tracker);
        BuildRetainers(tracker);
        BuildAwards(tracker.Records, Math.Max(best, CurrentStreak));
    }

    private void BuildSlot(int slot, ActivityDay? day, in ActivityTargets targets)
    {
        var offset = slot * ActivityGoals.RingCount;
        if (day is null)
        {
            for (var ring = 0; ring < ActivityGoals.RingCount; ring++)
            {
                Fractions[offset + ring] = 0f;
            }

            RingValues[offset] = Pair(Levels(0f), Levels(targets.Levels));
            RingValues[offset + 1] = Pair(Number(0), Number(targets.Duties));
            RingValues[offset + 2] = Pair(Compact(0), Compact(targets.Gil));
            RingDetails[offset] = string.Empty;
            RingDetails[offset + 1] = string.Empty;
            RingDetails[offset + 2] = string.Empty;
            PlayTexts[slot] = string.Empty;
            CollectibleTexts[slot] = string.Empty;
            return;
        }

        for (var ring = 0; ring < ActivityGoals.RingCount; ring++)
        {
            Fractions[offset + ring] = ActivityGoals.Fraction(targets, day, ring);
        }

        RingValues[offset] = Pair(Levels(day.LevelUnitsGained), Levels(targets.Levels));
        RingValues[offset + 1] = Pair(Number(day.DutiesCompleted), Number(targets.Duties));
        RingValues[offset + 2] = Pair(Compact(day.GilEarned), Compact(targets.Gil));
        var experience = Loc.T(L.Character.ExperienceDetail, "+" + Compact(day.ExpGained));
        RingDetails[offset] = day.LevelsGained > 0
            ? string.Concat(experience, " · ", Loc.Plural(L.Character.LevelUps, day.LevelsGained))
            : experience;
        RingDetails[offset + 1] = Loc.T(L.Character.PercentOfGoal, PercentValue(Fractions[offset + 1]));
        RingDetails[offset + 2] = Loc.T(L.Character.GilDetail, "+" + Number(day.GilEarned));
        PlayTexts[slot] = day.PlaySeconds > 0 ? Duration(day.PlaySeconds) : string.Empty;
        CollectibleTexts[slot] = day.MountsGained + day.MinionsGained > 0
            ? string.Concat(Loc.T(L.Character.Mounts), " ", Number(day.MountsGained), " · ",
                Loc.T(L.Character.Minions), " ", Number(day.MinionsGained))
            : string.Empty;
    }

    private void BuildTrends(IReadOnlyList<ActivityDay> days, DateTime todayDate)
    {
        TrendsAvailable = ActivityStats.HasTrendHistory(days, todayDate);
        for (var metric = 0; metric < ActivityStats.MetricCount; metric++)
        {
            var trend = ActivityStats.Trend(days, (ActivityMetric)metric, todayDate);
            TrendRising[metric] = trend.Rising;
            TrendValues[metric] = (ActivityMetric)metric switch
            {
                ActivityMetric.Levels => Loc.T(L.Character.LevelsShort, Levels((float)trend.Recent)),
                ActivityMetric.Duties => Average(trend.Recent),
                ActivityMetric.Gil => Compact((long)Math.Round(trend.Recent)),
                _ => Duration((long)Math.Round(trend.Recent)),
            };
        }
    }

    private void BuildSession(ActivityTracker tracker)
    {
        var session = tracker.Session;
        SessionPlay = Duration(session.PlaySeconds);
        SessionExperience = "+" + Compact(session.ExpGained);
        SessionDuties = Number(session.DutiesCompleted);
        SessionGil = "+" + Compact(session.GilEarned);
        var started = DateTimeOffset.FromUnixTimeSeconds(tracker.SessionStartedUnix).ToLocalTime();
        SessionSince = Loc.T(L.Character.SinceTime, TimeText.Clock(started.DateTime));
    }

    private void BuildRetainers(ActivityTracker tracker)
    {
        RetainersReady = tracker.VenturesReady > 0;
        if (RetainersReady)
        {
            RetainerValue = Loc.T(L.Character.VenturesReady, tracker.VenturesReady);
        }
        else if (tracker.VenturesActive > 0)
        {
            RetainerValue = Loc.T(L.Character.VenturesActive, tracker.VenturesActive);
        }
        else
        {
            RetainerValue = Number(tracker.RetainerCount);
        }
    }

    private void BuildAwards(ActivityRecords records, int bestStreak)
    {
        var notYet = Loc.T(L.Character.AwardNotYet);
        for (var index = 0; index < ActivityAwards.Count; index++)
        {
            var award = (ActivityAward)index;
            var earned = ActivityAwards.Earned(records, award, bestStreak);
            AwardEarned[index] = earned;
            AwardSubs[index] = award switch
            {
                ActivityAward.PerfectDay => earned ? Loc.Plural(L.Character.Times, records.PerfectDays) : notYet,
                ActivityAward.PerfectWeek => StreakProgress(bestStreak, ActivityAwards.WeekStreakDays),
                ActivityAward.PerfectMonth => StreakProgress(bestStreak, ActivityAwards.MonthStreakDays),
                ActivityAward.ExperienceRecord => earned ? "+" + Compact(records.BestExp) : notYet,
                ActivityAward.DutyRecord => earned ? Number(records.BestDuties) : notYet,
                ActivityAward.FortuneRecord => earned ? Compact(records.BestGil) : notYet,
                ActivityAward.LongestDay => earned ? Duration(records.BestPlaySeconds) : notYet,
                _ => earned ? Loc.Plural(L.Character.LevelUps, records.BestLevels) : notYet,
            };
        }
    }

    private static string StreakProgress(int best, int target) =>
        best >= target
            ? Loc.Plural(L.Character.StreakDays, best)
            : Pair(Number(best), Number(target));

    private static string DayTitle(DateTime date, DateTime todayDate)
    {
        if (date == todayDate)
        {
            return Loc.T(L.Time.Today);
        }

        if (date == todayDate.AddDays(-1))
        {
            return Loc.T(L.Time.Yesterday);
        }

        return Loc.Culture.TextInfo.ToTitleCase(date.ToString("dddd", Loc.Culture));
    }

    private static string Pair(string value, string goal) => string.Concat(value, "/", goal);

    public static string Levels(float value) => value.ToString(GoalFormat, Loc.Culture);

    private static string Average(double value) => value.ToString(GoalFormat, Loc.Culture);

    public static string Number(long value) => NumberText.Group(value);

    public static string Compact(long value) => NumberText.Compact(value);

    public static int PercentValue(float fraction) => (int)MathF.Round(Math.Clamp(fraction, 0f, 9.99f) * 100f);

    public static string Duration(long seconds)
    {
        var minutes = (int)(seconds / MinuteSeconds);
        var hours = minutes / MinuteSeconds;
        return hours > 0
            ? Loc.T(L.Character.DurationHoursMinutes, hours, minutes % MinuteSeconds)
            : Loc.T(L.Character.DurationMinutes, minutes);
    }
}
