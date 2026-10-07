using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Framework;

internal static class PressSurface
{
    public static bool Claim(string id, Rect area, out bool activated)
    {
        activated = false;
        if (area.Width <= 0f || area.Height <= 0f)
        {
            return false;
        }

        var cursor = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(area.Min);
        ImGui.InvisibleButton(id, area.Size);
        var hovered = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem) &&
                      UiInteract.Hover(area.Min, area.Max);
        activated = hovered && ImGui.IsItemActivated();
        if (hovered)
        {
            UiInteract.ReportDragSurface();
        }

        ImGui.SetCursorScreenPos(cursor);
        return hovered;
    }
}
