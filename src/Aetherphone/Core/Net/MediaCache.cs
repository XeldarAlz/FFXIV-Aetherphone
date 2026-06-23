using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Net;

internal readonly struct MediaResult
{
    public readonly IDalamudTextureWrap? Texture;
    public readonly bool Loading;

    public MediaResult(IDalamudTextureWrap? texture, bool loading)
    {
        Texture = texture;
        Loading = loading;
    }
}

internal sealed class MediaCache : IDisposable
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(30);

    private readonly ITextureProvider textures;
    private readonly DiskCache disk;
    private readonly CancellationTokenSource cancellation = new();

    private readonly ConcurrentDictionary<string, IDalamudTextureWrap> ready = new();
    private readonly ConcurrentDictionary<string, byte> inFlight = new();
    private readonly ConcurrentDictionary<string, byte> failed = new();

    public MediaCache(ITextureProvider textures, DiskCache disk)
    {
        this.textures = textures;
        this.disk = disk;
    }

    public MediaResult GetOrRequest(string key, Func<CancellationToken, Task<byte[]?>> source)
    {
        if (ready.TryGetValue(key, out var wrap))
        {
            return new MediaResult(wrap, false);
        }

        if (failed.ContainsKey(key))
        {
            return new MediaResult(null, false);
        }

        if (!inFlight.TryAdd(key, 0))
        {
            return new MediaResult(null, true);
        }

        _ = LoadAsync(key, source);
        return new MediaResult(null, true);
    }

    private async Task LoadAsync(string key, Func<CancellationToken, Task<byte[]?>> source)
    {
        try
        {
            var token = cancellation.Token;
            var bytes = disk.Get(key, MaxAge);
            if (bytes is null)
            {
                bytes = await source(token).ConfigureAwait(false);
                if (bytes is not null)
                {
                    disk.Set(key, bytes);
                }
            }

            if (bytes is null)
            {
                failed.TryAdd(key, 0);
                return;
            }

            var wrap = await textures.CreateFromImageAsync(bytes, key, token).ConfigureAwait(false);
            if (!ready.TryAdd(key, wrap))
            {
                wrap.Dispose();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            failed.TryAdd(key, 0);
            AepLog.Warning($"MediaCache load failed for {key}: {exception.Message}");
        }
        finally
        {
            inFlight.TryRemove(key, out _);
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        foreach (var wrap in ready.Values)
        {
            wrap.Dispose();
        }

        ready.Clear();
        cancellation.Dispose();
    }
}
