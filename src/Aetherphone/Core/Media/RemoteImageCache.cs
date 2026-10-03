using System.Collections.Concurrent;
using Aetherphone.Core.Net;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Core.Media;

internal sealed class RemoteImageCache : IDisposable
{
    private const long TextureBudgetBytes = 160L * 1024 * 1024;
    private static readonly TimeSpan FailureRetryFor = TimeSpan.FromMinutes(2);
    private const int MaxConcurrentDownloads = 6;
    private const long WantedWithinMilliseconds = 1_000;
    private static readonly TimeSpan DiskMaxAge = TimeSpan.FromDays(30);
    private static readonly byte[] Dropped = new byte[1];
    private readonly HttpService http;
    private readonly DiskCache disk;
    private readonly TextureLedger ready = new(TextureBudgetBytes);
    private readonly ConcurrentDictionary<LedgerKey, byte> loading = new();
    private readonly ConcurrentDictionary<LedgerKey, long> lastWanted = new();
    private readonly ConcurrentDictionary<string, DateTime> failed = new(StringComparer.Ordinal);
    private readonly RequestThrottle downloads = new(MaxConcurrentDownloads, TimeSpan.Zero);
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

    public async Task<byte[]?> FetchBytesAsync(string? url, CancellationToken token)
    {
        if (!Fetchable(url) || disposed)
        {
            return null;
        }

        var resolved = LegacyMediaHosts.Normalize(url!);
        var cached = disk.Get(resolved, DiskMaxAge);
        if (cached is not null)
        {
            return cached;
        }

        using var slot = await downloads.EnterAsync(token).ConfigureAwait(false);
        var bytes = await http.GetBytesAsync(new Uri(resolved), token).ConfigureAwait(false);
        if (bytes is not null)
        {
            disk.Set(resolved, bytes);
        }

        return bytes;
    }

    private void Request(string resolved, int level)
    {
        var key = new LedgerKey(resolved, level);
        if (!TryClaim(key))
        {
            return;
        }

        Start(key, token => FetchThroughDiskAsync(key, resolved, token));
    }

    private bool TryClaim(LedgerKey key)
    {
        if (failed.TryGetValue(key.Name, out var failedAtUtc))
        {
            if (DateTime.UtcNow - failedAtUtc < FailureRetryFor)
            {
                return false;
            }

            failed.TryRemove(key.Name, out _);
        }

        lastWanted[key] = Environment.TickCount64;
        return loading.TryAdd(key, 0);
    }

    private void Start(LedgerKey key, Func<CancellationToken, Task<byte[]?>> fetch)
    {
        _ = Task.Run(() => LoadAsync(key, fetch));
    }

    private async Task<byte[]?> FetchThroughDiskAsync(LedgerKey key, string url, CancellationToken token)
    {
        var cached = disk.Get(url, DiskMaxAge);
        if (cached is not null)
        {
            return cached;
        }

        using var slot = await downloads.EnterAsync(token).ConfigureAwait(false);
        if (!StillWanted(key))
        {
            AepLog.Verbose($"[Media] dropped {key.Name}, it scrolled away before a download slot opened");
            return Dropped;
        }

        var bytes = await http.GetBytesAsync(new Uri(url), token).ConfigureAwait(false);
        if (bytes is not null)
        {
            disk.Set(url, bytes);
        }

        return bytes;
    }

    private bool StillWanted(LedgerKey key)
    {
        return lastWanted.TryGetValue(key, out var wantedAt)
               && Environment.TickCount64 - wantedAt <= WantedWithinMilliseconds;
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
        var ledgerKey = new LedgerKey(key, TextureSizes.Native);
        if (!TryClaim(ledgerKey))
        {
            return null;
        }

        Start(ledgerKey, async token =>
        {
            var opaque = await FetchThroughDiskAsync(ledgerKey, url, token).ConfigureAwait(false);
            if (opaque is null || ReferenceEquals(opaque, Dropped))
            {
                return opaque;
            }

            return unseal(opaque);
        });
        return null;
    }

    public IDalamudTextureWrap? GetKeyed(string key, Func<CancellationToken, Task<byte[]?>> fetch)
    {
        if (ready.Get(key) is { } wrap)
        {
            return wrap;
        }

        var ledgerKey = new LedgerKey(key, TextureSizes.Native);
        if (TryClaim(ledgerKey))
        {
            Start(ledgerKey, fetch);
        }

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
            if (ReferenceEquals(bytes, Dropped))
            {
                return;
            }

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
        catch (ObjectDisposedException) when (disposed)
        {
        }
        catch (Exception exception)
        {
            failed[key.Name] = DateTime.UtcNow;
            AepLog.Warning(exception, $"[Media] failed to load image {key.Name}");
        }
        finally
        {
            lastWanted.TryRemove(key, out _);
            loading.TryRemove(key, out _);
        }
    }

    public void Dispose()
    {
        disposed = true;
        cancellation.Cancel();
        ready.DisposeAll();
        downloads.Dispose();
        cancellation.Dispose();
    }
}
