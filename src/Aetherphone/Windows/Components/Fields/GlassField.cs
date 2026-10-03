using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal static class GlassField
{
    public const float HeightUnits = 44f;
    private const float GlyphSizeUnits = 18f;
    private const float GlyphInsetUnits = 18f;
    private const float GlyphTextGapUnits = 8f;
    private const float TextInsetUnits = 16f;
    private const float ClearRadiusUnits = 9f;
    private const float ClearInsetUnits = 18f;
    private const float ClearTextGapUnits = 6f;
    private const float ClearArmUnits = 3.2f;
    private const float ClearStrokeUnits = 1.6f;
    private const float ClearFillAlpha = 0.18f;
    private const float ClearFillHoverAlpha = 0.30f;
    private static readonly Vector4 Transparent = new(0f, 0f, 0f, 0f);

    public static float Radius(Rect field) => field.Height * 0.5f;

    public static void Surface(ImDrawListPtr drawList, Rect field, float radius, float scale, float brightness,
        float opacity) =>
        Material.LiquidGlass(drawList, field.Min, field.Max, radius, scale, GlassTone.Light, brightness, opacity);

    public static bool Text(Rect field, string imguiId, string hint, ref string text, PhoneTheme theme, float scale,
        int maxLength, bool focus, ImGuiInputTextFlags flags) =>
        Input(field, imguiId, hint, ref text, theme, TextInsetUnits * scale, TextInsetUnits * scale, maxLength, focus,
            flags);

    public static void SearchGlyph(ImDrawListPtr drawList, Rect field, PhoneTheme theme, float scale, float alpha)
    {
        var glyphCenter = new Vector2(field.Min.X + GlyphInsetUnits * scale, field.Center.Y);
        PhoneIcon.Draw(drawList, glyphCenter, PhoneIcons.Search, Palette.WithAlpha(theme.TextMuted, alpha),
            GlyphSizeUnits * scale);
    }

    public static void Search(ImDrawListPtr drawList, Rect field, string imguiId, string hint, ref string text,
        PhoneTheme theme, float scale, int maxLength, bool focus)
    {
        var glyphSize = GlyphSizeUnits * scale;
        SearchGlyph(drawList, field, theme, scale, 1f);
        var hasText = text.Length > 0;
        var clearRadius = ClearRadiusUnits * scale;
        var clearCenter = new Vector2(field.Max.X - ClearInsetUnits * scale, field.Center.Y);
        var leftInset = GlyphInsetUnits * scale + glyphSize * 0.5f + GlyphTextGapUnits * scale;
        var rightInset = hasText
            ? field.Max.X - (clearCenter.X - clearRadius - ClearTextGapUnits * scale)
            : TextInsetUnits * scale;
        Input(field, imguiId, hint, ref text, theme, leftInset, rightInset, maxLength, focus, ImGuiInputTextFlags.None);
        if (!hasText)
        {
            return;
        }

        var clearMin = clearCenter - new Vector2(clearRadius, clearRadius);
        var clearMax = clearCenter + new Vector2(clearRadius, clearRadius);
        var hovered = UiInteract.Hover(clearMin, clearMax);
        drawList.AddCircleFilled(clearCenter, clearRadius,
            ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, hovered ? ClearFillHoverAlpha : ClearFillAlpha)), 16);
        var arm = ClearArmUnits * scale;
        var cross = ImGui.GetColorU32(theme.TextStrong);
        var stroke = ClearStrokeUnits * scale;
        drawList.AddLine(clearCenter - new Vector2(arm, arm), clearCenter + new Vector2(arm, arm), cross, stroke);
        drawList.AddLine(clearCenter + new Vector2(-arm, arm), clearCenter + new Vector2(arm, -arm), cross, stroke);
        if (!hovered)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (UiInteract.Click(clearMin, clearMax, hovered))
        {
            text = string.Empty;
        }
    }

    private static bool Input(Rect field, string imguiId, string hint, ref string text, PhoneTheme theme,
        float leftInset, float rightInset, int maxLength, bool focus, ImGuiInputTextFlags flags)
    {
        var left = field.Min.X + leftInset;
        var width = MathF.Max(field.Max.X - rightInset - left, 1f);
        ImGui.SetCursorScreenPos(new Vector2(left, field.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(width);
        Plugin.Fonts.NoticeText(hint);
        Plugin.Fonts.NoticeText(text);
        if (focus)
        {
            ImGui.SetKeyboardFocusHere();
        }

        using (ImRaii.PushColor(ImGuiCol.FrameBg, Transparent))
        using (ImRaii.PushColor(ImGuiCol.FrameBgHovered, Transparent))
        using (ImRaii.PushColor(ImGuiCol.FrameBgActive, Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        using (ImRaii.PushColor(ImGuiCol.TextDisabled, theme.TextMuted))
        {
            return ImGui.InputTextWithHint(imguiId, hint, ref text, maxLength, flags);
        }
    }
}
