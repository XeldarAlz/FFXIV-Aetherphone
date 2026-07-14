using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal static class SubmitField
{
    private const float PillHalfHeight = 17f;

    public static bool Draw(Rect bar, string imguiId, string hint, ref string text, PhoneTheme theme,
        int maxLength = 64)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var pillMin = new Vector2(bar.Min.X, bar.Center.Y - PillHalfHeight * scale);
        var pillMax = new Vector2(bar.Max.X, bar.Center.Y + PillHalfHeight * scale);
        var radius = (pillMax.Y - pillMin.Y) * 0.5f;
        Squircle.Fill(drawList, pillMin, pillMax, radius, ImGui.GetColorU32(theme.GroupedCard));
        var glyphCenter = new Vector2(pillMin.X + 16f * scale, bar.Center.Y);
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var glyph = FontAwesomeIcon.Search.ToIconString();
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

        var hovered = ImGui.IsMouseHoveringRect(clearCenter - new Vector2(clearRadius, clearRadius),
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
}
