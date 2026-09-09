using System.Text.Json.Serialization;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Telephony.Contracts;

internal static class SignalType
{
    public const string Hello = "hello";
    public const string Start = "call.start";
    public const string Invite = "call.invite";
    public const string Accept = "call.accept";
    public const string Decline = "call.decline";
    public const string Cancel = "call.cancel";
    public const string Leave = "call.leave";
    public const string Rejoin = "call.rejoin";
    public const string Mute = "call.mute";
    public const string Incoming = "call.incoming";
    public const string Ringing = "call.ringing";
    public const string Roster = "call.roster";
    public const string Accepted = "call.accepted";
    public const string Declined = "call.declined";
    public const string Left = "call.left";
    public const string Ended = "call.ended";
    public const string Handled = "call.handled";
    public const string Unavailable = "call.unavailable";
    public const string ContentRemoved = "content.removed";
    public const string KeysStale = "keys.stale";
    public const string KeysLinkPending = "keys.linkPending";
    public const string ChatPing = "chat.ping";
    public const string VelvetPing = "velvet.ping";
    public const string GramPing = "gram.ping";
    public const string SocialPing = "social.ping";
    public const string MusterPing = "muster.ping";
    public const string AnnouncePing = "announce.ping";
    public const string PollPing = "poll.ping";
    public const string CasinoPrefix = "casino.";
    public const string CasinoAttach = "casino.attach";
    public const string CasinoDetach = "casino.detach";
    public const string CasinoResync = "casino.resync";
    public const string CasinoAttached = "casino.attached";
    public const string CasinoDeclined = "casino.declined";
    public const string CasinoSnapshot = "casino.snapshot";
    public const string CasinoEvent = "casino.event";
    public const string CasinoPrivate = "casino.private";
    public const string CasinoEnded = "casino.ended";
    public const string CasinoPing = "casino.ping";
    public const string GamePrefix = "game.";
    public const string GameAttach = "game.attach";
    public const string GameDetach = "game.detach";
    public const string GameResync = "game.resync";
    public const string GameClaim = "game.claim";
    public const string GameAttached = "game.attached";
    public const string GameDeclined = "game.declined";
    public const string GameSnapshot = "game.snapshot";
    public const string GameEvent = "game.event";
    public const string GamePrivate = "game.private";
    public const string GameHandled = "game.handled";
    public const string GameEnded = "game.ended";
    public const string Error = "error";

    public const string StreamPrefix = "stream.";
    public const string StreamState = "stream.state";
    public const string StreamJoin = "stream.join";
    public const string StreamLeave = "stream.leave";
    public const string StreamJoined = "stream.joined";
    public const string StreamDeclined = "stream.declined";
    public const string StreamRoster = "stream.roster";
    public const string StreamLeft = "stream.left";
    public const string StreamEnded = "stream.ended";

    public const string StreamJoinRequest = "stream.joinRequest";
    public const string StreamApprove = "stream.approve";
    public const string StreamDeny = "stream.deny";
    public const string StreamJoinPending = "stream.joinPending";

    public const string StreamQueueSuggest = "stream.queueSuggest";
    public const string StreamQueueSuggestion = "stream.queueSuggestion";
    public const string StreamQueueApprove = "stream.queueApprove";
    public const string StreamQueueDeny = "stream.queueDeny";
    public const string StreamQueueSuggestionResult = "stream.queueSuggestionResult";

    public const string StreamPlaybackFailed = "stream.playbackFailed";
    public const string StreamViewerFailed = "stream.viewerFailed";

    public const string StreamKick = "stream.kick";
    public const string StreamKicked = "stream.kicked";

    public const string StreamNearby = "stream.nearby";
    public const string StreamNearbyRoster = "stream.nearby.roster";
}

internal static class ParticipantState
{
    public const string Ringing = "ringing";
    public const string Active = "active";
    public const string Left = "left";
}

internal sealed record ParticipantInfo(
    string UserId,
    string Name,
    string World,
    string DisplayName,
    int Slot,
    string State,
    bool Muted,
    string Handle = "",
    string? AvatarUrl = null);

internal sealed record NearbyStreamInfo(string HostId, string Name, string World, string DisplayName,
    string Handle = "", string? AvatarUrl = null);

internal sealed record StreamQueueEntry(string? Url, string? Title);

internal sealed record CallControl
{
    public string Type { get; init; } = string.Empty;
    public string? CallId { get; init; }
    public string[]? InviteeIds { get; init; }
    public ParticipantInfo? From { get; init; }
    public ParticipantInfo[]? Participants { get; init; }
    public string? UserId { get; init; }
    public bool? Muted { get; init; }
    public string? Reason { get; init; }

    public string? HostId { get; init; }
    public string? Url { get; init; }
    public double? PositionSeconds { get; init; }
    public long? StateAtUnixMs { get; init; }
    public bool? Paused { get; init; }
    public StreamQueueEntry[]? UpcomingQueue { get; init; }

    public bool? ApprovalRequired { get; init; }

    public string? SuggestionId { get; init; }

    public float? ScreenX { get; init; }
    public float? ScreenY { get; init; }
    public float? ScreenZ { get; init; }
    public float? ScreenYaw { get; init; }
    public float? ScreenScale { get; init; }

    public uint? TerritoryId { get; init; }
    public uint? WorldId { get; init; }
    public bool? Discoverable { get; init; }
    public NearbyStreamInfo[]? NearbyStreams { get; init; }

    public string? App { get; init; }
    public string? ContentKind { get; init; }
    public string? ContentId { get; init; }
    public string? ParentId { get; init; }
    public ChatMessageDto? Message { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CasinoPayload? Casino { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GamePayload? Game { get; init; }
}

internal sealed record CasinoPayload
{
    public string RoomId { get; init; } = string.Empty;
    public int Epoch { get; init; }
    public long Seq { get; init; }

    public long PairSeq { get; init; }

    public long ServerNowUnixMs { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EventKind { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CasinoRoomSnapshotDto? Snapshot { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CasinoRoomEventDto? Event { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CasinoPrivateDto? Private { get; init; }
}

internal sealed record GamePayload
{
    public string RoomId { get; init; } = string.Empty;
    public int Epoch { get; init; }
    public long Seq { get; init; }

    public long PairSeq { get; init; }

    public long ServerNowUnixMs { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EventKind { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GameRoomSnapshotDto? Snapshot { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GameRoomEventDto? Event { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GamePrivateDto? Private { get; init; }
}
