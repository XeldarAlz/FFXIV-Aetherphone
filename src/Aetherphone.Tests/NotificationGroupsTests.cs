using Aetherphone.Core.Notifications;
using Xunit;

namespace Aetherphone.Tests;

public sealed class NotificationGroupsTests
{
    private static readonly DateTime Anchor = new(2026, 10, 3, 12, 0, 0, DateTimeKind.Local);

    private static PhoneNotification Build(long id, string appId, string? groupKey, int minutesAgo) =>
        new(appId, "Title", "Body", Anchor.AddMinutes(-minutesAgo), default, groupKey) { Id = id };

    private static NotificationGroups BuildGroups(params PhoneNotification[] recent)
    {
        var groups = new NotificationGroups();
        groups.Rebuild(recent);
        return groups;
    }

    [Fact]
    public void GroupsRunNewestArrivalFirst()
    {
        var groups = BuildGroups(
            Build(1, "message", "alice", 30),
            Build(2, "message", "bob", 20),
            Build(3, "message", "alice", 10));

        Assert.Equal(2, groups.Count);
        Assert.Equal("alice", groups.Groups[0].Key);
        Assert.Equal("bob", groups.Groups[1].Key);
    }

    [Fact]
    public void ItemsWithinAGroupRunNewestFirst()
    {
        var groups = BuildGroups(
            Build(1, "message", "alice", 30),
            Build(2, "message", "alice", 20),
            Build(3, "message", "alice", 10));

        var group = groups.Groups[0];
        Assert.Equal(3, group.Count);
        Assert.Equal(3, group.Newest.Id);
        Assert.Equal(3, group.Items[0].Id);
        Assert.Equal(2, group.Items[1].Id);
        Assert.Equal(1, group.Items[2].Id);
    }

    [Fact]
    public void NotificationsWithoutAGroupKeyStackByApp()
    {
        var groups = BuildGroups(
            Build(1, "timers", null, 5),
            Build(2, "timers", null, 4),
            Build(3, "calendar", null, 3));

        Assert.Equal(2, groups.Count);
        Assert.Equal("calendar", groups.Groups[0].Key);
        Assert.Equal("timers", groups.Groups[1].Key);
        Assert.Equal(2, groups.Groups[1].Count);
    }

    [Fact]
    public void TotalCountAndOldestCoverEveryNotification()
    {
        var groups = BuildGroups(
            Build(1, "message", "alice", 30),
            Build(2, "timers", null, 90),
            Build(3, "message", "alice", 10));

        Assert.Equal(3, groups.TotalCount);
        Assert.Equal(Anchor.AddMinutes(-90), groups.OldestReceivedAt);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(7, 6)]
    public void HiddenCountIsEverythingBehindTheNewest(int itemCount, int expected)
    {
        Assert.Equal(expected, NotificationGroups.HiddenCount(itemCount));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(9, 3)]
    public void VisibleLayersCapAtThree(int itemCount, int expected)
    {
        Assert.Equal(expected, NotificationGroups.VisibleLayers(itemCount));
    }

    [Fact]
    public void GroupExposesItsOwnStackMath()
    {
        var groups = BuildGroups(
            Build(1, "message", "alice", 50),
            Build(2, "message", "alice", 40),
            Build(3, "message", "alice", 30),
            Build(4, "message", "alice", 20),
            Build(5, "message", "alice", 10));

        var group = groups.Groups[0];
        Assert.Equal(4, group.HiddenCount);
        Assert.Equal(3, group.VisibleLayers);
    }

    [Fact]
    public void RebuildDropsGroupsThatNoLongerExist()
    {
        var groups = BuildGroups(
            Build(1, "message", "alice", 30),
            Build(2, "message", "bob", 20));

        groups.Rebuild(new[] { Build(2, "message", "bob", 20) });

        Assert.Equal(1, groups.Count);
        Assert.False(groups.TryGet("alice", out _));
        Assert.True(groups.TryGet("bob", out var bob));
        Assert.Equal(1, bob.Count);
        Assert.False(groups.Contains(1));
        Assert.True(groups.Contains(2));
    }

    [Fact]
    public void ClearEmptiesEverything()
    {
        var groups = BuildGroups(Build(1, "message", "alice", 30));

        groups.Clear();

        Assert.Equal(0, groups.Count);
        Assert.Equal(0, groups.TotalCount);
        Assert.Equal(default, groups.OldestReceivedAt);
    }

    [Fact]
    public void EmptyInputProducesNoGroups()
    {
        var groups = BuildGroups();

        Assert.Equal(0, groups.Count);
        Assert.Equal(0, groups.TotalCount);
        Assert.Equal(default, groups.OldestReceivedAt);
    }
}
