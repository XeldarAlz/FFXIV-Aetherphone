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
    private readonly ConcurrentDictionary<LedgerKey, byte> loading = new();
    private readonly ConcurrentDictionary<string, DateTime> failed = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource cancellation = new();
    private volatile bool disposed;

    public RemoteImageCache(HttpService http, DiskCache disk)
    {
        this.http = http;
        this.disk = disk;
    }

    private static bool Fetchable(string? url)
    {
        return !string.IsNullOrEmpty(url) && url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    public IDalamudTextureWrap? Get(string? url) => GetAt(url, 0d);

    public IDalamudTextureWrap? GetAt(string? url, double timeSeconds)
    {
        if (!Fetchable(url))
        {
            return null;
        }

        var resolved = LegacyMediaHosts.Normalize(url!);
        if (ready.GetAt(new LedgerKey(resolved, TextureSizes.Native), timeSeconds) is { } wrap)
        {
            return wrap;
        }

        Request(resolved, TextureSizes.Native);
        return null;
    }

    public IDalamudTextureWrap? Sized(string? url, float drawnPixels)
    {
        return SizedAt(url, drawnPixels, 0d);
    }

    public IDalamudTextureWrap? SizedAt(string? url, float drawnPixels, double timeSeconds)
    {
        if (!Fetchable(url))
        {
            return null;
        }

        var resolved = LegacyMediaHosts.Normalize(url!);
        var level = TextureSizes.LevelFor(drawnPixels);
        if (ready.GetAt(new LedgerKey(resolved, level), timeSeconds) is { } wrap)
        {
            return wrap;
        }

        Request(resolved, level);
        return ready.Nearest(resolved, level, timeSeconds);
    }

    public AnimatedImage? GetAnimated(string? url)
    {
        if (!Fetchable(url))
        {
            return null;
        }

        var resolved = LegacyMediaHosts.Normalize(url!);
        if (ready.GetAnimated(resolved) is { } animation)
        {
            return animation;
        }

        if (ready.Get(resolved) is not null)
        {
            return null;
        }

        Request(resolved, TextureSizes.Native);
        return null;
    }

    private void Request(string resolved, int level)
    {
        Request(new LedgerKey(resolved, level), token => FetchThroughDiskAsync(resolved, token));
    }

    private void Request(LedgerKey key, Func<CancellationToken, Task<byte[]?>> fetch)
    {
        if (failed.TryGetValue(key.Name, out var failedAtUtc))
        {
            if (DateTime.UtcNow - failedAtUtc < FailureRetryFor)
            {
                return;
            }

            failed.TryRemove(key.Name, out _);
        }

        if (!loading.TryAdd(key, 0))
        {
            return;
        }

        _ = LoadAsync(key, fetch);
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

    public IDalamudTextureWrap? Resident(string key) => ready.Get(key);

    public IDalamudTextureWrap? ResidentAt(string key, double timeSeconds) =>
        ready.GetAt(new LedgerKey(key, TextureSizes.Native), timeSeconds);

    public IDalamudTextureWrap? GetSealed(string key, string url, Func<byte[], byte[]?> unseal,
        double timeSeconds = 0d)
    {
        if (ready.GetAt(new LedgerKey(key, TextureSizes.Native), timeSeconds) is { } wrap)
        {
            return wrap;
        }

        // The disk cache holds the sealed bytes, never the opened ones: a thread photo survives a
        // restart without a second download and without leaving readable pixels on disk.
        Request(new LedgerKey(key, TextureSizes.Native), async token =>
        {
            var opaque = await FetchThroughDiskAsync(url, token).ConfigureAwait(false);
            return opaque is null ? null : unseal(opaque);
        });
        return null;
    }

    public IDalamudTextureWrap? GetKeyed(string key, Func<CancellationToken, Task<byte[]?>> fetch)
    {
        if (ready.Get(key) is { } wrap)
        {
            return wrap;
        }

        Request(new LedgerKey(key, TextureSizes.Native), fetch);
        return null;
    }

    public Vector2 SizeOf(string? url)
    {
        return url is not null ? ready.SizeOf(LegacyMediaHosts.Normalize(url)) : Vector2.Zero;
    }

    public bool Failed(string? url)
    {
        return url is not null && failed.ContainsKey(LegacyMediaHosts.Normalize(url));
    }

    public AvatarHandle Avatar(string? url, float drawnPixels)
    {
        if (string.IsNullOrEmpty(url))
        {
            return AvatarHandle.Disabled;
        }

        var resolved = LegacyMediaHosts.Normalize(url);
        var texture = Sized(url, drawnPixels);
        if (texture is not null)
        {
            return new AvatarHandle(texture, AvatarLoadState.Ready, resolved);
        }

        var stalled = failed.ContainsKey(resolved);
        return new AvatarHandle(null, stalled ? AvatarLoadState.Failed : AvatarLoadState.Loading, resolved);
    }

    private async Task LoadAsync(LedgerKey key, Func<CancellationToken, Task<byte[]?>> fetch)
    {
        try
        {
            var token = cancellation.Token;
            var bytes = await fetch(token).ConfigureAwait(false);
            if (bytes is null)
            {
                failed[key.Name] = DateTime.UtcNow;
                return;
            }

            var kind = ImageProcessor.AnimationKindOf(bytes);
            if (kind != AnimationKind.None)
            {
                var animation = await ImageProcessor.DecodeAnimationAsync(Plugin.TextureProvider, bytes, kind,
                    $"Aetherphone.Anim.{key.Name}", TextureSizes.SizeOf(key.Level), token).ConfigureAwait(false);
                if (!ready.TryAddAnimated(key, animation))
                {
                    animation.Dispose();
                    return;
                }
            }
            else
            {
                var wrap = await ImageProcessor.DecodeToTextureAsync(Plugin.TextureProvider, bytes,
                        $"Aetherphone.Img.{key.Name}", ImageProcessor.MaxDecodePixels, TextureSizes.SizeOf(key.Level),
                        token)
                    .ConfigureAwait(false);
                if (!ready.TryAdd(key, wrap))
                {
                    wrap.Dispose();
                    return;
                }
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
            AepLog.Warning(exception, $"[Media] failed to load image {key.Name}");
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
