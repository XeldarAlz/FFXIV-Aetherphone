using Aetherphone.Core.Radio;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RadioCodecPolicyTests
{
    [Theory]
    [InlineData("MP3")]
    [InlineData("AAC")]
    [InlineData("AAC+")]
    [InlineData("aac+")]
    [InlineData("HE-AAC")]
    [InlineData("OPUS")]
    public void DecodableCodecsPass(string codec)
    {
        Assert.True(RadioCodecPolicy.IsPlayable(codec, "http://radio.example/stream", false));
    }

    [Theory]
    [InlineData("FLAC")]
    [InlineData("MP4")]
    [InlineData("FLV")]
    [InlineData("AAC,H.264")]
    [InlineData("AAC+,H.264")]
    [InlineData("WMA")]
    public void CodecsWeCannotDecodeAreHidden(string codec)
    {
        Assert.False(RadioCodecPolicy.IsPlayable(codec, "http://radio.example/stream", false));
    }

    [Fact]
    public void OggPassesOnlyWhenTheAddressNamesOpus()
    {
        Assert.True(RadioCodecPolicy.IsPlayable("OGG", "https://icecast.example:8443/jazz_opus", false));
        Assert.True(RadioCodecPolicy.IsPlayable("OGG", "https://radio.example/stream.opus", false));
        Assert.False(RadioCodecPolicy.IsPlayable("OGG", "https://radio.example/lounge.ogg", false));
    }

    [Theory]
    [InlineData("UNKNOWN", "http://radio.example/live", true, true)]
    [InlineData("UNKNOWN", "http://radio.example/live.mp3", false, true)]
    [InlineData("", "http://radio.example/live/playlist.m3u8", false, true)]
    [InlineData(null, "http://radio.example/stream.aac?x=1", false, true)]
    [InlineData("UNKNOWN", "http://radio.example/live", false, false)]
    [InlineData("UNKNOWN", "http://radio.example/live.flac", false, false)]
    public void UnknownCodecsLeanOnTheAddress(string? codec, string url, bool hls, bool expected)
    {
        Assert.Equal(expected, RadioCodecPolicy.IsPlayable(codec, url, hls));
    }

    [Fact]
    public void ProjectionDropsUnplayableStationsAndKeepsTheRest()
    {
        var dtos = new[]
        {
            new RadioStationDto { Name = "Mp3", UrlResolved = "http://a.example/s", Codec = "MP3" },
            new RadioStationDto { Name = "Flac", UrlResolved = "http://b.example/s", Codec = "FLAC" },
            new RadioStationDto
            {
                Name = " Aac ", Url = "http://c.example/s", Codec = "AAC+", Favicon = "http://c.example/i.png",
                CountryCode = "JP", Tags = "anime,jpop", Votes = 12,
            },
        };

        var stations = RadioService.Project(dtos);

        Assert.Equal(2, stations.Length);
        Assert.Equal("Mp3", stations[0].Name);
        Assert.Equal("Aac", stations[1].Name);
        Assert.Equal("http://c.example/i.png", stations[1].ArtworkUrl);
        Assert.Equal("JP", stations[1].CountryCode);
        Assert.Equal("anime,jpop", stations[1].Tags);
        Assert.Equal(12, stations[1].Votes);
        Assert.False(stations[1].IsCommunity);
    }
}
