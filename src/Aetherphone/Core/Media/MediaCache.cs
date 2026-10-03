using Aetherphone.Core.Net;
using System.Collections.Concurrent;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Media;

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
    private readonly ConcurrentDictionary<LedgerKey, byte> inFlight = new();
    private readonly ConcurrentDictionary<string, DateTime> failed = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Lazy<Task<byte[]?>>> fetches = new(StringComparer.Ordinal);
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

        return Request(new LedgerKey(key, TextureSizes.Native), source, null);
    }

    public MediaResult GetOrRequest(string key, Func<CancellationToken, Task<byte[]?>> source, float drawnPixels)
    {
        var level = TextureSizes.LevelFor(drawnPixels);
        if (ready.Get(key, level) is { } wrap)
        {
            return new MediaResult(wrap, false);
        }

        return Request(new LedgerKey(key, level), source, ready.Nearest(key, level));
    }

    public Task<byte[]?> BytesFor(string key, Func<CancellationToken, Task<byte[]?>> source) =>
        BytesAsync(key, source, cancellation.Token);

    private MediaResult Request(LedgerKey key, Func<CancellationToken, Task<byte[]?>> source,
        IDalamudTextureWrap? standIn)
    {
        if (failed.TryGetValue(key.Name, out var failedAtUtc))
        {
            if (DateTime.UtcNow - failedAtUtc < FailureRetryFor)
            {
                return new MediaResult(standIn, false);
            }

            failed.TryRemove(key.Name, out _);
        }

        if (!inFlight.TryAdd(key, 0))
        {
            return new MediaResult(standIn, true);
        }

        _ = Task.Run(() => LoadAsync(key, source));
        return new MediaResult(standIn, true);
    }

    private async Task<byte[]?> BytesAsync(string name, Func<CancellationToken, Task<byte[]?>> source,
        CancellationToken token)
    {
        if (disk.Get(name, MaxAge) is { } cached)
        {
            return cached;
        }

        var shared = fetches.GetOrAdd(name,
            _ => new Lazy<Task<byte[]?>>(() => FetchAsync(name, source, token)));
        try
        {
            return await shared.Value.ConfigureAwait(false);
        }
        finally
        {
            fetches.TryRemove(name, out _);
        }
    }

    private async Task<byte[]?> FetchAsync(string name, Func<CancellationToken, Task<byte[]?>> source,
        CancellationToken token)
    {
        var bytes = await source(token).ConfigureAwait(false);
        if (bytes is not null)
        {
            disk.Set(name, bytes);
        }

        return bytes;
    }

    private async Task LoadAsync(LedgerKey key, Func<CancellationToken, Task<byte[]?>> source)
    {
        try
        {
            var token = cancellation.Token;
            var bytes = await BytesAsync(key.Name, source, token).ConfigureAwait(false);
            if (bytes is null)
            {
                failed[key.Name] = DateTime.UtcNow;
                return;
            }

            var wrap = await ImageProcessor.DecodeToTextureAsync(textures, bytes, key.Name,
                ImageProcessor.MaxDecodePixels, TextureSizes.SizeOf(key.Level), token).ConfigureAwait(false);
            if (!ready.TryAdd(key, wrap))
            {
                wrap.Dispose();
                return;
            }

            if (disposed)
            {
                ready.RemoveAndDispose(key);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            failed[key.Name] = DateTime.UtcNow;
            AepLog.Warning(exception, $"MediaCache load failed for {key.Name}");
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
