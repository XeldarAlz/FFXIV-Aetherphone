using System.Text.Json.Serialization;

namespace Aetherphone.Core.Aethernet.Contracts;

internal sealed record GlamGearDto(
    int Slot = 0,
    uint ItemId = 0,
    byte Stain0 = 0,
    byte Stain1 = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    bool Optional = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    GlamModDto[]? Mods = null);

internal sealed record GlamModDto(
    string Name = "",
    string Author = "",
    string Version = "",
    string Link = "",
    GlamOptionDto[]? Options = null);

internal sealed record GlamOptionDto(string Group = "", string[]? Picked = null);

internal sealed record GlamAuthorDto(string Id = "", string DisplayName = "", string Handle = "", string AvatarUrl = "",
    string Bio = "");

internal sealed record GlamPhotoDto(string Url = "", int Width = 0, int Height = 0);

internal sealed record GlamDto(
    string Id = "",
    GlamAuthorDto? Author = null,
    string Title = "",
    GlamPhotoDto[]? Photos = null,
    GlamGearDto[]? Gear = null,
    string[]? Tags = null,
    int Race = 0,
    int Gender = 0,
    bool Vanilla = false,
    string WeekId = "",
    int Placing = 0,
    long Likes = 0,
    bool Liked = false,
    bool Saved = false,
    long TryOns = 0,
    long CreatedAtUnix = 0,
    string InspiredBy = "") : IIdentified;

internal sealed record GlamPage(GlamDto[]? Items = null, string? NextCursor = null);

internal sealed record GlamPhotoRequest(string Key, int Width, int Height);

internal sealed record CreateGlamRequest(
    string Title,
    GlamPhotoRequest[] Photos,
    GlamGearDto[] Gear,
    string[] Tags,
    int Race,
    int Gender,
    bool Vanilla,
    string InspiredBy);

internal sealed record GlamPlacingDto(string WeekId = "", string Title = "", int Rank = 0, string Track = "");

internal sealed record GlamProfileDto(
    int GlamCount = 0,
    long TryOnsReceived = 0,
    long LikesReceived = 0,
    GlamPlacingDto[]? Placings = null,
    bool Following = false,
    GlamAuthorDto? User = null);

internal sealed record GlamWornWithDto(uint ItemId = 0, int Count = 0);

internal sealed record GlamItemDto(uint ItemId = 0, int GlamCount = 0, GlamWornWithDto[]? WornWith = null);

internal sealed record GlamStandingDto(GlamAuthorDto? User = null, long Points = 0, int Rank = 0);

internal sealed record GlamSeasonDto(
    string Id = "",
    string Name = "",
    long StartsAtUnix = 0,
    long EndsAtUnix = 0,
    GlamStandingDto[]? Standings = null,
    GlamStandingDto? Me = null);
