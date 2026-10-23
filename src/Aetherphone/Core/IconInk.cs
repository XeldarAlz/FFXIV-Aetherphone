using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ManagedFontAtlas;

namespace Aetherphone.Core;

internal sealed class IconInk
{
    public static readonly Vector2 BoxCentre = new(0.5f, 0.5f);

    private const int MaximumSpan = 512;

    private readonly nint font;
    private readonly Dictionary<char, Vector2> centres;

    private IconInk(nint font, Dictionary<char, Vector2> centres)
    {
        this.font = font;
        this.centres = centres;
    }

    public unsafe bool Describes(ImFontPtr candidate) => (nint)candidate.Handle == font;

    public Vector2 Centre(char codepoint) => centres.TryGetValue(codepoint, out var centre) ? centre : BoxCentre;

    public static unsafe IconInk Measure(IFontAtlasBuildToolkitPostBuild toolkit, ImFontPtr font)
    {
        var atlas = toolkit.NewImAtlas;
        var width = atlas.TexWidth;
        var height = atlas.TexHeight;
        var centres = new Dictionary<char, Vector2>();
        if (font.Handle == null)
        {
            return new IconInk(0, centres);
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

            var span = new ReadOnlySpan<byte>(pixels, width * height * bytesPerPixel);
            if (TryFindCentre(span, bytesPerPixel, width, left, top, right - left, bottom - top, out var centre))
            {
                centres[(char)codepoint] = centre;
            }
        }

        return new IconInk((nint)font.Handle, centres);
    }

    public static bool TryFindCentre(ReadOnlySpan<byte> pixels, int bytesPerPixel, int stride, int left, int top,
        int columns, int rows, out Vector2 centre)
    {
        centre = BoxCentre;
        if (columns <= 0 || rows <= 0 || columns > MaximumSpan || rows > MaximumSpan)
        {
            return false;
        }

        Span<byte> columnPeaks = stackalloc byte[columns];
        Span<byte> rowPeaks = stackalloc byte[rows];
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var alpha = pixels[((top + row) * stride + left + column) * bytesPerPixel + bytesPerPixel - 1];
                columnPeaks[column] = Math.Max(columnPeaks[column], alpha);
                rowPeaks[row] = Math.Max(rowPeaks[row], alpha);
            }
        }

        if (!TryFindEdges(columnPeaks, out var inkLeft, out var inkRight)
            || !TryFindEdges(rowPeaks, out var inkTop, out var inkBottom))
        {
            return false;
        }

        centre = new Vector2((inkLeft + inkRight) * 0.5f / columns, (inkTop + inkBottom) * 0.5f / rows);
        return true;
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
