using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class PhoneIcon
{
    public static void Draw(ImDrawListPtr drawList, Vector2 center, string glyph, Vector4 color, float boxHeight,
        bool optical = false) =>
        Draw(drawList, center, glyph, ImGui.GetColorU32(color), boxHeight, optical);

    public static unsafe void Draw(ImDrawListPtr drawList, Vector2 center, string glyph, uint color,
        float boxHeight, bool optical = false)
    {
        using (Plugin.Fonts.PushIcon(boxHeight, glyph))
        {
            var font = ImGui.GetFont();
            if (font.FontSize <= 0f)
            {
                return;
            }

            ImFontGlyphPtr found = font.FindGlyph(glyph[0]);
            if (found.IsNull)
            {
                return;
            }

            var ink = Plugin.Fonts.InkMark(font, glyph[0]);
            var size = optical ? boxHeight * ink.Scale : boxHeight;
            var inkOffset = new Vector2(float.Lerp(found.X0, found.X1, ink.Center.X),
                float.Lerp(found.Y0, found.Y1, ink.Center.Y));
            var pen = IconInk.Pen(center, inkOffset, size / font.FontSize);
            // ImGui floors the text pen to whole pixels, so a scaled glyph's vertices carry the rest of its pen and
            // it grows or shrinks around one fixed point instead of stepping a pixel at a time.
            var whole = new Vector2(MathF.Floor(pen.X), MathF.Floor(pen.Y));
            var firstVertex = drawList.VtxBuffer.Size;
            drawList.AddText(font, size, whole, color, glyph);
            var remainder = pen - whole;
            if (remainder == Vector2.Zero)
            {
                return;
            }

            var vertices = drawList.VtxBuffer.AsSpan();
            for (var index = firstVertex; index < vertices.Length; index++)
            {
                vertices[index].Pos += remainder;
            }
        }
    }
}
