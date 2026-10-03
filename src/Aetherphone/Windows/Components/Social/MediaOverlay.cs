using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class MediaOverlay
{
    public const float PillHeight = 24f;
    public const float PillPadX = 10f;
    public const float PillGlyphSize = 13f;
    public const float PillGlyphGap = 5f;
    public const float GlassButtonRadius = 17f;
    public const float GlassGlyphSize = 19f;

    public static readonly Vector4 LiveGreen = new(0.24f, 0.82f, 0.44f, 1f);
    public static readonly Vector4 LiveInk = new(0.03f, 0.10f, 0.05f, 1f);
    public static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    public static readonly Vector4 Fill = new(0.03f, 0.02f, 0.01f, 0.58f);
    public static readonly Vector4 HoverFill = new(0.06f, 0.05f, 0.03f, 0.80f);
    public static readonly Vector4 ScrimClear = new(0f, 0f, 0f, 0f);
    public static readonly Vector4 ScrimDeep = new(0.02f, 0.015f, 0.005f, 0.86f);

    private static readonly TextStyle PillStyle = TextStyles.Caption1;
    private static readonly TextStyle PillEmphasisStyle = new(0.72f, FontWeight.SemiBold);

    public static float PillWidth(string label, float scale, bool glyph, bool emphasis = false)
    {
        var size = Typography.Measure(label, emphasis ? PillEmphasisStyle : PillStyle);
        return size.X + PillPadX * 2f * scale + (glyph ? (PillGlyphSize + PillGlyphGap) * scale : 0f);
    }

    public static float LivePillWidth(string label, float scale) =>
        Typography.Measure(label, PillEmphasisStyle).X + PillPadX * 2f * scale + 12f * scale;

    public static float Pill(ImDrawListPtr drawList, Vector2 topLeft, string label, Vector4 fill, Vector4 ink,
        float scale, string glyph = "", bool emphasis = false, Vector4? stroke = null)
    {
        var style = emphasis ? PillEmphasisStyle : PillStyle;
        var size = Typography.Measure(label, style);
        var height = PillHeight * scale;
        var hasGlyph = glyph.Length > 0;
        var width = size.X + PillPadX * 2f * scale + (hasGlyph ? (PillGlyphSize + PillGlyphGap) * scale : 0f);
        var max = topLeft + new Vector2(width, height);
        Squircle.Fill(drawList, topLeft, max, height * 0.5f, ImGui.GetColorU32(fill));
        if (stroke is { } strokeInk)
        {
            Squircle.Stroke(drawList, topLeft, max, height * 0.5f, ImGui.GetColorU32(strokeInk), 1f);
        }

        var textLeft = topLeft.X + PillPadX * scale;
        if (hasGlyph)
        {
            PhoneIcon.Draw(drawList, new Vector2(textLeft + PillGlyphSize * scale * 0.5f, topLeft.Y + height * 0.5f),
                glyph, ink, PillGlyphSize * scale);
            textLeft += (PillGlyphSize + PillGlyphGap) * scale;
        }

        Typography.Draw(drawList, new Vector2(textLeft, topLeft.Y + (height - size.Y) * 0.5f), label, ink, style);
        return width;
    }

    public static float LivePill(ImDrawListPtr drawList, Vector2 topLeft, string label, float scale)
    {
        var size = Typography.Measure(label, PillEmphasisStyle);
        var height = PillHeight * scale;
        var dotSpace = 12f * scale;
        var width = size.X + PillPadX * 2f * scale + dotSpace;
        var max = topLeft + new Vector2(width, height);
        Squircle.Fill(drawList, topLeft, max, height * 0.5f, ImGui.GetColorU32(LiveGreen));
        LiveDot(drawList, new Vector2(topLeft.X + PillPadX * scale + 2f * scale, topLeft.Y + height * 0.5f), LiveInk,
            scale);
        Typography.Draw(drawList,
            new Vector2(topLeft.X + PillPadX * scale + dotSpace, topLeft.Y + (height - size.Y) * 0.5f), label, LiveInk,
            PillEmphasisStyle);
        return width;
    }

    public static void LiveDot(ImDrawListPtr drawList, Vector2 center, Vector4 color, float scale)
    {
        var pulse = 0.55f + 0.45f * Pulse.Wave(Pulse.Calm);
        drawList.AddCircle(center, 5.2f * scale, ImGui.GetColorU32(Palette.WithAlpha(color, 0.40f * pulse)), 20,
            1.3f * scale);
        drawList.AddCircleFilled(center, 3f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(color, 0.65f + 0.35f * pulse)), 20);
    }

    public static void BottomScrim(ImDrawListPtr drawList, Vector2 min, Vector2 max, float share)
    {
        var top = new Vector2(min.X, max.Y - (max.Y - min.Y) * share);
        drawList.AddRectFilledMultiColor(top, max, ImGui.GetColorU32(ScrimClear), ImGui.GetColorU32(ScrimClear),
            ImGui.GetColorU32(ScrimDeep), ImGui.GetColorU32(ScrimDeep));
    }

    public static bool GlassButton(ImDrawListPtr drawList, Vector2 center, string glyph, string tooltip, float scale,
        Vector4? ringInk = null, Vector4? glyphInk = null, bool interactive = true)
    {
        var radius = GlassButtonRadius * scale;
        var extent = new Vector2(radius, radius);
        var hovered = interactive && UiInteract.Hover(center - extent, center + extent);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(hovered ? HoverFill : Fill), 32);
        if (ringInk is { } ring)
        {
            drawList.AddCircle(center, radius, ImGui.GetColorU32(ring), 32, 1.3f * scale);
        }

        PhoneIcon.Draw(drawList, center, glyph, glyphInk ?? White, GlassGlyphSize * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!interactive)
        {
            return false;
        }

        if (tooltip.Length > 0)
        {
            HoverTooltip.Show(new Rect(center - extent, center + extent), tooltip, HoverLabelSide.Below);
        }

        return UiInteract.Click(center - extent, center + extent, hovered);
    }
}
