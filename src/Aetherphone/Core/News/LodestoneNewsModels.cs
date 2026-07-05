using System.Text.Json.Serialization;

namespace Aetherphone.Core.News;

internal sealed class LodestoneNewsItem
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("url")] public string Url { get; set; } = string.Empty;
    [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
    [JsonPropertyName("time")] public DateTimeOffset Time { get; set; }
    [JsonPropertyName("image")] public string? Image { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("start")] public DateTimeOffset? Start { get; set; }
    [JsonPropertyName("end")] public DateTimeOffset? End { get; set; }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(LodestoneNewsItem[]), TypeInfoPropertyName = "NewsItems")]
internal sealed partial class LodestoneNewsJsonContext : JsonSerializerContext
{
}
