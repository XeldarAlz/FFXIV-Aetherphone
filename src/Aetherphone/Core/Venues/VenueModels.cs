using System.Text.Json.Serialization;

namespace Aetherphone.Core.Venues;

internal enum VenueState : byte
{
    Idle,
    Loading,
    Ready,
    Failed,
}

[Flags]
internal enum VenueSources : byte
{
    None = 0,
    FfxivVenues = 1,
    Partake = 2,
    Rolladeck = 4,
}

internal enum VenueTimeFilter : byte
{
    LiveNow,
    Today,
    Upcoming,
    All,
}

internal enum VenueLiveState : byte
{
    None,
    Scheduled,
    Confirmed,
}

internal sealed record VenueDj(string Name, string? AvatarUrl, int Viewers, string? TwitchUrl, string VenueId)
{
    public IReadOnlyList<string> Genres { get; init; } = Array.Empty<string>();
}

internal readonly record struct VenueSnapshot(VenueEvent[] Events, VenueDj[] Djs);

internal sealed record VenueEvent
{
    private static readonly TimeSpan OpenEndedWindow = TimeSpan.FromHours(4);

    public required string Id { get; init; }
    public required VenueSources Sources { get; init; }
    public required string Title { get; init; }
    public required string Host { get; init; }
    public required string Description { get; init; }
    public required string DataCenter { get; init; }
    public required string World { get; init; }
    public required string LocationLine { get; init; }
    public required string PlaceLine { get; init; }
    public required string? TeleportCode { get; init; }
    public required string? BannerUrl { get; init; }
    public required string? LogoUrl { get; init; }
    public required DateTime? StartUtc { get; init; }
    public required DateTime? EndUtc { get; init; }
    public required IReadOnlyList<string> Tags { get; init; }
    public required string? WebsiteUrl { get; init; }
    public required string? DiscordUrl { get; init; }
    public required string? ListingUrl { get; init; }
    public required int AttendeeCount { get; init; }
    public VenueAddress Address { get; init; }
    public DateTime? EventStartUtc { get; init; }
    public DateTime? EventEndUtc { get; init; }
    public string EventName { get; init; } = string.Empty;
    public string? RolladeckUrl { get; init; }
    public string? TwitchUrl { get; init; }
    public string LiveHeadline { get; init; } = string.Empty;
    public string LiveTitle { get; init; } = string.Empty;
    public IReadOnlyList<string> LiveGenres { get; init; } = Array.Empty<string>();
    public int LiveViewers { get; init; }
    public DateTime LiveConfirmedUntilUtc { get; init; }
    public DateTime DjLiveUntilUtc { get; init; }

    public bool CanTeleport => !string.IsNullOrEmpty(TeleportCode);
    public bool HasOpening => StartUtc.HasValue;
    public bool IsEvent => EventStartUtc.HasValue;

    public bool IsConfirmedLive(DateTime nowUtc) => nowUtc < LiveConfirmedUntilUtc;

    public bool HasLiveDj(DateTime nowUtc) => nowUtc < DjLiveUntilUtc;

    public bool IsScheduledOpen(DateTime nowUtc) => StartUtc is { } start && IsOpen(start, EndUtc, nowUtc);

    public bool IsEventOn(DateTime nowUtc) => EventStartUtc is { } start && IsOpen(start, EventEndUtc, nowUtc);

    private static bool IsOpen(DateTime start, DateTime? end, DateTime nowUtc)
    {
        if (start > nowUtc)
        {
            return false;
        }

        return end is { } endUtc ? endUtc > nowUtc : nowUtc - start < OpenEndedWindow;
    }

    public bool IsLive(DateTime nowUtc) => IsConfirmedLive(nowUtc) || IsScheduledOpen(nowUtc);

    public VenueLiveState LiveState(DateTime nowUtc)
    {
        if (IsConfirmedLive(nowUtc))
        {
            return VenueLiveState.Confirmed;
        }

        return IsScheduledOpen(nowUtc) ? VenueLiveState.Scheduled : VenueLiveState.None;
    }
}

internal sealed class GraphQlRequest
{
    [JsonPropertyName("query")] public string Query { get; set; } = string.Empty;
}

