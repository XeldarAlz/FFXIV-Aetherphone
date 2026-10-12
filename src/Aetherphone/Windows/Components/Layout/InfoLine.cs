using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class InfoLine
{
    private const float GlyphBox = 32f;
    private const float GlyphSize = 20f;

    public static float Draw(ImDrawListPtr drawList, string glyph, string text, Vector4 glyphInk, Vector4 textInk,
        float left, float top, float width, float scale)
    {
        var box = GlyphBox * scale;
        var textLeft = left + box + Metrics.Space.Md * scale;
        var textWidth = MathF.Max(1f, left + width - textLeft);
        var block = Typography.MeasureWrappedBlock(text, TextStyles.Subheadline, textWidth);
        var height = MathF.Max(box, block.Y);
        var firstLine = MathF.Min(block.Y, Typography.LineHeight(TextStyles.Subheadline));
        PhoneIcon.Draw(drawList, new Vector2(left + box * 0.5f, top + box * 0.5f), glyph, glyphInk,
            GlyphSize * scale);
        var textTop = top + MathF.Max(0f, (box - firstLine) * 0.5f);
        Typography.DrawWrappedLeft(new Vector2(textLeft, textTop), text, textInk, TextStyles.Subheadline, textWidth);
        return top + MathF.Max(height, textTop - top + block.Y);
    }
}
