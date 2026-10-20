namespace Aetherphone.Core.Aethernet.Contracts;

internal sealed record DmThreadDto(
    string Id,
    string OtherUserId,
    string OtherDisplayName,
    string OtherHandle,
    string? OtherAvatarUrl,
    long LastMessageAtUnix,
    string LastMessagePreview,
    int UnreadCount,
    int Presence,
    int? UtcOffsetMinutes = null,
    int LastMessageEncVersion = 0,
    string LastMessageSenderId = "",
    bool Pending = false,
    int LastMessageKind = 0,
    bool LastMessageSeen = false,
    string LastMessageId = "") : IIdentified;

internal sealed record DmThreadPage(DmThreadDto[] Items, string? NextCursor);

internal sealed record DmMessageDto(
    string Id,
    string ThreadId,
    string SenderId,
    string Body,
    int Kind,
    long CreatedAtUnix,
    int MediaWidth = 0,
    int MediaHeight = 0,
    long? ReadAtUnix = null,
    int EncVersion = 0,
    string? CommitmentTag = null,
    string? ReplyToId = null,
    string? ReplySenderId = null,
    string? ReplyBody = null,
    int ReplyKind = 0,
    int ReplyEncVersion = 0,
    bool Deleted = false,
    int DurationSecs = 0,
    ReactionSummaryDto[]? Reactions = null,
    long? EditedAtUnix = null,
    string? StoryId = null,
    string? StoryMediaUrl = null,
    bool StoryExpired = false) : IDirectMessage;

internal sealed record DmMessagePage(DmMessageDto[] Items, string? NextCursor);

internal sealed record SendDmMessageRequest(
    string Body,
    int Kind,
    string? MediaKey = null,
    int MediaWidth = 0,
    int MediaHeight = 0,
    int EncVersion = 0,
    string? CommitmentTag = null,
    string? ReplyToId = null,
    int DurationSecs = 0,
    string? StoryId = null);

internal sealed record DmMediaUrlDto(string Url, long ExpiresAtUnix);

internal sealed record DmTypingDto(bool OtherTyping);
