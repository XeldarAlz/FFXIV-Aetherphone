using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class TimeTextAgoPreciseTests
{
    [Fact]
    public void ShowsSecondsUnderAMinute()
    {
        var moment = DateTime.UtcNow - TimeSpan.FromSeconds(42);

        var result = TimeText.AgoPrecise(moment);

        Assert.Equal("42s ago", result);
    }

    [Fact]
    public void IncludesSecondsPastTheFirstMinute()
    {
        var moment = DateTime.UtcNow - TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(23);

        var result = TimeText.AgoPrecise(moment);

        Assert.Equal("5m 23s ago", result);
    }

    [Fact]
    public void DropsSecondsWhenTheyAreExactlyZeroPastAMinute()
    {
        var moment = DateTime.UtcNow - TimeSpan.FromMinutes(5);

        var result = TimeText.AgoPrecise(moment);

        Assert.Equal("5m ago", result);
    }

    [Fact]
    public void IncludesMinutesPastTheFirstHour()
    {
        var moment = DateTime.UtcNow - TimeSpan.FromHours(2) - TimeSpan.FromMinutes(15);

        var result = TimeText.AgoPrecise(moment);

        Assert.Equal("2h 15m ago", result);
    }

    [Fact]
    public void DropsTheMinutesWhenTheyAreExactlyZero()
    {
        var moment = DateTime.UtcNow - TimeSpan.FromHours(3);

        var result = TimeText.AgoPrecise(moment);

        Assert.Equal("3h ago", result);
    }

    [Fact]
    public void FallsBackToDaysPastTwentyFourHours()
    {
        var moment = DateTime.UtcNow - TimeSpan.FromHours(30);

        var result = TimeText.AgoPrecise(moment);

        Assert.Equal("1d ago", result);
    }
}
