using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Framework;

internal static class GameHud
{
    public static void Pill(Vector2 center, string label, string value, Vector4 accent, PhoneTheme theme,
        bool highlight = false, float sizeScale = 1f)
    {
        DrawPill(center, label, value, accent, theme, highlight, 1f, sizeScale);
    }

    public static void ScorePill(Vector2 center, string label, ref RollingValue value, int target, Vector4 accent,
        PhoneTheme theme, float deltaSeconds, bool highlight = false, float sizeScale = 1f)
    {
        value.Update(target, deltaSeconds);
        var display = value.Display;
        var text = display == target ? GameNumber.Label(target) : GameNumber.Label(display);
        DrawPill(center, label, text, accent, theme, highlight, value.PopScale, sizeScale);
    }

    public const float PillHeight = 46f;

    public static float PillWidth(string label, string value, float sizeScale = 1f)
    {
        var scale = UiScale.Current * sizeScale;
        var valueSize = Typography.Measure(value, TextStyles.Title3.Scale * sizeScale, TextStyles.Title3.Weight);
        var labelSize = Typography.Measure(label, TextStyles.Caption2.Scale * sizeScale, TextStyles.Caption2.Weight);
        return MathF.Max(valueSize.X, labelSize.X) + 26f * scale;
    }

    private static void DrawPill(Vector2 center, string label, string value, Vector4 accent, PhoneTheme theme,
        bool highlight, float valuePop, float sizeScale)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var pillWidth = PillWidth(label, value, sizeScale);
        var pillHeight = PillHeight * scale * sizeScale;
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

        Typography.DrawCentered(new Vector2(center.X, center.Y - 7f * scale * sizeScale), value,
            highlight ? accent : theme.TextStrong, TextStyles.Title3.Scale * sizeScale * valuePop,
            TextStyles.Title3.Weight);
        Typography.DrawCentered(new Vector2(center.X, center.Y + 12f * scale * sizeScale),
            Loc.Culture.TextInfo.ToUpper(label), theme.TextMuted, TextStyles.Caption2.Scale * sizeScale,
            TextStyles.Caption2.Weight);
    }

    public static bool RestartButton(Vector2 center, float radius, PhoneTheme theme)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        var hovered = UiInteract.Hover(min, max);
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
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var hoverHalf = size * 0.5f;
        var hovered = UiInteract.Hover(center - hoverHalf, center + hoverHalf);
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
