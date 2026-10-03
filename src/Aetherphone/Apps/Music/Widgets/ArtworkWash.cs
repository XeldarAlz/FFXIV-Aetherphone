using Aetherphone.Core;
using Aetherphone.Core.Media;
using Aetherphone.Core.Theme;

namespace Aetherphone.Apps.Music.Widgets;

internal sealed class ArtworkWash : IDisposable
{
    public const float TopLuminance = 0.3f;
    public const float BottomLuminance = 0.12f;

    private const int SampleSide = 24;
    private const int Capacity = 32;
    private const int Channels = 4;
    private const float OpaqueAlpha = 0.5f;
    private const float BaseWeight = 0.2f;
    private static readonly Vector4 Neutral = new(0.32f, 0.32f, 0.34f, 1f);

    private readonly MediaCache media;
    private readonly object gate = new();
    private readonly Dictionary<string, Vector4> colors = new(StringComparer.Ordinal);
    private readonly HashSet<string> pending = new(StringComparer.Ordinal);
    private readonly HashSet<string> failed = new(StringComparer.Ordinal);
    private volatile bool disposed;

    public ArtworkWash(MediaCache media)
    {
        this.media = media;
    }

    public bool TryGet(string url, Func<CancellationToken, Task<byte[]?>> source, out Vector4 color)
    {
        lock (gate)
        {
            if (colors.TryGetValue(url, out color))
            {
                return true;
            }

            if (failed.Contains(url) || !pending.Add(url))
            {
                return false;
            }
        }

        _ = Task.Run(() => SampleAsync(url, source));
        return false;
    }

    public bool Failed(string url)
    {
        lock (gate)
        {
            return failed.Contains(url);
        }
    }

    public static Vector4 Average(ReadOnlySpan<byte> rgba)
    {
        var red = 0f;
        var green = 0f;
        var blue = 0f;
        var total = 0f;
        for (var index = 0; index + Channels - 1 < rgba.Length; index += Channels)
        {
            if (rgba[index + 3] / 255f < OpaqueAlpha)
            {
                continue;
            }

            var pixelRed = rgba[index] / 255f;
            var pixelGreen = rgba[index + 1] / 255f;
            var pixelBlue = rgba[index + 2] / 255f;
            var maximum = MathF.Max(pixelRed, MathF.Max(pixelGreen, pixelBlue));
            var minimum = MathF.Min(pixelRed, MathF.Min(pixelGreen, pixelBlue));
            var saturation = maximum > 0f ? (maximum - minimum) / maximum : 0f;
            var weight = BaseWeight + saturation;
            red += pixelRed * weight;
            green += pixelGreen * weight;
            blue += pixelBlue * weight;
            total += weight;
        }

        return total > 0f ? new Vector4(red / total, green / total, blue / total, 1f) : Neutral;
    }

    public static Vector4 Top(Vector4 average) => Palette.ShadeToLuminance(average with { W = 1f }, TopLuminance);

    public static Vector4 Bottom(Vector4 average) =>
        Palette.ShadeToLuminance(average with { W = 1f }, BottomLuminance);

    private async Task SampleAsync(string url, Func<CancellationToken, Task<byte[]?>> source)
    {
        var resolved = false;
        var color = Neutral;
        try
        {
            var bytes = await media.BytesFor(url, source).ConfigureAwait(false);
            if (bytes is not null && !disposed)
            {
                var (pixels, _, _) = ImageProcessor.DecodeRgba32(bytes, SampleSide);
                color = Average(pixels);
                resolved = true;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"[NowPlayingWidget] could not sample artwork colour for {url}");
        }

        lock (gate)
        {
            pending.Remove(url);
            if (!resolved)
            {
                failed.Add(url);
                return;
            }

            if (colors.Count >= Capacity)
            {
                colors.Clear();
            }

            colors[url] = color;
        }
    }

    public void Dispose()
    {
        disposed = true;
    }
}
