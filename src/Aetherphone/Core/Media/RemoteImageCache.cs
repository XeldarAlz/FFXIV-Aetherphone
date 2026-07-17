using System.Collections.Concurrent;
using System.Numerics;
using Aetherphone.Core.Net;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Core.Media;

internal sealed class RemoteImageCache : IDisposable
{
    private const long TextureBudgetBytes = 160L * 1024 * 1024;
    private static readonly TimeSpan FailureRetryFor = TimeSpan.FromMinutes(2);
    private readonly HttpService http;
    private readonly TextureLedger ready = new(TextureBudgetBytes);
    private readonly ConcurrentDictionary<string, byte> loading = new();
    private readonly ConcurrentDictionary<string, DateTime> failed = new();
    private readonly CancellationTokenSource cancellation = new();
    private volatile bool disposed;

    public RemoteImageCache(HttpService http)
    {
        this.http = http;
    }

    public IDalamudTextureWrap? Get(string? url)
    {
        if (string.IsNullOrEmpty(url) || !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (ready.Get(url) is { } wrap)
        {
            return wrap;
        }

        if (failed.TryGetValue(url, out var failedAtUtc))
        {
            if (DateTime.UtcNow - failedAtUtc < FailureRetryFor)
            {
                return null;
            }

            failed.TryRemove(url, out _);
        }

        if (!loading.TryAdd(url, 0))
        {
            return null;
        }

        _ = LoadAsync(url, token => http.GetBytesAsync(new Uri(url), token));
        return null;
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
        return url is not null ? ready.SizeOf(url) : Vector2.Zero;
    }

    public bool Failed(string? url) => url is not null && failed.ContainsKey(url);

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
