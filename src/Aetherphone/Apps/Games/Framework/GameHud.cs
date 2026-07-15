using System.Numerics;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Games.Framework;

internal static class GameHud
{
    public static void Pill(Vector2 center, string label, string value, Vector4 accent, PhoneTheme theme,
        bool highlight = false)
    {
        DrawPill(center, label, value, accent, theme, highlight, 1f);
    }

    public static void ScorePill(Vector2 center, string label, ref RollingValue value, int target, Vector4 accent,
        PhoneTheme theme, float deltaSeconds, bool highlight = false)
    {
        value.Update(target, deltaSeconds);
        var display = value.Display;
        var text = display == target ? GameNumber.Label(target) : GameNumber.Label(display);
        DrawPill(center, label, text, accent, theme, highlight, value.PopScale);
    }

    private static void DrawPill(Vector2 center, string label, string value, Vector4 accent, PhoneTheme theme,
        bool highlight, float valuePop)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var valueSize = Typography.Measure(value, TextStyles.Title3);
        var labelSize = Typography.Measure(label, TextStyles.Caption2);
        var pillWidth = MathF.Max(valueSize.X, labelSize.X) + 26f * scale;
        var pillHeight = 46f * scale;
        var half = new Vector2(pillWidth * 0.5f, pillHeight * 0.5f);
        var min = center - half;
        var max = center + half;
        var radius = pillHeight * 0.5f;
        if (highlight)
        {
            ProgressRing.Glow(center, radius * 1.15f, accent, 0.5f);
        }

        Material.Frosted(drawList, min, max, radius, scale);
        if (highlight)
        {
            Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(accent with { W = 0.85f }), 1.5f * scale);
        }

        Typography.DrawCentered(new Vector2(center.X, center.Y - 7f * scale), value,
            highlight ? accent : theme.TextStrong, TextStyles.Title3.Scale * valuePop, TextStyles.Title3.Weight);
        Typography.DrawCentered(new Vector2(center.X, center.Y + 12f * scale), Loc.Culture.TextInfo.ToUpper(label),
            theme.TextMuted, TextStyles.Caption2);
    }

    public static bool RestartButton(Vector2 center, float radius, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        var hovered = ImGui.IsMouseHoveringRect(min, max);
        Material.Frosted(drawList, min, max, radius, scale, hovered ? 1f : 0.92f);
        if (hovered)
        {
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(theme.Accent with { W = 0.16f }));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        ProgressRing.CenterIcon(center, FontAwesomeIcon.Redo, hovered ? theme.TextStrong : theme.Accent,
            radius * 0.95f);
        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    public static bool Button(Vector2 center, Vector2 size, string label, Vector4 accent, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var hoverHalf = size * 0.5f;
        var hovered = ImGui.IsMouseHoveringRect(center - hoverHalf, center + hoverHalf);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var visualScale = pressed ? 0.955f : 1f;
        var half = hoverHalf * visualScale;
        var min = center - half;
        var max = center + half;
        var radius = size.Y * 0.5f * visualScale;
        if (hovered)
        {
            ProgressRing.Glow(center, radius * 1.4f, accent, 0.55f);
        }

        var fillTop = ImGui.GetColorU32(GamePalette.Lighten(accent, hovered ? 0.24f : 0.14f));
        var fillBottom = ImGui.GetColorU32(GamePalette.Darken(accent, hovered ? 0.04f : 0.12f));
        Squircle.FillVerticalGradient(drawList, min, max, radius, fillTop, fillBottom);
        Squircle.Stroke(drawList, min, max, radius,
            ImGui.GetColorU32(GamePalette.Lighten(accent, 0.35f) with { W = 0.55f }), 1f * scale);
        drawList.AddLine(new Vector2(min.X + radius, min.Y + 1f * scale), new Vector2(max.X - radius, min.Y + 1f * scale),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.30f)), 1f * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        Typography.DrawCentered(center, label, GamePalette.InkOn(accent), TextStyles.Headline);
        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }
}
