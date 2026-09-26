namespace Aetherphone.Core.Aethernet.Contracts;

internal static class AdStatuses
{
    public const string Live = "live";
    public const string Expired = "expired";
    public const string Hidden = "hidden";
}

internal sealed record AdScheduleSlot(int Day, int StartMinute, int DurationMinutes);

internal sealed record CreateAdRequest(
    int Category,
    string Title,
    string Body,
    string[]? Tags,
    int Region,
    int DataCenterId,
    int WorldId,
    int TerritoryId,
    int MapId,
    float MapX,
    float MapY,
    int Ward,
    int Plot,
    string? AddressNote,
    AdScheduleSlot[]? Schedule,
    int PriceMode,
    long PriceGil,
    string? Turnaround,
    string? SlotsLine,
    string? Requirements,
    bool AfterDark,
    string[]? MediaKeys,
    string? LinkUrl = null,
    bool AllowInquiries = true,
    bool Wanted = false,
    int Accent = 0);

internal sealed record AdDto(
    string Id,
    string OwnerId,
    string OwnerName,
    string OwnerHandle,
    string OwnerAvatarUrl,
    int Archetype,
    int Category,
    string Title,
    string Body,
    string[] Tags,
    int Region,
    int DataCenterId,
    int WorldId,
    int TerritoryId,
    int MapId,
    float MapX,
    float MapY,
    int Ward,
    int Plot,
    string AddressNote,
    AdScheduleSlot[] Schedule,
    long OpenUntilUnix,
    int PriceMode,
    long PriceGil,
    string Turnaround,
    string SlotsLine,
    string Requirements,
    string LinkUrl,
    bool AfterDark,
    string? MediaUrl,
    string[] MediaUrls,
    int Views,
    bool Saved,
    string Status,
    long CreatedAtUnix,
    long RenewedAtUnix,
    long ExpiresAtUnix,
    bool AllowInquiries = true,
    int OwnerBadges = 0,
    string[]? OwnerBadgeIds = null,
    string OwnerFrameId = "",
    string? Lang = null,
    bool Wanted = false,
    int Accent = 0) : IIdentified;

internal sealed record AdPage(AdDto[] Items, string? NextCursor);

internal sealed record AdInquiryDto(
    string Id,
    string AdId,
    string AdTitle,
    string? AdMediaUrl,
    int AdCategory,
    bool Mine,
    string OtherUserId,
    string OtherName,
    string OtherHandle,
    string OtherAvatarUrl,
    string LastBody,
    string LastSenderId,
    long LastMessageAtUnix,
    int UnreadCount,
    int LastEncVersion = 0,
    string? LastCommitmentTag = null,
    string? LastMessageId = null,
    int LastKind = 0,
    int Presence = 0) : IIdentified;

internal sealed record AdInquiryPage(AdInquiryDto[] Items, string? NextCursor);

internal sealed record AdInquiryMessageDto(
    string Id,
    string InquiryId,
    string SenderId,
    string Body,
    long CreatedAtUnix,
    long ReadAtUnix,
    int EncVersion = 0,
    string? CommitmentTag = null,
    bool Deleted = false,
    int Kind = 0,
    int MediaWidth = 0,
    int MediaHeight = 0,
    string? ReplyToId = null,
    string? ReplySenderId = null,
    string? ReplyBody = null,
    int ReplyKind = 0,
    int ReplyEncVersion = 0,
    int DurationSecs = 0,
    ReactionSummaryDto[]? Reactions = null,
    long? EditedAtUnix = null) : IIdentified;

internal sealed record AdInquiryMessagePage(AdInquiryMessageDto[] Items, string? NextCursor);

internal sealed record SendAdInquiryRequest(
    string Body,
    int EncVersion = 0,
    string? CommitmentTag = null,
    int Kind = 0,
    string? MediaKey = null,
    int MediaWidth = 0,
    int MediaHeight = 0,
    string? ReplyToId = null,
    int DurationSecs = 0);

internal sealed record AdInquiryTypingDto(bool OtherTyping);

internal sealed record AdInquiryMediaUrlDto(string Url, long ExpiresAtUnix);

internal sealed record SetAdSavedRequest(bool Saved);

internal sealed record SetAdOpenRequest(bool Open, int Minutes);

internal sealed record SetAdOpenResult(long OpenUntilUnix);
