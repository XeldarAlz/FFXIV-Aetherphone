using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class ControlTile
{
    public static bool Toggle(Rect rect, FontAwesomeIcon icon, string label, bool active, Vector4 accent, PhoneTheme theme, float opacity, bool interactive)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var radius = 18f * scale;
        var hovered = interactive && ImGui.IsMouseHoveringRect(rect.Min, rect.Max);

        var fill = active
            ? Palette.WithAlpha(accent, (hovered ? 0.96f : 0.86f) * opacity)
            : new Vector4(1f, 1f, 1f, (hovered ? 0.17f : 0.10f) * opacity);
        Squircle.Fill(dl, rect.Min, rect.Max, radius, ImGui.GetColorU32(fill));
        Material.EdgeSquircle(dl, rect.Min, rect.Max, radius, scale, opacity);

        var iconColor = Palette.WithAlpha(active ? new Vector4(1f, 1f, 1f, 1f) : theme.TextStrong, opacity);
        var iconCenter = new Vector2(rect.Center.X, rect.Min.Y + rect.Height * 0.40f);
        ProgressRing.CenterIcon(iconCenter, icon, iconColor, rect.Height * 0.27f);

        var labelColor = active ? new Vector4(1f, 1f, 1f, opacity) : Palette.WithAlpha(theme.TextMuted, opacity);
        Typography.DrawCentered(new Vector2(rect.Center.X, rect.Max.Y - rect.Height * 0.24f), label, labelColor, 0.7f, FontWeight.Medium);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    public static float VerticalSlider(Rect rect, float value, FontAwesomeIcon icon, PhoneTheme theme, float opacity, bool interactive, out bool released)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var radius = 18f * scale;
        var result = Math.Clamp(value, 0f, 1f);
        released = false;

        var hovered = interactive && ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left) && rect.Height > 0f)
            {
                result = Math.Clamp(1f - (ImGui.GetMousePos().Y - rect.Min.Y) / rect.Height, 0f, 1f);
            }

            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                released = true;
            }
        }

        Squircle.Fill(dl, rect.Min, rect.Max, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f * opacity)));

        var fillTop = rect.Min.Y + (1f - result) * rect.Height;
        dl.PushClipRect(new Vector2(rect.Min.X, fillTop), rect.Max, true);
        Squircle.Fill(dl, rect.Min, rect.Max, radius, ImGui.GetColorU32(new Vector4(0.96f, 0.96f, 0.97f, 0.92f * opacity)));
        dl.PopClipRect();

        Material.EdgeSquircle(dl, rect.Min, rect.Max, radius, scale, opacity);

        var iconOnFill = result > 0.16f;
        var iconColor = Palette.WithAlpha(iconOnFill ? new Vector4(0.14f, 0.14f, 0.17f, 1f) : theme.TextStrong, opacity);
        ProgressRing.CenterIcon(new Vector2(rect.Center.X, rect.Max.Y - 20f * scale), icon, iconColor, 17f * scale);

        return result;
    }

    public static bool Swatch(Vector2 center, float radius, Vector4 color, bool selected, float opacity, bool interactive)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var hovered = interactive && ImGui.IsMouseHoveringRect(center - new Vector2(radius, radius), center + new Vector2(radius, radius));

        dl.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(color, opacity)), 32);
        if (selected)
        {
            dl.AddCircle(center, radius + 3f * scale, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.92f * opacity)), 32, 2f * scale);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }
}
