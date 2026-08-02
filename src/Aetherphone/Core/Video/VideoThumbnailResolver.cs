using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Dalamud.Interface.Textures.TextureWraps;
using YoutubeExplode.Videos;

namespace Aetherphone.Core.Video;

// Tier 1 only - a YouTube thumbnail URL built straight from the video id, never a page fetch.
// There is no tier 2 (yt-dlp's own JSON) here: for non-YouTube URLs mpv's bundled ytdl_hook.lua
// invokes yt-dlp entirely inside the mpv process (see VideoPlayer.cs's "ytdl" option toggling) and
// never hands its output back to C#. Reaching it would mean a second, separate yt-dlp invocation
// solely for a thumbnail, which is the one thing this was told not to do - see the reply that
// shipped this file for what was flagged instead of decided. Anything that isn't YouTube falls
// straight through to the caller's placeholder glyph, same as a resolution failure - both are the
// same "nothing to show yet" case, not an error.
internal static class VideoThumbnailResolver
{
    // fallbackThumbnailUrl is whatever a site-specific enrichment step already put on the entry
    // (today, only YoutubeExplode's own thumbnail guess, kept as a courtesy) - it's used only when
    // the URL isn't YouTube, never raced against tier 1, so a YouTube row never fires two fetches.
    public static IDalamudTextureWrap? Get(RemoteImageCache cache, HttpService http, string? url,
        string? fallbackThumbnailUrl = null)
    {
        var id = url is not null ? VideoId.TryParse(url)?.Value : null;
        return id is not null
            ? cache.GetKeyed($"ytthumb:{id}", token => FetchAsync(http, id, token))
            : cache.Get(fallbackThumbnailUrl);
    }

    // maxresdefault.jpg only exists for videos uploaded at 720p or higher; hqdefault.jpg is
    // generated for every upload, so a miss on the first falls back to the second rather than
    // leaving the row without a thumbnail at all.
    private static async Task<byte[]?> FetchAsync(HttpService http, string videoId, CancellationToken token)
    {
        var maxres = await http.GetBytesAsync(new Uri($"https://i.ytimg.com/vi/{videoId}/maxresdefault.jpg"), token)
            .ConfigureAwait(false);
        if (maxres is not null)
        {
            return maxres;
        }

        return await http.GetBytesAsync(new Uri($"https://i.ytimg.com/vi/{videoId}/hqdefault.jpg"), token)
            .ConfigureAwait(false);
    }
}
