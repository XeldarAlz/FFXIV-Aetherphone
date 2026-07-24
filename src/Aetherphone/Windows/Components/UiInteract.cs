using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class UiInteract
{
    private const int OverlayReservationLifetimeFrames = 1;
    private const float RectMatchEpsilon = 0.5f;

    private static int blockedFrame = -1;
    private static Rect overlayRect;
    private static int overlayFrame = -1;
    private static Vector2 pendingTapMin;
    private static Vector2 pendingTapMax;
    private static Vector2 pendingTapWindowPos;
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
    /// <remarks>
    /// The press is claimed by the rect itself, held in the window's content space so that scrolling
    /// or a fling between press and release cannot hand the tap to whichever rect drifted under the
    /// pointer. When rects overlap, the last one to claim on the press frame owns it, matching the
    /// draw order that put it on top. Content space follows the window, so the claim is dropped when
    /// the window itself moves: dragging an unlocked phone by one of its controls is a move, not a tap.
    /// </remarks>
    public static bool Click(Vector2 min, Vector2 max, bool hovered)
    {
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left) && !ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            hasPendingTap = false;
        }

        var windowPos = ImGui.GetWindowPos();
        var contentMin = ToContentSpace(min, windowPos);
        var contentMax = ToContentSpace(max, windowPos);
        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            pendingTapMin = contentMin;
            pendingTapMax = contentMax;
            pendingTapWindowPos = windowPos;
            hasPendingTap = true;
        }

        if (!hasPendingTap || !ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            return false;
        }

        var activated = hovered && Claimed(windowPos, pendingTapWindowPos) &&
            Claimed(contentMin, pendingTapMin) && Claimed(contentMax, pendingTapMax);
        if (activated)
        {
            hasPendingTap = false;
        }

        return activated;
    }

    private static Vector2 ToContentSpace(Vector2 screen, Vector2 windowPos) =>
        screen - windowPos + new Vector2(ImGui.GetScrollX(), ImGui.GetScrollY());

    private static bool Claimed(Vector2 corner, Vector2 claim) =>
        MathF.Abs(corner.X - claim.X) <= RectMatchEpsilon && MathF.Abs(corner.Y - claim.Y) <= RectMatchEpsilon;

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
