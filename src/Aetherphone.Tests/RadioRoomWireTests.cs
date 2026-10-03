using System.Text;
using System.Text.Json;
using Aetherphone.Core;
using Aetherphone.Core.Radio;
using Aetherphone.Core.Telephony.Contracts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RadioRoomWireTests
{
    private const string Station = "st_01";

    internal static CallControl Parse(string json)
    {
        var control = JsonSerializer.Deserialize(Encoding.UTF8.GetBytes(json), TelephonyJsonContext.Default.CallControl);
        Assert.NotNull(control);
        return control!;
    }

    [Fact]
    public void RoomSnapshotParsesEveryField()
    {
        var control = Parse(RadioRoomSamples.Room(Station));

        Assert.Equal(SignalType.RadioRoom, control.Type);
        Assert.Equal(Station, control.StationId);
        Assert.NotNull(control.Messages);
        Assert.Equal(2, control.Messages!.Length);
        Assert.Equal(1, control.Messages[0].MessageId);
        Assert.Equal("u_dj", control.Messages[0].UserId);
        Assert.Equal("Lumi", control.Messages[0].DisplayName);
        Assert.Equal("lumi", control.Messages[0].Handle);
        Assert.Equal("https://cdn/a.png", control.Messages[0].AvatarUrl);
        Assert.True(control.Messages[0].IsDj);
        Assert.Null(control.Messages[1].AvatarUrl);
        Assert.Equal("Welcome in", control.Pinned);
        Assert.NotNull(control.Requests);
        Assert.Single(control.Requests!);
        Assert.Equal(4, control.Requests![0].RequestId);
        Assert.Equal(1, control.Requests[0].State);
        Assert.True(control.RequestsOpen);
        Assert.Equal(12, control.ListenerCount);
        Assert.False(control.IsDj);
        Assert.False(control.IsModerator);
        Assert.True(control.IsLive);
        Assert.Null(control.MutedUntilUnixMs);
    }

    [Fact]
    public void ServerMessagesParse()
    {
        var presence = Parse($"{{\"type\":\"radio.presence\",\"stationId\":\"{Station}\",\"listenerCount\":3,\"isLive\":false}}");
        Assert.Equal(3, presence.ListenerCount);
        Assert.False(presence.IsLive);

        var message = Parse(RadioRoomSamples.Message(Station, 9, "u_b", "hello"));
        Assert.Equal(SignalType.RadioMessage, message.Type);
        Assert.Equal(9, message.MessageId);
        Assert.Equal("u_b", message.UserId);
        Assert.Equal("Bee", message.DisplayName);
        Assert.Equal("bee", message.Handle);
        Assert.Equal("hello", message.Text);
        Assert.Equal(RadioRoomSamples.SentAt, message.SentAtUnixMs);
        Assert.False(message.IsDj);

        var deleted = Parse($"{{\"type\":\"radio.deleted\",\"stationId\":\"{Station}\",\"messageId\":9}}");
        Assert.Equal(9, deleted.MessageId);

        var muted = Parse($"{{\"type\":\"radio.muted\",\"stationId\":\"{Station}\",\"userId\":\"u_b\",\"mutedUntilUnixMs\":0}}");
        Assert.Equal("u_b", muted.UserId);
        Assert.Equal(0, muted.MutedUntilUnixMs);

        var pinned = Parse($"{{\"type\":\"radio.pinned\",\"stationId\":\"{Station}\",\"text\":\"Theme night\"}}");
        Assert.Equal("Theme night", pinned.Text);

        var unpinned = Parse($"{{\"type\":\"radio.pinned\",\"stationId\":\"{Station}\"}}");
        Assert.Null(unpinned.Text);

        var reaction = Parse($"{{\"type\":\"radio.reaction\",\"stationId\":\"{Station}\",\"userId\":\"u_b\",\"reaction\":5}}");
        Assert.Equal(5, reaction.Reaction);

        var requests = Parse(RadioRoomSamples.Requests(Station, false, "u_b"));
        Assert.False(requests.RequestsOpen);
        Assert.Single(requests.Requests!);
        Assert.Equal(0, requests.Requests![0].State);

        var refused = Parse($"{{\"type\":\"radio.refused\",\"stationId\":\"{Station}\",\"action\":\"radio.chat\",\"reason\":\"muted\"}}");
        Assert.Equal(SignalType.RadioChat, refused.Action);
        Assert.Equal("muted", refused.Reason);
    }

    [Fact]
    public void OutboundSignalsOmitNullRadioFields()
    {
        var json = Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(
            new CallControl { Type = SignalType.RadioChat, StationId = Station, Text = "hi" },
            TelephonyJsonContext.Default.CallControl));

        Assert.Contains("\"stationId\":\"st_01\"", json);
        Assert.Contains("\"text\":\"hi\"", json);
        Assert.DoesNotContain("\"messages\"", json);
        Assert.DoesNotContain("\"pinned\"", json);
        Assert.DoesNotContain("\"requestsOpen\"", json);
    }

    [Theory]
    [InlineData("unavailable", RadioRefusalKind.Unavailable)]
    [InlineData("cooldown", RadioRefusalKind.Cooldown)]
    [InlineData("notInRoom", RadioRefusalKind.NotInRoom)]
    [InlineData("forbidden", RadioRefusalKind.Forbidden)]
    [InlineData("banned", RadioRefusalKind.Banned)]
    [InlineData("muted", RadioRefusalKind.Muted)]
    [InlineData("empty", RadioRefusalKind.Empty)]
    [InlineData("tooLong", RadioRefusalKind.TooLong)]
    [InlineData("requestsClosed", RadioRefusalKind.RequestsClosed)]
    [InlineData("requestOpen", RadioRefusalKind.RequestOpen)]
    [InlineData("queueFull", RadioRefusalKind.QueueFull)]
    [InlineData("notFound", RadioRefusalKind.NotFound)]
    [InlineData("invalid", RadioRefusalKind.Invalid)]
    [InlineData("somethingNew", RadioRefusalKind.Unknown)]
    [InlineData(null, RadioRefusalKind.Unknown)]
    public void RefusalReasonsMapToKinds(string? reason, object expected)
    {
        Assert.Equal(expected, (object)RadioRoomWire.RefusalKindOf(reason));
    }

    [Theory]
    [InlineData(SignalType.RadioAttach, RadioRoomAction.Attach)]
    [InlineData(SignalType.RadioChat, RadioRoomAction.Chat)]
    [InlineData(SignalType.RadioDelete, RadioRoomAction.Delete)]
    [InlineData(SignalType.RadioMute, RadioRoomAction.Mute)]
    [InlineData(SignalType.RadioUnmute, RadioRoomAction.Unmute)]
    [InlineData(SignalType.RadioPin, RadioRoomAction.Pin)]
    [InlineData(SignalType.RadioUnpin, RadioRoomAction.Unpin)]
    [InlineData(SignalType.RadioReact, RadioRoomAction.React)]
    [InlineData(SignalType.RadioRequest, RadioRoomAction.Request)]
    [InlineData(SignalType.RadioRequestAccept, RadioRoomAction.RequestAccept)]
    [InlineData(SignalType.RadioRequestSkip, RadioRoomAction.RequestSkip)]
    [InlineData(SignalType.RadioRequestPlayed, RadioRoomAction.RequestPlayed)]
    [InlineData(SignalType.RadioRequestsOpen, RadioRoomAction.RequestsOpen)]
    [InlineData("radio.unknown", RadioRoomAction.Unknown)]
    public void RefusedActionsMapToActions(string type, object expected)
    {
        Assert.Equal(expected, (object)RadioRoomWire.ActionOf(type));
    }

    [Fact]
    public void RadioSignalsReachTheBusRadioChannel()
    {
        var bus = new RealtimeSignalBus();
        CallControl? received = null;
        bus.RadioReceived += control => received = control;

        bus.PublishRadio(Parse(RadioRoomSamples.Message(Station, 1, "u_b", "yo")));

        Assert.NotNull(received);
        Assert.Equal(SignalType.RadioMessage, received!.Type);
    }

    [Fact]
    public void ReportReasonCarriesTheMessageAndStaysWithinTheServerCap()
    {
        var entry = new RadioChatEntry(7, "u_b", "Bee", "bee", "@bee", null, new string('x', 300), 0, string.Empty,
            false, false);

        var reason = RadioRoomReport.ComposeReason("Harassment: " + new string('y', 300), Station, entry);

        Assert.Equal(RadioRoomReport.MaxReasonLength, reason.Length);
        Assert.StartsWith("Harassment: ", reason);
        Assert.Contains("message 7", RadioRoomReport.ComposeReason(null, Station, entry));
        Assert.Equal("user", RadioRoomReport.TargetType);
    }
}

