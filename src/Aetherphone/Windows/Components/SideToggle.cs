using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class SideToggle
{
    public static bool Update(Rect bounds, PhoneTheme theme, bool active, string hint)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var hitMin = new Vector2(bounds.Min.X - 4f * scale, bounds.Min.Y - 6f * scale);
        var hitMax = new Vector2(bounds.Max.X + 8f * scale, bounds.Max.Y + 6f * scale);
        var hovered = ImGui.IsMouseHoveringRect(hitMin, hitMax);
        DrawButton(bounds, theme, active, hovered);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoverTooltip.Show(bounds, hint);
        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static void DrawButton(Rect bounds, PhoneTheme theme, bool active, bool hovered)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var rounding = bounds.Width * 0.5f;
        var resting = Palette.Mix(theme.BezelRim, theme.Accent, active ? 0.85f : 0.18f);
        var fill = hovered ? Palette.Mix(resting, theme.Accent, 0.4f) : resting;
        dl.AddRectFilled(bounds.Min, bounds.Max, ImGui.GetColorU32(fill), rounding);
        var glowAlpha = active ? 0.95f : hovered ? 0.55f : 0.25f;
        var glow = ImGui.GetColorU32(Palette.WithAlpha(theme.Accent, glowAlpha));
        dl.AddLine(new Vector2(bounds.Min.X + scale, bounds.Min.Y + rounding),
            new Vector2(bounds.Min.X + scale, bounds.Max.Y - rounding), glow, 2f * scale);
    }
}
