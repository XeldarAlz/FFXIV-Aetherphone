using Aetherphone.Core.Media;
using Aetherphone.Core.Songs;
using Aetherphone.Core.SystemMedia;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Playback;

internal sealed class PlaybackSystemBridge : IDisposable
{
    private const long TimelineIntervalMilliseconds = 1000;

    private readonly PlaybackHub hub;
    private readonly WindowsMediaPublisher publisher;
    private readonly RemoteImageCache images;
    private readonly Configuration configuration;
    private readonly IFramework framework;
    private readonly CancellationTokenSource cancellation = new();
    private bool enabled;
    private string publishedTitle = string.Empty;
    private string publishedArtist = string.Empty;
    private string publishedArtwork = string.Empty;
    private int metadataRevision;
    private long nextTimelineAt;

    public PlaybackSystemBridge(PlaybackHub hub, WindowsMediaPublisher publisher, RemoteImageCache images,
        Configuration configuration, IFramework framework)
    {
        this.hub = hub;
        this.publisher = publisher;
        this.images = images;
        this.configuration = configuration;
        this.framework = framework;
        framework.Update += OnUpdate;
        publisher.ButtonPressed += OnButton;
    }

    public void Dispose()
    {
        framework.Update -= OnUpdate;
        publisher.ButtonPressed -= OnButton;
        cancellation.Cancel();
        cancellation.Dispose();
    }

    private void OnUpdate(IFramework _)
    {
        if (!configuration.PublishToWindowsMedia || !hub.IsActive)
        {
            Shutdown();
            return;
        }

        if (!enabled)
        {
            enabled = true;
            publishedTitle = string.Empty;
            publishedArtist = string.Empty;
            publishedArtwork = string.Empty;
            nextTimelineAt = 0;
            publisher.Enable();
        }

        publisher.SetPlaybackStatus(Status());
        publisher.SetNavigation(CanNext(), CanPrevious());
        PublishMetadataIfChanged();
        PublishTimeline();
    }

    private void Shutdown()
    {
        if (!enabled)
        {
            return;
        }

        enabled = false;
        metadataRevision++;
        publisher.SetPlaybackStatus(MediaTransportStatus.Stopped);
        publisher.Disable();
    }

    private MediaTransportStatus Status()
    {
        if (hub.IsBuffering)
        {
            return MediaTransportStatus.Changing;
        }

        return hub.IsPlaying ? MediaTransportStatus.Playing : MediaTransportStatus.Paused;
    }

    private bool CanNext()
    {
        if (!hub.SongActive)
        {
            return hub.HasQueue;
        }

        var queue = hub.Queue;
        return queue.QueuedCount > 0 || queue.AutoplayCount > 0 || hub.RepeatMode != SongRepeatMode.Off;
    }

    private bool CanPrevious() => hub.SongActive || hub.HasQueue;

    private void PublishMetadataIfChanged()
    {
        var songs = hub.SongActive;
        var nowPlaying = hub.RadioNowPlaying;
        var title = songs ? hub.Title : nowPlaying.Length > 0 ? nowPlaying : hub.Title;
        var artist = songs ? hub.Songs.CurrentAuthor : hub.Title;
        var artwork = hub.ArtworkUrl;
        if (string.Equals(title, publishedTitle, StringComparison.Ordinal) &&
            string.Equals(artist, publishedArtist, StringComparison.Ordinal) &&
            string.Equals(artwork, publishedArtwork, StringComparison.Ordinal))
        {
            return;
        }

        publishedTitle = title;
        publishedArtist = artist;
        publishedArtwork = artwork;
        var album = songs ? hub.Queue.ContextTitle : string.Empty;
        var revision = ++metadataRevision;
        publisher.SetMetadata(title, artist, album, null);
        if (artwork.Length == 0)
        {
            return;
        }

        _ = Task.Run(() => PublishThumbnailAsync(revision, title, artist, album, artwork));
    }

    private async Task PublishThumbnailAsync(int revision, string title, string artist, string album, string url)
    {
        try
        {
            var bytes = await images.FetchBytesAsync(url, cancellation.Token).ConfigureAwait(false);
            if (bytes is null || Volatile.Read(ref metadataRevision) != revision)
            {
                return;
            }

            publisher.SetMetadata(title, artist, album, bytes);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception exception)
        {
            AepLog.Debug(exception, "[SystemMedia] artwork fetch for the media overlay failed");
        }
    }

    private void PublishTimeline()
    {
        if (!hub.CanSeek)
        {
            return;
        }

        var now = Environment.TickCount64;
        if (now < nextTimelineAt)
        {
            return;
        }

        nextTimelineAt = now + TimelineIntervalMilliseconds;
        publisher.SetTimeline(TimeSpan.FromSeconds(hub.Position), TimeSpan.FromSeconds(hub.Duration));
    }

    private void OnButton(MediaTransportButton button)
    {
        switch (button)
        {
            case MediaTransportButton.Play:
                if (!hub.IsPlaying)
                {
                    hub.TogglePlayPause();
                }

                break;
            case MediaTransportButton.Pause:
                if (hub.IsPlaying)
                {
                    hub.TogglePlayPause();
                }

                break;
            case MediaTransportButton.Stop:
                hub.Stop();
                break;
            case MediaTransportButton.Next:
                hub.Next();
                break;
            case MediaTransportButton.Previous:
                hub.Previous();
                break;
        }
    }
}
