using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Aetherphone.Core.Hunts;

internal sealed class HuntsRefreshRequest
{
    [JsonPropertyName("sessionId")]
    public string? SessionId { get; set; }
}

internal sealed class HuntsRefreshResponse
{
    [JsonPropertyName("data")]
    public HuntsRefreshData? Data { get; set; }
}

internal sealed class HuntsRefreshData
{
    [JsonPropertyName("token")]
    public string? Token { get; set; }

    [JsonPropertyName("sessionId")]
    public string? SessionId { get; set; }
}

internal sealed class HuntsLoginRequest
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("rememberMe")]
    public bool RememberMe { get; set; }

    [JsonPropertyName("sessionId")]
    public string? SessionId { get; set; }
}

internal sealed class HuntsLoginResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public HuntsLoginData? Data { get; set; }
}

internal sealed class HuntsLoginData
{
    [JsonPropertyName("sessionId")]
    public string? SessionId { get; set; }

    [JsonPropertyName("token")]
    public string? Token { get; set; }
}

internal sealed class HuntsDataCenterResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public HuntsDataCenterData? Data { get; set; }
}

internal sealed class HuntsDataCenterData
{
    [JsonPropertyName("status")]
    public HuntsStatus? Status { get; set; }
}

internal sealed class HuntsStatus
{
    [JsonPropertyName("windows")]
    public HuntWindowDto[]? Windows { get; set; }

    [JsonPropertyName("spawns")]
    public HuntSpawnEntryDto[]? Spawns { get; set; }

    [JsonPropertyName("sightings")]
    public JsonNode[]? Sightings { get; set; }

    [JsonPropertyName("theory")]
    public JsonNode? Theory { get; set; }
}

internal sealed class HuntWindowDto
{
    [JsonPropertyName("num")]
    public int Num { get; set; }

    [JsonPropertyName("startedAt")]
    public DateTimeOffset? StartedAtNormal { get; set; }

    [JsonPropertyName("startedAtSniped2")]
    public DateTimeOffset? StartedAtSniped { get; set; }

    [JsonPropertyName("prevStartedAt")]
    public DateTimeOffset? PrevStartedAt { get; set; }

    [JsonPropertyName("snipedNum")]
    public int? SnipedNum { get; set; }

    [JsonPropertyName("mobId2")]
    public string MobId { get; set; } = string.Empty;

    [JsonPropertyName("worldId2")]
    public string WorldId { get; set; } = string.Empty;

    [JsonPropertyName("zoneInstance")]
    public int ZoneInstance { get; set; }

    public bool UseMaintenanceTiming { get; init; }

    public bool IsSniped => StartedAtSniped is not null || SnipedNum is not null;

    public DateTimeOffset StartedAt => StartedAtSniped ?? StartedAtNormal ?? default;
}

internal sealed class HuntSpawnEntryDto
{
    [JsonPropertyName("mobId2")]
    public string MobId { get; set; } = string.Empty;

    [JsonPropertyName("worldId2")]
    public string WorldId { get; set; } = string.Empty;

    [JsonPropertyName("zoneInstance")]
    public int ZoneInstance { get; set; }

    [JsonPropertyName("zonePoiIds")]
    public int[]? ZonePoiIds { get; set; }

    [JsonPropertyName("isScheduled")]
    public bool IsScheduled { get; set; }

    [JsonPropertyName("window")]
    public int? WindowNum { get; set; }

    [JsonPropertyName("stage")]
    public int? PhaseNum { get; set; }

    [JsonPropertyName("zoneId2")]
    public string? ZoneId { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset? Timestamp { get; set; }
}

internal sealed class HuntsRecentLogsRequest
{
    [JsonPropertyName("dataCenterId")]
    public string DataCenterId { get; set; } = string.Empty;

    [JsonPropertyName("scopes")]
    public HuntsRecentLogsScope[] Scopes { get; set; } = Array.Empty<HuntsRecentLogsScope>();
}

internal sealed class HuntsRecentLogsScope
{
    [JsonPropertyName("worldIds")]
    public string[] WorldIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("mobIds")]
    public string[] MobIds { get; set; } = Array.Empty<string>();
}

internal sealed class HuntsRecentLogsResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public HuntsRecentLogsData? Data { get; set; }
}

internal sealed class HuntsRecentLogsData
{
    [JsonPropertyName("logs")]
    public HuntLogEntryDto[]? Logs { get; set; }
}

internal sealed class HuntLogEntryDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("mobId2")]
    public string MobId { get; set; } = string.Empty;

    [JsonPropertyName("worldId2")]
    public string WorldId { get; set; } = string.Empty;

    [JsonPropertyName("zoneInstance")]
    public int? ZoneInstance { get; set; }

    [JsonPropertyName("spawnedAt")]
    public DateTimeOffset? SpawnedAt { get; set; }

    [JsonPropertyName("killedAt")]
    public DateTimeOffset? KilledAt { get; set; }

    [JsonPropertyName("zoneId2")]
    public string? ZoneId { get; set; }

    [JsonPropertyName("isFailed")]
    public bool IsFailed { get; set; }
}

