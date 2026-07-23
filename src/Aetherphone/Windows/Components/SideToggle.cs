using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class SideToggle
{
    public static bool Update(Rect bounds, PhoneTheme theme, bool active, string hint, bool isLandscape)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var hitMin = new Vector2(bounds.Min.X - 8f * scale, bounds.Min.Y - 6f * scale);
        var hitMax = new Vector2(bounds.Max.X + 4f * scale, bounds.Max.Y + 6f * scale);
        var hovered = ImGui.IsMouseHoveringRect(hitMin, hitMax);
        var press = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left) ? 1f : 0f;
        var side = isLandscape ? RailSide.Bottom : RailSide.Left;
        HardwareButton.Draw(ImGui.GetWindowDrawList(), bounds, theme, side, hovered, press, active ? 1f : 0f);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoverTooltip.Show(bounds, hint);
        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }
}
