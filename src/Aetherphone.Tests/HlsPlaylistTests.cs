using Aetherphone.Core.Radio;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HlsPlaylistTests
{
    private static readonly Uri MasterUri = new("https://radio.example/live/master.m3u8?token=abc");

    private const string Master = """
        #EXTM3U
        #EXT-X-VERSION:3
        #EXT-X-STREAM-INF:BANDWIDTH=56000,CODECS="mp4a.40.5"
        low/index.m3u8
        #EXT-X-STREAM-INF:BANDWIDTH=135000,CODECS="mp4a.40.2"
        mid/index.m3u8
        #EXT-X-STREAM-INF:BANDWIDTH=339200,CODECS="mp4a.40.2"
        https://cdn.example/high/index.m3u8
        #EXT-X-STREAM-INF:BANDWIDTH=900000,CODECS="mp4a.40.2"
        /absolute/max.m3u8
        #EXT-X-STREAM-INF:BANDWIDTH=150000,RESOLUTION=320x180,CODECS="avc1.42c00d,mp4a.40.2"
        video/index.m3u8
        """;

    [Fact]
    public void ParsesVariantsAndResolvesRelativeUris()
    {
        var master = HlsPlaylist.ParseMaster(Master, MasterUri);

        Assert.Equal(5, master.Variants.Length);
        Assert.Equal("https://radio.example/live/low/index.m3u8", master.Variants[0].Uri.ToString());
        Assert.Equal(56000, master.Variants[0].Bandwidth);
        Assert.Equal("https://cdn.example/high/index.m3u8", master.Variants[2].Uri.ToString());
        Assert.Equal("https://radio.example/absolute/max.m3u8", master.Variants[3].Uri.ToString());
        Assert.False(master.Variants[1].HasVideo);
        Assert.True(master.Variants[4].HasVideo);
    }

    [Fact]
    public void PicksTheBestAudioVariantUnderTheCap()
    {
        var master = HlsPlaylist.ParseMaster(Master, MasterUri);

        Assert.Equal("https://cdn.example/high/index.m3u8", HlsPlaylist.SelectAudio(master, 360_000)!.ToString());
        Assert.Equal("https://radio.example/live/mid/index.m3u8", HlsPlaylist.SelectAudio(master, 140_000)!.ToString());
        Assert.Equal("https://radio.example/live/low/index.m3u8", HlsPlaylist.SelectAudio(master, 10_000)!.ToString());
    }

    [Fact]
    public void FallsBackToAnAudioRenditionWhenEveryVariantHasVideo()
    {
        const string text = """
            #EXTM3U
            #EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID="aud",NAME="English",DEFAULT=NO,URI="audio/en.m3u8"
            #EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID="aud",NAME="Main",DEFAULT=YES,URI="audio/main.m3u8"
            #EXT-X-STREAM-INF:BANDWIDTH=800000,RESOLUTION=640x360,AUDIO="aud"
            video.m3u8
            """;
        var master = HlsPlaylist.ParseMaster(text, MasterUri);

        Assert.Equal("https://radio.example/live/audio/main.m3u8", HlsPlaylist.SelectAudio(master, 360_000)!.ToString());
    }

    [Fact]
    public void ParsesSequenceNumbersDurationsAndDiscontinuities()
    {
        const string text = """
            #EXTM3U
            #EXT-X-VERSION:3
            #EXT-X-TARGETDURATION:10
            #EXT-X-MEDIA-SEQUENCE:4711
            #EXT-X-DISCONTINUITY-SEQUENCE:2
            #EXTINF:9.984,
            seg4711.ts
            #EXT-X-DISCONTINUITY
            #EXTINF:10.005,title
            seg4712.ts?x=1
            #EXTINF:10,
            https://other.example/seg4713.aac
            """;
        var playlist = HlsPlaylist.ParseMedia(text, new Uri("https://radio.example/live/mid/index.m3u8"));

        Assert.Equal(10, playlist.TargetDuration);
        Assert.False(playlist.EndList);
        Assert.Equal(3, playlist.Segments.Length);
        Assert.Equal(4711, playlist.Segments[0].Sequence);
        Assert.Equal(4713, playlist.Segments[2].Sequence);
        Assert.Equal(9.984, playlist.Segments[0].Duration, 3);
        Assert.False(playlist.Segments[0].Discontinuity);
        Assert.True(playlist.Segments[1].Discontinuity);
        Assert.False(playlist.Segments[2].Discontinuity);
        Assert.Equal("https://radio.example/live/mid/seg4712.ts?x=1", playlist.Segments[1].Uri.ToString());
        Assert.Equal("https://other.example/seg4713.aac", playlist.Segments[2].Uri.ToString());
    }

    [Fact]
    public void FlagsEndListEncryptionAndFragmentedMp4()
    {
        const string text = """
            #EXTM3U
            #EXT-X-TARGETDURATION:6
            #EXT-X-KEY:METHOD=AES-128,URI="key.bin"
            #EXT-X-MAP:URI="init.mp4"
            #EXTINF:6,
            a.m4s
            #EXT-X-ENDLIST
            """;
        var playlist = HlsPlaylist.ParseMedia(text, MasterUri);

        Assert.True(playlist.EndList);
        Assert.True(playlist.Encrypted);
        Assert.True(playlist.Fragmented);
    }

    [Fact]
    public void AKeyWithMethodNoneIsNotEncryption()
    {
        const string text = "#EXTM3U\n#EXT-X-TARGETDURATION:6\n#EXT-X-KEY:METHOD=NONE\n#EXTINF:6,\na.ts\n";
        Assert.False(HlsPlaylist.ParseMedia(text, MasterUri).Encrypted);
    }

    [Fact]
    public void ToleratesWindowsLineEndings()
    {
        var text = "#EXTM3U\r\n#EXT-X-TARGETDURATION:4\r\n#EXT-X-MEDIA-SEQUENCE:7\r\n#EXTINF:4.0,\r\nseg.ts\r\n";
        var playlist = HlsPlaylist.ParseMedia(text, MasterUri);

        Assert.Single(playlist.Segments);
        Assert.Equal(7, playlist.Segments[0].Sequence);
        Assert.Equal("https://radio.example/live/seg.ts", playlist.Segments[0].Uri.ToString());
    }

    [Fact]
    public void TellsHlsApartFromAPlainM3u()
    {
        Assert.True(HlsPlaylist.IsHls(Master));
        Assert.True(HlsPlaylist.IsHls("#EXTM3U\n#EXT-X-TARGETDURATION:6\n#EXTINF:6,\na.ts"));
        Assert.False(HlsPlaylist.IsHls("#EXTM3U\n#EXTINF:-1,My Station\nhttp://radio.example/stream.mp3\n"));
    }

    [Fact]
    public void ReadsQuotedAttributesContainingCommas()
    {
        const string attributes = "BANDWIDTH=1000,CODECS=\"mp4a.40.2,avc1.4d401f\",NAME=\"A, B\"";

        Assert.Equal("1000", HlsPlaylist.Attribute(attributes, "BANDWIDTH"));
        Assert.Equal("mp4a.40.2,avc1.4d401f", HlsPlaylist.Attribute(attributes, "CODECS"));
        Assert.Equal("A, B", HlsPlaylist.Attribute(attributes, "NAME"));
        Assert.Equal(string.Empty, HlsPlaylist.Attribute(attributes, "RESOLUTION"));
    }
}
