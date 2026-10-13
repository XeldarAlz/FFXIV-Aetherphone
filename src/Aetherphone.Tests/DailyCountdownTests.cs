using Aetherphone.Apps.Games.Hub;
using Xunit;

namespace Aetherphone.Tests;

public sealed class DailyCountdownTests
{
    private static long At(int hour, int minute, int second) =>
        new DateTime(2026, 10, 7, hour, minute, second, DateTimeKind.Utc).Ticks;

    [Theory]
    [InlineData(0, 0, 0, 900)]
    [InlineData(14, 59, 30, 1)]
    [InlineData(15, 0, 0, 1440)]
    [InlineData(18, 48, 0, 1212)]
    [InlineData(18, 48, 59, 1212)]
    [InlineData(18, 49, 0, 1211)]
    [InlineData(23, 59, 30, 901)]
    public void TheCountdownRunsToTheNextCoinDayInWholeMinutes(int hour, int minute, int second, int expected)
    {
        Assert.Equal(expected, DailyCountdown.MinutesUntilReset(At(hour, minute, second)));
    }

    [Fact]
    public void TheLabelReadsHoursAndMinutes()
    {
        var countdown = new DailyCountdown();

        Assert.Equal("Resets in 20h 12m", countdown.Label(At(18, 48, 0)));
        Assert.Equal("Resets in 16h 0m", countdown.Label(At(23, 0, 0)));
    }

    [Fact]
    public void TheLastHourReadsMinutesOnly()
    {
        var countdown = new DailyCountdown();

        Assert.Equal("Resets in 42m", countdown.Label(At(14, 18, 0)));
    }

    [Fact]
    public void TheLabelIsReusedWithinTheSameMinute()
    {
        var countdown = new DailyCountdown();

        var first = countdown.Label(At(18, 48, 1));
        var second = countdown.Label(At(18, 48, 40));

        Assert.Same(first, second);
    }
}
