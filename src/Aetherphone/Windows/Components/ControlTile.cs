using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class ControlTile
{
    private const float PressSlop = 6f;
    private static Vector2 armedOrigin;
    private static bool armed;

    public static void CancelPress() => armed = false;

    public static bool Toggle(ImDrawListPtr dl, Rect rect, FontAwesomeIcon icon, string label, bool active,
        Vector4 accent, PhoneTheme theme, float opacity, bool interactive)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var radius = MathF.Min(rect.Width, rect.Height) * 0.30f;
        var released = Release(rect, interactive, out var hovered);
        var press = Pressed(rect, interactive) ? 0.96f : 1f;
        var fill = active
            ? Palette.WithAlpha(accent, (hovered ? 0.98f : 0.88f) * press * opacity)
            : new Vector4(1f, 1f, 1f, (hovered ? 0.19f : 0.11f) * press * opacity);
        Squircle.Fill(dl, rect.Min, rect.Max, radius, ImGui.GetColorU32(fill));
        Material.EdgeSquircle(dl, rect.Min, rect.Max, radius, scale, opacity);
        var iconColor = Palette.WithAlpha(active ? new Vector4(1f, 1f, 1f, 1f) : theme.TextStrong, opacity);
        var iconCenter = new Vector2(rect.Center.X, rect.Min.Y + rect.Height * 0.34f);
        ProgressRing.CenterIcon(dl, iconCenter, icon, iconColor, MathF.Min(rect.Height, rect.Width) * 0.26f);
        var labelColor = active
            ? new Vector4(1f, 1f, 1f, opacity)
            : Palette.WithAlpha(theme.TextStrong, opacity * 0.85f);
        Typography.DrawWrappedCentered(dl, new Vector2(rect.Center.X, rect.Max.Y - rect.Height * 0.26f), label,
            labelColor, TextStyles.FootnoteEmphasized, rect.Width - 20f * scale);
        return released;
    }

    public static float VerticalSlider(ImDrawListPtr dl, Rect rect, float value, FontAwesomeIcon icon, string label,
        PhoneTheme theme, float opacity, bool interactive, out bool released)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var radius = MathF.Min(rect.Width, rect.Height * 0.5f) * 0.44f;
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

        Squircle.Fill(dl, rect.Min, rect.Max, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.11f * opacity)));
        var fillTop = rect.Min.Y + (1f - result) * rect.Height;
        dl.PushClipRect(new Vector2(rect.Min.X, fillTop), rect.Max, true);
        Squircle.Fill(dl, rect.Min, rect.Max, radius,
            ImGui.GetColorU32(new Vector4(0.96f, 0.96f, 0.97f, 0.94f * opacity)));
        dl.PopClipRect();
        Material.EdgeSquircle(dl, rect.Min, rect.Max, radius, scale, opacity);
        var iconOnFill = result > 0.14f;
        var iconColor =
            Palette.WithAlpha(iconOnFill ? new Vector4(0.14f, 0.14f, 0.17f, 1f) : theme.TextStrong, opacity);
        ProgressRing.CenterIcon(dl, new Vector2(rect.Center.X, rect.Max.Y - 22f * scale), icon, iconColor, 17f * scale);
        return result;
    }

    public static bool Swatch(ImDrawListPtr dl, Vector2 center, float radius, Vector4 color, bool selected,
        float opacity, bool interactive)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rect = new Rect(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        var released = Release(rect, interactive, out var hovered);
        var grow = hovered ? 1.08f : 1f;
        dl.AddCircleFilled(center, radius * grow, ImGui.GetColorU32(Palette.WithAlpha(color, opacity)), 32);
        if (selected)
        {
            dl.AddCircle(center, radius * grow + 3f * scale, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.94f * opacity)),
                32, 2f * scale);
        }

        return released;
    }

    private static bool Pressed(Rect rect, bool interactive) =>
        interactive && armed && ImGui.IsMouseDown(ImGuiMouseButton.Left) &&
        ImGui.IsMouseHoveringRect(rect.Min, rect.Max);

    private static bool Release(Rect rect, bool interactive, out bool hovered)
    {
        hovered = interactive && ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        if (!interactive)
        {
            return false;
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            armed = true;
            armedOrigin = ImGui.GetMousePos();
        }

        if (!hovered || !ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            return false;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var moved = (ImGui.GetMousePos() - armedOrigin).Length() > PressSlop * scale;
        var fire = armed && !moved;
        armed = false;
        return fire;
    }
}
