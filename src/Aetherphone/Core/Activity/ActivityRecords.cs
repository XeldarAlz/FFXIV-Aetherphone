using Newtonsoft.Json;

namespace Aetherphone.Core.Activity;

internal sealed class ActivityRecords
{
    [JsonProperty("exp")]
    public long BestExp { get; set; }

    [JsonProperty("expDate")]
    public string BestExpDate { get; set; } = string.Empty;

    [JsonProperty("duties")]
    public int BestDuties { get; set; }

    [JsonProperty("dutiesDate")]
    public string BestDutiesDate { get; set; } = string.Empty;

    [JsonProperty("gil")]
    public long BestGil { get; set; }

    [JsonProperty("gilDate")]
    public string BestGilDate { get; set; } = string.Empty;

    [JsonProperty("play")]
    public long BestPlaySeconds { get; set; }

    [JsonProperty("playDate")]
    public string BestPlayDate { get; set; } = string.Empty;

    [JsonProperty("levels")]
    public int BestLevels { get; set; }

    [JsonProperty("levelsDate")]
    public string BestLevelsDate { get; set; } = string.Empty;

    [JsonProperty("streak")]
    public int BestStreak { get; set; }

    [JsonProperty("perfect")]
    public int PerfectDays { get; set; }

    [JsonProperty("seeded")]
    public bool Seeded { get; set; }
}
