using Aetherphone.Core.Net;
using YoutubeExplode;
using YoutubeExplode.Common;

namespace Aetherphone.Core.Songs;

internal readonly record struct SongPlaylistHit(string PlaylistId, string Title, string Author, string ThumbnailUrl);

internal sealed class SongSearchService : IDisposable
{
    private const int MaxResults = 25;
    private const int MaxPlaylistResults = 20;
    private const int MinSongSeconds = 30;
    private const int MaxSongSeconds = 360;
    private const int ResolverFetchCount = 40;
    private readonly YoutubeClient youtube;
    private readonly SongLinkResolver linkResolver;
    private readonly RequestThrottle throttle;
    private readonly CancellationTokenSource cancellation = new();

    public SongSearchService(YoutubeClient youtube, SongLinkResolver linkResolver)
    {
        this.youtube = youtube;
        this.linkResolver = linkResolver;
        throttle = new RequestThrottle(1, TimeSpan.FromMilliseconds(400));
    }

    public async Task<Song[]> SearchAsync(string query, SongSearchScope scope, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<Song>();
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, cancellation.Token);
        try
        {
            using (await throttle.EnterAsync(linked.Token).ConfigureAwait(false))
            {
                var results = new List<Song>(MaxResults);
                await foreach (var video in youtube.Search.GetVideosAsync(query, linked.Token).ConfigureAwait(false))
                {
                    if (video.Duration is null)
                    {
                        continue;
                    }

                    var seconds = (int)video.Duration.Value.TotalSeconds;
                    if (!MatchesScope(scope, seconds))
                    {
                        continue;
                    }

                    var song = new Song(video.Id.Value, video.Title, video.Author.ChannelTitle,
                        PickThumbnail(video.Thumbnails), seconds, video.Author.ChannelId.Value);
                    results.Add(song);
                    if (results.Count >= MaxResults)
                    {
                        break;
                    }
                }

                return results.ToArray();
            }
        }
        catch (OperationCanceledException)
        {
            return Array.Empty<Song>();
        }
        catch (Exception exception)
        {
            if (linkResolver.IsInstalled)
            {
                AepLog.Warning(exception, $"Song search failed for '{query}', trying the link resolver");
                return SearchThroughResolver(query, scope, token);
            }

            AepLog.Warning(exception, $"Song search failed for '{query}'");
            return Array.Empty<Song>();
        }
    }

    public async Task<SongPlaylistHit[]> SearchPlaylistsAsync(string query, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<SongPlaylistHit>();
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, cancellation.Token);
        try
        {
            using (await throttle.EnterAsync(linked.Token).ConfigureAwait(false))
            {
                var results = new List<SongPlaylistHit>(MaxPlaylistResults);
                await foreach (var playlist in youtube.Search.GetPlaylistsAsync(query, linked.Token)
                                   .ConfigureAwait(false))
                {
                    results.Add(new SongPlaylistHit(playlist.Id.Value, playlist.Title,
                        playlist.Author?.ChannelTitle ?? string.Empty, PickThumbnail(playlist.Thumbnails)));
                    if (results.Count >= MaxPlaylistResults)
                    {
                        break;
                    }
                }

                return results.ToArray();
            }
        }
        catch (OperationCanceledException)
        {
            return Array.Empty<SongPlaylistHit>();
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"Playlist search failed for '{query}'");
            return Array.Empty<SongPlaylistHit>();
        }
    }

    private Song[] SearchThroughResolver(string query, SongSearchScope scope, CancellationToken token)
    {
        var entries = linkResolver.Search(query, ResolverFetchCount, token);
        if (entries is null)
        {
            return Array.Empty<Song>();
        }

        var results = new List<Song>(MaxResults);
        for (var index = 0; index < entries.Length && results.Count < MaxResults; index++)
        {
            var entry = entries[index];
            if (!MatchesScope(scope, entry.DurationSeconds))
            {
                continue;
            }

            results.Add(new Song(entry.VideoId, entry.Title, entry.Author, entry.ThumbnailUrl,
                entry.DurationSeconds, entry.ChannelId));
        }

        return results.ToArray();
    }

    private static bool MatchesScope(SongSearchScope scope, int seconds)
    {
        if (seconds < MinSongSeconds)
        {
            return false;
        }

        if (scope == SongSearchScope.Songs)
        {
            return seconds <= MaxSongSeconds;
        }

        if (scope == SongSearchScope.LongPlays)
        {
            return seconds > MaxSongSeconds;
        }

        return true;
    }

    private static string PickThumbnail(IReadOnlyList<Thumbnail> thumbnails)
    {
        if (thumbnails is null || thumbnails.Count == 0)
        {
            return string.Empty;
        }

        var best = thumbnails[0];
        for (var index = 1; index < thumbnails.Count; index++)
        {
            if (thumbnails[index].Resolution.Area > best.Resolution.Area)
            {
                best = thumbnails[index];
            }
        }

        return best.Url;
    }

    public void Dispose()
    {
        cancellation.Cancel();
        throttle.Dispose();
        cancellation.Dispose();
    }
}
