using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class ComposerFieldPress
{
    public static bool PressedAroundInput(Vector2 fieldMin, Vector2 fieldMax)
    {
        if (!UiInteract.Hover(fieldMin, fieldMax))
        {
            return false;
        }

        UiInteract.ReportGestureSurface();
        if (ImGui.IsMouseHoveringRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax()))
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.TextInput);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }
}