internal static class RadioRoomSamples
{
    public const long SentAt = 1_759_500_000_000;

    public static string Room(string station, bool isDj = false, bool isModerator = false, long? mutedUntil = null)
    {
        var muted = mutedUntil is { } until ? $",\"mutedUntilUnixMs\":{until}" : string.Empty;
        return "{\"type\":\"radio.room\",\"stationId\":\"" + station + "\",\"messages\":["
            + "{\"messageId\":1,\"userId\":\"u_dj\",\"displayName\":\"Lumi\",\"handle\":\"lumi\",\"avatarUrl\":\"https://cdn/a.png\",\"text\":\"Hi all\",\"sentAtUnixMs\":" + SentAt + ",\"isDj\":true},"
            + "{\"messageId\":2,\"userId\":\"u_b\",\"displayName\":\"\",\"handle\":\"bee\",\"text\":\"hey\",\"sentAtUnixMs\":" + SentAt + ",\"isDj\":false}"
            + "],\"pinned\":\"Welcome in\",\"requests\":["
            + "{\"requestId\":4,\"userId\":\"u_b\",\"displayName\":\"Bee\",\"handle\":\"bee\",\"text\":\"Song A\",\"state\":1,\"createdAtUnixMs\":" + SentAt + "}"
            + "],\"requestsOpen\":true,\"listenerCount\":12,\"isDj\":" + (isDj ? "true" : "false")
            + ",\"isModerator\":" + (isModerator ? "true" : "false") + ",\"isLive\":true" + muted + "}";
    }

    public static string Message(string station, long messageId, string userId, string text)
    {
        return "{\"type\":\"radio.message\",\"stationId\":\"" + station + "\",\"messageId\":" + messageId
            + ",\"userId\":\"" + userId + "\",\"displayName\":\"Bee\",\"handle\":\"bee\",\"text\":\"" + text
            + "\",\"sentAtUnixMs\":" + SentAt + ",\"isDj\":false}";
    }

    public static string Requests(string station, bool open, string userId)
    {
        return "{\"type\":\"radio.requests\",\"stationId\":\"" + station + "\",\"requests\":["
            + "{\"requestId\":5,\"userId\":\"" + userId + "\",\"displayName\":\"Bee\",\"handle\":\"bee\",\"text\":\"Song B\",\"state\":0,\"createdAtUnixMs\":" + SentAt + "}"
            + "],\"requestsOpen\":" + (open ? "true" : "false") + "}";
    }
}
