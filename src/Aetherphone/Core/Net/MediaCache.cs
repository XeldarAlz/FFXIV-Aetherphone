using System.Collections.Concurrent;
using Aetherphone.Core.Media;
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
    private const long TextureBudgetBytes = 96L * 1024 * 1024;
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(30);
    private static readonly TimeSpan FailureRetryFor = TimeSpan.FromMinutes(2);
    private readonly ITextureProvider textures;
    private readonly DiskCache disk;
    private readonly CancellationTokenSource cancellation = new();
    private readonly TextureLedger ready = new(TextureBudgetBytes);
    private readonly ConcurrentDictionary<string, byte> inFlight = new();
    private readonly ConcurrentDictionary<string, DateTime> failed = new();
    private volatile bool disposed;

    public MediaCache(ITextureProvider textures, DiskCache disk)
    {
        this.textures = textures;
        this.disk = disk;
    }

    public MediaResult GetOrRequest(string key, Func<CancellationToken, Task<byte[]?>> source)
    {
        if (ready.Get(key) is { } wrap)
        {
            return new MediaResult(wrap, false);
        }

        if (failed.TryGetValue(key, out var failedAtUtc))
        {
            if (DateTime.UtcNow - failedAtUtc < FailureRetryFor)
            {
                return new MediaResult(null, false);
            }

            failed.TryRemove(key, out _);
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
                failed[key] = DateTime.UtcNow;
                return;
            }

            var wrap = await ImageProcessor.DecodeToTextureAsync(textures, bytes, key, token).ConfigureAwait(false);
            if (!ready.TryAdd(key, wrap))
            {
                wrap.Dispose();
                return;
            }

            if (disposed && ready.TryRemove(key, out var lateWrap))
            {
                lateWrap.Dispose();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            failed[key] = DateTime.UtcNow;
            AepLog.Warning($"MediaCache load failed for {key}: {exception.Message}");
        }
        finally
        {
            inFlight.TryRemove(key, out _);
        }
    }

    public void Dispose()
    {
        disposed = true;
        cancellation.Cancel();
        ready.DisposeAll();
        cancellation.Dispose();
    }
}
