using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ManagedFontAtlas;

namespace Aetherphone.Core;

internal readonly record struct GlyphCrest(Vector2 First, float FirstWidth, Vector2 Second, float SecondWidth)
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

    private static readonly (int Low, int High)[] MeasuredRanges = { (0x0021, 0x052F), (0x1E00, 0x1EFF) };

    private static Dictionary<long, GlyphCrest> crests = new();

    public static bool TryGet(Vector2 uvMin, out GlyphCrest crest) =>
        Volatile.Read(ref crests).TryGetValue(Key(uvMin.X, uvMin.Y), out crest);

    public static unsafe void Measure(IFontAtlasBuildToolkit toolkit)
    {
        var atlas = toolkit.NewImAtlas;
        var width = atlas.TexWidth;
        var height = atlas.TexHeight;
        var measured = new Dictionary<long, GlyphCrest>();
        var fonts = toolkit.Fonts;
        for (var fontIndex = 0; fontIndex < fonts.Length; fontIndex++)
        {
            foreach (var (low, high) in MeasuredRanges)
            {
                for (var codepoint = low; codepoint <= high; codepoint++)
                {
                    ImFontGlyphPtr glyph = fonts[fontIndex].FindGlyphNoFallback((char)codepoint);
                    if (glyph.IsNull || glyph.Visible == 0 || glyph.TextureIndex >= atlas.Textures.Size)
                    {
                        continue;
                    }

                    var texture = atlas.Textures[(int)glyph.TextureIndex];
                    var bytesPerPixel = texture.TexPixelsAlpha8 != null ? 1 : 4;
                    var pixels = bytesPerPixel == 1 ? texture.TexPixelsAlpha8 : (byte*)texture.TexPixelsRGBA32;
                    var left = (int)MathF.Round(glyph.U0 * width);
                    var top = (int)MathF.Round(glyph.V0 * height);
                    var right = (int)MathF.Round(glyph.U1 * width);
                    var bottom = (int)MathF.Round(glyph.V1 * height);
                    if (pixels == null || left < 0 || top < 0 || right > width || bottom > height)
                    {
                        continue;
                    }

                    var span = new ReadOnlySpan<byte>(pixels, width * height * bytesPerPixel);
                    if (TryFind(span, bytesPerPixel, width, left, top, right - left, bottom - top, out var crest))
                    {
                        measured.TryAdd(Key(glyph.U0, glyph.V0), crest);
                    }
                }
            }
        }

        Volatile.Write(ref crests, measured);
    }

    public static bool TryFind(ReadOnlySpan<byte> pixels, int bytesPerPixel, int stride, int left, int top,
        int columns, int rows, out GlyphCrest crest)
    {
        crest = default;
        if (columns <= 0 || rows <= 0 || columns > MaximumSpan)
        {
            return false;
        }

        Span<int> peaks = stackalloc int[columns];
        var highest = int.MaxValue;
        for (var column = 0; column < columns; column++)
        {
            peaks[column] = -1;
            for (var row = 0; row < rows; row++)
            {
                if (pixels[((top + row) * stride + left + column) * bytesPerPixel + bytesPerPixel - 1] >= InkThreshold)
                {
                    peaks[column] = row;
                    highest = Math.Min(highest, row);
                    break;
                }
            }
        }

        if (highest == int.MaxValue)
        {
            return false;
        }

        var limit = highest + Math.Max(1, (int)(rows * PeakTolerance));
        var best = (Start: 0, Length: 0);
        var next = (Start: 0, Length: 0);
        var runStart = -1;
        for (var column = 0; column <= columns; column++)
        {
            if (column < columns && peaks[column] >= 0 && peaks[column] <= limit)
            {
                runStart = runStart < 0 ? column : runStart;
                continue;
            }

            if (runStart < 0)
            {
                continue;
            }

            var run = (Start: runStart, Length: column - runStart);
            if (run.Length > best.Length)
            {
                (next, best) = (best, run);
            }
            else if (run.Length > next.Length)
            {
                next = run;
            }

            runStart = -1;
        }

        var gap = Math.Abs(next.Start - best.Start) - (next.Start > best.Start ? best.Length : next.Length);
        if (next.Length < Math.Max(1f, best.Length * TwinBalance) || gap < Math.Max(2f, columns * TwinGap))
        {
            crest = new GlyphCrest(Point(peaks, best, rows), best.Length / (float)columns, default, 0f);
            return true;
        }

        var (first, second) = best.Start < next.Start ? (best, next) : (next, best);
        crest = new GlyphCrest(Point(peaks, first, rows), first.Length / (float)columns, Point(peaks, second, rows),
            second.Length / (float)columns);
        return true;
    }

    private static Vector2 Point(ReadOnlySpan<int> peaks, (int Start, int Length) run, int rows)
    {
        var top = int.MaxValue;
        for (var column = run.Start; column < run.Start + run.Length; column++)
        {
            top = Math.Min(top, peaks[column]);
        }

        return new Vector2((run.Start + run.Length * 0.5f) / peaks.Length, top / (float)rows);
    }

    private static long Key(float u, float v) =>
        ((long)MathF.Round(u * KeyPrecision) << 32) | (uint)MathF.Round(v * KeyPrecision);
}
