using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Social;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PinnedProfileOrderTests
{
    [Fact]
    public void ARefreshThatPinsAPostMovesItAheadOfTheTimeline()
    {
        var lane = new FeedLane<PostDto>(PostOrder.PinnedThenNewest);
        lane.ApplyRefresh(new[] { Post("old-pin", 10, 100), Post("latest", 90), Post("older", 80) }, "cursor");

        lane.ApplyRefresh(new[] { Post("older", 80, 200), Post("old-pin", 10, 100), Post("latest", 90) }, "cursor");

        Assert.Equal(new[] { "older", "old-pin", "latest" }, Ids(lane.Items));
    }

    [Fact]
    public void ARefreshThatUnpinsAPostDropsItBackIntoTheTimeline()
    {
        var lane = new FeedLane<PostDto>(PostOrder.PinnedThenNewest);
        lane.ApplyRefresh(new[] { Post("pinned", 10, 100), Post("latest", 90), Post("older", 80) }, "cursor");

        lane.ApplyRefresh(new[] { Post("latest", 90), Post("older", 80), Post("pinned", 10) }, "cursor");

        Assert.Equal(new[] { "latest", "older", "pinned" }, Ids(lane.Items));
    }

    [Fact]
    public void OlderPagesStayBehindThePinnedRun()
    {
        var lane = new FeedLane<PostDto>(PostOrder.PinnedThenNewest);
        lane.ApplyRefresh(new[] { Post("pinned", 10, 100), Post("latest", 90) }, "cursor");

        lane.ApplyMore(new[] { Post("older", 80), Post("oldest", 70) }, null);

        Assert.Equal(new[] { "pinned", "latest", "older", "oldest" }, Ids(lane.Items));
    }

    [Fact]
    public void PostsWithoutPinsKeepNewestFirst()
    {
        Assert.True(PostOrder.PinnedThenNewest(Post("a", 5), Post("b", 9)) > 0);
        Assert.True(PostOrder.PinnedThenNewest(Post("a", 9), Post("b", 5)) < 0);
        Assert.True(PostOrder.PinnedThenNewest(Post("a", 5, 1), Post("b", 9)) < 0);
    }

    private static PostDto Post(string id, long createdAt, long? pinnedAt = null)
    {
        return new PostDto(id, "author", string.Empty, string.Empty, string.Empty, "author", id, createdAt,
            System.Array.Empty<int>(), 0, -1, 1, null, 0, 0, null, 0, false) with { PinnedAtUnix = pinnedAt };
    }

    private static string[] Ids(PostDto[] posts)
    {
        var ids = new string[posts.Length];
        for (var index = 0; index < posts.Length; index++)
        {
            ids[index] = posts[index].Id;
        }

        return ids;
    }
}
