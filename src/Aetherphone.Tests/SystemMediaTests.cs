using Aetherphone.Core.SystemMedia;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SystemMediaTests
{
    private static readonly Guid IterableId = new("faa585ea-6214-4217-afda-7f46de5869b3");
    private static readonly Guid AsyncOperationId = new("9fc2b0bb-e446-44e2-aa61-9cab8f636af2");

    [Fact]
    public void PinterfaceMatchesKnownIterableOfString()
    {
        var id = WinRtPinterface.Compute(WinRtPinterface.Generic(IterableId, "string"));

        Assert.Equal(new Guid("e2fcc7c1-3bfc-5a0b-b2b0-72e769d1cb7e"), id);
    }

    [Fact]
    public void PinterfaceMatchesKnownAsyncOperationOfBoolean()
    {
        var id = WinRtPinterface.Compute(WinRtPinterface.Generic(AsyncOperationId, "b1"));

        Assert.Equal(new Guid("cdb5efb3-5788-509d-9be1-71ccb8a3362a"), id);
    }

    [Fact]
    public void ButtonHandlerUsesTheTypedEventHandlerInterfaceId()
    {
        Assert.Equal(new Guid("0557e996-7b23-5bae-aa81-ea0d671143a4"), TransportButtonHandler.InterfaceId);
    }

    [Theory]
    [InlineData("Spotify.exe", "Spotify")]
    [InlineData("foobar2000.exe", "foobar2000")]
    [InlineData("chrome", "Google Chrome")]
    [InlineData("msedge.exe", "Microsoft Edge")]
    [InlineData("C:\\Program Files\\VideoLAN\\VLC\\vlc.exe", "VLC")]
    [InlineData("SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify", "Spotify")]
    [InlineData("Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic", "Media Player")]
    [InlineData("Contoso.Player_abcdef123!App", "Player")]
    [InlineData("Contoso.Player_abcdef123!Contoso.Tunes", "Tunes")]
    [InlineData("SomeTool.exe", "SomeTool")]
    [InlineData("  ", "")]
    public void AppNameComesFromTheAppUserModelId(string appUserModelId, string expected)
    {
        Assert.Equal(expected, MediaAppNames.FromAppUserModelId(appUserModelId));
    }

    [Theory]
    [InlineData("ffxiv_dx11.exe", "ffxiv_dx11.exe", true)]
    [InlineData("FFXIV_DX11.EXE", "ffxiv_dx11.exe", true)]
    [InlineData("ffxiv_dx11", "ffxiv_dx11.exe", true)]
    [InlineData("Spotify.exe", "ffxiv_dx11.exe", false)]
    [InlineData("", "ffxiv_dx11.exe", false)]
    public void OwnProcessIsRecognised(string appUserModelId, string processFileName, bool expected)
    {
        Assert.Equal(expected, MediaAppNames.IsOwnProcess(appUserModelId, processFileName));
    }

    [Fact]
    public void PickerPrefersThePlayingCurrentSession()
    {
        var candidates = new[]
        {
            new MediaSessionCandidate(false, false, true, false),
            new MediaSessionCandidate(false, true, true, false),
        };

        Assert.Equal(1, MediaSessionPicker.Pick(candidates));
    }

    [Fact]
    public void PickerPrefersAnyPlayingSessionOverAPausedCurrentOne()
    {
        var candidates = new[]
        {
            new MediaSessionCandidate(false, true, false, false),
            new MediaSessionCandidate(false, false, true, false),
        };

        Assert.Equal(1, MediaSessionPicker.Pick(candidates));
    }

    [Fact]
    public void PickerKeepsThePreviousSelectionWhenNothingPlays()
    {
        var candidates = new[]
        {
            new MediaSessionCandidate(false, true, false, false),
            new MediaSessionCandidate(false, false, false, true),
        };

        Assert.Equal(1, MediaSessionPicker.Pick(candidates));
    }

    [Fact]
    public void PickerIgnoresOwnProcessEvenWhenPlaying()
    {
        var candidates = new[]
        {
            new MediaSessionCandidate(true, true, true, true),
            new MediaSessionCandidate(false, false, false, false),
        };

        Assert.Equal(1, MediaSessionPicker.Pick(candidates));
    }

    [Fact]
    public void PickerReturnsNoneWhenOnlyOwnProcessExists()
    {
        var candidates = new[] { new MediaSessionCandidate(true, true, true, false), };

        Assert.Equal(MediaSessionPicker.None, MediaSessionPicker.Pick(candidates));
        Assert.Equal(MediaSessionPicker.None, MediaSessionPicker.Pick(ReadOnlySpan<MediaSessionCandidate>.Empty));
    }

    [Fact]
    public void PlayingPositionExtrapolatesFromTheTimelineStamp()
    {
        var stamp = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc).Ticks;
        var snapshot = Snapshot(MediaSessionPlayback.Playing, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(3), stamp);

        var position = snapshot.PositionAt(stamp + TimeSpan.FromSeconds(12).Ticks);

        Assert.Equal(TimeSpan.FromSeconds(42), position);
    }

    [Fact]
    public void PausedPositionDoesNotMove()
    {
        var stamp = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc).Ticks;
        var snapshot = Snapshot(MediaSessionPlayback.Paused, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(3), stamp);

        Assert.Equal(TimeSpan.FromSeconds(30), snapshot.PositionAt(stamp + TimeSpan.FromMinutes(5).Ticks));
    }

    [Fact]
    public void ExtrapolatedPositionClampsToDurationAndIgnoresClockSkew()
    {
        var stamp = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc).Ticks;
        var snapshot = Snapshot(MediaSessionPlayback.Playing, TimeSpan.FromSeconds(170), TimeSpan.FromMinutes(3),
            stamp);

        Assert.Equal(TimeSpan.FromMinutes(3), snapshot.PositionAt(stamp + TimeSpan.FromMinutes(1).Ticks));
        Assert.Equal(TimeSpan.FromSeconds(170), snapshot.PositionAt(stamp - TimeSpan.FromSeconds(5).Ticks));
    }

    [Fact]
    public void LiveStreamWithoutDurationKeepsCounting()
    {
        var stamp = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc).Ticks;
        var snapshot = Snapshot(MediaSessionPlayback.Playing, TimeSpan.FromSeconds(5), TimeSpan.Zero, stamp);

        Assert.Equal(TimeSpan.FromSeconds(65), snapshot.PositionAt(stamp + TimeSpan.FromMinutes(1).Ticks));
        Assert.False(snapshot.CanSeek);
    }

    [Fact]
    public void EmptySnapshotHasNoSession()
    {
        Assert.False(MediaSessionSnapshot.Empty.HasSession);
        Assert.False(MediaSessionSnapshot.Empty.IsPlaying);
        Assert.False(MediaSessionSnapshot.Empty.CanPlayPause);
        Assert.Equal(TimeSpan.Zero, MediaSessionSnapshot.Empty.PositionAt(DateTime.UtcNow.Ticks));
    }

    [Fact]
    public void FingerprintSeparatesDifferentArtwork()
    {
        var first = new byte[] { 1, 2, 3, 4 };
        var same = new byte[] { 1, 2, 3, 4 };
        var other = new byte[] { 1, 2, 3, 5 };
        var longer = new byte[] { 1, 2, 3, 4, 0 };

        Assert.Equal(ArtworkFingerprint.Of(first), ArtworkFingerprint.Of(same));
        Assert.NotEqual(ArtworkFingerprint.Of(first), ArtworkFingerprint.Of(other));
        Assert.NotEqual(ArtworkFingerprint.Of(first), ArtworkFingerprint.Of(longer));
        Assert.Equal(0UL, ArtworkFingerprint.Of(ReadOnlySpan<byte>.Empty));
        Assert.NotEqual(0UL, ArtworkFingerprint.Of(first));
    }

    [Fact]
    public void ArtworkFetchesOnTrackChangeAndConfirmsOnce()
    {
        var refresh = new ArtworkRefresh();

        Assert.True(refresh.ShouldFetch(11, 0));
        refresh.Fetched(true, 0);
        Assert.False(refresh.ShouldFetch(11, ArtworkRefresh.ConfirmDelayMilliseconds - 1));
        Assert.True(refresh.ShouldFetch(11, ArtworkRefresh.ConfirmDelayMilliseconds));
        refresh.Fetched(true, ArtworkRefresh.ConfirmDelayMilliseconds);
        Assert.False(refresh.ShouldFetch(11, 60_000));
        Assert.True(refresh.ShouldFetch(12, 60_000));
    }

    [Fact]
    public void MissingArtworkRetriesAFewTimesThenStops()
    {
        var refresh = new ArtworkRefresh();
        var now = 0L;
        Assert.True(refresh.ShouldFetch(7, now));
        refresh.Fetched(false, now);
        now += ArtworkRefresh.ConfirmDelayMilliseconds;
        var fetches = 0;
        for (var attempt = 0; attempt < 10; attempt++)
        {
            if (refresh.ShouldFetch(7, now))
            {
                fetches++;
                refresh.Fetched(false, now);
            }

            now += ArtworkRefresh.MissingRetryMilliseconds;
        }

        Assert.Equal(1 + ArtworkRefresh.MaximumMissingRetries, fetches);
    }

    private static MediaSessionSnapshot Snapshot(MediaSessionPlayback playback, TimeSpan position, TimeSpan duration,
        long stamp) =>
        new("Spotify.exe", "Spotify", "Title", "Artist", "Album", playback,
            MediaSessionControls.PlayPauseToggle | MediaSessionControls.Seek, position, duration, stamp, null, 0);
}
