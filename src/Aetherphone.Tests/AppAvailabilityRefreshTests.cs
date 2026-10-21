using Aetherphone.Core.Aethernet;
using Xunit;

namespace Aetherphone.Tests;

public sealed class AppAvailabilityRefreshTests
{
    private const long Now = 1_000_000;

    [Fact]
    public void APushPullsAFarRefreshForwardToTheDebounce()
    {
        var scheduled = AppAvailability.ScheduleAfterPush(Now, Now + 5 * 60 * 1000);

        Assert.Equal(Now + 1500, scheduled);
    }

    [Fact]
    public void APushNeverPushesASoonerRefreshBack()
    {
        Assert.Equal(Now + 200, AppAvailability.ScheduleAfterPush(Now, Now + 200));
        Assert.Equal(0, AppAvailability.ScheduleAfterPush(Now, 0));
    }

    [Fact]
    public void RepeatedPushesCollapseIntoOneRefresh()
    {
        var first = AppAvailability.ScheduleAfterPush(Now, Now + 5 * 60 * 1000);
        var second = AppAvailability.ScheduleAfterPush(Now + 400, first);

        Assert.Equal(first, second);
    }

    [Fact]
    public void AServedFetchWaitsTheSlowIntervalAndAFailedOneRetriesSooner()
    {
        Assert.Equal(Now + 5 * 60 * 1000, AppAvailability.ScheduleAfterFetch(Now, served: true, pushedMeanwhile: false));
        Assert.Equal(Now + 60 * 1000, AppAvailability.ScheduleAfterFetch(Now, served: false, pushedMeanwhile: false));
    }

    [Fact]
    public void APushThatLandsDuringAFetchIsFetchedAgainRightAfter()
    {
        Assert.Equal(Now + 1500, AppAvailability.ScheduleAfterFetch(Now, served: true, pushedMeanwhile: true));
        Assert.Equal(Now + 1500, AppAvailability.ScheduleAfterFetch(Now, served: false, pushedMeanwhile: true));
    }
}
