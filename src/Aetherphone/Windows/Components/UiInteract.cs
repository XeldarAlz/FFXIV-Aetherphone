using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class UiInteract
{
    public static bool HoverClick(Vector2 min, Vector2 max)
    {
        if (!ImGui.IsMouseHoveringRect(min, max))
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }
}
