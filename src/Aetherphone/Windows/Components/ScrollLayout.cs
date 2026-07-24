using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

/// <summary>Content layout width that stays stable across the scrollbar-overflow boundary so width-driven layouts can't vibrate.</summary>
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
