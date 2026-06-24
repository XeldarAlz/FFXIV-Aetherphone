using System.Threading;
using System.Threading.Tasks;
using Aetherphone.Core.Net;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Search;

namespace Aetherphone.Core.Songs;

internal sealed class SongSearchService : IDisposable
{
    private const int MaxResults = 25;
    private const int MinSongSeconds = 30;
    private const int MaxSongSeconds = 360;

    private readonly YoutubeClient youtube;
    private readonly RequestThrottle throttle;
    private readonly CancellationTokenSource cancellation = new();

    public SongSearchService(YoutubeClient youtube)
    {
        this.youtube = youtube;
        throttle = new RequestThrottle(1, TimeSpan.FromMilliseconds(400));
    }

    public async Task<Song[]> SearchAsync(string query, CancellationToken token)
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
                    if (seconds < MinSongSeconds || seconds > MaxSongSeconds)
                    {
                        continue;
                    }

                    var song = new Song(video.Id.Value, video.Title, video.Author.ChannelTitle, PickThumbnail(video.Thumbnails), seconds);
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
            AepLog.Warning($"Song search failed for '{query}': {exception.Message}");
            return Array.Empty<Song>();
        }
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
