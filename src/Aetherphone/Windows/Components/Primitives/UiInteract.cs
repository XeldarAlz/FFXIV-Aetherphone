using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Notifications;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class UiInteract
{
    private const int OverlayReservationLifetimeFrames = 1;
    private const float RectMatchEpsilon = 0.5f;
    private const int OverlayReservationCapacity = 8;

    private static int blockedFrame = -1;
    private static readonly Rect[] OverlayRects = new Rect[OverlayReservationCapacity];
    private static readonly int[] OverlayFrames = CreateOverlayFrames();
    private static int overlayCursor;
    private static Vector2 pendingTapMin;
    private static Vector2 pendingTapMax;
    private static Vector2 pendingTapWindowPos;
    private static bool hasPendingTap;
    private static bool windowHovered = true;
    private static int windowHoveredFrame = -1;
    private static bool windowFocused = true;
    private static int windowFocusedFrame = -1;
    private static int gestureSurfaceFrame = -1;
    private static int dragSurfaceFrame = -1;

    public static void BlockThisFrame()
    {
        if (InputShield.Active)
        {
            return;
        }

        blockedFrame = ImGui.GetFrameCount();
    }

    public static bool InputBlocked => blockedFrame == ImGui.GetFrameCount();

    public static void SetWindowHovered(bool hovered)
    {
        windowHovered = hovered;
        windowHoveredFrame = ImGui.GetFrameCount();
    }

    private static bool WindowHovered => windowHoveredFrame != ImGui.GetFrameCount() || windowHovered;

    public static void SetWindowFocused(bool focused)
    {
        windowFocused = focused;
        windowFocusedFrame = ImGui.GetFrameCount();
    }

    public static bool WindowFocused => windowFocusedFrame != ImGui.GetFrameCount() || windowFocused;

    public static void CancelPendingTap() => hasPendingTap = false;

    public static void ReportGestureSurface() => gestureSurfaceFrame = ImGui.GetFrameCount();

    public static bool PointerOverGestureSurface => ImGui.GetFrameCount() - gestureSurfaceFrame <= 1;

    public static void ReportDragSurface()
    {
        ReportGestureSurface();
        dragSurfaceFrame = ImGui.GetFrameCount();
    }

    public static bool PointerOverDragSurface => ImGui.GetFrameCount() - dragSurfaceFrame <= 1;

    private static int[] CreateOverlayFrames()
    {
        var frames = new int[OverlayReservationCapacity];
        Array.Fill(frames, int.MinValue / 2);
        return frames;
    }

    public static bool HoverOverlay(Rect rect)
    {
        if (InputShield.Active)
        {
            return false;
        }

        OverlayRects[overlayCursor] = rect;
        OverlayFrames[overlayCursor] = ImGui.GetFrameCount();
        overlayCursor = (overlayCursor + 1) % OverlayReservationCapacity;
        return !InputBlocked && WindowHovered && ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
    }

    private static bool MouseOverOverlay
    {
        get
        {
            var frame = ImGui.GetFrameCount();
            for (var slotIndex = 0; slotIndex < OverlayReservationCapacity; slotIndex++)
            {
                if (frame - OverlayFrames[slotIndex] > OverlayReservationLifetimeFrames)
                {
                    continue;
                }

                var rect = OverlayRects[slotIndex];
                if (ImGui.IsMouseHoveringRect(rect.Min, rect.Max, false))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public static bool Hover(Vector2 min, Vector2 max) =>
        !InputBlocked && !MouseOverOverlay && WindowHovered && ImGui.IsMouseHoveringRect(min, max);

    public static bool HoverWindowOnly(Vector2 min, Vector2 max) => WindowHovered && ImGui.IsMouseHoveringRect(min, max);

    public static bool HoverWindowOnly(Vector2 min, Vector2 max, bool clip) =>
        WindowHovered && ImGui.IsMouseHoveringRect(min, max, clip);

    public static bool ClickedOutside(Vector2 min, Vector2 max) =>
        WindowHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.IsMouseHoveringRect(min, max);

    public static bool ClickedOutside(Vector2 min, Vector2 max, bool clip) =>
        WindowHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.IsMouseHoveringRect(min, max, clip);

    public static bool ClickedOutside(bool hoveringContent) =>
        WindowHovered && !hoveringContent && ImGui.IsMouseClicked(ImGuiMouseButton.Left);

    public static bool Hover(Vector2 min, Vector2 max, bool clip) =>
        !InputBlocked && !MouseOverOverlay && WindowHovered && ImGui.IsMouseHoveringRect(min, max, clip);

    public static bool Click(Vector2 min, Vector2 max, bool hovered) => Click(min, max, hovered, true);

    public static bool Click(Vector2 min, Vector2 max, bool hovered, bool tapSound) =>
        Click(min, max, hovered, tapSound ? UiSound.Tap : null);

    public static bool Click(Vector2 min, Vector2 max, bool hovered, UiSound? tapSound)
    {
        hovered = hovered && WindowHovered;
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
            if (tapSound is { } sound)
            {
                UiFeedback.PlayTap(sound);
            }
        }

        return activated;
    }


    private static Vector2 ToContentSpace(Vector2 screen, Vector2 windowPos) =>
        screen - windowPos + new Vector2(ImGui.GetScrollX(), ImGui.GetScrollY());

    private static bool Claimed(Vector2 corner, Vector2 claim) =>
        MathF.Abs(corner.X - claim.X) <= RectMatchEpsilon && MathF.Abs(corner.Y - claim.Y) <= RectMatchEpsilon;

    public static bool Click(Vector2 min, Vector2 max) => Click(min, max, Hover(min, max));

    public static bool HoverClick(Vector2 min, Vector2 max) => Click(min, max, HoverWithHand(min, max));

    public static bool HoverClick(Vector2 min, Vector2 max, UiSound sound) =>
        Click(min, max, HoverWithHand(min, max), sound);

    private static bool HoverWithHand(Vector2 min, Vector2 max)
    {
        var hovering = Hover(min, max);
        if (hovering)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovering;
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
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)));
    }
}
