using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace Aetherphone.Core.Video;

internal sealed record VideoMetadata(string Title, string Source, TimeSpan? Duration, string? ThumbnailUrl);

internal sealed record ResolvedStream(string VideoUrl, string? AudioUrl, string QualityLabel);

// Stage 4, deliberately NOT a port of AlphaChannel's yt-dlp path. AlphaChannel downloads a
// yt-dlp binary at runtime and hands mpv's own ytdl_hook script a URL to resolve internally
// (see docs/video-pipeline.md §5 in the AlphaChannel repo) - AlphaChannel's C# code never
// touches yt-dlp's output directly. Aetherphone already depends on YoutubeExplode (managed,
// no external process) for the Music app's own YouTube resolution (Core/Songs/SongSearchService,
// SongPlayer). Reusing it here removes an entire runtime-downloaded-binary dependency and its
// failure modes (network required on first use, download hangs, binary goes missing).
//
// YouTube only serves muxed (single-file, audio+video together) streams up to 720p - anything
// higher only exists as separate video-only and audio-only streams. This resolves adaptive
// streams first (best video-only <= the requested cap, paired with the best audio-only track)
// and only falls back to a muxed stream if no adaptive pair is available, so quality above 720p
// is actually reachable rather than the dropdown just listing numbers that silently clamp.
internal sealed class VideoUrlResolver
{
    // YouTube's own practical ceiling for muxed streams. Below this, always prefer muxed even if
    // a particular video's own muxed ceiling happens to be lower than what adaptive could offer
    // at the same cap - the adaptive (external audio-file) path is new and its track-selection
    // behavior under Wine isn't fully verified yet, so it's scoped to only the quality tier that
    // has no muxed option at all, rather than opportunistically replacing muxed playback that
    // was already known to work.
    private const int MuxedCeiling = 720;

    private readonly YoutubeClient youtube = new();

    public static bool IsYouTubeUrl(string url) => VideoId.TryParse(url) is not null;

    public async Task<(ResolvedStream? Stream, string? Error)> ResolveAsync(string url, int maxHeight,
        CancellationToken token)
    {
        try
        {
            var manifest = await youtube.Videos.Streams.GetManifestAsync(url, token).ConfigureAwait(false);

            var video = manifest.GetVideoOnlyStreams()
                .Where(stream => stream.VideoQuality.MaxHeight <= maxHeight)
                .OrderByDescending(stream => stream.VideoQuality.MaxHeight)
                .ThenByDescending(stream => stream.Bitrate)
                .FirstOrDefault();
            var audio = manifest.GetAudioOnlyStreams().OrderByDescending(stream => stream.Bitrate).FirstOrDefault();

            var muxed = manifest.GetMuxedStreams().Where(stream => stream.VideoQuality.MaxHeight <= maxHeight)
                .OrderByDescending(stream => stream.VideoQuality.MaxHeight).FirstOrDefault();

            // Only reach for adaptive when the requested quality is above what muxed can ever
            // offer - not just above what this particular video's muxed ceiling happens to be.
            if (maxHeight > MuxedCeiling && video is not null && audio is not null)
            {
                var label = video.VideoQuality.Label;
                AepLog.Debug($"[Video] Resolved {url} -> video={video.Url} audio={audio.Url} ({label}, adaptive)");
                return (new ResolvedStream(video.Url, audio.Url, label), null);
            }

            muxed ??= manifest.GetMuxedStreams().OrderBy(stream => stream.VideoQuality.MaxHeight).FirstOrDefault();
            if (muxed is not null)
            {
                AepLog.Debug($"[Video] Resolved {url} -> {muxed.Url} ({muxed.VideoQuality.Label}, muxed)");
                return (new ResolvedStream(muxed.Url, null, muxed.VideoQuality.Label), null);
            }

            return (null, "No playable stream found for this video.");
        }
        catch (OperationCanceledException)
        {
            return (null, null);
        }
        catch (Exception exception)
        {
            return (null, $"Failed to resolve YouTube URL: {exception.Message}");
        }
    }

    public async Task<VideoMetadata?> ResolveMetadataAsync(string url, CancellationToken token)
    {
        try
        {
            var video = await youtube.Videos.GetAsync(url, token).ConfigureAwait(false);
            var thumbnail = video.Thumbnails.OrderByDescending(t => t.Resolution.Area).FirstOrDefault();
            return new VideoMetadata(video.Title, video.Author.ChannelTitle, video.Duration, thumbnail?.Url);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Video] Failed to fetch metadata for {url}: {exception.Message}");
            return null;
        }
    }
}
