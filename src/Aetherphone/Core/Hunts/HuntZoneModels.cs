using System.Text.Json.Serialization;

namespace Aetherphone.Core.Hunts;

internal sealed class HuntZoneDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public Dictionary<string, string> Name { get; set; } = new();

    [JsonPropertyName("expansionId")]
    public string ExpansionId { get; set; } = string.Empty;

    [JsonPropertyName("territoryId")]
    public string TerritoryId { get; set; } = string.Empty;

    [JsonPropertyName("map")]
    public HuntZoneMap Map { get; set; } = new();

    [JsonPropertyName("pois")]
    public HuntPoiEntry[] Pois { get; set; } = Array.Empty<HuntPoiEntry>();
}

internal sealed class HuntZoneMap
{
    [JsonPropertyName("sizeFactor")]
    public int SizeFactor { get; set; } = 100;
}

internal sealed class HuntPoiEntry
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public Dictionary<string, string>? Name { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;

    [JsonPropertyName("journeyPoiId")]
    public int? JourneyPoiId { get; set; }

    private (float X, float Y)? parsed;

    public (float X, float Y) ParsedLocation()
    {
        if (parsed is { } cached)
        {
            return cached;
        }

        var span = Location.AsSpan();
        var comma = span.IndexOf(',');
        var result = comma < 0
            ? (0f, 0f)
            : (float.Parse(span[..comma]), float.Parse(span[(comma + 1)..]));
        parsed = result;
        return result;
    }
}

[JsonSerializable(typeof(Dictionary<string, HuntZoneDefinition>))]
internal partial class HuntZoneJsonContext : JsonSerializerContext
{
}
