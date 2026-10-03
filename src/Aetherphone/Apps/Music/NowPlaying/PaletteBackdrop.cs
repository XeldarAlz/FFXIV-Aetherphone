using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Media;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Music.NowPlaying;

internal sealed class PaletteBackdrop : IDisposable
{
    private const int Columns = 6;
    private const int BodyRows = 10;
    private const int CornerRows = 4;
    private const int RowCount = BodyRows + CornerRows * 2;
    private const int VertexColumns = Columns + 1;
    private const int VertexRows = RowCount + 1;
    private const float CrossfadeSmoothTime = 0.2f;
    private const float BlobStrength = 0.82f;
    private const float BaseValue = 0.15f;
    private const float BaseSaturation = 0.75f;
    private const float BlobMinimumValue = 0.32f;
    private const float BlobMaximumValue = 0.62f;
    private const float BlobSaturationBoost = 1.15f;
    private const float BlobMaximumSaturation = 0.85f;
    private const float TopShade = 0.08f;
    private const float BottomShade = 0.42f;
    private const float DriftX = 0.36f;
    private const float DriftY = 0.38f;

    private static readonly float[] BlobRadius = [0.78f, 0.66f, 0.6f, 0.52f];
    private static readonly float[] SpeedX = [0.071f, 0.053f, 0.089f, 0.061f];
    private static readonly float[] SpeedY = [0.047f, 0.067f, 0.043f, 0.079f];
    private static readonly float[] PhaseX = [0.0f, 2.1f, 4.2f, 1.3f];
    private static readonly float[] PhaseY = [1.6f, 0.4f, 3.3f, 5.0f];

    private CancellationTokenSource? loading;
    private volatile PaletteResult? arrived;
    private string requestedKey = string.Empty;
    private string requestedUrl = string.Empty;
    private bool hasPalette;
    private ArtworkSwatch from = Treat(ArtworkPalette.Neutral);
    private ArtworkSwatch to = Treat(ArtworkPalette.Neutral);
    private Spring blend = new(1f);

    public void Request(RemoteImageCache images, string url, string seed)
    {
        var key = url.Length > 0 ? url : seed;
        if (string.Equals(key, requestedKey, StringComparison.Ordinal))
        {
            return;
        }

        requestedKey = key;
        requestedUrl = url;
        CancelLoad();
        if (url.Length == 0)
        {
            Present(FromSeed(seed));
            return;
        }

        if (!hasPalette)
        {
            Present(FromSeed(seed));
        }

        loading = new CancellationTokenSource();
        var token = loading.Token;
        _ = Task.Run(() => LoadAsync(images, url, seed, token));
    }

    public void Tick(float delta)
    {
        if (arrived is { } result)
        {
            arrived = null;
            if (string.Equals(result.Url, requestedUrl, StringComparison.Ordinal))
            {
                Present(result.Swatch);
            }
        }

        blend.Step(1f, CrossfadeSmoothTime, delta);
    }

    public Vector4 Accent => Current.Secondary;

    private ArtworkSwatch Current => ArtworkSwatch.Lerp(from, to, Math.Clamp(blend.Value, 0f, 1f));

    public void Draw(ImDrawListPtr drawList, Rect panel, float rounding, float clock)
    {
        if (panel.Width <= 1f || panel.Height <= 1f)
        {
            return;
        }

        var swatch = Current;
        var baseColor = BaseOf(swatch.Primary);
        Span<float> columnX = stackalloc float[VertexColumns];
        Span<float> rowY = stackalloc float[VertexRows];
        Span<uint> colors = stackalloc uint[VertexColumns * VertexRows];
        Span<Vector2> centers = stackalloc Vector2[ArtworkPalette.ColorCount];
        Span<float> radii = stackalloc float[ArtworkPalette.ColorCount];
        Layout(panel, rounding, columnX, rowY);
        var extent = MathF.Max(panel.Width, panel.Height);
        for (var blobIndex = 0; blobIndex < ArtworkPalette.ColorCount; blobIndex++)
        {
            var driftX = 0.5f + DriftX * MathF.Sin(clock * SpeedX[blobIndex] * MathF.Tau + PhaseX[blobIndex]);
            var driftY = 0.5f + DriftY * MathF.Cos(clock * SpeedY[blobIndex] * MathF.Tau + PhaseY[blobIndex]);
            centers[blobIndex] = new Vector2(panel.Min.X + panel.Width * driftX, panel.Min.Y + panel.Height * driftY);
            radii[blobIndex] = extent * BlobRadius[blobIndex];
        }

        for (var rowIndex = 0; rowIndex < VertexRows; rowIndex++)
        {
            var shadeAmount = TopShade + (BottomShade - TopShade) * ((rowY[rowIndex] - panel.Min.Y) / panel.Height);
            for (var columnIndex = 0; columnIndex < VertexColumns; columnIndex++)
            {
                var point = new Vector2(columnX[columnIndex], rowY[rowIndex]);
                var color = baseColor;
                for (var blobIndex = ArtworkPalette.ColorCount - 1; blobIndex >= 0; blobIndex--)
                {
                    var distance = Vector2.Distance(point, centers[blobIndex]) / radii[blobIndex];
                    var falloff = Math.Clamp(1f - distance, 0f, 1f);
                    falloff = falloff * falloff * (3f - 2f * falloff);
                    color = Vector4.Lerp(color, swatch.At(blobIndex), falloff * BlobStrength);
                }

                color *= 1f - Math.Clamp(shadeAmount, 0f, 1f);
                color.W = 1f;
                colors[rowIndex * VertexColumns + columnIndex] = ImGui.GetColorU32(color);
            }
        }

        var firstVertex = drawList.VtxBuffer.Size;
        for (var rowIndex = 0; rowIndex < RowCount; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < Columns; columnIndex++)
            {
                var topLeft = colors[rowIndex * VertexColumns + columnIndex];
                var topRight = colors[rowIndex * VertexColumns + columnIndex + 1];
                var bottomLeft = colors[(rowIndex + 1) * VertexColumns + columnIndex];
                var bottomRight = colors[(rowIndex + 1) * VertexColumns + columnIndex + 1];
                drawList.AddRectFilledMultiColor(new Vector2(columnX[columnIndex], rowY[rowIndex]),
                    new Vector2(columnX[columnIndex + 1], rowY[rowIndex + 1]), topLeft, topRight, bottomRight,
                    bottomLeft);
            }
        }

