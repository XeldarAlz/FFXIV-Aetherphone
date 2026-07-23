using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class UiInteract
{
    private const int OverlayReservationLifetimeFrames = 1;

    private static int blockedFrame = -1;
    private static Rect overlayRect;
    private static int overlayFrame = -1;
    private static Vector2 pendingTapPos;
    private static bool hasPendingTap;

    public static void BlockThisFrame() => blockedFrame = ImGui.GetFrameCount();

    public static bool InputBlocked => blockedFrame == ImGui.GetFrameCount();

    /// <summary>Discards the in-flight tap without activating it (called when a drag starts).</summary>
    public static void CancelPendingTap() => hasPendingTap = false;

    public static bool HoverOverlay(Rect rect)
    {
        overlayRect = rect;
        overlayFrame = ImGui.GetFrameCount();
        return !InputBlocked && ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
    }

    private static bool MouseOverOverlay =>
        ImGui.GetFrameCount() - overlayFrame <= OverlayReservationLifetimeFrames &&
        ImGui.IsMouseHoveringRect(overlayRect.Min, overlayRect.Max, false);

    public static bool Hover(Vector2 min, Vector2 max) =>
        !InputBlocked && !MouseOverOverlay && ImGui.IsMouseHoveringRect(min, max);

    /// <summary>
    /// Release-triggered activation for a rect the caller has already hover-tested, so a drag can
    /// cancel the pending tap before it fires. Pass the same rect <paramref name="hovered"/> was
    /// computed from.
    /// </summary>
    public static bool Click(Vector2 min, Vector2 max, bool hovered)
    {
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left) && !ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            hasPendingTap = false;
        }

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            pendingTapPos = ImGui.GetMousePos();
            hasPendingTap = true;
        }

        if (!hasPendingTap || !ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            return false;
        }

        var activated = hovered &&
            pendingTapPos.X >= min.X && pendingTapPos.X <= max.X &&
            pendingTapPos.Y >= min.Y && pendingTapPos.Y <= max.Y;

        if (activated)
        {
            hasPendingTap = false;
        }

        return activated;
    }

    /// <summary>
    /// <see cref="Click(Vector2, Vector2, bool)"/> gated by the standard <see cref="Hover"/> test.
    /// </summary>
    public static bool Click(Vector2 min, Vector2 max) => Click(min, max, Hover(min, max));

    /// <summary>
    /// <see cref="Click(Vector2, Vector2, bool)"/> plus the hand cursor while hovered.
    /// </summary>
    public static bool HoverClick(Vector2 min, Vector2 max)
    {
        var hovering = Hover(min, max);
        if (hovering)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return Click(min, max, hovering);
    }

    public static bool HoverClickCircle(Vector2 center, float radius)
    {
        var offset = ImGui.GetMousePos() - center;
        if (offset.LengthSquared() > radius * radius)
        {
            return false;
        }

        var corner = new Vector2(radius, radius);
        return HoverClick(center - corner, center + corner);
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
