namespace Aetherphone.Core.Aethernet.Contracts;

internal sealed record GramThreadDto(
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
    int LastMessageKind = 0) : IIdentified;

internal sealed record GramThreadPage(GramThreadDto[] Items, string? NextCursor);

internal sealed record GramMessageDto(
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
    long? EditedAtUnix = null) : IIdentified;

internal sealed record GramMessagePage(GramMessageDto[] Items, string? NextCursor);

internal sealed record SendGramMessageRequest(
    string Body,
    int Kind,
    string? MediaKey = null,
    int MediaWidth = 0,
    int MediaHeight = 0,
    int EncVersion = 0,
    string? CommitmentTag = null,
    string? ReplyToId = null,
    int DurationSecs = 0);

internal sealed record GramMediaUrlDto(string Url, long ExpiresAtUnix);

internal sealed record GramTypingDto(bool OtherTyping);
