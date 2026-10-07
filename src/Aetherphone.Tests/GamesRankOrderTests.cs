using Aetherphone.Apps.Games;
using Xunit;

namespace Aetherphone.Tests;

public sealed class GamesRankOrderTests
{
    [Fact]
    public void RanksSortBestFirstAndKeepTiesInArrivalOrder()
    {
        var values = new[] { 12, 3, 48, 3, 1 };
        var order = new int[values.Length];

        GamesApp.SortByRank(order, values, values.Length);

        Assert.Equal(new[] { 4, 1, 3, 0, 2 }, order);
    }

    [Fact]
    public void SortingOnlyTouchesTheCountedSlots()
    {
        var values = new[] { 9, 2, 1 };
        var order = new[] { -1, -1, 7 };

        GamesApp.SortByRank(order, values, 2);

        Assert.Equal(new[] { 1, 0, 7 }, order);
    }

    [Fact]
    public void AnEmptyListLeavesTheOrderAlone()
    {
        var order = new[] { 5 };

        GamesApp.SortByRank(order, new[] { 1 }, 0);

        Assert.Equal(new[] { 5 }, order);
    }
}
