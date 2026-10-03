using Aetherphone.Core.Fishing;
using Xunit;

namespace Aetherphone.Tests;

public sealed class FishWindowMathTests
{
    private const long Origin = 1_790_000_000;
    private const long Horizon = 6 * 86400;
    private static readonly byte[] None = Array.Empty<byte>();

    private static readonly WeatherOdds[] Odds =
    {
        new(1, 20),
        new(2, 50),
        new(3, 80),
        new(4, 100),
    };

    [Theory]
    [InlineData(1080, 360)]
    [InlineData(600, 1080)]
    [InlineData(360, 600)]
    [InlineData(1410, 1440)]
    public void TimeOnlyWindowsMatchAMinuteByMinuteScan(int start, int end)
    {
        var rule = new FishWindowRule((ushort)start, (ushort)end, None, None);

        AssertMatchesScan(rule, Origin);
        AssertMatchesScan(rule, Origin + 5000);
    }

    [Fact]
    public void WeatherOnlyWindowCoversWholeWeatherPeriods()
    {
        var rule = new FishWindowRule(0, FishWindowRule.MinutesPerDay, new byte[] { 2 }, None);

        var window = FishWindowMath.Next(rule, Odds, Origin);

        Assert.True(window.Exists);
        Assert.Equal(0, (window.EndUnix - window.StartUnix) % EorzeaClock.PeriodSeconds);
        AssertMatchesScan(rule, Origin);
    }

    [Fact]
    public void PreviousWeatherIsCheckedAgainstTheEarlierPeriod()
    {
        var rule = new FishWindowRule(0, FishWindowRule.MinutesPerDay, new byte[] { 3, 4 }, new byte[] { 1 });

        AssertMatchesScan(rule, Origin);
        AssertMatchesScan(rule, Origin + 40000);
    }

    [Fact]
    public void TimeAndWeatherWindowsCrossingAPeriodBoundaryMerge()
    {
        var rule = new FishWindowRule(360, 600, new byte[] { 2, 3 }, None);

        AssertMatchesScan(rule, Origin);
        AssertMatchesScan(rule, Origin + 90000);
    }

    [Fact]
    public void FractionalHoursAreHonoured()
    {
        var rule = new FishWindowRule(960, 1125, new byte[] { 1, 2 }, None);

        AssertMatchesScan(rule, Origin);
    }

    [Fact]
    public void AnOpenWindowReportsItsTrueStart()
    {
        var rule = new FishWindowRule(1080, 360, None, None);
        var first = FishWindowMath.Next(rule, Odds, Origin);
        var inside = first.StartUnix + 600;

        var again = FishWindowMath.Next(rule, Odds, inside);

        Assert.True(again.IsOpen(inside));
        Assert.Equal(first, again);
    }

    [Fact]
    public void WeatherRulesWithoutOddsNeverOpen()
    {
        var rule = new FishWindowRule(0, FishWindowRule.MinutesPerDay, new byte[] { 2 }, None);

        Assert.False(FishWindowMath.Next(rule, ReadOnlySpan<WeatherOdds>.Empty, Origin).Exists);
    }

    [Fact]
    public void UpcomingListsConsecutiveWindowsInOrder()
    {
        var rule = new FishWindowRule(1080, 360, new byte[] { 1, 2 }, None);
        Span<FishWindow> windows = stackalloc FishWindow[4];

        var count = FishWindowMath.Upcoming(rule, Odds, Origin, windows);

        Assert.Equal(4, count);
        for (var index = 1; index < count; index++)
        {
            Assert.True(windows[index].StartUnix >= windows[index - 1].EndUnix);
        }
    }

    [Fact]
    public void AllDayRuleWithoutWeatherIsAlwaysOpen()
    {
        Assert.True(new FishWindowRule(0, FishWindowRule.MinutesPerDay, None, None).AlwaysOpen);
        Assert.False(new FishWindowRule(0, FishWindowRule.MinutesPerDay, new byte[] { 1 }, None).AlwaysOpen);
        Assert.False(new FishWindowRule(60, 120, None, None).AlwaysOpen);
    }

    private static void AssertMatchesScan(in FishWindowRule rule, long from)
    {
        var expected = Scan(rule, from);
        var actual = FishWindowMath.Next(rule, Odds, from);

        Assert.True(expected.Exists, "the scan found no window inside the test horizon");
        Assert.Equal(expected.StartUnix, actual.StartUnix);
        Assert.Equal(expected.EndUnix, actual.EndUnix);
    }

    private static FishWindow Scan(in FishWindowRule rule, long from)
    {
        var cursor = from;
        while (cursor < from + Horizon && !IsOpen(rule, cursor))
        {
            cursor++;
        }

        if (cursor >= from + Horizon)
        {
            return FishWindow.None;
        }

        var start = cursor;
        while (IsOpen(rule, start - 1))
        {
            start--;
        }

        var end = cursor;
        while (IsOpen(rule, end))
        {
            end++;
        }

        return new FishWindow(start, end);
    }

    private static bool IsOpen(in FishWindowRule rule, long unixSeconds)
    {
        if (!FishWindowMath.PeriodMatches(rule, Odds, EorzeaClock.PeriodStart(unixSeconds)))
        {
            return false;
        }

        if (rule.AllDay)
        {
            return true;
        }

        var minute = EorzeaClock.MinuteOfDay(unixSeconds);
        return rule.StartMinute < rule.EndMinute
            ? minute >= rule.StartMinute && minute < rule.EndMinute
            : minute >= rule.StartMinute || minute < rule.EndMinute;
    }
}
