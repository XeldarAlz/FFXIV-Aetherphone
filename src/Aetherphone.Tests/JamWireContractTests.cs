using System.Text.Json;
using Aetherphone.Core.Jam;
using Aetherphone.Core.Songs;
using Aetherphone.Core.Telephony.Contracts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class JamWireContractTests
{
    private const string Members =
        "\"jamMembers\":[{\"userId\":\"host\",\"displayName\":\"Aria\",\"handle\":\"aria\",\"avatarUrl\":\"https://a/1.png\",\"isHost\":true,\"permissions\":3},"
        + "{\"userId\":\"guest\",\"displayName\":\"Bo\",\"handle\":\"bo\",\"isHost\":false,\"permissions\":1}]";

    private const string Entries =
        "\"entries\":[{\"entryId\":4,\"track\":{\"videoId\":\"v4\",\"title\":\"Four\",\"author\":\"Band\",\"durationSeconds\":201.6},\"addedByUserId\":\"guest\",\"addedByName\":\"Bo\"},"
        + "{\"entryId\":5,\"track\":{\"videoId\":\"v5\"},\"addedByUserId\":\"host\",\"addedByName\":\"Aria\"}]";

    private static CallControl Parse(string json)
    {
        var control = JsonSerializer.Deserialize(json, TelephonyJsonContext.Default.CallControl);
        Assert.NotNull(control);
        return control!;
    }

    [Fact]
    public void JoinedSnapshotCarriesTheWholeRoom()
    {
        var message = Parse("{\"type\":\"jam.joined\",\"jamId\":\"j1\",\"hostId\":\"host\","
            + "\"track\":{\"videoId\":\"v1\",\"title\":\"One\",\"author\":\"Band\",\"thumbnailUrl\":\"https://t/1.jpg\",\"durationSeconds\":180},"
            + "\"positionSeconds\":42.5,\"stateAtUnixMs\":1700000000000,\"paused\":false,\"stale\":false,\"queueVersion\":7,"
            + "\"code\":\"ABC234\",\"title\":\"Friday night\",\"guestPermissions\":1,\"approvalRequired\":true,"
            + Entries + "," + Members + "}");

        Assert.Equal(SignalType.JamJoined, message.Type);
        Assert.Equal("j1", message.JamId);
        Assert.Equal("host", message.HostId);
        Assert.Equal("ABC234", message.Code);
        Assert.Equal("Friday night", message.Title);
        Assert.Equal(42.5, message.PositionSeconds);
        Assert.Equal(1700000000000L, message.StateAtUnixMs);
        Assert.False(message.Paused);
        Assert.False(message.Stale);
        Assert.Equal(7, message.QueueVersion);
        Assert.Equal(1, message.GuestPermissions);
        Assert.True(message.ApprovalRequired);

        var song = JamWire.ToSong(message.Track);
        Assert.Equal("v1", song.VideoId);
        Assert.Equal("One", song.Title);
        Assert.Equal("Band", song.Author);
        Assert.Equal("https://t/1.jpg", song.ThumbnailUrl);
        Assert.Equal(180, song.DurationSeconds);

        var members = message.JamMembers!;
        Assert.Equal(2, members.Length);
        Assert.True(members[0].IsHost);
        Assert.Equal(JamPermission.All, members[0].Permissions);
        Assert.Equal("https://a/1.png", members[0].AvatarUrl);
        Assert.Null(members[1].AvatarUrl);
        Assert.Equal("bo", members[1].Handle);

        var queue = JamWire.ToQueue(message.Entries);
        Assert.Equal(2, queue.Length);
        Assert.Equal(4, queue[0].EntryId);
        Assert.Equal(202, queue[0].Song.DurationSeconds);
        Assert.Equal("Bo", queue[0].AddedByName);
        Assert.Equal("guest", queue[0].AddedByUserId);
        Assert.Equal(string.Empty, queue[1].Song.Title);
        Assert.Equal(0, queue[1].Song.DurationSeconds);
    }

    [Fact]
    public void HostChangedIsASnapshotWithThePreviousHost()
    {
        var message = Parse("{\"type\":\"jam.hostChanged\",\"jamId\":\"j1\",\"hostId\":\"guest\",\"userId\":\"host\","
            + "\"positionSeconds\":0,\"stateAtUnixMs\":5,\"paused\":true,\"stale\":false,\"queueVersion\":0,"
            + "\"code\":\"ABC234\",\"guestPermissions\":3,\"approvalRequired\":false,\"entries\":[]," + Members + "}");

        Assert.Equal(SignalType.JamHostChanged, message.Type);
        Assert.Equal("guest", message.HostId);
        Assert.Equal("host", message.UserId);
        Assert.Null(message.Track);
        Assert.Null(message.Title);
        Assert.True(JamWire.ToSong(message.Track).IsEmpty);
        Assert.Empty(JamWire.ToQueue(message.Entries));
    }

    [Theory]
    [InlineData("code", (byte)JamDeclineReason.BadCode)]
    [InlineData("full", (byte)JamDeclineReason.Full)]
    [InlineData("denied", (byte)JamDeclineReason.Denied)]
    [InlineData("ended", (byte)JamDeclineReason.Ended)]
    [InlineData("busy", (byte)JamDeclineReason.Busy)]
    [InlineData("something-new", (byte)JamDeclineReason.BadCode)]
    public void DeclinedReasonsMapToTheEnum(string reason, byte expected)
    {
        var message = Parse("{\"type\":\"jam.declined\",\"reason\":\"" + reason + "\"}");

        Assert.Equal(SignalType.JamDeclined, message.Type);
        Assert.Null(message.JamId);
        Assert.Equal((JamDeclineReason)expected, JamDeclineCodes.Parse(message.Reason));
    }

    [Fact]
    public void JoinPendingCarriesOnlyTheJam()
    {
        var message = Parse("{\"type\":\"jam.joinPending\",\"jamId\":\"j9\"}");

        Assert.Equal(SignalType.JamJoinPending, message.Type);
        Assert.Equal("j9", message.JamId);
    }

    [Fact]
    public void JoinRequestBecomesAnAnonymousRequestRow()
    {
        var message = Parse("{\"type\":\"jam.joinRequest\",\"jamId\":\"j1\",\"userId\":\"u7\","
            + "\"from\":{\"userId\":\"u7\",\"name\":\"\",\"world\":\"\",\"displayName\":\"Cid\",\"handle\":\"cid\","
            + "\"avatarUrl\":\"https://a/7.png\",\"slot\":0,\"state\":\"active\",\"muted\":false}}");

        var request = JamWire.ToJoinRequest(message);

        Assert.NotNull(request);
        Assert.Equal("u7", request!.UserId);
        Assert.Equal("Cid", request.DisplayName);
        Assert.Equal("cid", request.Handle);
        Assert.Equal("https://a/7.png", request.AvatarUrl);
    }

    [Fact]
    public void JoinCancelledNamesTheUser()
    {
        var message = Parse("{\"type\":\"jam.joinCancelled\",\"jamId\":\"j1\",\"userId\":\"u7\"}");

        Assert.Equal(SignalType.JamJoinCancelled, message.Type);
        Assert.Equal("u7", message.UserId);
    }

    [Fact]
    public void RosterCarriesSettingsAndMembers()
    {
        var message = Parse("{\"type\":\"jam.roster\",\"jamId\":\"j1\",\"hostId\":\"host\",\"title\":\"Chill\","
            + "\"guestPermissions\":0,\"approvalRequired\":false," + Members + "}");

        Assert.Equal(SignalType.JamRoster, message.Type);
        Assert.Equal("Chill", message.Title);
        Assert.Equal(0, message.GuestPermissions);
        Assert.False(message.ApprovalRequired);
        Assert.Equal(2, message.JamMembers!.Length);
    }

    [Fact]
    public void StateCarriesTheStaleFlagAndANullTrack()
    {
        var stale = Parse("{\"type\":\"jam.state\",\"jamId\":\"j1\",\"hostId\":\"host\","
            + "\"track\":{\"videoId\":\"v1\"},\"positionSeconds\":12,\"stateAtUnixMs\":99,\"paused\":false,"
            + "\"stale\":true,\"queueVersion\":3}");
        var idle = Parse("{\"type\":\"jam.state\",\"jamId\":\"j1\",\"hostId\":\"host\",\"positionSeconds\":0,"
            + "\"stateAtUnixMs\":100,\"paused\":true,\"stale\":false,\"queueVersion\":3}");

        Assert.True(stale.Stale);
        Assert.Equal("v1", stale.Track!.VideoId);
        Assert.Null(stale.Track.DurationSeconds);
        Assert.Null(idle.Track);
        Assert.True(idle.Paused);
    }

    [Fact]
    public void QueueCarriesItsVersion()
    {
        var message = Parse("{\"type\":\"jam.queue\",\"jamId\":\"j1\",\"queueVersion\":12," + Entries + "}");

        Assert.Equal(12, message.QueueVersion);
        Assert.Equal(2, JamWire.ToQueue(message.Entries).Length);
    }

    [Fact]
    public void ControlRequestCarriesTheActionAndPosition()
    {
        var message = Parse("{\"type\":\"jam.controlRequest\",\"jamId\":\"j1\",\"userId\":\"guest\","
            + "\"action\":\"seek\",\"positionSeconds\":61.25,\"from\":{\"userId\":\"guest\",\"name\":\"\",\"world\":\"\","
            + "\"displayName\":\"Bo\",\"handle\":\"bo\",\"slot\":0,\"state\":\"active\",\"muted\":false}}");

        Assert.Equal(JamControlAction.Seek, message.Action);
        Assert.Equal(61.25, message.PositionSeconds);
        Assert.Equal("Bo", JamWire.PublicName(message.From));
    }

    [Fact]
    public void ReactionCarriesKindAndUser()
    {
        var message = Parse("{\"type\":\"jam.reaction\",\"jamId\":\"j1\",\"userId\":\"guest\",\"reaction\":5}");

        Assert.Equal(5, message.Reaction);
        Assert.Equal("guest", message.UserId);
    }

    [Theory]
    [InlineData("jam.kicked")]
    [InlineData("jam.ended")]
    public void TerminalMessagesCarryOnlyTheJam(string type)
    {
        var message = Parse("{\"type\":\"" + type + "\",\"jamId\":\"j1\"}");

        Assert.Equal(type, message.Type);
        Assert.Equal("j1", message.JamId);
    }

    [Fact]
    public void InvitedCarriesCodeTitleAndSender()
    {
        var message = Parse("{\"type\":\"jam.invited\",\"jamId\":\"j1\",\"code\":\"ABC234\","
            + "\"from\":{\"userId\":\"host\",\"name\":\"\",\"world\":\"\",\"displayName\":\"\",\"handle\":\"aria\","
            + "\"slot\":0,\"state\":\"active\",\"muted\":false}}");

        Assert.Equal("ABC234", message.Code);
        Assert.Null(message.Title);
        Assert.Equal("@aria", JamWire.PublicName(message.From));
    }

    [Fact]
    public void OutboundSettingsOmitWhatItLeavesAlone()
    {
        var json = JsonSerializer.Serialize(new CallControl { Type = SignalType.JamSettings, GuestPermissions = 2 },
            TelephonyJsonContext.Default.CallControl);

        Assert.Contains("\"guestPermissions\":2", json);
        Assert.DoesNotContain("\"title\"", json);
        Assert.DoesNotContain("\"track\"", json);
        Assert.DoesNotContain("\"jamMembers\"", json);
    }

    [Fact]
    public void OutboundStateRoundTripsTheTrack()
    {
        var song = new Song("v1", "One", "Band", "https://t/1.jpg", 180);
        var json = JsonSerializer.Serialize(new CallControl
        {
            Type = SignalType.JamState,
            Track = JamWire.ToTrack(song),
            PositionSeconds = 3.5,
            Paused = false,
        }, TelephonyJsonContext.Default.CallControl);

        Assert.Contains("\"track\":{\"videoId\":\"v1\"", json);
        Assert.Contains("\"durationSeconds\":180", json);
        Assert.Equal(song.VideoId, JamWire.ToSong(Parse(json).Track).VideoId);
    }

    [Fact]
    public void TrackConversionDropsEmptyFields()
    {
        var track = JamWire.ToTrack(new Song("v2", string.Empty, "Band", string.Empty, 0));

        Assert.NotNull(track);
        Assert.Null(track!.Title);
        Assert.Null(track.ThumbnailUrl);
        Assert.Null(track.DurationSeconds);
        Assert.Null(JamWire.ToTrack(default));
    }

    [Fact]
    public void QueueDropsEntriesWithoutAVideo()
    {
        var queue = JamWire.ToQueue(new[]
        {
            new JamQueueEntry(1, new JamTrack(string.Empty)),
            new JamQueueEntry(2, new JamTrack("v2"), "u", "U"),
        });

        Assert.Single(queue);
        Assert.Equal(2, queue[0].EntryId);
        Assert.Equal(0, JamWire.IndexOfEntry(queue, 2));
        Assert.Equal(-1, JamWire.IndexOfEntry(queue, 1));
    }
}
