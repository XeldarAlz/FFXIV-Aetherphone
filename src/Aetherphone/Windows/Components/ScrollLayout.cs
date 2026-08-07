using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class ScrollLayout
{
    public static float StableContentWidth()
    {
        var available = ImGui.GetContentRegionAvail().X;
        if (DragScrollHost.Enabled)
        {
            return available;
        }

        return ImGui.GetScrollMaxY() > 0f ? available : available - ImGui.GetStyle().ScrollbarSize;
    }
}
