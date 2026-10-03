using Aetherphone.Core.SystemMedia;
using Dalamud.Interface;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PcMediaTests
{
    [Theory]
    [InlineData((byte)MediaSessionPlayback.Playing, true)]
    [InlineData((byte)MediaSessionPlayback.Paused, true)]
    [InlineData((byte)MediaSessionPlayback.Stopped, false)]
    [InlineData((byte)MediaSessionPlayback.Changing, false)]
    [InlineData((byte)MediaSessionPlayback.Closed, false)]
    public void OnlyPlayingOrPausedSessionsAreLive(byte playback, bool expected)
    {
        var snapshot = Snapshot("Spotify.exe", (MediaSessionPlayback)playback);
        Assert.Equal(expected, PcMediaSource.IsLive(snapshot));
    }

    [Fact]
    public void EmptySnapshotIsNeverLive()
    {
        Assert.False(PcMediaSource.IsLive(MediaSessionSnapshot.Empty));
    }

    [Fact]
    public void SessionWithoutAppIsNotLive()
    {
        Assert.False(PcMediaSource.IsLive(Snapshot(string.Empty, MediaSessionPlayback.Playing)));
    }

    [Theory]
    [InlineData("Spotify", FontAwesomeIcon.Headphones)]
    [InlineData("foobar2000", FontAwesomeIcon.Music)]
    [InlineData("Google Chrome", FontAwesomeIcon.Globe)]
    [InlineData("Firefox", FontAwesomeIcon.Globe)]
    [InlineData("Something Else", FontAwesomeIcon.Desktop)]
    [InlineData("", FontAwesomeIcon.Desktop)]
    public void KnownAppsGetAGlyphAndUnknownAppsFallBack(string appName, FontAwesomeIcon expected)
    {
        Assert.Equal(expected, PcMediaGlyph.For(appName));
    }

    private static MediaSessionSnapshot Snapshot(string appId, MediaSessionPlayback playback) =>
        new(appId, "Spotify", "Title", "Artist", "Album", playback, MediaSessionControls.None, TimeSpan.Zero,
            TimeSpan.Zero, 0, null, 0);
}