internal sealed class HuntsAppSessionResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public HuntsAppSessionData? Data { get; set; }
}

internal sealed class HuntsAppSessionData
{
    [JsonPropertyName("status")]
    public HuntsAppStatus? Status { get; set; }

    [JsonPropertyName("session")]
    public HuntsAppSessionInfo? Session { get; set; }
}

internal sealed class HuntsAppStatus
{
    [JsonPropertyName("maintenance")]
    public HuntsMaintenanceInfo? Maintenance { get; set; }

    [JsonPropertyName("zones")]
    public Dictionary<string, HuntsZoneInfo>? Zones { get; set; }
}

internal sealed class HuntsMaintenanceInfo
{
    [JsonPropertyName("restarts")]
    public HuntsMaintenanceRestarts? Restarts { get; set; }
}

internal sealed class HuntsMaintenanceRestarts
{
    [JsonPropertyName("timeline")]
    public HuntsMaintenanceRestartEntry[]? Timeline { get; set; }
}

internal sealed class HuntsMaintenanceRestartEntry
{
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    [JsonPropertyName("worldId")]
    public string? WorldId { get; set; }
}

internal sealed class HuntsZoneInfo
{
    [JsonPropertyName("numInstances")]
    public int NumInstances { get; set; }
}

internal sealed class HuntsAppSessionInfo
{
    [JsonPropertyName("access")]
    public HuntsAppAccess? Access { get; set; }
}

internal sealed class HuntsAppAccess
{
    [JsonPropertyName("mobStatus")]
    public HuntsMobStatusAccess? MobStatus { get; set; }
}

internal sealed class HuntsMobStatusAccess
{
    [JsonPropertyName("windows")]
    public Dictionary<string, JsonNode[]>? Windows { get; set; }
}

internal sealed class HuntsSocketConnectPayload
{
    [JsonPropertyName("sessionid")]
    public string SessionId { get; set; } = string.Empty;
}

internal sealed class HuntsSocketMessage
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("subType")]
    public string? SubType { get; set; }

    [JsonPropertyName("data")]
    public HuntsSocketMobReport? Data { get; set; }
}

internal sealed class HuntsSocketMobReport
{
    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("id")]
    public HuntsSocketMobIdentity? Id { get; set; }

    [JsonPropertyName("data")]
    public HuntsSocketMobData? Data { get; set; }
}

internal sealed class HuntsSocketMobIdentity
{
    [JsonPropertyName("mobId")]
    public string MobId { get; set; } = string.Empty;

    [JsonPropertyName("worldId")]
    public string WorldId { get; set; } = string.Empty;

    [JsonPropertyName("zoneInstance")]
    public int ZoneInstance { get; set; }

    [JsonPropertyName("windowNum")]
    public int? WindowNum { get; set; }

    [JsonPropertyName("phaseNum")]
    public int? PhaseNum { get; set; }

    [JsonPropertyName("zoneId")]
    public string? ZoneId { get; set; }
}

internal sealed class HuntsSocketMobData
{
    [JsonPropertyName("num")]
    public int Num { get; set; }

    [JsonPropertyName("startedAt")]
    public DateTimeOffset? StartedAt { get; set; }

    [JsonPropertyName("startedAtSniped2")]
    public DateTimeOffset? StartedAtSniped { get; set; }

    [JsonPropertyName("prevStartedAt")]
    public DateTimeOffset? PrevStartedAt { get; set; }

    [JsonPropertyName("snipedNum")]
    public int? SnipedNum { get; set; }

    [JsonPropertyName("zonePoiId")]
    public int? ZonePoiId { get; set; }

    [JsonPropertyName("zonePoiIds")]
    public int[]? ZonePoiIds { get; set; }

    [JsonPropertyName("isScheduled")]
    public bool IsScheduled { get; set; }

    [JsonPropertyName("reporters")]
    public HuntsSocketReporter[]? Reporters { get; set; }
}

internal sealed class HuntsSocketReporter
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

[JsonSerializable(typeof(HuntsRefreshRequest))]
[JsonSerializable(typeof(HuntsRefreshResponse))]
[JsonSerializable(typeof(HuntsLoginRequest))]
[JsonSerializable(typeof(HuntsLoginResponse))]
[JsonSerializable(typeof(HuntsDataCenterResponse))]
[JsonSerializable(typeof(HuntsAppSessionResponse))]
[JsonSerializable(typeof(HuntsRecentLogsRequest))]
[JsonSerializable(typeof(HuntsRecentLogsResponse))]
[JsonSerializable(typeof(HuntLogEntryDto))]
[JsonSerializable(typeof(HuntsSocketConnectPayload))]
[JsonSerializable(typeof(HuntsSocketMessage))]
internal partial class HuntsJsonContext : JsonSerializerContext
{
}
