using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games;

internal sealed class TileRail
{
    private const float AxisSlop = 6f;
    private const float ArrowInset = 18f;
    private const float PageFraction = 0.8f;
    private const float RestEpsilon = 0.5f;

    private enum Axis : byte
    {
        Undecided,
        Horizontal,
        Vertical,
    }

    private readonly KineticScroller scroller = new();
    private bool pressed;
    private Axis axis;
    private Vector2 pressPosition;
    private bool paging;
    private float pageTarget;
    private Spring pageSpring;
    private bool hovered;
    private float bound;

    public float Offset => scroller.Offset;

    public bool TapAllowed => axis != Axis.Horizontal && !scroller.IsControlling;

    public bool Swiping => pressed && axis == Axis.Horizontal;

    public void Reset()
    {
        scroller.Reset();
        pressed = false;
        axis = Axis.Undecided;
        paging = false;
    }

    // A real ImGui item owns the press: without an active item Dear ImGui starts its native window drag on any
    // press over the window background, which dragged the whole phone while a shelf was swiped.
    public void Begin(ImDrawListPtr drawList, string id, Rect row, Rect claim, float contentWidth)
    {
        var scale = UiScale.Current;
        scroller.Scale = scale;
        bound = MathF.Max(0f, contentWidth - row.Width);
        scroller.SetBounds(bound);
        var cursor = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(claim.Min);
        ImGui.InvisibleButton(id, claim.Size);
        hovered = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem)
                  && UiInteract.Hover(claim.Min, claim.Max);
        var activated = hovered && ImGui.IsItemActivated();
        if (hovered)
        {
            UiInteract.ReportGestureSurface();
        }

        ImGui.SetCursorScreenPos(cursor);
        HandleDrag(activated, scale);
        StepPaging();
        drawList.PushClipRect(row.Min, row.Max, true);
    }

    public void End(ImDrawListPtr drawList, Rect row, float contentWidth, AppSkin ui) =>
        End(drawList, row, contentWidth, ui, 0f);

    public void End(ImDrawListPtr drawList, Rect row, float contentWidth, AppSkin ui, float pageStride)
    {
        drawList.PopClipRect();
        if (!hovered || scroller.IsDragging)
        {
            return;
        }

        var maxOffset = MathF.Max(0f, contentWidth - row.Width);
        var offset = scroller.Offset;
        var scale = UiScale.Current;
        if (offset > RestEpsilon && DrawArrow(drawList, ui,
                new Vector2(row.Min.X + ArrowInset * scale, row.Center.Y), PhoneIcons.ChevronLeft, scale))
        {
            PageTo(PageStep(offset, row.Width, pageStride, -1f), maxOffset);
        }

        if (offset < maxOffset - RestEpsilon && DrawArrow(drawList, ui,
                new Vector2(row.Max.X - ArrowInset * scale, row.Center.Y), PhoneIcons.ChevronRight, scale))
        {
            PageTo(PageStep(offset, row.Width, pageStride, 1f), maxOffset);
        }
    }

    public void SettleTo(float target)
    {
        scroller.CancelMomentum();
        PageTo(target, bound);
    }

    public static float PageStep(float offset, float rowWidth, float pageStride, float direction)
    {
        if (pageStride <= 0f)
        {
            return offset + rowWidth * PageFraction * direction;
        }

        return (MathF.Round(offset / pageStride) + direction) * pageStride;
    }

    private static bool DrawArrow(ImDrawListPtr drawList, AppSkin ui, Vector2 center, string glyph, float scale) =>
        RoundButton.Icon(drawList, center, RoundButton.SmallRadius * scale, glyph, ui.Ink);

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

        if (pressed)
        {
            paging = false;
            return;
        }

        var offset = pageSpring.Step(pageTarget, Motion.PageSettle,
            MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds));
        if (pageSpring.IsResting(pageTarget, RestEpsilon, 1f))
        {
            offset = pageTarget;
            paging = false;
        }

        scroller.SyncOffset(offset);
    }

    private void HandleDrag(bool activated, float scale)
    {
        var io = ImGui.GetIO();
        var deltaSeconds = io.DeltaTime;
        var mouse = io.MousePos;
        var down = ImGui.IsMouseDown(ImGuiMouseButton.Left);
        if (!pressed)
        {
            if (activated && down && !UiInteract.InputBlocked)
            {
                pressed = true;
                axis = Axis.Undecided;
                pressPosition = mouse;
                paging = false;
                scroller.Press(mouse.X);
                return;
            }

            if (!paging)
            {
                scroller.Tick(deltaSeconds);
            }

            return;
        }

        if (!down)
        {
            scroller.Release();
            scroller.Tick(deltaSeconds);
            pressed = false;
            if (axis == Axis.Horizontal)
            {
                UiInteract.BlockThisFrame();
            }

            axis = Axis.Undecided;
            return;
        }

        if (axis == Axis.Undecided)
        {
            var travel = mouse - pressPosition;
            var slop = AxisSlop * scale;
            if (MathF.Abs(travel.X) > slop && MathF.Abs(travel.X) >= MathF.Abs(travel.Y))
            {
                axis = Axis.Horizontal;
                UiInteract.CancelPendingTap();
            }
            else if (MathF.Abs(travel.Y) > slop)
            {
                axis = Axis.Vertical;
                scroller.CancelGesture();
                UiInteract.CancelPendingTap();
            }
        }

        if (axis == Axis.Horizontal)
        {
            scroller.Move(mouse.X, deltaSeconds);
            UiInteract.BlockThisFrame();
        }
    }
}
