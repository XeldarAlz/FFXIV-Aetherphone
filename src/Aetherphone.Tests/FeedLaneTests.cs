using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Social;
using Xunit;

namespace Aetherphone.Tests;

public sealed class FeedLaneTests
{
    private sealed record FakePost(string Id, long CreatedAtUnix) : IIdentified;

    private static readonly Comparison<FakePost> ByNewestFirst = (left, right) =>
    {
        var byTime = right.CreatedAtUnix.CompareTo(left.CreatedAtUnix);
        return byTime != 0 ? byTime : string.CompareOrdinal(right.Id, left.Id);
    };

    private static FeedLane<FakePost> NewLane() => new(ByNewestFirst);

    [Fact]
    public void RefreshOnEmptyLaneAdoptsItemsAndCursor()
    {
        var lane = NewLane();
        var incoming = new[] { new FakePost("a", 2), new FakePost("b", 1) };

        lane.ApplyRefresh(incoming, "cursor-1");

        Assert.Same(incoming, lane.Items);
        Assert.Equal("cursor-1", lane.Cursor);
        Assert.True(lane.HasMore);
    }

    [Fact]
    public void RefreshOnPopulatedLaneMergesWithoutTouchingCursor()
    {
        var lane = NewLane();
        lane.ApplyRefresh(new[] { new FakePost("a", 10) }, "older-cursor");

        lane.ApplyRefresh(new[] { new FakePost("b", 20) }, null);

        Assert.Equal(2, lane.Items.Length);
        Assert.Equal("b", lane.Items[0].Id);
        Assert.Equal("a", lane.Items[1].Id);
        Assert.Equal("older-cursor", lane.Cursor);
    }

    [Fact]
    public void ApplyMoreMergesAndAdvancesCursor()
    {
        var lane = NewLane();
        lane.ApplyRefresh(new[] { new FakePost("b", 20), new FakePost("a", 10) }, "page-2");

        lane.ApplyMore(new[] { new FakePost("c", 5) }, null);

        Assert.Equal(3, lane.Items.Length);
        Assert.Equal("c", lane.Items[2].Id);
        Assert.Null(lane.Cursor);
        Assert.False(lane.HasMore);
    }

    [Fact]
    public void IncomingReplacesExistingPostWithSameId()
    {
        var lane = NewLane();
        lane.ApplyRefresh(new[] { new FakePost("a", 10) }, null);

        lane.ApplyRefresh(new[] { new FakePost("a", 30) }, null);

        Assert.Single(lane.Items);
        Assert.Equal(30, lane.Items[0].CreatedAtUnix);
    }

    [Fact]
    public void RefreshDropsPostDeletedFromNewestPage()
    {
        var lane = NewLane();
        lane.ApplyRefresh(new[] { new FakePost("b", 20), new FakePost("a", 10) }, "page-2");

        lane.ApplyRefresh(new[] { new FakePost("a", 10) }, "page-2");

        Assert.Single(lane.Items);
        Assert.Equal("a", lane.Items[0].Id);
    }

    [Fact]
    public void RefreshKeepsOlderPagesWhenNewestPageBackfills()
    {
        var lane = NewLane();
        lane.ApplyRefresh(new[] { new FakePost("d", 40), new FakePost("c", 30) }, "page-2");
        lane.ApplyMore(new[] { new FakePost("b", 20), new FakePost("a", 10) }, null);

        lane.ApplyRefresh(new[] { new FakePost("d", 40), new FakePost("b", 20) }, "page-2");

        Assert.Equal(3, lane.Items.Length);
        Assert.Equal("d", lane.Items[0].Id);
        Assert.Equal("b", lane.Items[1].Id);
        Assert.Equal("a", lane.Items[2].Id);
        Assert.DoesNotContain(lane.Items, post => post.Id == "c");
    }
}
