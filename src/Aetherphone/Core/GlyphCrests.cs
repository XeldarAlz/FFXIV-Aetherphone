using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ManagedFontAtlas;

namespace Aetherphone.Core;

internal readonly record struct GlyphCrest(float First, float FirstWidth, float FirstTop, float Second,
    float SecondWidth, float SecondTop)
{
    public bool Twin => SecondWidth > 0f;
}

internal static class GlyphCrests
{
    private const int InkThreshold = 128;
    private const float PeakTolerance = 0.08f;
    private const float TwinGap = 0.15f;
    private const float TwinBalance = 0.4f;
    private const int MaximumSpan = 512;
    private const float KeyPrecision = 65536f;

    private static readonly (int Low, int High)[] MeasuredRanges =
    {
        (0x0021, 0x052F),
        (0x1E00, 0x1EFF),
    };

    private static Dictionary<long, GlyphCrest> crests = new();

    public static bool TryGet(Vector2 uvMin, out GlyphCrest crest) =>
        Volatile.Read(ref crests).TryGetValue(Key(uvMin.X, uvMin.Y), out crest);

    public static unsafe void Measure(IFontAtlasBuildToolkit toolkit)
    {
        var atlas = toolkit.NewImAtlas;
        var width = atlas.TexWidth;
        var height = atlas.TexHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var measured = new Dictionary<long, GlyphCrest>();
        var fonts = toolkit.Fonts;
        for (var fontIndex = 0; fontIndex < fonts.Length; fontIndex++)
        {
            var font = fonts[fontIndex];
            for (var rangeIndex = 0; rangeIndex < MeasuredRanges.Length; rangeIndex++)
            {
                var (low, high) = MeasuredRanges[rangeIndex];
                for (var codepoint = low; codepoint <= high; codepoint++)
                {
                    ImFontGlyphPtr glyph = font.FindGlyphNoFallback((char)codepoint);
                    if (glyph.IsNull || glyph.Visible == 0)
                    {
                        continue;
                    }

                    var key = Key(glyph.U0, glyph.V0);
                    if (measured.ContainsKey(key) || glyph.TextureIndex >= atlas.Textures.Size)
                    {
                        continue;
                    }

                    var texture = atlas.Textures[(int)glyph.TextureIndex];
                    var left = (int)MathF.Round(glyph.U0 * width);
                    var top = (int)MathF.Round(glyph.V0 * height);
                    var right = (int)MathF.Round(glyph.U1 * width);
                    var bottom = (int)MathF.Round(glyph.V1 * height);
                    if (left < 0 || top < 0 || right > width || bottom > height)
                    {
                        continue;
                    }

                    if (texture.TexPixelsAlpha8 != null)
                    {
                        var pixels = new ReadOnlySpan<byte>(texture.TexPixelsAlpha8, width * height);
                        if (TryFind(pixels, width, left, top, right - left, bottom - top, out var crest))
                        {
                            measured[key] = crest;
                        }
                    }
                    else if (texture.TexPixelsRGBA32 != null)
                    {
                        var pixels = new ReadOnlySpan<uint>(texture.TexPixelsRGBA32, width * height);
                        if (TryFind(pixels, width, left, top, right - left, bottom - top, out var crest))
                        {
                            measured[key] = crest;
                        }
                    }
                }
            }
        }

        Volatile.Write(ref crests, measured);
    }

    public static bool TryFind(ReadOnlySpan<byte> pixels, int stride, int left, int top, int columns, int rows,
        out GlyphCrest crest)
    {
        crest = default;
        if (columns <= 0 || rows <= 0 || columns > MaximumSpan)
        {
            return false;
        }

        Span<int> peaks = stackalloc int[columns];
        for (var column = 0; column < columns; column++)
        {
            peaks[column] = -1;
            for (var row = 0; row < rows; row++)
            {
                if (pixels[(top + row) * stride + left + column] >= InkThreshold)
                {
                    peaks[column] = row;
                    break;
                }
            }
        }

        return TryFindInPeaks(peaks, rows, out crest);
    }

