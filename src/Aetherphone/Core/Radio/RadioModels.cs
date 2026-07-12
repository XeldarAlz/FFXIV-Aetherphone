using System.Text.Json.Serialization;

namespace Aetherphone.Core.Radio;

internal readonly struct RadioStation
{
    public readonly string Name;
    public readonly string StreamUrl;
    public readonly string Codec;
    public readonly int Bitrate;
    public readonly string Country;

    public RadioStation(string name, string streamUrl, string codec, int bitrate, string country)
    {
        Name = name;
        StreamUrl = streamUrl;
        Codec = codec;
        Bitrate = bitrate;
        Country = country;
    }
}

internal readonly struct RadioPage
{
    public static readonly RadioPage Empty = new(Array.Empty<RadioStation>(), false);

    public readonly RadioStation[] Stations;
    public readonly bool HasMore;

    public RadioPage(RadioStation[] stations, bool hasMore)
    {
        Stations = stations;
        HasMore = hasMore;
    }
}

internal sealed class RadioStationDto
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("url")] public string? Url { get; set; }
    [JsonPropertyName("url_resolved")] public string? UrlResolved { get; set; }
    [JsonPropertyName("codec")] public string? Codec { get; set; }
    [JsonPropertyName("bitrate")] public int Bitrate { get; set; }
    [JsonPropertyName("country")] public string? Country { get; set; }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(RadioStationDto[]))]
internal sealed partial class RadioJsonContext : JsonSerializerContext
{
}
