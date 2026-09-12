using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class TimeTextCacheTests
{
    private static string ExpectedClock(DateTime moment) =>
        moment.ToString(TimeText.ClockPattern, Loc.Culture);

    private static string ExpectedHourLabel(int hourOfDay)
    {
        if (TimeText.Use24Hour)
        {
            return hourOfDay.ToString("D2", Loc.Culture);
        }

        var hour = hourOfDay % 12;
        return (hour == 0 ? 12 : hour).ToString(Loc.Culture);
    }

    private static string ExpectedMinutesSeconds(int totalSeconds)
    {
        if (totalSeconds < 0)
        {
            totalSeconds = 0;
        }

        return $"{totalSeconds / 60}:{totalSeconds % 60:D2}";
    }

    private static string ExpectedDuration(int seconds)
    {
        if (seconds < 0)
        {
            seconds = 0;
        }

        var minutes = seconds / 60;
        return minutes >= 60
            ? $"{minutes / 60}:{minutes % 60:00}:{seconds % 60:00}"
            : $"{minutes}:{seconds % 60:00}";
    }

    [Fact]
    public void ClockMatchesTheUncachedFormatAcrossEveryMinuteOfADay()
    {
        var start = new DateTime(2026, 3, 14, 0, 0, 0, DateTimeKind.Local);
        for (var minute = 0; minute < 24 * 60; minute++)
        {
            var moment = start.AddMinutes(minute);
            Assert.Equal(ExpectedClock(moment), TimeText.Clock(moment));
        }
    }

    [Fact]
    public void ClockIgnoresSecondsWithinTheSameMinute()
    {
        var baseline = new DateTime(2026, 3, 14, 9, 41, 0, DateTimeKind.Local);
        var expected = TimeText.Clock(baseline);
        for (var second = 1; second < 60; second++)
        {
            Assert.Equal(expected, TimeText.Clock(baseline.AddSeconds(second)));
        }
    }

    [Fact]
    public void ClockSeparatesAdjacentMinutes()
    {
        var baseline = new DateTime(2026, 3, 14, 9, 41, 30, DateTimeKind.Local);
        Assert.NotEqual(TimeText.Clock(baseline), TimeText.Clock(baseline.AddMinutes(1)));
    }

    [Fact]
    public void ClockFromAnOffsetMatchesTheUncachedFormat()
    {
        var start = new DateTimeOffset(2026, 3, 14, 0, 0, 0, TimeSpan.FromHours(2));
        for (var minute = 0; minute < 24 * 60; minute += 7)
        {
            var moment = start.AddMinutes(minute);
            Assert.Equal(moment.ToString(TimeText.ClockPattern, Loc.Culture), TimeText.Clock(moment));
        }
    }

    [Fact]
    public void ClockStaysCorrectPastTheCacheLimit()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local);
        for (var minute = 0; minute < 5000; minute++)
        {
            var moment = start.AddMinutes(minute);
            Assert.Equal(ExpectedClock(moment), TimeText.Clock(moment));
        }
    }

    [Fact]
    public void HourAndMinuteLabelsMatchTheUncachedFormat()
    {
        for (var hour = 0; hour < 24; hour++)
        {
            Assert.Equal(ExpectedHourLabel(hour), TimeText.HourLabel(hour));
        }

        for (var minute = 0; minute < 60; minute++)
        {
            Assert.Equal(minute.ToString("D2", Loc.Culture), TimeText.MinuteLabel(minute));
        }
    }

    [Fact]
    public void HourAndMinuteLabelsStillHandleValuesOutsideTheTable()
    {
        Assert.Equal(ExpectedHourLabel(25), TimeText.HourLabel(25));
        Assert.Equal((-1).ToString("D2", Loc.Culture), TimeText.MinuteLabel(-1));
        Assert.Equal(61.ToString("D2", Loc.Culture), TimeText.MinuteLabel(61));
    }

    [Fact]
    public void MinutesSecondsMatchesTheUncachedFormatPastTheCacheLimit()
    {
        Assert.Equal(ExpectedMinutesSeconds(0), TimeText.MinutesSeconds(-5));
        for (var seconds = 0; seconds < 4000; seconds++)
        {
            Assert.Equal(ExpectedMinutesSeconds(seconds), TimeText.MinutesSeconds(seconds));
        }
    }

    [Fact]
    public void DurationMatchesTheUncachedFormatAcrossBothBranches()
    {
        Assert.Equal(ExpectedDuration(0), TimeText.Duration(-1));
        for (var seconds = 0; seconds < 4000; seconds++)
        {
            Assert.Equal(ExpectedDuration(seconds), TimeText.Duration(seconds));
        }

        foreach (var seconds in new[] { 3599, 3600, 3601, 7199, 7200, 86399, 90061 })
        {
            Assert.Equal(ExpectedDuration(seconds), TimeText.Duration(seconds));
        }
    }

    [Fact]
    public void ClockProbesFollowTheClockPatternAndStayCached()
    {
        var (morning, afternoon) = TimeText.ClockProbes;
        Assert.Equal(ExpectedClock(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)), morning);
        Assert.Equal(ExpectedClock(new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Unspecified)), afternoon);
        var again = TimeText.ClockProbes;
        Assert.Same(morning, again.Morning);
        Assert.Same(afternoon, again.Afternoon);
    }

    [Fact]
    public void RepeatedCallsReturnAStableValue()
    {
        var moment = new DateTime(2026, 7, 4, 18, 5, 12, DateTimeKind.Local);
        var first = TimeText.Clock(moment);
        for (var attempt = 0; attempt < 50; attempt++)
        {
            Assert.Equal(first, TimeText.Clock(moment));
            Assert.Equal(ExpectedDuration(125), TimeText.Duration(125));
            Assert.Equal(ExpectedMinutesSeconds(125), TimeText.MinutesSeconds(125));
        }
    }
}
