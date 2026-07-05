using System.Text.Json.Serialization;

namespace Aetherphone.Core.Character;

internal sealed class CollectCharacter
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("achievements")] public CollectAchievements? Achievements { get; set; }
    [JsonPropertyName("mounts")] public CollectGroup? Mounts { get; set; }
    [JsonPropertyName("minions")] public CollectGroup? Minions { get; set; }
}

internal sealed class CollectAchievements
{
    [JsonPropertyName("count")] public int Count { get; set; }
    [JsonPropertyName("total")] public int Total { get; set; }
    [JsonPropertyName("points")] public int Points { get; set; }
    [JsonPropertyName("points_total")] public int PointsTotal { get; set; }
    [JsonPropertyName("public")] public bool Public { get; set; }
}

internal sealed class CollectGroup
{
    [JsonPropertyName("count")] public int Count { get; set; }
    [JsonPropertyName("total")] public int Total { get; set; }
    [JsonPropertyName("public")] public bool Public { get; set; }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(CollectCharacter))]
internal sealed partial class CollectJsonContext : JsonSerializerContext
{
}
