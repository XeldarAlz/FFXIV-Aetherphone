using System.Collections.Concurrent;
using Aetherphone.Core.Net;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Core.Media;

internal sealed class RemoteImageCache : IDisposable
{
    private const long TextureBudgetBytes = 160L * 1024 * 1024;
    private static readonly TimeSpan FailureRetryFor = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan DiskMaxAge = TimeSpan.FromDays(30);
    private readonly HttpService http;
    private readonly DiskCache disk;
    private readonly TextureLedger ready = new(TextureBudgetBytes);
    private readonly ConcurrentDictionary<string, byte> loading = new();
    private readonly ConcurrentDictionary<string, DateTime> failed = new();
    private readonly CancellationTokenSource cancellation = new();
    private volatile bool disposed;

    public RemoteImageCache(HttpService http, DiskCache disk)
    {
        this.http = http;
        this.disk = disk;
    }

    public IDalamudTextureWrap? Get(string? url)
    {
        if (string.IsNullOrEmpty(url) || !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var resolved = LegacyMediaHosts.Normalize(url);
        if (ready.Get(resolved) is { } wrap)
        {
            return wrap;
        }

        if (failed.TryGetValue(resolved, out var failedAtUtc))
        {
            if (DateTime.UtcNow - failedAtUtc < FailureRetryFor)
            {
                return null;
            }

            failed.TryRemove(resolved, out _);
        }

        if (!loading.TryAdd(resolved, 0))
        {
            return null;
        }

        _ = LoadAsync(resolved, token => FetchThroughDiskAsync(resolved, token));
        return null;
    }

    private async Task<byte[]?> FetchThroughDiskAsync(string url, CancellationToken token)
    {
        var cached = disk.Get(url, DiskMaxAge);
        if (cached is not null)
        {
            return cached;
        }

        var bytes = await http.GetBytesAsync(new Uri(url), token).ConfigureAwait(false);
        if (bytes is not null)
        {
            disk.Set(url, bytes);
        }

        return bytes;
    }

    public IDalamudTextureWrap? GetKeyed(string key, Func<CancellationToken, Task<byte[]?>> fetch)
    {
        if (ready.Get(key) is { } wrap)
        {
            return wrap;
        }

        if (failed.TryGetValue(key, out var failedAtUtc))
        {
            if (DateTime.UtcNow - failedAtUtc < FailureRetryFor)
            {
                return null;
            }

            failed.TryRemove(key, out _);
        }

        if (!loading.TryAdd(key, 0))
        {
            return null;
        }

        _ = LoadAsync(key, fetch);
        return null;
    }

    public Vector2 SizeOf(string? url)
    {
        return url is not null ? ready.SizeOf(LegacyMediaHosts.Normalize(url)) : Vector2.Zero;
    }

    public bool Failed(string? url) => url is not null && failed.ContainsKey(LegacyMediaHosts.Normalize(url));

    private async Task LoadAsync(string key, Func<CancellationToken, Task<byte[]?>> fetch)
    {
        try
        {
            var token = cancellation.Token;
            var bytes = await fetch(token).ConfigureAwait(false);
            if (bytes is null)
            {
                failed[key] = DateTime.UtcNow;
                return;
            }

            var wrap = await Plugin.TextureProvider.CreateFromImageAsync(bytes, $"Aetherphone.Img.{key}", token)
                .ConfigureAwait(false);
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
            AepLog.Warning($"[Media] failed to load image {key}: {exception.Message}");
        }
        finally
        {
            loading.TryRemove(key, out _);
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
