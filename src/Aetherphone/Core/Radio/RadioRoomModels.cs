using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Radio;

internal enum RadioRoomStatus : byte
{
    Idle,
    Attaching,
    Attached,
    Unavailable,
}

internal enum RadioRefusalKind : byte
{
    Unknown,
    Unavailable,
    Cooldown,
    NotInRoom,
    Forbidden,
    Banned,
    Muted,
    Empty,
    TooLong,
    RequestsClosed,
    RequestOpen,
    QueueFull,
    NotFound,
    Invalid,
}

internal enum RadioRoomAction : byte
{
    Unknown,
    Attach,
    Chat,
    Delete,
    Mute,
    Unmute,
    Pin,
    Unpin,
    React,
    Request,
    RequestAccept,
    RequestSkip,
    RequestPlayed,
    RequestsOpen,
}

internal enum RadioRequestState : byte
{
    Pending,
    Accepted,
}

internal readonly record struct RadioRefusal(RadioRoomAction Action, RadioRefusalKind Kind);

internal readonly record struct RadioReactionPulse(int Reaction, long AtTick, bool IsMine);

internal readonly record struct RadioInbound(CallControl? Control, bool Connected);

internal sealed record RadioChatEntry(
    long MessageId,
    string UserId,
    string DisplayName,
    string Handle,
    string HandleLabel,
    string? AvatarUrl,
    string Text,
    long SentAtUnixMs,
    string TimeLabel,
    bool IsDj,
    bool IsMine);

internal sealed record RadioRequestEntry(
    long RequestId,
    string UserId,
    string DisplayName,
    string Handle,
    string HandleLabel,
    string? AvatarUrl,
    string Text,
    RadioRequestState State,
    long CreatedAtUnixMs,
    bool IsMine);

internal static class RadioRoomWire
{
    public static RadioRefusalKind RefusalKindOf(string? reason)
    {
        return reason switch
        {
            "unavailable" => RadioRefusalKind.Unavailable,
            "cooldown" => RadioRefusalKind.Cooldown,
            "notInRoom" => RadioRefusalKind.NotInRoom,
            "forbidden" => RadioRefusalKind.Forbidden,
            "banned" => RadioRefusalKind.Banned,
            "muted" => RadioRefusalKind.Muted,
            "empty" => RadioRefusalKind.Empty,
            "tooLong" => RadioRefusalKind.TooLong,
            "requestsClosed" => RadioRefusalKind.RequestsClosed,
            "requestOpen" => RadioRefusalKind.RequestOpen,
            "queueFull" => RadioRefusalKind.QueueFull,
            "notFound" => RadioRefusalKind.NotFound,
            "invalid" => RadioRefusalKind.Invalid,
            _ => RadioRefusalKind.Unknown,
        };
    }

    public static RadioRoomAction ActionOf(string? type)
    {
        return type switch
        {
            SignalType.RadioAttach => RadioRoomAction.Attach,
            SignalType.RadioChat => RadioRoomAction.Chat,
            SignalType.RadioDelete => RadioRoomAction.Delete,
            SignalType.RadioMute => RadioRoomAction.Mute,
            SignalType.RadioUnmute => RadioRoomAction.Unmute,
            SignalType.RadioPin => RadioRoomAction.Pin,
            SignalType.RadioUnpin => RadioRoomAction.Unpin,
            SignalType.RadioReact => RadioRoomAction.React,
            SignalType.RadioRequest => RadioRoomAction.Request,
            SignalType.RadioRequestAccept => RadioRoomAction.RequestAccept,
            SignalType.RadioRequestSkip => RadioRoomAction.RequestSkip,
            SignalType.RadioRequestPlayed => RadioRoomAction.RequestPlayed,
            SignalType.RadioRequestsOpen => RadioRoomAction.RequestsOpen,
            _ => RadioRoomAction.Unknown,
        };
    }

    public static RadioRequestState RequestStateOf(int state)
    {
        return state == 1 ? RadioRequestState.Accepted : RadioRequestState.Pending;
    }

    public static string HandleLabelOf(string handle)
    {
        return handle.Length == 0 ? string.Empty : string.Concat("@", handle);
    }

    public static string PublicNameOf(string displayName, string handle)
    {
        return displayName.Length > 0 ? displayName : handle;
    }

    public static RadioChatEntry BuildEntry(long messageId, string userId, string displayName, string handle,
        string? avatarUrl, string text, long sentAtUnixMs, string timeLabel, bool isDj, string? me)
    {
        return new RadioChatEntry(messageId, userId, PublicNameOf(displayName, handle), handle, HandleLabelOf(handle),
            avatarUrl, text, sentAtUnixMs, timeLabel, isDj, string.Equals(userId, me, StringComparison.Ordinal));
    }
}

internal static class RadioRoomReport
{
    public const string TargetType = "user";
    public const int MaxReasonLength = 500;
    public const string JamEvidenceTag = "Jam chat";
    private const string EvidenceTag = "Community Radio chat";

    public static string ComposeReason(string? reason, string stationId, RadioChatEntry entry)
    {
        return Compose(reason, EvidenceTag, stationId, entry);
    }

    public static string Compose(string? reason, string evidenceTag, string scopeId, RadioChatEntry entry)
    {
        var evidence = $"{evidenceTag} ({scopeId}, message {entry.MessageId}): \"{entry.Text}\"";
        var composed = string.IsNullOrWhiteSpace(reason) ? evidence : string.Concat(reason.Trim(), " | ", evidence);
        return composed.Length <= MaxReasonLength ? composed : composed[..MaxReasonLength];
    }
}
