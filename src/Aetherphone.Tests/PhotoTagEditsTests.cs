using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Social;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PhotoTagEditsTests
{
    private static PhotoTagDto Stored(string userId, int photoIndex, float x, float y) =>
        new(string.Concat("tag-", userId), userId, userId, userId, photoIndex, x, y, 1);

    [Fact]
    public void NoTagsOnEitherSideIsUnchanged()
    {
        Assert.True(PhotoTagEdits.Unchanged(null, Array.Empty<PhotoTagInput>()));
        Assert.True(PhotoTagEdits.Unchanged(Array.Empty<PhotoTagDto>(), Array.Empty<PhotoTagInput>()));
    }

    [Fact]
    public void TheSameTagsInAnotherOrderAreUnchanged()
    {
        var existing = new[] { Stored("alice", 0, 0.25f, 0.5f), Stored("bob", 1, 0.75f, 0.1f) };
        var wanted = new[] { new PhotoTagInput("bob", 1, 0.75f, 0.1f), new PhotoTagInput("alice", 0, 0.25f, 0.5f) };

        Assert.True(PhotoTagEdits.Unchanged(existing, wanted));
    }

    [Fact]
    public void AMovedTagIsAChange()
    {
        var existing = new[] { Stored("alice", 0, 0.25f, 0.5f) };

        Assert.False(PhotoTagEdits.Unchanged(existing, new[] { new PhotoTagInput("alice", 0, 0.3f, 0.5f) }));
        Assert.False(PhotoTagEdits.Unchanged(existing, new[] { new PhotoTagInput("alice", 1, 0.25f, 0.5f) }));
    }

    [Fact]
    public void ADroppedOrAddedTagIsAChange()
    {
        var existing = new[] { Stored("alice", 0, 0.25f, 0.5f) };

        Assert.False(PhotoTagEdits.Unchanged(existing, Array.Empty<PhotoTagInput>()));
        Assert.False(PhotoTagEdits.Unchanged(null, new[] { new PhotoTagInput("alice", 0, 0.25f, 0.5f) }));
    }

    [Fact]
    public void ASwappedPersonIsAChange()
    {
        var existing = new[] { Stored("alice", 0, 0.25f, 0.5f) };

        Assert.False(PhotoTagEdits.Unchanged(existing, new[] { new PhotoTagInput("bob", 0, 0.25f, 0.5f) }));
    }
}
