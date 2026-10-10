using Aetherphone.Apps.Calendar.Widgets;
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
}
