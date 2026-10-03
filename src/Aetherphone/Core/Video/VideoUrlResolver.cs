using Aetherphone.Core.Net;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;

namespace Aetherphone.Core.Video;

internal sealed record VideoMetadata(string Title, string Source, TimeSpan? Duration, string? ThumbnailUrl);

internal readonly record struct PlaylistVideoEntry(string Url, VideoMetadata Metadata);

internal sealed record VideoPlaylist(string Title, string SourceUrl, PlaylistVideoEntry[] Videos, bool Truncated);

internal sealed class VideoUrlResolver : IDisposable
{
    internal const int MaxPlaylistVideos = 200;

    private const string PlaylistMarker = "list=";

    private readonly YoutubeClient youtube;
    private readonly RequestThrottle throttle = new(1, TimeSpan.FromMilliseconds(400));

    internal VideoUrlResolver(YoutubeClient youtube)
    {
        this.youtube = youtube;
    }

    internal static bool IsYouTubeUrl(string url) => VideoId.TryParse(url) is not null;

    internal static bool IsPlaylistUrl(string url) =>
        url.Contains(PlaylistMarker, StringComparison.OrdinalIgnoreCase) && PlaylistId.TryParse(url) is not null;

    internal static bool NamesOneVideo(string url) =>
        url.Contains("v=", StringComparison.Ordinal) || url.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase);

    internal async Task<VideoMetadata?> ResolveMetadataAsync(string url, CancellationToken token)
    {
        try
        {
            using (await throttle.EnterAsync(token).ConfigureAwait(false))
            {
                var video = await youtube.Videos.GetAsync(url, token).ConfigureAwait(false);
                var thumbnail = BestThumbnail(video.Thumbnails);
                return new VideoMetadata(video.Title, video.Author.ChannelTitle, video.Duration, thumbnail);
            }
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Video] Could not read details for {url}: {exception.Message}");
            return null;
        }
    }

    internal async Task<VideoPlaylist?> ResolvePlaylistAsync(string url, CancellationToken token)
    {
        if (PlaylistId.TryParse(url) is not { } playlistId)
        {
            return null;
        }

        try
        {
            using (await throttle.EnterAsync(token).ConfigureAwait(false))
            {
                var playlist = await youtube.Playlists.GetAsync(playlistId, token).ConfigureAwait(false);
                var videos = new List<PlaylistVideoEntry>(64);
                var truncated = false;
                await foreach (var video in youtube.Playlists.GetVideosAsync(playlistId, token).ConfigureAwait(false))
                {
                    if (videos.Count >= MaxPlaylistVideos)
                    {
                        truncated = true;
                        break;
                    }

                    videos.Add(new PlaylistVideoEntry(video.Url,
                        new VideoMetadata(video.Title, video.Author.ChannelTitle, video.Duration,
                            BestThumbnail(video.Thumbnails))));
                }

                return videos.Count == 0
                    ? null
                    : new VideoPlaylist(playlist.Title, playlist.Url, videos.ToArray(), truncated);
            }
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Video] Could not read the playlist at {url}: {exception.Message}");
            return null;
        }
    }

    private static string? BestThumbnail(IReadOnlyList<Thumbnail> thumbnails)
    {
        string? best = null;
        var bestArea = 0;
        for (var index = 0; index < thumbnails.Count; index++)
        {
            var area = thumbnails[index].Resolution.Area;
            if (area <= bestArea)
            {
                continue;
            }

            bestArea = area;
            best = thumbnails[index].Url;
        }

        return best;
    }

    public void Dispose() => throttle.Dispose();
}
