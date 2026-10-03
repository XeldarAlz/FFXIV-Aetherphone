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
    public const string AdPing = "ads.ping";
    public const string SocialPing = "social.ping";
    public const string MusterPing = "muster.ping";
    public const string AnnouncePing = "announce.ping";
    public const string PollPing = "poll.ping";
    public const string FeedbackPing = "feedback.ping";
    public const string ChatTyping = "chat.typing";
    public const string VelvetTyping = "velvet.typing";
    public const string GramTyping = "gram.typing";
    public const string AdTyping = "ads.typing";
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

    public const string StreamTransfer = "stream.transfer";
    public const string StreamHostChanged = "stream.hostChanged";
    public const string StreamControl = "stream.control";
    public const string StreamControlRequest = "stream.controlRequest";
    public const string StreamReact = "stream.react";
    public const string StreamReaction = "stream.reaction";

    public const string RadioPrefix = "radio.";
    public const string RadioAttach = "radio.attach";
    public const string RadioDetach = "radio.detach";
    public const string RadioChat = "radio.chat";
    public const string RadioDelete = "radio.delete";
    public const string RadioMute = "radio.mute";
    public const string RadioUnmute = "radio.unmute";
    public const string RadioPin = "radio.pin";
    public const string RadioUnpin = "radio.unpin";
    public const string RadioReact = "radio.react";
    public const string RadioRequest = "radio.request";
    public const string RadioRequestAccept = "radio.requestAccept";
    public const string RadioRequestSkip = "radio.requestSkip";
    public const string RadioRequestPlayed = "radio.requestPlayed";
    public const string RadioRequestsOpen = "radio.requestsOpen";
    public const string RadioRoom = "radio.room";
    public const string RadioPresence = "radio.presence";
    public const string RadioMessage = "radio.message";
    public const string RadioDeleted = "radio.deleted";
    public const string RadioMuted = "radio.muted";
    public const string RadioPinned = "radio.pinned";
    public const string RadioReaction = "radio.reaction";
    public const string RadioRequests = "radio.requests";
    public const string RadioRefused = "radio.refused";
    public const string JamPrefix = "jam.";
    public const string JamStart = "jam.start";
    public const string JamJoin = "jam.join";
    public const string JamLeave = "jam.leave";
    public const string JamEnd = "jam.end";
    public const string JamKick = "jam.kick";
    public const string JamTransfer = "jam.transfer";
    public const string JamApprove = "jam.approve";
    public const string JamDeny = "jam.deny";
    public const string JamSettings = "jam.settings";
    public const string JamState = "jam.state";
    public const string JamControl = "jam.control";
    public const string JamQueueAdd = "jam.queueAdd";
    public const string JamQueueRemove = "jam.queueRemove";
    public const string JamQueueMove = "jam.queueMove";
    public const string JamQueueAdvance = "jam.queueAdvance";
    public const string JamReact = "jam.react";
    public const string JamInvite = "jam.invite";
    public const string JamJoined = "jam.joined";
    public const string JamDeclined = "jam.declined";
    public const string JamJoinPending = "jam.joinPending";
    public const string JamJoinRequest = "jam.joinRequest";
    public const string JamJoinCancelled = "jam.joinCancelled";
    public const string JamRoster = "jam.roster";
    public const string JamQueue = "jam.queue";
    public const string JamHostChanged = "jam.hostChanged";
    public const string JamControlRequest = "jam.controlRequest";
    public const string JamReaction = "jam.reaction";
    public const string JamKicked = "jam.kicked";
    public const string JamEnded = "jam.ended";
    public const string JamInvited = "jam.invited";
    public const string JamChat = "jam.chat";
    public const string JamMessage = "jam.message";
    public const string JamDeleteMessage = "jam.deleteMessage";
    public const string JamMessageDeleted = "jam.messageDeleted";
    public const string JamRefused = "jam.refused";
    public const string JamNearby = "jam.nearby";
    public const string JamNearbyRoster = "jam.nearby.roster";
}

internal static class StreamPermission
{
    public const int AddToQueue = 1;
    public const int ControlPlayback = 2;
    public const int CanHost = 4;
    public const int GrantMask = AddToQueue | ControlPlayback;
}

internal static class StreamFeature
{
    public const int Party = 1;
}

internal static class StreamControlAction
{
    public const string Play = "play";
    public const string Pause = "pause";
    public const string Seek = "seek";
    public const string Next = "next";
}

internal static class StreamDeclineReason
{
    public const string Denied = "denied";
    public const string Full = "full";
    public const string BadCode = "code";
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

internal sealed record StreamMember(string UserId, int Flags);

internal sealed record RadioChatMessage(
    long MessageId,
    string UserId,
    string DisplayName,
    string Handle,
    string? AvatarUrl,
    string Text,
    long SentAtUnixMs,
    bool IsDj);

internal sealed record RadioSongRequest(
    long RequestId,
    string UserId,
    string DisplayName,
    string Handle,
    string? AvatarUrl,
    string Text,
    int State,
    long CreatedAtUnixMs);
internal sealed record JamTrack(string VideoId, string? Title = null, string? Author = null,
    string? ThumbnailUrl = null, double? DurationSeconds = null);

internal sealed record JamQueueEntry(int EntryId, JamTrack Track, string AddedByUserId = "", string AddedByName = "");

internal sealed record JamMember(string UserId, string DisplayName = "", string Handle = "", string? AvatarUrl = null,
    bool IsHost = false, int Permissions = 0);

internal sealed record JamNearbyInfo(string JamId, string Code, string? Title = null, string HostId = "",
    string HostDisplayName = "", string HostHandle = "", string? HostAvatarUrl = null, int MemberCount = 0,
    JamTrack? Track = null);

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
    public float? ScreenPitch { get; init; }
    public float? ScreenRoll { get; init; }
    public float? ScreenCurve { get; init; }

    public string? Code { get; init; }
    public bool? CodeEnabled { get; init; }
    public int? Features { get; init; }
    public int? GuestPermissions { get; init; }
    public StreamMember[]? Members { get; init; }
    public string? Action { get; init; }
    public int? Reaction { get; init; }

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

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StationId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? MessageId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? RequestId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Minutes { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Open { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DisplayName { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Handle { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AvatarUrl { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? SentAtUnixMs { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsDj { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsModerator { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsLive { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ListenerCount { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? RequestsOpen { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Pinned { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? MutedUntilUnixMs { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RadioChatMessage[]? Messages { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RadioSongRequest[]? Requests { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? JamId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JamTrack? Track { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? QueueVersion { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JamQueueEntry[]? Entries { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? EntryId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ToIndex { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Mode { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Stale { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JamMember[]? JamMembers { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JamNearbyInfo[]? NearbyJams { get; init; }
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
