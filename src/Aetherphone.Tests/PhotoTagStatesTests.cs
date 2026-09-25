using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Social;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PhotoTagStatesTests
{
    private const string Me = "me";
    private const string Friend = "friend";

    [Fact]
    public void PendingForFindsOnlyMyTagThatStillWaits()
    {
        var tags = new[]
        {
            Tag("approved", Me, PhotoTagStates.Approved),
            Tag("theirs", Friend, PhotoTagStates.Pending),
            Tag("mine", Me, PhotoTagStates.Pending),
        };

        var pending = PhotoTagStates.PendingFor(tags, Me);

        Assert.NotNull(pending);
        Assert.Equal("mine", pending!.Id);
    }

    [Fact]
    public void PendingForIgnoresMissingTagsAndSignedOutViewers()
    {
        var tags = new[] { Tag("mine", Me, PhotoTagStates.Pending) };

        Assert.Null(PhotoTagStates.PendingFor(null, Me));
        Assert.Null(PhotoTagStates.PendingFor(tags, null));
        Assert.Null(PhotoTagStates.PendingFor(tags, Friend));
    }

    [Fact]
    public void WithStateCopiesOnlyWhenTheTagChanges()
    {
        var tags = new[] { Tag("mine", Me, PhotoTagStates.Pending), Tag("theirs", Friend, PhotoTagStates.Pending) };

        var approved = PhotoTagStates.WithState(tags, "mine", PhotoTagStates.Approved);
        var unchanged = PhotoTagStates.WithState(approved, "mine", PhotoTagStates.Approved);
        var unknown = PhotoTagStates.WithState(tags, "missing", PhotoTagStates.Approved);

        Assert.NotSame(tags, approved);
        Assert.Equal(PhotoTagStates.Approved, approved![0].State);
        Assert.Equal(PhotoTagStates.Pending, approved[1].State);
        Assert.Equal(PhotoTagStates.Pending, tags[0].State);
        Assert.Same(approved, unchanged);
        Assert.Same(tags, unknown);
    }

    [Fact]
    public void WithoutDropsTheTagAndKeepsTheOthersInOrder()
    {
        var tags = new[]
        {
            Tag("first", Friend, PhotoTagStates.Approved),
            Tag("mine", Me, PhotoTagStates.Pending),
            Tag("last", Friend, PhotoTagStates.Approved),
        };

        var remaining = PhotoTagStates.Without(tags, "mine");

        Assert.Equal(2, remaining!.Length);
        Assert.Equal("first", remaining[0].Id);
        Assert.Equal("last", remaining[1].Id);
        Assert.Same(tags, PhotoTagStates.Without(tags, "missing"));
        Assert.Null(PhotoTagStates.Without(null, "mine"));
    }

    private static PhotoTagDto Tag(string id, string userId, int state) =>
        new(id, userId, userId, userId, 0, 0.5f, 0.5f, state);
}
