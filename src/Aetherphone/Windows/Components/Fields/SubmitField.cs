using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal static class SubmitField
{
    private const float PillHalfHeight = 17f;

    public static bool Draw(Rect bar, string imguiId, string hint, ref string text, PhoneTheme theme,
        int maxLength = 64, FontAwesomeIcon icon = FontAwesomeIcon.Search)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var pillMin = new Vector2(bar.Min.X, bar.Center.Y - PillHalfHeight * scale);
        var pillMax = new Vector2(bar.Max.X, bar.Center.Y + PillHalfHeight * scale);
        var radius = (pillMax.Y - pillMin.Y) * 0.5f;
        Squircle.Fill(drawList, pillMin, pillMax, radius, ImGui.GetColorU32(theme.GroupedCard));
        var glyphCenter = new Vector2(pillMin.X + 16f * scale, bar.Center.Y);
        using (Plugin.Fonts.PushDalamudIcon())
        {
            var glyph = IconGlyph.Of(icon);
            var size = ImGui.CalcTextSize(glyph);
            ImGui.SetCursorScreenPos(new Vector2(glyphCenter.X - size.X * 0.5f, glyphCenter.Y - size.Y * 0.5f));
            using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
            {
                Typography.Plain(glyph);
            }
        }

        var hasText = text.Length > 0;
        var clearRadius = 9f * scale;
        var clearCenter = new Vector2(pillMax.X - 16f * scale, bar.Center.Y);
        var inputLeft = glyphCenter.X + 14f * scale;
        var inputRight = hasText ? clearCenter.X - clearRadius - 6f * scale : pillMax.X - 14f * scale;
        ImGui.SetCursorScreenPos(new Vector2(inputLeft, bar.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(inputRight - inputLeft);
        var submitted = false;
        Plugin.Fonts.NoticeText(hint);
        Plugin.Fonts.NoticeText(text);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            submitted = ImGui.InputTextWithHint(imguiId, hint, ref text, maxLength,
                ImGuiInputTextFlags.EnterReturnsTrue);
        }

        if (!hasText)
        {
            return submitted;
        }

        var hovered = UiInteract.Hover(clearCenter - new Vector2(clearRadius, clearRadius),
            clearCenter + new Vector2(clearRadius, clearRadius));
        drawList.AddCircleFilled(clearCenter, clearRadius,
            ImGui.GetColorU32(hovered ? theme.TextMuted : theme.SurfaceMuted), 16);
        var arm = 3.2f * scale;
        var cross = ImGui.GetColorU32(theme.AppBackground);
        drawList.AddLine(clearCenter - new Vector2(arm, arm), clearCenter + new Vector2(arm, arm), cross, 1.6f * scale);
        drawList.AddLine(clearCenter + new Vector2(-arm, arm), clearCenter + new Vector2(arm, -arm), cross,
            1.6f * scale);
        if (!hovered)
        {
            return submitted;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            text = string.Empty;
        }

        return submitted;
    }

    public static bool Multiline(Rect bar, string imguiId, string hint, SoftWrapEditor editor, PhoneTheme theme,
        int maxLength, int maxLines, FontAwesomeIcon icon = FontAwesomeIcon.Search)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var rowHeight = PillHalfHeight * 2f * scale;
        var halfHeight = MathF.Max(PillHalfHeight * scale, bar.Height * 0.5f);
        var pillMin = new Vector2(bar.Min.X, bar.Center.Y - halfHeight);
        var pillMax = new Vector2(bar.Max.X, bar.Center.Y + halfHeight);
        var rowCenterY = pillMax.Y - rowHeight * 0.5f;
        Squircle.Fill(drawList, pillMin, pillMax, MathF.Min(pillMax.Y - pillMin.Y, rowHeight) * 0.5f,
            ImGui.GetColorU32(theme.GroupedCard));
        var glyphCenter = new Vector2(pillMin.X + 16f * scale, rowCenterY);
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var glyph = IconGlyph.Of(icon);
            var size = ImGui.CalcTextSize(glyph);
            ImGui.SetCursorScreenPos(new Vector2(glyphCenter.X - size.X * 0.5f, glyphCenter.Y - size.Y * 0.5f));
            using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
            {
                Typography.Plain(glyph);
            }
        }

        var hasText = editor.Text.Length > 0;
        var clearRadius = 9f * scale;
        var clearCenter = new Vector2(pillMax.X - 16f * scale, rowCenterY);
        var inputLeft = glyphCenter.X + 14f * scale;
        var inputRight = hasText ? clearCenter.X - clearRadius - 6f * scale : pillMax.X - 14f * scale;
        var padding = ImGui.GetStyle().FramePadding;
        var fieldWidth = MathF.Max(1f, inputRight - inputLeft);
        editor.Rewrap(MathF.Max(1f, fieldWidth - padding.X * 2f - 4f * scale));
        var lines = Math.Clamp(editor.LineCount, 1, maxLines);
        var fieldHeight = lines * ImGui.GetTextLineHeight() + padding.Y * 2f;
        var fieldTop = (pillMin.Y + pillMax.Y) * 0.5f - fieldHeight * 0.5f;
        ImGui.SetCursorScreenPos(new Vector2(inputLeft, fieldTop));
        Plugin.Fonts.NoticeText(hint);
        bool submitted;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            submitted = editor.Draw(imguiId, new Vector2(fieldWidth, fieldHeight), maxLength, 0);
        }

        if (!hasText)
        {
            var placeholder = Typography.FitText(hint, fieldWidth - padding.X * 2f, TextStyles.Body);
            Typography.Draw(drawList, new Vector2(inputLeft + padding.X, fieldTop + padding.Y), placeholder,
                theme.TextMuted, TextStyles.Body);
            return submitted;
        }

        var hovered = UiInteract.Hover(clearCenter - new Vector2(clearRadius, clearRadius),
            clearCenter + new Vector2(clearRadius, clearRadius));
        drawList.AddCircleFilled(clearCenter, clearRadius,
            ImGui.GetColorU32(hovered ? theme.TextMuted : theme.SurfaceMuted), 16);
        var arm = 3.2f * scale;
        var cross = ImGui.GetColorU32(theme.AppBackground);
        drawList.AddLine(clearCenter - new Vector2(arm, arm), clearCenter + new Vector2(arm, arm), cross, 1.6f * scale);
        drawList.AddLine(clearCenter + new Vector2(-arm, arm), clearCenter + new Vector2(arm, -arm), cross,
            1.6f * scale);
        if (!hovered)
        {
            return submitted;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            editor.Adopt(string.Empty);
        }

        return submitted;
    }
}
