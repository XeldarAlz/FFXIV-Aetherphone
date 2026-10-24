using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class SwipeClaim
{
    // A real ImGui item has to own the press: without an active item Dear ImGui starts its native window drag
    // on any press over the window background, which moved the whole phone while a carousel was dragged.
    public static bool Claim(string id, Rect zone)
    {
        if (zone.Width <= 0f || zone.Height <= 0f)
        {
            return false;
        }

        var cursor = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(zone.Min);
        ImGui.InvisibleButton(id, new Vector2(zone.Width, zone.Height));
        var activated = ImGui.IsItemActivated() && UiInteract.Hover(zone.Min, zone.Max);
        ImGui.SetCursorScreenPos(cursor);
        return activated;
    }
}
