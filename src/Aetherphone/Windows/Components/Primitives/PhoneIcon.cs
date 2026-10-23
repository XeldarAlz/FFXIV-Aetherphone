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
            var ratio = size / font.FontSize;
            // ImGui floors the text pen to whole pixels, which left every icon up to a pixel up and left of centre.
            var pen = new Vector2(MathF.Round(center.X - float.Lerp(found.X0, found.X1, ink.Centre.X) * ratio),
                MathF.Round(center.Y - float.Lerp(found.Y0, found.Y1, ink.Centre.Y) * ratio));
            drawList.AddText(font, size, pen, color, glyph);
        }
    }
}
