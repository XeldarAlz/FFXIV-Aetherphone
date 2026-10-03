using System.Text;
using Aetherphone.Core.Radio;
using Xunit;

namespace Aetherphone.Tests;

public sealed class StationPlaylistTests
{
    private static readonly Uri BaseUri = new("http://radio.example/listen/station.pls");

    [Fact]
    public void APlsResolvesToItsLowestNumberedFile()
    {
        const string text = """
            [playlist]
            NumberOfEntries=2
            File2=http://backup.example:8000/stream
            Title2=Backup
            File1=http://main.example:8000/stream
            Title1=Main
            Length1=-1
            Version=2
            """;

        Assert.True(StationPlaylist.TryFirstEntry(text, BaseUri, out var entry));
        Assert.Equal("http://main.example:8000/stream", entry.ToString());
    }

    [Fact]
    public void AnM3uSkipsCommentsAndResolvesRelativeEntries()
    {
        const string text = "\uFEFF#EXTM3U\r\n#EXTINF:-1,Station\r\n\r\nlive/high.mp3\r\nhttp://other.example/second.mp3\r\n";

        Assert.True(StationPlaylist.TryFirstEntry(text, BaseUri, out var entry));
        Assert.Equal("http://radio.example/listen/live/high.mp3", entry.ToString());
    }

    [Fact]
    public void APlaylistWithNoStreamResolvesToNothing()
    {
        Assert.False(StationPlaylist.TryFirstEntry("#EXTM3U\n#EXTINF:-1,Nothing here\n", BaseUri, out _));
        Assert.False(StationPlaylist.TryFirstEntry("[playlist]\nNumberOfEntries=0\n", BaseUri, out _));
    }

    [Theory]
    [InlineData("audio/x-scpls", "http://radio.example/a", "http://x.example/s.mp3")]
    [InlineData("application/vnd.apple.mpegurl", "http://radio.example/a", "#EXTM3U")]
    [InlineData("audio/mpeg", "http://radio.example/a.m3u", "http://x.example/s.mp3")]
    [InlineData(null, "http://radio.example/a", "[playlist]\nFile1=x")]
    [InlineData("audio/mpeg", "http://radio.example/a", "#EXTM3U\n")]
    public void RecognisesPlaylists(string? contentType, string url, string head)
    {
        Assert.True(StationPlaylist.LooksLikePlaylist(contentType, new Uri(url), Encoding.UTF8.GetBytes(head)));
    }

    [Fact]
    public void AudioBehindAPlaylistLookingAddressIsStillAudio()
    {
        var audio = RadioStreamFixtures.ToneAac().AsSpan(0, 64);

        Assert.False(StationPlaylist.LooksLikePlaylist("audio/aac", new Uri("http://radio.example/live.m3u"), audio));
        Assert.False(StationPlaylist.LooksLikePlaylist("audio/x-mpegurl", new Uri("http://radio.example/a"), audio));
    }
}
