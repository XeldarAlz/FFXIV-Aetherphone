namespace Aetherphone.Core.Aethernet.Contracts;

internal sealed record CreateMusterRequest(
    int Category,
    string Description,
    int TerritoryId,
    int MapId,
    float MapX,
    float MapY,
    int WorldId,
    int Ward,
    int Plot,
    int Room,
    string Spot,
    int Region,
    int StartInMinutes,
    int DurationMinutes,
    int MaxAttendees,
    bool UnlistWhenFull,
    bool IsPublic);

internal sealed record MusterDto(
    string Id,
    string HostId,
    string HostDisplayName,
    string HostHandle,
    string? HostAvatarUrl,
    int Category,
    string Description,
    int TerritoryId,
    int MapId,
    float MapX,
    float MapY,
    int WorldId,
    int Ward,
    int Plot,
    int Room,
    string Spot,
    int Region,
    long StartsAtUnix,
    long EndsAtUnix,
    int RsvpCount,
    int MaxAttendees,
    bool UnlistWhenFull,
    bool IsPublic,
    bool Going,
    long CreatedAtUnix) : IIdentified;

internal sealed record MusterPage(MusterDto[] Items, string? NextCursor);

internal sealed record SetMusterRsvpRequest(bool Going);

internal sealed record MusterRsvpResult(bool Going, int RsvpCount);

internal sealed record MusterAttendeeDto(
    string UserId,
    string DisplayName,
    string Handle,
    string? AvatarUrl,
    long CreatedAtUnix);

internal sealed record MusterSync(
    MusterDto? Mine,
    MusterAttendeeDto[] MineAttendees,
    MusterDto[] ContactMusters,
    string[] GoingMusterIds);