internal sealed class PartakeEnvelope
{
    [JsonPropertyName("data")] public PartakeData? Data { get; set; }
}

internal sealed class PartakeData
{
    [JsonPropertyName("events")] public PartakeEventDto[]? Events { get; set; }
}

internal sealed class PartakeEventDto
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("location")] public string? Location { get; set; }
    [JsonPropertyName("tags")] public string[]? Tags { get; set; }
    [JsonPropertyName("ageRating")] public string? AgeRating { get; set; }
    [JsonPropertyName("startsAt")] public DateTimeOffset? StartsAt { get; set; }
    [JsonPropertyName("endsAt")] public DateTimeOffset? EndsAt { get; set; }
    [JsonPropertyName("attendeeCount")] public int AttendeeCount { get; set; }
    [JsonPropertyName("locationData")] public PartakeLocationDto? LocationData { get; set; }
    [JsonPropertyName("team")] public PartakeTeamDto? Team { get; set; }
}

internal sealed class PartakeLocationDto
{
    [JsonPropertyName("server")] public PartakeServerDto? Server { get; set; }
    [JsonPropertyName("dataCenter")] public PartakeDataCenterDto? DataCenter { get; set; }
}

internal sealed class PartakeServerDto
{
    [JsonPropertyName("name")] public string? Name { get; set; }
}

internal sealed class PartakeDataCenterDto
{
    [JsonPropertyName("name")] public string? Name { get; set; }
}

internal sealed class PartakeTeamDto
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("iconUrl")] public string? IconUrl { get; set; }
    [JsonPropertyName("websiteUrl")] public string? WebsiteUrl { get; set; }
    [JsonPropertyName("discordUrl")] public string? DiscordUrl { get; set; }
}

internal sealed class FfxivVenueDto
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("bannerUri")] public string? BannerUri { get; set; }
    [JsonPropertyName("description")] public string[]? Description { get; set; }
    [JsonPropertyName("location")] public FfxivLocationDto? Location { get; set; }
    [JsonPropertyName("website")] public string? Website { get; set; }
    [JsonPropertyName("discord")] public string? Discord { get; set; }
    [JsonPropertyName("sfw")] public bool Sfw { get; set; }
    [JsonPropertyName("tags")] public string[]? Tags { get; set; }
    [JsonPropertyName("schedule")] public FfxivScheduleDto[]? Schedule { get; set; }

    [JsonPropertyName("scheduleOverrides")]
    public FfxivOverrideDto[]? ScheduleOverrides { get; set; }
}

internal sealed class FfxivLocationDto
{
    [JsonPropertyName("dataCenter")] public string? DataCenter { get; set; }
    [JsonPropertyName("world")] public string? World { get; set; }
    [JsonPropertyName("district")] public string? District { get; set; }
    [JsonPropertyName("ward")] public int Ward { get; set; }
    [JsonPropertyName("plot")] public int Plot { get; set; }
    [JsonPropertyName("apartment")] public int Apartment { get; set; }
    [JsonPropertyName("room")] public int Room { get; set; }
    [JsonPropertyName("subdivision")] public bool Subdivision { get; set; }
    [JsonPropertyName("override")] public string? Override { get; set; }
}

internal sealed class FfxivScheduleDto
{
    [JsonPropertyName("resolution")] public FfxivResolutionDto? Resolution { get; set; }
}

internal sealed class FfxivResolutionDto
{
    [JsonPropertyName("start")] public DateTimeOffset? Start { get; set; }
    [JsonPropertyName("end")] public DateTimeOffset? End { get; set; }
    [JsonPropertyName("isNow")] public bool IsNow { get; set; }
}

internal sealed class FfxivOverrideDto
{
    [JsonPropertyName("open")] public bool Open { get; set; }
    [JsonPropertyName("start")] public DateTimeOffset? Start { get; set; }
    [JsonPropertyName("end")] public DateTimeOffset? End { get; set; }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(GraphQlRequest))]
[JsonSerializable(typeof(PartakeEnvelope))]
[JsonSerializable(typeof(FfxivVenueDto[]))]
internal sealed partial class VenueJsonContext : JsonSerializerContext
{
}
