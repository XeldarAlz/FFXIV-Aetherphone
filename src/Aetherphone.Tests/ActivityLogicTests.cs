using Aetherphone.Core.Activity;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ActivityLogicTests
{
    private static readonly DateTime Today = new(2026, 10, 3);
    private static readonly ActivityTargets Targets = new(1f, 3, 50000);

    private static ActivityDay Day(int daysAgo, float levels = 0f, int duties = 0, long gil = 0, long play = 0,
        long exp = 0, int levelsGained = 0) =>
        new()
        {
            Date = ActivityStats.DateKey(Today.AddDays(-daysAgo)),
            LevelUnitsGained = levels,
            DutiesCompleted = duties,
            GilEarned = gil,
            PlaySeconds = play,
            ExpGained = exp,
            LevelsGained = levelsGained,
        };

    private static ActivityDay Closed(int daysAgo) => Day(daysAgo, 1f, 3, 50000);

    [Fact]
    public void CurrentStreakCountsBackFromYesterdayWhenTodayIsOpen()
    {
        var days = new List<ActivityDay> { Closed(3), Closed(2), Closed(1), Day(0, 0.5f) };
        Assert.Equal(3, ActivityStats.CurrentStreak(days, Targets, Today));
    }

    [Fact]
    public void CurrentStreakIncludesTodayWhenClosed()
    {
        var days = new List<ActivityDay> { Closed(1), Closed(0) };
        Assert.Equal(2, ActivityStats.CurrentStreak(days, Targets, Today));
    }

    [Fact]
    public void BestStreakBreaksOnGapsAndOpenDays()
    {
        var days = new List<ActivityDay> { Closed(9), Closed(8), Closed(7), Closed(5), Day(4), Closed(3), Closed(2) };
        Assert.Equal(3, ActivityStats.BestStreak(days, Targets));
    }

    [Fact]
    public void TrendNeedsTwoWeeksOfHistory()
    {
        var days = new List<ActivityDay> { Day(10, duties: 4), Day(1, duties: 2) };
        Assert.False(ActivityStats.Trend(days, ActivityMetric.Duties, Today).Available);
    }

    [Fact]
    public void TrendComparesTheLastWeekWithTheWeekBefore()
    {
        var days = new List<ActivityDay>
        {
            Day(14, duties: 7),
            Day(9, duties: 7),
            Day(3, duties: 14),
            Day(0, duties: 50),
        };
        var trend = ActivityStats.Trend(days, ActivityMetric.Duties, Today);
        Assert.True(trend.Available);
        Assert.Equal(2d, trend.Recent, 6);
        Assert.Equal(2d, trend.Previous, 6);
        Assert.True(trend.Rising);
    }

    [Fact]
    public void TrendFallsWhenTheRecentWeekIsLower()
    {
        var days = new List<ActivityDay> { Day(14, gil: 70000), Day(2, gil: 7000) };
        var trend = ActivityStats.Trend(days, ActivityMetric.Gil, Today);
        Assert.False(trend.Rising);
        Assert.Equal(1000d, trend.Recent, 6);
    }

    [Fact]
    public void FoldKeepsTheBestDayAndIsIdempotent()
    {
        var records = new ActivityRecords();
        var days = new List<ActivityDay> { Day(2, exp: 500, duties: 2), Day(1, exp: 900, duties: 1, levelsGained: 2) };
        Assert.True(ActivityStats.FoldAll(records, days));
        Assert.False(ActivityStats.FoldAll(records, days));
        Assert.Equal(900, records.BestExp);
        Assert.Equal(days[1].Date, records.BestExpDate);
        Assert.Equal(2, records.BestDuties);
        Assert.Equal(days[0].Date, records.BestDutiesDate);
        Assert.Equal(2, records.BestLevels);
    }

    [Fact]
    public void AwardsFollowTheRecords()
    {
        var records = new ActivityRecords { PerfectDays = 1, BestStreak = 7, BestGil = 10 };
        Assert.True(ActivityAwards.Earned(records, ActivityAward.PerfectDay));
        Assert.True(ActivityAwards.Earned(records, ActivityAward.PerfectWeek));
        Assert.False(ActivityAwards.Earned(records, ActivityAward.PerfectMonth));
        Assert.True(ActivityAwards.Earned(records, ActivityAward.FortuneRecord));
        Assert.False(ActivityAwards.Earned(records, ActivityAward.ExperienceRecord));
        Assert.Equal(3, ActivityAwards.EarnedCount(records));
    }

    [Fact]
    public void StreakAwardsFollowTheDisplayedBestStreak()
    {
        var records = new ActivityRecords { BestStreak = 3 };
        Assert.False(ActivityAwards.Earned(records, ActivityAward.PerfectWeek, 6));
        Assert.True(ActivityAwards.Earned(records, ActivityAward.PerfectWeek, 7));
        Assert.False(ActivityAwards.Earned(records, ActivityAward.PerfectMonth, 7));
        Assert.True(ActivityAwards.Earned(new ActivityRecords { BestStreak = 30 }, ActivityAward.PerfectMonth, 0));
    }

    [Fact]
    public void CountFlaggedCountsOnlyMarkedDays()
    {
        var days = new List<ActivityDay> { Day(2), Day(1), Day(0) };
        days[0].RingsNotified = 8;
        days[2].RingsNotified = 15;
        Assert.Equal(2, ActivityStats.CountFlagged(days, 8));
    }

    [Theory]
    [InlineData(0, 1, 1.5f, 3, 50000L)]
    [InlineData(0, -5, 0.5f, 3, 50000L)]
    [InlineData(1, 1, 1f, 4, 50000L)]
    [InlineData(1, 20, 1f, 10, 50000L)]
    [InlineData(2, 1, 1f, 3, 100000L)]
    [InlineData(2, -1, 1f, 3, 25000L)]
    public void GoalStepsMoveOneNotchAndClamp(int ring, int delta, float levels, int duties, long gil)
    {
        var index = ActivityGoalSteps.IndexOf(ring, Targets);
        var next = ActivityGoalSteps.With(ring, index + delta, Targets);
        Assert.Equal(new ActivityTargets(levels, duties, gil), next);
    }

    [Fact]
    public void GilGoalSnapsToTheNearestStep()
    {
        Assert.Equal(2, ActivityGoalSteps.IndexOf(2, Targets with { Gil = 60000 }));
    }

    [Fact]
    public void FractionsOverflowPastTheGoal()
    {
        var day = Day(0, 2f, 6, 25000);
        Assert.Equal(2f, ActivityGoals.Fraction(Targets, day, 0), 4);
        Assert.Equal(2f, ActivityGoals.Fraction(Targets, day, 1), 4);
        Assert.Equal(0.5f, ActivityGoals.Fraction(Targets, day, 2), 4);
        Assert.False(ActivityGoals.AllClosed(Targets, day));
    }
}
