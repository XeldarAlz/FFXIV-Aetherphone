using System.Text.Json;
using Aetherphone.Core.Jam;
using Aetherphone.Core.Playback;
using Aetherphone.Core.Radio;
using Aetherphone.Core.Songs;
using Aetherphone.Core.Telephony.Contracts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class JamWaveTwoTests
{
    private const string Me = "me";

    private static JamChatLog NewLog() => new(unixMs => string.Concat("t", unixMs.ToString()));

    private static RadioChatMessage Message(long id, string userId = "other", string text = "hi", bool isHost = false)
    {
        return new RadioChatMessage(id, userId, "Name" + userId, "handle" + userId, null, text, 1_000 + id, isHost);
    }

    private static CallControl Parse(string json)
    {
        var control = JsonSerializer.Deserialize(json, TelephonyJsonContext.Default.CallControl);
        Assert.NotNull(control);
        return control!;
    }

    private static JamQueueItem Item(int entryId)
    {
        return new JamQueueItem(entryId, new Song("v" + entryId, "Song", "Band", string.Empty, 0), "user", "User");
    }

    private static int[] Order(JamQueueItem[] items)
    {
        var order = new int[items.Length];
        for (var index = 0; index < items.Length; index++)
        {
            order[index] = items[index].EntryId;
        }

        return order;
    }

    [Fact]
    public void ChatPacingStaysSlightlySlowerThanTheServerBucket()
    {
        var pacer = new RadioChatPacer();
        for (var index = 0; index < RadioRoomPacing.ChatBurst; index++)
        {
            Assert.True(pacer.CanSend(10_000));
            pacer.Take(10_000);
        }

        Assert.False(pacer.CanSend(10_000));
        Assert.False(pacer.CanSend(10_000 + 1_500));
        Assert.True(RadioRoomPacing.ChatRefillMilliseconds > 1_500);
        Assert.True(pacer.CanSend(10_000 + RadioRoomPacing.ChatRefillMilliseconds));
    }

    [Fact]
    public void ChatRingKeepsTheNewestHundredWithPreformattedLabels()
    {
        var log = NewLog();
        for (var id = 1; id <= JamChatLog.Capacity + 5; id++)
        {
            log.Append(new[] { Message(id) }, Me);
        }

        Assert.Equal(JamChatLog.Capacity, log.Count);
        Assert.Equal(6, log.At(0).MessageId);
        Assert.Equal(JamChatLog.Capacity + 5, log.At(log.Count - 1).MessageId);
        var newest = log.At(log.Count - 1);
        Assert.Equal("@handleother", newest.HandleLabel);
        Assert.Equal("t" + (1_000 + newest.MessageId), newest.TimeLabel);
        Assert.False(newest.IsMine);
    }

    [Fact]
    public void ChatIgnoresDuplicatesAndEmptyEntriesAndBumpsTheVersion()
    {
        var log = NewLog();
        var start = log.Version;
        Assert.True(log.Append(new[] { Message(5) }, Me));
        Assert.False(log.Append(new[] { Message(5) }, Me));
        Assert.False(log.Append(new[] { Message(4) }, Me));
        Assert.False(log.Append(new[] { Message(6, text: string.Empty) }, Me));
        Assert.False(log.Append(null, Me));
        Assert.Equal(1, log.Count);
        Assert.Equal(start + 1, log.Version);
    }

    [Fact]
    public void ChatSnapshotReplacesTheTranscriptAndFlagsHostAndOwnMessages()
    {
        var log = NewLog();
        log.Append(new[] { Message(40) }, Me);
        log.Load(new[] { Message(1, "host", isHost: true), Message(2, Me) }, Me);

        Assert.Equal(2, log.Count);
        Assert.True(log.At(0).IsDj);
        Assert.True(log.At(1).IsMine);
        Assert.Equal("Nameme", log.At(1).DisplayName);
    }

    [Fact]
    public void ChatDeleteAndHideRemoveMessagesAndHiddenUsersStayHidden()
    {
        var log = NewLog();
        log.Append(new[] { Message(1, "a"), Message(2, "b"), Message(3, "a") }, Me);

        Assert.True(log.Remove(2));
        Assert.False(log.Remove(2));
        log.HideUser("a");
        Assert.Equal(0, log.Count);
        Assert.True(log.IsHidden("a"));
        Assert.False(log.Append(new[] { Message(9, "a") }, Me));
        log.Load(new[] { Message(10, "a"), Message(11, "b") }, Me);
        Assert.Equal(1, log.Count);
        Assert.Equal("b", log.At(0).UserId);
    }

    [Theory]
    [InlineData("   ", (byte)JamChatRefusal.Empty)]
    [InlineData("", (byte)JamChatRefusal.Empty)]
    public void ChatValidationRefusesEmptyText(string text, byte expected)
    {
        Assert.Null(JamWire.ValidateChat(text, out var refusal));
        Assert.Equal((JamChatRefusal)expected, refusal);
    }

    [Fact]
    public void ChatValidationTrimsAndCapsAtThreeHundred()
    {
        Assert.Equal("hello", JamWire.ValidateChat("  hello  ", out var refusal));
        Assert.Equal(JamChatRefusal.None, refusal);
        Assert.NotNull(JamWire.ValidateChat(new string('a', JamWire.MaxChatLength), out _));
        Assert.Null(JamWire.ValidateChat(new string('a', JamWire.MaxChatLength + 1), out var tooLong));
        Assert.Equal(JamChatRefusal.TooLong, tooLong);
    }

    [Theory]
    [InlineData("cooldown", (byte)JamChatRefusal.Cooldown)]
    [InlineData("tooLong", (byte)JamChatRefusal.TooLong)]
    [InlineData("empty", (byte)JamChatRefusal.Empty)]
    [InlineData("notInJam", (byte)JamChatRefusal.NotInJam)]
    [InlineData("whatever", (byte)JamChatRefusal.Unknown)]
    public void RefusalReasonsParse(string reason, byte expected)
    {
        Assert.Equal((JamChatRefusal)expected, JamChatRefusalCodes.Parse(reason));
    }

    [Fact]
    public void ChatFramesCarryTheRadioMessageShape()
    {
        var message = Parse("{\"type\":\"jam.message\",\"jamId\":\"j1\",\"messages\":[{\"messageId\":7,\"userId\":\"u\","
            + "\"displayName\":\"Aria\",\"handle\":\"aria\",\"avatarUrl\":null,\"text\":\"yo\",\"sentAtUnixMs\":5,\"isDj\":true}]}");

        Assert.Equal(SignalType.JamMessage, message.Type);
        Assert.Single(message.Messages!);
        Assert.Equal(7, message.Messages![0].MessageId);
        Assert.True(message.Messages[0].IsDj);

        var deleted = Parse("{\"type\":\"jam.messageDeleted\",\"jamId\":\"j1\",\"messageId\":7}");
        Assert.Equal(7, deleted.MessageId);

        var refused = Parse("{\"type\":\"jam.refused\",\"action\":\"chat\",\"reason\":\"cooldown\"}");
        Assert.Equal(JamChatRefusal.Cooldown, JamChatRefusalCodes.Parse(refused.Reason));
    }

    [Fact]
    public void OutgoingChatAndNearbyFramesUseTheContractNames()
    {
        var chat = JsonSerializer.Serialize(new CallControl { Type = SignalType.JamChat, Text = "hey" },
            TelephonyJsonContext.Default.CallControl);
        Assert.Contains("\"type\":\"jam.chat\"", chat);
        Assert.Contains("\"text\":\"hey\"", chat);
        Assert.DoesNotContain("nearbyJams", chat);

        var nearby = JsonSerializer.Serialize(
            new CallControl { Type = SignalType.JamNearby, TerritoryId = 132, WorldId = 73 },
            TelephonyJsonContext.Default.CallControl);
        Assert.Contains("\"territoryId\":132", nearby);
        Assert.Contains("\"worldId\":73", nearby);

        var settings = JsonSerializer.Serialize(new CallControl { Type = SignalType.JamSettings, Discoverable = true },
            TelephonyJsonContext.Default.CallControl);
        Assert.Contains("\"discoverable\":true", settings);
    }

    [Fact]
    public void NearbyRosterParsesFiltersAndFallsBackToTheHandle()
    {
        var message = Parse("{\"type\":\"jam.nearby.roster\",\"nearbyJams\":["
            + "{\"jamId\":\"j1\",\"code\":\"ABC234\",\"title\":\"Friday\",\"hostId\":\"h1\",\"hostDisplayName\":\"Aria\","
            + "\"hostHandle\":\"aria\",\"hostAvatarUrl\":\"https://a/1.png\",\"memberCount\":3,"
            + "\"track\":{\"videoId\":\"v1\",\"title\":\"One\",\"author\":\"Band\"}},"
            + "{\"jamId\":\"mine\",\"code\":\"QWE234\",\"hostId\":\"h2\",\"memberCount\":2},"
            + "{\"jamId\":\"j3\",\"code\":\"bad\",\"hostId\":\"h3\",\"memberCount\":2},"
            + "{\"jamId\":\"j1\",\"code\":\"ABC234\",\"hostId\":\"h1\",\"memberCount\":3},"
            + "{\"jamId\":\"j4\",\"code\":\"zxc-789\",\"hostId\":\"h4\",\"hostDisplayName\":\"\",\"hostHandle\":\"bo\",\"memberCount\":0}]}");

        var jams = JamWire.ToNearby(message.NearbyJams, "mine");

        Assert.Equal(2, jams.Length);
        Assert.Equal("ABC234", jams[0].Code);
        Assert.Equal("Friday", jams[0].Title);
        Assert.Equal("Aria", jams[0].HostName);
        Assert.Equal("@aria", jams[0].HostHandleLabel);
        Assert.Equal(3, jams[0].MemberCount);
        Assert.Equal("v1", jams[0].Track.VideoId);
        Assert.Equal("ZXC789", jams[1].Code);
        Assert.Equal("@bo", jams[1].HostName);
        Assert.Equal(1, jams[1].MemberCount);
        Assert.True(jams[1].Track.IsEmpty);
    }

    [Fact]
    public void NearbyRosterIsCappedAndEmptyIsShared()
    {
        Assert.Same(JamWire.ToNearby(null, string.Empty), JamWire.ToNearby(Array.Empty<JamNearbyInfo>(), "x"));
        var many = new JamNearbyInfo[JamWire.MaxNearbyJams + 5];
        for (var index = 0; index < many.Length; index++)
        {
            many[index] = new JamNearbyInfo("j" + index, "ABC" + (100 + index), MemberCount: 1);
        }

        Assert.Equal(JamWire.MaxNearbyJams, JamWire.ToNearby(many, string.Empty).Length);
    }

    [Fact]
    public void NearbyReportsAtOnceThenOnTheIntervalOrWhenTheZoneChanges()
    {
        var cadence = new JamNearbyCadence();
        Assert.False(cadence.ShouldReport(1_000, 0, 73));
        Assert.False(cadence.ShouldReport(1_000, 132, 0));
        Assert.True(cadence.ShouldReport(1_000, 132, 73));
        cadence.MarkSent(1_000, 132, 73);

        Assert.False(cadence.MayReport(1_500));
        Assert.True(cadence.MayReport(1_000 + JamNearbyCadence.MinimumGapMilliseconds));
        Assert.False(cadence.ShouldReport(2_000, 132, 73));
        Assert.False(cadence.ShouldReport(1_500, 133, 73));
        Assert.True(cadence.ShouldReport(1_000 + JamNearbyCadence.MinimumGapMilliseconds, 133, 73));
        Assert.True(cadence.ShouldReport(1_000 + JamNearbyCadence.IntervalMilliseconds, 132, 73));

        cadence.Reset();
        Assert.True(cadence.ShouldReport(1_100, 132, 73));
    }

    [Fact]
    public void NearbyIsWantedWhileVisibleOrHostingADiscoverableJam()
    {
        Assert.False(JamNearbyCadence.Wanted(5_000, 0, false));
        Assert.True(JamNearbyCadence.Wanted(5_000, 4_000, false));
        Assert.False(JamNearbyCadence.Wanted(5_000 + JamNearbyCadence.InterestMilliseconds + 1, 5_000, false));
        Assert.True(JamNearbyCadence.Wanted(90_000, 0, true));
    }

    [Fact]
    public void PendingMoveHoldsTheRowUntilTheServerAgrees()
    {
        var moves = new JamPendingMoves();
        var server = new[] { Item(1), Item(2), Item(3), Item(4) };
        moves.Add(4, 0, 1_000);

        Assert.Equal(new[] { 4, 1, 2, 3 }, Order(moves.Project(server, 1_100)));
        Assert.Equal(1, moves.Count);

        var unrelated = new[] { Item(1), Item(2), Item(3), Item(4), Item(5) };
        Assert.Equal(new[] { 4, 1, 2, 3, 5 }, Order(moves.Project(unrelated, 1_200)));

        var confirmed = new[] { Item(4), Item(1), Item(2), Item(3), Item(5) };
        Assert.Same(confirmed, moves.Project(confirmed, 1_300));
        Assert.Equal(0, moves.Count);
    }

    [Fact]
    public void PendingMoveDropsWhenTheEntryLeavesOrTimesOut()
    {
        var moves = new JamPendingMoves();
        var server = new[] { Item(1), Item(2), Item(3) };
        moves.Add(1, 2, 1_000);
        Assert.False(moves.HasExpired(1_000 + JamPendingMoves.TimeoutMilliseconds));
        Assert.True(moves.HasExpired(1_000 + JamPendingMoves.TimeoutMilliseconds + 1));
        Assert.Same(server, moves.Project(server, 1_000 + JamPendingMoves.TimeoutMilliseconds + 1));
        Assert.Equal(0, moves.Count);

        moves.Add(9, 0, 2_000);
        Assert.Same(server, moves.Project(server, 2_100));
        Assert.Equal(0, moves.Count);
    }

    [Fact]
    public void PendingMovesApplyInOrderAndClampToTheEnd()
    {
        var moves = new JamPendingMoves();
        var server = new[] { Item(1), Item(2), Item(3) };
        moves.Add(1, 10, 1_000);
        moves.Add(3, 0, 1_000);

        Assert.Equal(new[] { 3, 2, 1 }, Order(moves.Project(server, 1_100)));
        Assert.Equal(2, moves.Count);

        var firstApplied = new[] { Item(2), Item(3), Item(1) };
        Assert.Equal(new[] { 3, 2, 1 }, Order(moves.Project(firstApplied, 1_200)));
        Assert.Equal(1, moves.Count);
    }

    [Fact]
    public void PendingMovesKeepOnlyTheNewestFew()
    {
        var moves = new JamPendingMoves();
        for (var index = 0; index < JamPendingMoves.Capacity + 2; index++)
        {
            moves.Add(index + 1, 0, 1_000);
        }

        Assert.Equal(JamPendingMoves.Capacity, moves.Count);
    }

    [Theory]
    [InlineData(1_000L, false, false, (byte)JamAdvanceStep.Wait)]
    [InlineData(JamHostRecovery.AdvanceTimeoutMilliseconds, false, false, (byte)JamAdvanceStep.Wait)]
    [InlineData(JamHostRecovery.AdvanceTimeoutMilliseconds + 1, false, false, (byte)JamAdvanceStep.Retry)]
    [InlineData(JamHostRecovery.AdvanceTimeoutMilliseconds + 1, true, false, (byte)JamAdvanceStep.GiveUp)]
    [InlineData(JamHostRecovery.AdvanceTimeoutMilliseconds + 1, false, true, (byte)JamAdvanceStep.PlayPopped)]
    [InlineData(JamHostRecovery.AdvanceTimeoutMilliseconds + 1, true, true, (byte)JamAdvanceStep.PlayPopped)]
    public void AdvanceRetriesOnceThenGivesUp(long elapsed, bool retried, bool popped, byte expected)
    {
        Assert.Equal((JamAdvanceStep)expected, JamHostRecovery.Advance(elapsed, retried, popped));
    }

    [Theory]
    [InlineData(1, false, 100L, (byte)JamQueueWaitStep.Advance)]
    [InlineData(0, true, 100L, (byte)JamQueueWaitStep.Wait)]
    [InlineData(0, false, 100L, (byte)JamQueueWaitStep.Stop)]
    [InlineData(0, true, JamHostRecovery.QueueWaitLimitMilliseconds + 1, (byte)JamQueueWaitStep.Stop)]
    public void TrackEndWaitsForSeededSongsInsteadOfStopping(int queued, bool inFlight, long elapsed, byte expected)
    {
        Assert.Equal((JamQueueWaitStep)expected, JamHostRecovery.QueueWait(queued, inFlight, elapsed));
    }

    [Fact]
    public void AddsCountAsInFlightUntilTheEchoWindowPasses()
    {
        Assert.True(JamHostRecovery.AddInFlight(true, 0, 50_000));
        Assert.False(JamHostRecovery.AddInFlight(false, 0, 50_000));
        Assert.True(JamHostRecovery.AddInFlight(false, 50_000, 50_000 + JamHostRecovery.AddEchoMilliseconds - 1));
        Assert.False(JamHostRecovery.AddInFlight(false, 50_000, 50_000 + JamHostRecovery.AddEchoMilliseconds));
    }

    [Fact]
    public void HostTrackEndAwaitsTheQueueOnlyWhileAddsAreInFlight()
    {
        var waiting = JamIntentRouting.ForHost(PlaybackIntentKind.TrackEnded, new JamHostContext(true, 0, false, true));
        Assert.Equal(JamRoute.AwaitQueue, waiting.Route);
        Assert.True(waiting.Handled);

        Assert.Equal(JamRoute.LocalThenPublish,
            JamIntentRouting.ForHost(PlaybackIntentKind.TrackEnded, new JamHostContext(true, 0, false, false)).Route);
        Assert.Equal(JamRoute.LocalThenPublish,
            JamIntentRouting.ForHost(PlaybackIntentKind.TrackEnded, new JamHostContext(true, 0, true, true)).Route);
        Assert.Equal(JamRoute.Advance,
            JamIntentRouting.ForHost(PlaybackIntentKind.TrackEnded, new JamHostContext(true, 2, false, true)).Route);
    }
}
