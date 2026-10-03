using System.Text.Json.Serialization;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Aethernet.Contracts;

internal sealed record ListeningUpdateRequest(
    string VideoId,
    string Title,
    string Author,
    string ThumbnailUrl,
    double DurationSeconds,
    double PositionSeconds,
    bool Paused,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? JamCode = null);

internal sealed record ListeningFriendDto(
    string UserId,
    string DisplayName = "",
    string Handle = "",
    string? AvatarUrl = null,
    JamTrack? Track = null,
    double PositionSeconds = 0,
    bool Paused = false,
    long UpdatedAtUnixMs = 0,
    string? JamCode = null);

internal sealed record ListeningFriendsPage(ListeningFriendDto[]? Friends = null);
