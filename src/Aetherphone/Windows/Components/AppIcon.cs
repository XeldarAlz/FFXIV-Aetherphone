using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class AppIcon
{
    private const float PressedScale = 0.88f;
    private const float PressSmoothTime = 0.09f;
    private static readonly Vector4 PressTint = new(0f, 0f, 0f, 1f);
    private static readonly Dictionary<string, Spring> PressSprings = new();
    private static string pressedId = string.Empty;

    public static bool Draw(Vector2 center, float size, IPhoneApp app, PhoneTheme theme, float delta)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var half = size * 0.5f;
        var min = center - new Vector2(half, half);
        var max = center + new Vector2(half, half);
        var hovered = ImGui.IsMouseHoveringRect(min, max);
        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            pressedId = app.Id;
        }

        var pressing = pressedId == app.Id && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var opened = false;
        if (pressedId == app.Id && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            opened = hovered;
            pressedId = string.Empty;
        }

        var pressScale = StepPress(app.Id, pressing ? PressedScale : 1f, delta);
        var drawHalf = half * pressScale;
        var drawMin = new Vector2(center.X - drawHalf, center.Y - drawHalf);
        var drawMax = new Vector2(center.X + drawHalf, center.Y + drawHalf);
        var drawList = ImGui.GetWindowDrawList();
        var fill = pressing ? Palette.Mix(app.Accent, PressTint, 0.14f) :
            hovered ? Palette.Mix(app.Accent, theme.TextStrong, 0.14f) : app.Accent;
        Squircle.Fill(drawList, drawMin, drawMax, size * 0.26f * pressScale, ImGui.GetColorU32(fill));
        if (!AppIconArt.TryDraw(app.Id, center, size * pressScale, theme.TextStrong, fill))
        {
            var glyphHeight = Typography.Measure(app.Glyph).Y;
            var glyphScale = glyphHeight > 0f ? size * pressScale * 0.5f / glyphHeight : 1f;
            Typography.DrawCentered(center, app.Glyph, theme.TextStrong, glyphScale);
        }

        Typography.DrawCentered(new Vector2(center.X, max.Y + 11f * scale), app.DisplayName,
            Palette.WithAlpha(theme.TextStrong, 0.95f), 0.85f, FontWeight.Medium);
        if (app.BadgeCount > 0)
        {
            DrawBadge(new Vector2(max.X - 5f * scale, min.Y + 5f * scale), app.BadgeCount, theme, scale);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return opened;
    }

    private static float StepPress(string id, float target, float delta)
    {
        if (!PressSprings.TryGetValue(id, out var spring))
        {
            spring = new Spring(1f);
        }

        spring.Step(target, PressSmoothTime, delta);
        PressSprings[id] = spring;
        return spring.Value;
    }

    private static void DrawBadge(Vector2 center, int count, PhoneTheme theme, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddCircleFilled(center, 9f * scale, ImGui.GetColorU32(theme.Danger), 24);
        var label = count > 99 ? "99+" : count.ToString();
        Typography.DrawCentered(center, label, theme.TextStrong, 0.7f, FontWeight.SemiBold);
    }
}
