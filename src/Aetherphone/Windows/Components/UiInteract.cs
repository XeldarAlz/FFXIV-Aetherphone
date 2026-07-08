using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class UiInteract
{
    private static int blockedFrame = -1;

    public static void BlockThisFrame() => blockedFrame = ImGui.GetFrameCount();

    public static bool InputBlocked => blockedFrame == ImGui.GetFrameCount();

    public static bool Hover(Vector2 min, Vector2 max) =>
        !InputBlocked && ImGui.IsMouseHoveringRect(min, max);

    public static bool HoverClick(Vector2 min, Vector2 max)
    {
        if (!Hover(min, max))
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    public static void HoverHighlight(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding)
    {
        if (!Hover(min, max))
        {
            return;
        }

        var alpha = ImGui.IsMouseDown(ImGuiMouseButton.Left) ? 0.14f : 0.07f;
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)), rounding);
    }
}
