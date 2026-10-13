using Aetherphone.Apps.Calendar.Widgets;
using Aetherphone.Core.Theme;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HalloweenCountdownTests
{
    [Theory]
    [InlineData(2026, 10, 10, 21)]
    [InlineData(2026, 10, 30, 1)]
    [InlineData(2026, 10, 31, 0)]
    [InlineData(2026, 11, 1, 364)]
    [InlineData(2027, 1, 1, 303)]
    [InlineData(2028, 10, 1, 30)]
    public void CountsWholeDaysToTheNextHalloweenNight(int year, int month, int day, int expected)
    {
        Assert.Equal(expected, HalloweenWidget.DaysUntilHalloween(new DateTime(year, month, day)));
    }

    [Theory]
    [InlineData(2026, 10, 30, 23, 1)]
    [InlineData(2026, 10, 31, 0, 0)]
    [InlineData(2026, 10, 31, 21, 0)]
    public void IgnoresTheTimeOfDay(int year, int month, int day, int hour, int expected)
    {
        Assert.Equal(expected, HalloweenWidget.DaysUntilHalloween(new DateTime(year, month, day, hour, 30, 0)));
    }

    [Theory]
    [InlineData(10, 31, false)]
    [InlineData(11, 1, true)]
    [InlineData(11, 2, true)]
    [InlineData(11, 3, false)]
    public void KnowsWhenTheNightHasPassed(int month, int day, bool expected)
    {
        Assert.Equal(expected, SeasonalTheme.IsAfterHalloweenNight(new DateTime(2026, month, day)));
    }
}