    public static bool TryFind(ReadOnlySpan<uint> pixels, int stride, int left, int top, int columns, int rows,
        out GlyphCrest crest)
    {
        crest = default;
        if (columns <= 0 || rows <= 0 || columns > MaximumSpan)
        {
            return false;
        }

        Span<int> peaks = stackalloc int[columns];
        for (var column = 0; column < columns; column++)
        {
            peaks[column] = -1;
            for (var row = 0; row < rows; row++)
            {
                if (pixels[(top + row) * stride + left + column] >> 24 >= InkThreshold)
                {
                    peaks[column] = row;
                    break;
                }
            }
        }

        return TryFindInPeaks(peaks, rows, out crest);
    }

    public static bool TryFindInPeaks(ReadOnlySpan<int> peaks, int rows, out GlyphCrest crest)
    {
        crest = default;
        var highest = int.MaxValue;
        for (var column = 0; column < peaks.Length; column++)
        {
            if (peaks[column] >= 0 && peaks[column] < highest)
            {
                highest = peaks[column];
            }
        }

        if (highest == int.MaxValue)
        {
            return false;
        }

        var limit = highest + Math.Max(1, (int)(rows * PeakTolerance));
        var bestStart = -1;
        var bestLength = 0;
        var nextStart = -1;
        var nextLength = 0;
        var runStart = -1;
        for (var column = 0; column <= peaks.Length; column++)
        {
            var peaked = column < peaks.Length && peaks[column] >= 0 && peaks[column] <= limit;
            if (peaked)
            {
                if (runStart < 0)
                {
                    runStart = column;
                }

                continue;
            }

            if (runStart < 0)
            {
                continue;
            }

            var length = column - runStart;
            if (length > bestLength)
            {
                nextStart = bestStart;
                nextLength = bestLength;
                bestStart = runStart;
                bestLength = length;
            }
            else if (length > nextLength)
            {
                nextStart = runStart;
                nextLength = length;
            }

            runStart = -1;
        }

        var columns = (float)peaks.Length;
        var twin = nextLength > 0 && nextLength >= bestLength * TwinBalance &&
                   Gap(bestStart, bestLength, nextStart, nextLength) >= Math.Max(2f, columns * TwinGap);
        if (!twin)
        {
            crest = new GlyphCrest(Middle(bestStart, bestLength, columns), bestLength / columns,
                RunTop(peaks, bestStart, bestLength, rows), 0f, 0f, 0f);
            return true;
        }

        var (leftStart, leftLength, rightStart, rightLength) = bestStart < nextStart
            ? (bestStart, bestLength, nextStart, nextLength)
            : (nextStart, nextLength, bestStart, bestLength);
        crest = new GlyphCrest(Middle(leftStart, leftLength, columns), leftLength / columns,
            RunTop(peaks, leftStart, leftLength, rows), Middle(rightStart, rightLength, columns),
            rightLength / columns, RunTop(peaks, rightStart, rightLength, rows));
        return true;
    }

    private static float Gap(int firstStart, int firstLength, int secondStart, int secondLength) =>
        firstStart < secondStart
            ? secondStart - (firstStart + firstLength)
            : firstStart - (secondStart + secondLength);

    private static float Middle(int start, int length, float columns) => (start + length * 0.5f) / columns;

    private static float RunTop(ReadOnlySpan<int> peaks, int start, int length, int rows)
    {
        var top = int.MaxValue;
        for (var column = start; column < start + length; column++)
        {
            top = Math.Min(top, peaks[column]);
        }

        return top / (float)rows;
    }

    private static long Key(float u, float v) =>
        ((long)MathF.Round(u * KeyPrecision) << 32) | (uint)MathF.Round(v * KeyPrecision);
}
