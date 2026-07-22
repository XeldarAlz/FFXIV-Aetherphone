namespace Aetherphone.Core.Radio;

internal sealed class RadioStationRecord
{
    public string Uuid { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string StreamUrl { get; set; } = string.Empty;

    public string Codec { get; set; } = string.Empty;

    public int Bitrate { get; set; }

    public string Country { get; set; } = string.Empty;

    public static RadioStationRecord From(RadioStation station)
    {
        return new RadioStationRecord
        {
            Uuid = station.Uuid,
            Name = station.Name,
            StreamUrl = station.StreamUrl,
            Codec = station.Codec,
            Bitrate = station.Bitrate,
            Country = station.Country,
        };
    }

    public RadioStation ToStation() => new(Name, StreamUrl, Codec, Bitrate, Country, Uuid);
}
