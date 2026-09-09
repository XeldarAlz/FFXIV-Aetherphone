using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Social;
using Xunit;

namespace Aetherphone.Tests;

public sealed class FeedLaneOrderTests
{
    private sealed record Entry(string Id, long Stamp) : IIdentified;

    [Fact]
    public void AServerOrderedLaneKeepsTheOrderItWasHanded()
    {
        var lane = FeedLane<Entry>.ServerOrdered();

        lane.ApplyRefresh(new[] { new Entry("c", 1), new Entry("a", 3), new Entry("b", 2) }, "r:s:30");

        Assert.Equal(new[] { "c", "a", "b" }, Ids(lane.Items));
        Assert.Equal("r:s:30", lane.Cursor);
        Assert.True(lane.KeepsServerOrder);
    }

    [Fact]
    public void ARefreshReplacesTheSessionAndTakesTheNewCursor()
    {
        var lane = FeedLane<Entry>.ServerOrdered();
        lane.ApplyRefresh(new[] { new Entry("a", 1) }, "r:one:30");

        lane.ApplyRefresh(new[] { new Entry("b", 2), new Entry("a", 1) }, "r:two:30");

        Assert.Equal(new[] { "b", "a" }, Ids(lane.Items));
        Assert.Equal("r:two:30", lane.Cursor);
    }

    [Fact]
    public void MorePagesAppendWithoutResortingOrRepeating()
    {
        var lane = FeedLane<Entry>.ServerOrdered();
        lane.ApplyRefresh(new[] { new Entry("z", 9), new Entry("y", 8) }, "r:s:30");

        lane.ApplyMore(new[] { new Entry("y", 8), new Entry("x", 99), new Entry("w", 1) }, "t:s:123");

        Assert.Equal(new[] { "z", "y", "x", "w" }, Ids(lane.Items));
        Assert.Equal("t:s:123", lane.Cursor);
    }

    [Fact]
    public void TrimmingNeverRewritesARankedCursor()
    {
        var lane = FeedLane<Entry>.ServerOrdered();
        lane.ApplyRefresh(new[] { new Entry("a", 1), new Entry("b", 2), new Entry("c", 3) }, "r:s:30");

        lane.Trim(1);

        Assert.Equal(3, lane.Items.Length);
        Assert.Equal("r:s:30", lane.Cursor);
    }

    [Fact]
    public void AChronologicalLaneStillSortsAndTrims()
    {
        var lane = new FeedLane<Entry>(static (left, right) => right.Stamp.CompareTo(left.Stamp), static entry => entry.Stamp);
        lane.ApplyRefresh(new[] { new Entry("new", 3), new Entry("old", 1) }, "cursor");

        lane.ApplyMore(new[] { new Entry("mid", 2) }, "cursor");

        Assert.Equal(new[] { "new", "mid", "old" }, Ids(lane.Items));
        Assert.False(lane.KeepsServerOrder);

        lane.Trim(1);
        Assert.Single(lane.Items);
        Assert.NotEqual("cursor", lane.Cursor);
    }

    private static string[] Ids(Entry[] items)
    {
        var ids = new string[items.Length];
        for (var index = 0; index < items.Length; index++)
        {
            ids[index] = items[index].Id;
        }

        return ids;
    }
}
