using System.Text.Json.Serialization;

namespace Aetherphone.Core.Hunts;

internal sealed class HuntMobDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public Dictionary<string, string> Name { get; set; } = new();

    [JsonPropertyName("rank")]
    public string Rank { get; set; } = string.Empty;

    [JsonPropertyName("expansionId")]
    public string ExpansionId { get; set; } = string.Empty;

    [JsonPropertyName("zoneIds")]
    public string[] ZoneIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("windows")]
    public HuntMobWindowDef[] Windows { get; set; } = Array.Empty<HuntMobWindowDef>();

    [JsonPropertyName("landmine")]
    public bool Landmine { get; set; }

    [JsonPropertyName("conditions")]
    public HuntMobConditions? Conditions { get; set; }
}

internal sealed class HuntMobConditions
{
    [JsonPropertyName("automatic")]
    public HuntMobConditionWindow[]? Automatic { get; set; }
}

internal sealed class HuntMobConditionWindow
{
    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("rule")]
    public HuntSpawnConditionRule? Rule { get; set; }

    [JsonPropertyName("rules")]
    public HuntSpawnConditionRule[]? Rules { get; set; }

    [JsonIgnore]
    public IReadOnlyList<HuntSpawnConditionRule> RuleSet =>
        Rules is { Length: > 0 } ? Rules : Rule is { } single ? new[] { single } : Array.Empty<HuntSpawnConditionRule>();
}

internal sealed class HuntSpawnConditionRule
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("phase")]
    public string? Phase { get; set; }

    [JsonPropertyName("periods")]
    public HuntConditionPeriod[]? Periods { get; set; }

    [JsonPropertyName("hours")]
    public int[]? Hours { get; set; }

    [JsonPropertyName("duration")]
    public int? Duration { get; set; }

    [JsonPropertyName("conditions")]
    public string[]? MatchingWeather { get; set; }

    [JsonPropertyName("probabilities")]
    public HuntWeatherProbability[]? Probabilities { get; set; }

    [JsonPropertyName("offset")]
    public int? Offset { get; set; }
}

internal sealed class HuntConditionPeriod
{
    [JsonPropertyName("from")]
    public int From { get; set; }

    [JsonPropertyName("to")]
    public int To { get; set; }
}

internal sealed class HuntWeatherProbability
{
    [JsonPropertyName("chance")]
    public int Chance { get; set; }

    [JsonPropertyName("condition")]
    public string Condition { get; set; } = string.Empty;
}

internal sealed class HuntMobWindowDef
{
    [JsonPropertyName("timing")]
    public HuntMobTiming? Timing { get; set; }

    [JsonPropertyName("phases")]
    public HuntMobPhase[] Phases { get; set; } = Array.Empty<HuntMobPhase>();
}

internal sealed class HuntMobTiming
{
    [JsonPropertyName("normal")]
    public HuntMobTimingWindow? Normal { get; set; }

    [JsonPropertyName("maintenance")]
    public HuntMobTimingWindow? Maintenance { get; set; }
}

internal sealed class HuntMobTimingWindow
{
    [JsonPropertyName("min")]
    public double Min { get; set; }

    [JsonPropertyName("avg")]
    public double? Avg { get; set; }

    [JsonPropertyName("cap")]
    public double? Cap { get; set; }

    [JsonPropertyName("excess")]
    public double? Excess { get; set; }
}

internal sealed class HuntMobPhase
{
    [JsonPropertyName("name")]
    public Dictionary<string, string>? Name { get; set; }

    [JsonPropertyName("zonePoiIds")]
    public int[] ZonePoiIds { get; set; } = Array.Empty<int>();

    [JsonPropertyName("timeLimit")]
    public int? TimeLimit { get; set; }

    [JsonPropertyName("mobId")]
    public string? MobId { get; set; }

    [JsonPropertyName("grouped")]
    public bool? Grouped { get; set; }
}

[JsonSerializable(typeof(Dictionary<string, HuntMobDefinition>))]
internal partial class HuntMobCatalogJsonContext : JsonSerializerContext
{
}
