using System.Collections.Concurrent;
using Aetherphone.Core.Media;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

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

            var wrap = await DecodeToTextureAsync(bytes, key, token).ConfigureAwait(false);
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

    /// <summary>
    /// Decodes with ImageSharp (pure managed, cross-platform) and hands Dalamud already-decoded
    /// pixels via CreateFromRawAsync, instead of raw JPEG bytes via CreateFromImageAsync. The
    /// latter goes through a native WIC/COM decode path that has been observed to fail 100% of
    /// the time for these thumbnails under Wine, in a way that (unlike the search/network code)
    /// is not itself the cause of any exception this method's catch block was designed for -
    /// it just never produces a usable texture. Decoding is CPU-bound, so run it off whatever
    /// thread is awaiting this rather than blocking it.
    /// </summary>
    private async Task<IDalamudTextureWrap> DecodeToTextureAsync(byte[] bytes, string key, CancellationToken token)
    {
        var (pixels, width, height) = await Task.Run(() =>
        {
            using var image = Image.Load<Rgba32>(bytes);
            var buffer = new byte[image.Width * image.Height * 4];
            image.CopyPixelDataTo(buffer);
            return (buffer, image.Width, image.Height);
        }, token).ConfigureAwait(false);

        return await textures.CreateFromRawAsync(RawImageSpecification.Rgba32(width, height), pixels, key, token)
            .ConfigureAwait(false);
    }

    public void Dispose()
    {
        disposed = true;
        cancellation.Cancel();
        ready.DisposeAll();
        cancellation.Dispose();
    }
}
