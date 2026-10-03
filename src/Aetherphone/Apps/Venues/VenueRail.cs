using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Venues;

internal sealed class VenueRail
{
    private const float ArrowRadius = 14f;
    private const float PageFraction = 0.8f;

    private readonly KineticScroller scroller = new();
    private bool pressed;
    private bool paging;
    private float pageTarget;
    private Spring pageSpring;

    public float Offset => scroller.Offset;
    public bool Interactive => !scroller.IsDragging;

    public void Reset()
    {
        scroller.Reset();
        paging = false;
        pressed = false;
    }

    public void Begin(Rect row, float contentWidth)
    {
        scroller.Scale = UiScale.Current;
        scroller.SetBounds(MathF.Max(0f, contentWidth - row.Width));
        HandleDrag(row);
        StepPaging();
    }

    public void DrawArrows(ImDrawListPtr drawList, Rect row, float contentWidth, float inset)
    {
        var maxOffset = MathF.Max(0f, contentWidth - row.Width);
        var offset = scroller.Offset;
        var page = row.Width * PageFraction;
        if (offset > 0.5f && DrawArrow(drawList, new Vector2(row.Min.X + inset, row.Center.Y), PhoneIcons.ChevronLeft))
        {
            PageTo(offset - page, maxOffset);
        }

        if (offset < maxOffset - 0.5f &&
            DrawArrow(drawList, new Vector2(row.Max.X - inset, row.Center.Y), PhoneIcons.ChevronRight))
        {
            PageTo(offset + page, maxOffset);
        }
    }

    private static bool DrawArrow(ImDrawListPtr drawList, Vector2 center, string glyph)
    {
        var scale = UiScale.Current;
        var radius = ArrowRadius * scale;
        var extent = new Vector2(radius, radius);
        var hovered = UiInteract.Hover(center - extent, center + extent);
        drawList.AddCircleFilled(center, radius,
            ImGui.GetColorU32(hovered ? MediaOverlay.HoverFill : MediaOverlay.Fill), 28);
        PhoneIcon.Draw(drawList, center, glyph, MediaOverlay.White, 16f * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(center - extent, center + extent, hovered);
    }

    private void PageTo(float target, float maxOffset)
    {
        pageSpring = new Spring(scroller.Offset);
        pageTarget = Math.Clamp(target, 0f, maxOffset);
        paging = true;
    }

    private void StepPaging()
    {
        if (!paging)
        {
            return;
        }

        if (scroller.IsDragging)
        {
            paging = false;
            return;
        }

        var offset = pageSpring.Step(pageTarget, Motion.PageSettle,
            MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds));
        if (pageSpring.IsResting(pageTarget, 0.5f, 1f))
        {
            offset = pageTarget;
            paging = false;
        }

        scroller.SyncOffset(offset);
    }

    private void HandleDrag(Rect row)
    {
        var io = ImGui.GetIO();
        var deltaSeconds = io.DeltaTime;
        var mouseX = io.MousePos.X;
        var down = ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var shouldBlock = false;
        if (pressed)
        {
            if (down)
            {
                var wasDragging = scroller.IsDragging;
                scroller.Move(mouseX, deltaSeconds);
                if (!wasDragging && scroller.IsDragging)
                {
                    UiInteract.CancelPendingTap();
                }

                shouldBlock = scroller.IsDragging;
            }
            else
            {
                shouldBlock = scroller.IsDragging;
                scroller.Release();
                pressed = false;
                scroller.Tick(deltaSeconds);
            }
        }
        else if (down && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && UiInteract.Hover(row.Min, row.Max) &&
                 !UiInteract.InputBlocked)
        {
            scroller.Press(mouseX);
            pressed = true;
        }
        else if (!paging)
        {
            scroller.Tick(deltaSeconds);
        }

        if (shouldBlock)
        {
            UiInteract.BlockThisFrame();
        }
    }
}
