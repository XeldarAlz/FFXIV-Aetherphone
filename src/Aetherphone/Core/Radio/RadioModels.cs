using System.Text.Json.Serialization;

namespace Aetherphone.Core.Radio;

internal enum RadioOrder : byte
{
    Popular,
    Trending,
    TopVoted,
    Name,
    Bitrate,
}

internal readonly struct RadioFilter
{
    public static readonly RadioFilter Default = new(string.Empty, string.Empty, RadioOrder.Popular);

    public readonly string CountryCode;
    public readonly string Language;
    public readonly RadioOrder Order;

    public RadioFilter(string countryCode, string language, RadioOrder order)
    {
        CountryCode = countryCode;
        Language = language;
        Order = order;
    }

    public bool IsDefault => CountryCode.Length == 0 && Language.Length == 0 && Order == RadioOrder.Popular;
}

internal readonly struct RadioFacet
{
    public readonly string Display;
    public readonly string Value;
    public readonly int Count;

    public RadioFacet(string display, string value, int count)
    {
        Display = display;
        Value = value;
        Count = count;
    }
}

internal readonly struct RadioStation
{
    public readonly string Name;
    public readonly string StreamUrl;
    public readonly string Codec;
    public readonly int Bitrate;
    public readonly string Country;
    public readonly string Uuid;

    public RadioStation(string name, string streamUrl, string codec, int bitrate, string country, string uuid)
    {
        Name = name;
        StreamUrl = streamUrl;
        Codec = codec;
        Bitrate = bitrate;
        Country = country;
        Uuid = uuid;
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
    [JsonPropertyName("stationuuid")] public string? StationUuid { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("url")] public string? Url { get; set; }
    [JsonPropertyName("url_resolved")] public string? UrlResolved { get; set; }
    [JsonPropertyName("codec")] public string? Codec { get; set; }
    [JsonPropertyName("bitrate")] public int Bitrate { get; set; }
    [JsonPropertyName("country")] public string? Country { get; set; }
}

internal sealed class RadioCountryDto
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("iso_3166_1")] public string? IsoCode { get; set; }
    [JsonPropertyName("stationcount")] public int StationCount { get; set; }
}

internal sealed class RadioLanguageDto
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("stationcount")] public int StationCount { get; set; }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(RadioStationDto[]))]
[JsonSerializable(typeof(RadioCountryDto[]))]
[JsonSerializable(typeof(RadioLanguageDto[]))]
internal sealed partial class RadioJsonContext : JsonSerializerContext
{
}
