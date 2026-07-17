using Newtonsoft.Json;

namespace Aetherphone.Core.Activity;

internal sealed class ActivityDay
{
    [JsonProperty("date")] public string Date { get; set; } = string.Empty;

    [JsonProperty("exp")] public long ExpGained { get; set; }

    [JsonProperty("units")] public float LevelUnitsGained { get; set; }

    [JsonProperty("levels")] public int LevelsGained { get; set; }

    [JsonProperty("gil")] public long GilEarned { get; set; }

    [JsonProperty("duties")] public int DutiesCompleted { get; set; }

    [JsonProperty("mounts")] public int MountsGained { get; set; }

    [JsonProperty("minions")] public int MinionsGained { get; set; }

    [JsonProperty("play")] public long PlaySeconds { get; set; }

    [JsonProperty("noted")] public int RingsNotified { get; set; }

    public void Clear()
    {
        Date = string.Empty;
        ExpGained = 0;
        LevelUnitsGained = 0f;
        LevelsGained = 0;
        GilEarned = 0;
        DutiesCompleted = 0;
        MountsGained = 0;
        MinionsGained = 0;
        PlaySeconds = 0;
        RingsNotified = 0;
    }
}
