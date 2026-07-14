using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

/// <summary>
/// Layout width that stays constant whether or not a vertical scrollbar is showing. Sizing content
/// straight from <c>GetContentRegionAvail</c> inside a scrollable child oscillates for any content
/// that grows shorter as it grows narrower (image grids, image bubbles): the scrollbar steals width,
/// the content scales down until it fits, the scrollbar is dropped, the content scales back up, and
/// the flip repeats every frame. Reserving the scrollbar gutter unconditionally removes the width
/// from the feedback path, so the scrollbar decision is made once and holds.
/// </summary>
internal static class ScrollLayout
{
    public static float StableContentWidth()
    {
        var available = ImGui.GetContentRegionAvail().X;
        if (ImGui.GetScrollMaxY() > 0f)
        {
            return available;
        }

        return available - ImGui.GetStyle().ScrollbarSize;
    }
}
