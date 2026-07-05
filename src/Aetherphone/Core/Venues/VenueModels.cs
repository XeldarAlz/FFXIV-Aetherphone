using System.Text.Json.Serialization;

namespace Aetherphone.Core.Venues;

internal enum VenueState : byte
{
    Idle,
    Loading,
    Ready,
    Failed,
}

internal enum VenueSource : byte
{
    FfxivVenues,
    Partake,
}

internal enum VenueTimeFilter : byte
{
    LiveNow,
    Today,
    Upcoming,
    All,
}

internal sealed class VenueEvent
{
    public required string Id { get; init; }
    public required VenueSource Source { get; init; }
    public required string Title { get; init; }
    public required string Host { get; init; }
    public required string Description { get; init; }
    public required string DataCenter { get; init; }
    public required string World { get; init; }
    public required string LocationLine { get; init; }
    public required string? TeleportCode { get; init; }
    public required string? BannerUrl { get; init; }
    public required string? IconUrl { get; init; }
    public required DateTime StartUtc { get; init; }
    public required DateTime? EndUtc { get; init; }
    public required bool Recurring { get; init; }
    public required IReadOnlyList<string> Tags { get; init; }
    public required string Url { get; init; }
    public required string? DiscordUrl { get; init; }
    public required int AttendeeCount { get; init; }
    public bool IsLive(DateTime nowUtc) => StartUtc <= nowUtc && (EndUtc is null || EndUtc > nowUtc);
    public bool CanTeleport => !string.IsNullOrEmpty(TeleportCode);
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
