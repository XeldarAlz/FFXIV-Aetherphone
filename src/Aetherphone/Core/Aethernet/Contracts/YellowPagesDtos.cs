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
    string[]? MediaKeys);

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
    bool AfterDark,
    string? MediaUrl,
    string[] MediaUrls,
    bool Saved,
    string Status,
    long CreatedAtUnix,
    long RenewedAtUnix,
    long ExpiresAtUnix) : IIdentified;

internal sealed record AdPage(AdDto[] Items, string? NextCursor);

internal sealed record SetAdSavedRequest(bool Saved);

internal sealed record SetAdOpenRequest(bool Open, int Minutes);

internal sealed record SetAdOpenResult(long OpenUntilUnix);
