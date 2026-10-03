using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Dalamud.Interface.Textures.TextureWraps;
using YoutubeExplode.Videos;

namespace Aetherphone.Core.Video;

internal static class VideoThumbnailResolver
{
    public static IDalamudTextureWrap? Get(RemoteImageCache cache, HttpService http, string? url,
        string? fallbackThumbnailUrl = null)
    {
        var id = url is not null ? VideoId.TryParse(url)?.Value : null;
        return id is not null
            ? cache.GetKeyed($"ytthumb:{id}", token => FetchAsync(http, id, token))
            : cache.Get(fallbackThumbnailUrl);
    }

    public static Func<CancellationToken, Task<byte[]?>>? Source(HttpService http, string? url, out string key)
    {
        var id = url is not null ? VideoId.TryParse(url)?.Value : null;
        if (id is null)
        {
            key = string.Empty;
            return null;
        }

        key = $"ytthumb:{id}";
        return token => FetchAsync(http, id, token);
    }

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
