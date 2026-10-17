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
    public void TrimKeepsNewestItemsAndDropsTheTail()
    {
        var lane = NewLane();
        lane.ApplyRefresh(Posts(10), "page-2");

        lane.Trim(4);

        Assert.Equal(4, lane.Items.Length);
        Assert.Equal("post-0", lane.Items[0].Id);
        Assert.Equal("post-3", lane.Items[3].Id);
    }

    [Fact]
    public void TrimLeavesLaneAloneWhenUnderCap()
    {
        var lane = NewLane();
        var incoming = Posts(3);
        lane.ApplyRefresh(incoming, null);

        lane.Trim(400);

        Assert.Same(incoming, lane.Items);
    }

    [Fact]
    public void TrimIgnoresNonPositiveCaps()
    {
        var lane = NewLane();
        lane.ApplyRefresh(Posts(3), null);

        lane.Trim(0);

        Assert.Equal(3, lane.Items.Length);
    }

    [Fact]
    public void TrimKeepsPagingAliveSoInfiniteScrollStillFetches()
    {
        var lane = NewLane();
        lane.ApplyRefresh(Posts(6), "page-2");

        lane.Trim(2);

        Assert.Equal("page-2", lane.Cursor);
        Assert.True(lane.HasMore);
    }

    [Fact]
    public void TrimmedLaneRegrowsFromRefreshWithoutDuplicates()
    {
        var lane = NewLane();
        lane.ApplyRefresh(Posts(6), "page-2");
        lane.Trim(2);

        lane.ApplyRefresh(new[] { new FakePost("post-0", 100), new FakePost("post-1", 99) }, "page-2");

        Assert.Equal(2, lane.Items.Length);
        Assert.Equal("post-0", lane.Items[0].Id);
        Assert.Equal("post-1", lane.Items[1].Id);
    }

    private static FakePost[] Posts(int count)
    {
        var posts = new FakePost[count];
        for (var index = 0; index < count; index++)
        {
            posts[index] = new FakePost($"post-{index}", 100 - index);
        }

        return posts;
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

    [Fact]
    public void ParkHoldsThePageUntilApplied()
    {
        var lane = NewLane();
        lane.ApplyRefresh(new[] { new FakePost("a", 10) }, "page-2");

        lane.Park(new[] { new FakePost("b", 20), new FakePost("a", 10) }, null);

        Assert.Single(lane.Items);
        Assert.True(lane.HasPending);
        Assert.Equal(1, lane.PendingCount);

        Assert.True(lane.ApplyPending());

        Assert.False(lane.HasPending);
        Assert.Equal(0, lane.PendingCount);
        Assert.Equal(2, lane.Items.Length);
        Assert.Equal("b", lane.Items[0].Id);
        Assert.Equal("page-2", lane.Cursor);
    }

    [Fact]
    public void PendingCountIgnoresPostsThatArrivedMeanwhile()
    {
        var lane = NewLane();
        lane.ApplyRefresh(new[] { new FakePost("a", 10) }, null);
        lane.Park(new[] { new FakePost("c", 30), new FakePost("b", 20), new FakePost("a", 10) }, null);
        Assert.Equal(2, lane.PendingCount);

        lane.Items = new[] { new FakePost("b", 20), new FakePost("a", 10) };

        Assert.Equal(1, lane.PendingCount);
    }

    [Fact]
    public void ExplicitRefreshDropsThePendingPage()
    {
        var lane = NewLane();
        lane.ApplyRefresh(new[] { new FakePost("a", 10) }, null);
        lane.Park(new[] { new FakePost("b", 20) }, null);

        lane.ApplyRefresh(new[] { new FakePost("c", 30) }, null);

        Assert.False(lane.HasPending);
        Assert.False(lane.ApplyPending());
        Assert.Equal(2, lane.Items.Length);
        Assert.Equal("c", lane.Items[0].Id);
    }

    [Fact]
    public void ServerOrderedPendingPageReplacesItemsAndCursor()
    {
        var lane = FeedLane<FakePost>.ServerOrdered();
        lane.ApplyRefresh(new[] { new FakePost("a", 10), new FakePost("b", 9) }, "page-2");
        lane.ApplyMore(new[] { new FakePost("c", 8) }, null);

        lane.Park(new[] { new FakePost("d", 20), new FakePost("a", 10) }, "fresh-page-2");

        Assert.Equal(1, lane.PendingCount);
        Assert.True(lane.ApplyPending());
        Assert.Equal(2, lane.Items.Length);
        Assert.Equal("d", lane.Items[0].Id);
        Assert.Equal("fresh-page-2", lane.Cursor);
    }

    [Fact]
    public void ClearDropsThePendingPage()
    {
        var lane = NewLane();
        lane.ApplyRefresh(new[] { new FakePost("a", 10) }, null);
        lane.Park(new[] { new FakePost("b", 20) }, null);

        lane.Clear();

        Assert.False(lane.HasPending);
        Assert.False(lane.Prefetching);
        Assert.Empty(lane.Items);
    }
}
