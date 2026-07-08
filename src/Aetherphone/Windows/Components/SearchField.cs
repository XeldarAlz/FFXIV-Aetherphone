using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal static class SearchField
{
    private const float PillHalfHeight = 17f;

    public static bool DrawSubmit(Rect bar, string imguiId, string hint, ref string text, PhoneTheme theme,
        int maxLength = 64, float sideInset = 4f) =>
        DrawSubmit(bar, imguiId, hint, ref text, theme.GroupedCard, theme.TextMuted, theme.TextStrong, maxLength,
            sideInset);

    public static bool DrawSubmit(Rect bar, string imguiId, string hint, ref string text, in AppPalette palette,
        int maxLength = 64, float sideInset = 12f) =>
        DrawSubmit(bar, imguiId, hint, ref text, palette.FieldSurface, palette.MutedInk, palette.TitleInk, maxLength,
            sideInset);

    public static bool DrawSubmit(Rect bar, string imguiId, string hint, ref string text, Vector4 fieldSurface,
        Vector4 mutedInk, Vector4 titleInk, int maxLength, float sideInset)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var pillMin = new Vector2(bar.Min.X + sideInset * scale, bar.Min.Y + 9f * scale);
        var pillMax = new Vector2(bar.Max.X - sideInset * scale, bar.Max.Y - 9f * scale);
        Squircle.Fill(drawList, pillMin, pillMax, (pillMax.Y - pillMin.Y) * 0.5f, ImGui.GetColorU32(fieldSurface));
        AppSkin.Icon(new Vector2(pillMin.X + 16f * scale, (pillMin.Y + pillMax.Y) * 0.5f),
            FontAwesomeIcon.Search.ToIconString(), mutedInk, 0.85f);
        ImGui.SetCursorScreenPos(new Vector2(pillMin.X + 32f * scale,
            (pillMin.Y + pillMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(pillMax.X - pillMin.X - 44f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, titleInk))
        {
            return ImGui.InputTextWithHint(imguiId, hint, ref text, maxLength, ImGuiInputTextFlags.EnterReturnsTrue);
        }
    }

    public static void Draw(Rect bar, string imguiId, string hint, ref string text, PhoneTheme theme,
        int maxLength = 100) =>
        Draw(bar, imguiId, hint, ref text, theme.GroupedCard, theme.TextMuted, theme.TextStrong, theme.SurfaceMuted,
            theme.AppBackground, maxLength);

    public static void Draw(Rect bar, string imguiId, string hint, ref string text, in AppPalette palette,
        int maxLength = 100) =>
        Draw(bar, imguiId, hint, ref text, palette.FieldSurface, palette.MutedInk, palette.TitleInk,
            new Vector4(1f, 1f, 1f, 0.14f), palette.BackdropBottom, maxLength);

    public static void Draw(Rect bar, string imguiId, string hint, ref string text, Vector4 fieldSurface,
        Vector4 mutedInk, Vector4 titleInk, Vector4 clearFill, Vector4 clearCross, int maxLength)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var pillMin = new Vector2(bar.Min.X, bar.Center.Y - PillHalfHeight * scale);
        var pillMax = new Vector2(bar.Max.X, bar.Center.Y + PillHalfHeight * scale);
        var radius = (pillMax.Y - pillMin.Y) * 0.5f;
        Squircle.Fill(drawList, pillMin, pillMax, radius, ImGui.GetColorU32(fieldSurface));
        var glyphCenter = new Vector2(pillMin.X + 16f * scale, bar.Center.Y);
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var glyph = FontAwesomeIcon.Search.ToIconString();
            var size = ImGui.CalcTextSize(glyph);
            ImGui.SetCursorScreenPos(new Vector2(glyphCenter.X - size.X * 0.5f, glyphCenter.Y - size.Y * 0.5f));
            using (ImRaii.PushColor(ImGuiCol.Text, mutedInk))
            {
                ImGui.TextUnformatted(glyph);
            }
        }

        var hasText = text.Length > 0;
        var clearRadius = 9f * scale;
        var clearCenter = new Vector2(pillMax.X - 16f * scale, bar.Center.Y);
        var inputLeft = glyphCenter.X + 14f * scale;
        var inputRight = hasText ? clearCenter.X - clearRadius - 6f * scale : pillMax.X - 14f * scale;
        ImGui.SetCursorScreenPos(new Vector2(inputLeft, bar.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(inputRight - inputLeft);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, titleInk))
        {
            ImGui.InputTextWithHint(imguiId, hint, ref text, maxLength, ImGuiInputTextFlags.None);
        }

        if (!hasText)
        {
            return;
        }

        var hovered = ImGui.IsMouseHoveringRect(clearCenter - new Vector2(clearRadius, clearRadius),
            clearCenter + new Vector2(clearRadius, clearRadius));
        drawList.AddCircleFilled(clearCenter, clearRadius,
            ImGui.GetColorU32(hovered ? mutedInk : clearFill), 16);
        var arm = 3.2f * scale;
        var cross = ImGui.GetColorU32(clearCross);
        drawList.AddLine(clearCenter - new Vector2(arm, arm), clearCenter + new Vector2(arm, arm), cross, 1.6f * scale);
        drawList.AddLine(clearCenter + new Vector2(-arm, arm), clearCenter + new Vector2(arm, -arm), cross,
            1.6f * scale);
        if (!hovered)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            text = string.Empty;
        }
    }
}
