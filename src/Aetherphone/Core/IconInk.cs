using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ManagedFontAtlas;

namespace Aetherphone.Core;

internal readonly record struct IconInkBox(Vector2 Center, Vector2 Size, float Area, float Edge);

internal readonly record struct IconInkMark(Vector2 Center, float Scale);

internal sealed class IconInk
{
    public static readonly Vector2 BoxCenter = new(0.5f, 0.5f);

    public static readonly IconInkMark Unmeasured = new(BoxCenter, 1f);

    // Tabler's ink boxes average 0.8 of the box (geometric mean of width and height), its star reaches 22 of 24
    // units, and its stroke is 2 units.
    public const float OpticalSize = 0.80f;
    public const float MaximumExtent = 22f / 24f;
    public const float OutlineWeight = 0.10f;
    public const float SolidWeight = 0.16f;
    public const float SolidAllowance = 0.04f;

    private const int MaximumSpan = 512;

    private readonly nint font;
    private readonly Dictionary<char, IconInkMark> marks;

    private IconInk(nint font, Dictionary<char, IconInkMark> marks)
    {
        this.font = font;
        this.marks = marks;
    }

    public unsafe bool Describes(ImFontPtr candidate) => (nint)candidate.Handle == font;

    public IconInkMark Mark(char codepoint) => marks.TryGetValue(codepoint, out var mark) ? mark : Unmeasured;

    public static Vector2 Pen(Vector2 center, Vector2 inkOffset, float ratio)
    {
        var anchor = new Vector2(MathF.Round(center.X - inkOffset.X), MathF.Round(center.Y - inkOffset.Y)) + inkOffset;
        return anchor - inkOffset * ratio;
    }

    public static unsafe IconInk Measure(IFontAtlasBuildToolkitPostBuild toolkit, ImFontPtr font)
    {
        var atlas = toolkit.NewImAtlas;
        var width = atlas.TexWidth;
        var height = atlas.TexHeight;
        var marks = new Dictionary<char, IconInkMark>();
        if (font.Handle == null || font.FontSize <= 0f)
        {
            return new IconInk(0, marks);
        }

        for (var codepoint = IconPlan.FirstIconCodepoint; codepoint <= IconPlan.LastIconCodepoint; codepoint++)
        {
            ImFontGlyphPtr glyph = font.FindGlyphNoFallback((char)codepoint);
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

            var texel = new Vector2((glyph.X1 - glyph.X0) / Math.Max(right - left, 1),
                (glyph.Y1 - glyph.Y0) / Math.Max(bottom - top, 1)) / font.FontSize;
            var span = new ReadOnlySpan<byte>(pixels, width * height * bytesPerPixel);
            if (!TryMeasure(span, bytesPerPixel, width, left, top, right - left, bottom - top, texel, out var box))
            {
                continue;
            }

            var tabler = codepoint >= IconPlan.FirstTablerCodepoint && codepoint <= IconPlan.LastTablerCodepoint;
            marks[(char)codepoint] = new IconInkMark(box.Center, OpticalScale(box, tabler));
        }

        return new IconInk((nint)font.Handle, marks);
    }

    public static float OpticalScale(in IconInkBox box, bool onTablerGrid)
    {
        var extent = MathF.Max(box.Size.X, box.Size.Y);
        if (extent <= 0f || box.Edge <= 0f)
        {
            return 1f;
        }

        var fit = onTablerGrid
            ? 1f
            : MathF.Min(1f, MathF.Min(OpticalSize / MathF.Sqrt(box.Size.X * box.Size.Y), MaximumExtent / extent));
        var weight = 2f * box.Area / box.Edge;
        var solid = Math.Clamp((weight - OutlineWeight) / (SolidWeight - OutlineWeight), 0f, 1f);
        return fit * (1f - SolidAllowance * solid);
    }

    public static bool TryMeasure(ReadOnlySpan<byte> pixels, int bytesPerPixel, int stride, int left, int top,
        int columns, int rows, Vector2 texel, out IconInkBox box)
    {
        box = new IconInkBox(BoxCenter, Vector2.Zero, 0f, 0f);
        if (columns <= 0 || rows <= 0 || columns > MaximumSpan || rows > MaximumSpan)
        {
            return false;
        }

        Span<byte> columnPeaks = stackalloc byte[columns];
        Span<byte> rowPeaks = stackalloc byte[rows];
        var area = 0f;
        var edge = 0f;
        for (var row = -1; row < rows; row++)
        {
            for (var column = -1; column < columns; column++)
            {
                var alpha = Alpha(pixels, bytesPerPixel, stride, left, top, columns, rows, column, row);
                var across = Alpha(pixels, bytesPerPixel, stride, left, top, columns, rows, column + 1, row) - alpha;
                var down = Alpha(pixels, bytesPerPixel, stride, left, top, columns, rows, column, row + 1) - alpha;
                edge += MathF.Sqrt(across * texel.Y * across * texel.Y + down * texel.X * down * texel.X);
                if (column < 0 || row < 0)
                {
                    continue;
                }

                columnPeaks[column] = Math.Max(columnPeaks[column], (byte)alpha);
                rowPeaks[row] = Math.Max(rowPeaks[row], (byte)alpha);
                area += alpha;
            }
        }

        if (!TryFindEdges(columnPeaks, out var inkLeft, out var inkRight)
            || !TryFindEdges(rowPeaks, out var inkTop, out var inkBottom))
        {
            return false;
        }

        box = new IconInkBox(
            new Vector2((inkLeft + inkRight) * 0.5f / columns, (inkTop + inkBottom) * 0.5f / rows),
            new Vector2(inkRight - inkLeft, inkBottom - inkTop) * texel,
            area / 255f * texel.X * texel.Y, edge / 255f);
        return true;
    }

    private static float Alpha(ReadOnlySpan<byte> pixels, int bytesPerPixel, int stride, int left, int top,
        int columns, int rows, int column, int row)
    {
        if (column < 0 || row < 0 || column >= columns || row >= rows)
        {
            return 0f;
        }

        return pixels[((top + row) * stride + left + column) * bytesPerPixel + bytesPerPixel - 1];
    }

    private static bool TryFindEdges(ReadOnlySpan<byte> peaks, out float start, out float end)
    {
        var first = -1;
        var last = -1;
        for (var index = 0; index < peaks.Length; index++)
        {
            if (peaks[index] == 0)
            {
                continue;
            }

            first = first < 0 ? index : first;
            last = index;
        }

        start = first + 1f - peaks[Math.Max(first, 0)] / 255f;
        end = last + peaks[Math.Max(last, 0)] / 255f;
        return first >= 0;
    }
}
