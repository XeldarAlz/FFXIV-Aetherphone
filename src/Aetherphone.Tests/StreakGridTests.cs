using Aetherphone.Apps.Games.Hub;
using Aetherphone.Core.Games;
using Xunit;

namespace Aetherphone.Tests;

public sealed class StreakGridTests
{
    private const string DailyId = "sudoku";
    private static readonly int Today = new DateOnly(2026, 10, 7).DayNumber;

    [Fact]
    public void DayNumbersMapToTheirWeekday()
    {
        Assert.Equal((int)DayOfWeek.Monday, StreakGrid.Weekday(0));
        Assert.Equal((int)DayOfWeek.Wednesday, StreakGrid.Weekday(Today));
        Assert.Equal((int)DayOfWeek.Sunday, StreakGrid.Weekday(new DateOnly(2026, 10, 11).DayNumber));
    }

    [Theory]
    [InlineData(DayOfWeek.Monday, 2026, 10, 5, 0)]
    [InlineData(DayOfWeek.Monday, 2026, 10, 11, 6)]
    [InlineData(DayOfWeek.Sunday, 2026, 10, 11, 0)]
    [InlineData(DayOfWeek.Sunday, 2026, 10, 10, 6)]
    [InlineData(DayOfWeek.Saturday, 2026, 10, 10, 0)]
    public void ColumnsStartAtTheCulturesFirstDay(DayOfWeek firstDay, int year, int month, int day, int expected)
    {
        Assert.Equal(expected, StreakGrid.Column(new DateOnly(year, month, day).DayNumber, firstDay));
    }

    [Theory]
    [InlineData(DayOfWeek.Monday)]
    [InlineData(DayOfWeek.Sunday)]
    [InlineData(DayOfWeek.Saturday)]
    public void TodaySitsInTheLastWeekAndTheGridStartsOnAWeekBoundary(DayOfWeek firstDay)
    {
        for (var today = Today; today < Today + StreakGrid.Days; today++)
        {
            var start = StreakGrid.Start(today, firstDay);
            var cell = StreakGrid.TodayCell(today, firstDay);

            Assert.Equal(today, start + cell);
            Assert.Equal(StreakGrid.Weeks - 1, cell / StreakGrid.Days);
            Assert.Equal(0, StreakGrid.Column(start, firstDay));
        }
    }

    [Fact]
    public void TheMaskMarksEveryDoneDayInsideTheGrid()
    {
        var configuration = new FakeStatsConfiguration();
        var stats = new GameStatsStore(configuration) { DailyGameId = DailyId };
        stats.CompleteDaily(DailyId, Today - 9);
        stats.CompleteDaily(DailyId, Today - 2);
        stats.CompleteDaily(DailyId, Today - 1);
        stats.CompleteDaily(DailyId, Today);
        var start = StreakGrid.Start(Today, DayOfWeek.Monday);

        var mask = StreakGrid.Mask(stats, start, Today);

        for (var cell = 0; cell < StreakGrid.Cells; cell++)
        {
            var day = start + cell;
            var expected = day == Today - 9 || day == Today - 2 || day == Today - 1 || day == Today;
            Assert.Equal(expected, StreakGrid.Done(mask, cell));
        }
    }

    [Fact]
    public void TheMaskIgnoresDaysAfterToday()
    {
        var configuration = new FakeStatsConfiguration();
        var stats = new GameStatsStore(configuration) { DailyGameId = DailyId };
        stats.CompleteDaily(DailyId, Today + 1);

        var mask = StreakGrid.Mask(stats, StreakGrid.Start(Today, DayOfWeek.Monday), Today);

        Assert.Equal(0UL, mask);
    }

    [Fact]
    public void RunsJoinConsecutiveDaysWithinAWeekRowOnly()
    {
        var mask = (1UL << 4) | (1UL << 5) | (1UL << 6) | (1UL << 7) | (1UL << 8) | (1UL << 12);

        Assert.True(StreakGrid.StartsRun(mask, 4));
        Assert.Equal(6, StreakGrid.RunEnd(mask, 4));
        Assert.False(StreakGrid.StartsRun(mask, 5));
        Assert.False(StreakGrid.StartsRun(mask, 6));
        Assert.True(StreakGrid.StartsRun(mask, 7));
        Assert.Equal(8, StreakGrid.RunEnd(mask, 7));
        Assert.True(StreakGrid.StartsRun(mask, 12));
        Assert.Equal(12, StreakGrid.RunEnd(mask, 12));
        Assert.False(StreakGrid.StartsRun(mask, 3));
    }

    [Fact]
    public void CellsOutsideTheGridAreNeverDone()
    {
        Assert.False(StreakGrid.Done(ulong.MaxValue, -1));
        Assert.False(StreakGrid.Done(ulong.MaxValue, StreakGrid.Cells));
        Assert.Equal(StreakGrid.Cells - 1, StreakGrid.RunEnd(ulong.MaxValue, StreakGrid.Cells - StreakGrid.Days));
    }
}