        RoundCorners(drawList, firstVertex, panel, rounding);
    }

    public void Dispose() => CancelLoad();

    private static void Layout(Rect panel, float rounding, Span<float> columnX, Span<float> rowY)
    {
        for (var columnIndex = 0; columnIndex < VertexColumns; columnIndex++)
        {
            columnX[columnIndex] = panel.Min.X + panel.Width * columnIndex / Columns;
        }

        var band = Math.Clamp(rounding, 0f, panel.Height * 0.25f);
        var bodyTop = panel.Min.Y + band;
        var bodyBottom = panel.Max.Y - band;
        for (var rowIndex = 0; rowIndex <= CornerRows; rowIndex++)
        {
            rowY[rowIndex] = panel.Min.Y + band * rowIndex / CornerRows;
            rowY[VertexRows - 1 - rowIndex] = panel.Max.Y - band * rowIndex / CornerRows;
        }

        for (var rowIndex = 1; rowIndex < BodyRows; rowIndex++)
        {
            rowY[CornerRows + rowIndex] = bodyTop + (bodyBottom - bodyTop) * rowIndex / BodyRows;
        }
    }

    private static void RoundCorners(ImDrawListPtr drawList, int firstVertex, Rect panel, float rounding)
    {
        var radius = Math.Clamp(rounding, 0f, MathF.Min(panel.Width, panel.Height) * 0.5f);
        if (radius <= 0.5f)
        {
            return;
        }

        var vertices = drawList.VtxBuffer.AsSpan();
        for (var vertexIndex = Math.Max(0, firstVertex); vertexIndex < vertices.Length; vertexIndex++)
        {
            ref var vertex = ref vertices[vertexIndex];
            var fromTop = vertex.Pos.Y - panel.Min.Y;
            var fromBottom = panel.Max.Y - vertex.Pos.Y;
            var depth = MathF.Min(fromTop, fromBottom);
            if (depth >= radius)
            {
                continue;
            }

            var offset = radius - MathF.Max(0f, depth);
            var inset = radius - MathF.Sqrt(MathF.Max(0f, radius * radius - offset * offset));
            vertex.Pos.X = Math.Clamp(vertex.Pos.X, panel.Min.X + inset, panel.Max.X - inset);
        }
    }

    private void Present(in ArtworkSwatch swatch)
    {
        from = hasPalette ? Current : Treat(swatch);
        to = Treat(swatch);
        hasPalette = true;
        blend.SnapTo(0f);
    }

    private void CancelLoad()
    {
        if (loading is null)
        {
            return;
        }

        loading.Cancel();
        loading.Dispose();
        loading = null;
    }

    private async Task LoadAsync(RemoteImageCache images, string url, string seed, CancellationToken token)
    {
        try
        {
            var bytes = await images.FetchBytesAsync(url, token).ConfigureAwait(false);
            if (token.IsCancellationRequested)
            {
                return;
            }

            var swatch = bytes is null ? FromSeed(seed) : Decode(bytes);
            arrived = new PaletteResult(url, swatch);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception exception)
        {
            AepLog.Debug(exception, "[Music] artwork palette extraction failed");
            arrived = new PaletteResult(url, FromSeed(seed));
        }
    }

    private static ArtworkSwatch Decode(byte[] bytes)
    {
        var (pixels, width, height) = ImageProcessor.DecodeRgba32(bytes, ArtworkPalette.SampleDimension);
        return ArtworkPalette.Extract(pixels, width, height);
    }

    private static ArtworkSwatch FromSeed(string seed)
    {
        var swatch = ArtGradient.FromName(seed);
        return new ArtworkSwatch(swatch.Bottom, swatch.Top, swatch.Glow, Palette.Darken(swatch.Bottom, 0.3f));
    }

    private static ArtworkSwatch Treat(in ArtworkSwatch swatch)
    {
        return new ArtworkSwatch(TreatBlob(swatch.Primary), TreatBlob(swatch.Secondary),
            TreatBlob(swatch.Tertiary), TreatBlob(swatch.Quaternary));
    }

    private static Vector4 TreatBlob(Vector4 color)
    {
        var hsv = HsvColor.FromRgb(color);
        var saturation = MathF.Min(hsv.Saturation * BlobSaturationBoost, BlobMaximumSaturation);
        var value = Math.Clamp(hsv.Value, BlobMinimumValue, BlobMaximumValue);
        return new HsvColor(hsv.Hue, saturation, value).ToRgb() with { W = 1f };
    }

    private static Vector4 BaseOf(Vector4 primary)
    {
        var hsv = HsvColor.FromRgb(primary);
        return new HsvColor(hsv.Hue, hsv.Saturation * BaseSaturation, BaseValue).ToRgb() with { W = 1f };
    }

    private sealed record PaletteResult(string Url, ArtworkSwatch Swatch);
}
