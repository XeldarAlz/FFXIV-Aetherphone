using Aetherphone.Core.Animation;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class DragScrollHost
{
    internal sealed class Region
    {
        public readonly KineticScroller Scroller = new();
        public int LastFrame = -2;
        public bool Pressed;
        public bool EdgePress;
        public bool Grabbing;
        public float GrabPointerY;
        public float GrabScrollY;
        public Spring Wheel;
        public float WheelTarget;
        public float WheelLast;
        public bool Wheeling;
        public bool Smooth;

        public void Reset()
        {
            Scroller.Reset();
            Pressed = false;
            Wheeling = false;
            ClearGrab();
        }

        public void CancelGesture()
        {
            Scroller.CancelGesture();
            Pressed = false;
            Wheeling = false;
            ClearGrab();
        }

        private void ClearGrab()
        {
            EdgePress = false;
            Grabbing = false;
            GrabPointerY = 0f;
            GrabScrollY = 0f;
        }
    }

    public readonly struct Surface
    {
        private readonly Region? region;

        internal Surface(Region? region, float pull, bool dragging, bool grabbing)
        {
            this.region = region;
            Pull = pull;
            Dragging = dragging || grabbing;
            Grabbing = grabbing;
        }

        public float Pull { get; }

        public bool Dragging { get; }

        public bool Grabbing { get; }

        public bool Scrolling => region is not null && (region.Scroller.IsControlling || region.Grabbing);

        public void JumpToTop() => JumpTo(0f);

        public void JumpTo(float scrollY)
        {
            ImGui.SetScrollY(scrollY);
            region?.Reset();
        }

        public void CancelDrag() => region?.CancelGesture();
    }

    private const int EvictAfterFrames = 240;
    private const float ImGuiWheelStepFontSizes = 5f;
    private const float ImGuiWheelStepMaxShare = 0.67f;
    private const float WheelRestEpsilon = 0.5f;
    private const float WheelYieldPixels = 1f;

    private static readonly Dictionary<uint, Region> Regions = new();
    private static readonly List<uint> stale = new();

    public static bool Enabled { get; set; } = true;

    public static bool AnyDragging
    {
        get
        {
            var frame = ImGui.GetFrameCount();
            foreach (var region in Regions.Values)
            {
                if (frame - region.LastFrame <= 1 && (region.Scroller.IsDragging || region.Grabbing))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public static void CancelMomentum()
    {
        if (Regions.TryGetValue(ImGuiP.GetCurrentWindowRead().ChildId, out var region))
        {
            region.Scroller.CancelMomentum();
        }
    }

    public static ImGuiWindowFlags ScrollFlags(ImGuiWindowFlags baseFlags) =>
        Enabled ? baseFlags | ImGuiWindowFlags.NoScrollbar : baseFlags;

    public static bool HoversEdgeStrip(float stripWidth)
    {
        var windowMin = ImGui.GetWindowPos();
        var windowMax = windowMin + ImGui.GetWindowSize();
        return UiInteract.HoverWindowOnly(new Vector2(windowMax.X - stripWidth, windowMin.Y), windowMax, false);
    }

    public static Surface Begin(uint key, float grabStripWidth = 0f, bool smoothWheel = false)
    {
        var frame = ImGui.GetFrameCount();
        EvictStale(frame);
        if (!Regions.TryGetValue(key, out var region))
        {
            region = new Region();
            Regions[key] = region;
        }

        var gapped = region.LastFrame != frame - 1;
        region.LastFrame = frame;
        region.Smooth = smoothWheel;

        var scroller = region.Scroller;
        scroller.Scale = UiScale.Current;
        scroller.SetBounds(ImGui.GetScrollMaxY());
        if (gapped)
        {
            region.Reset();
            UiInteract.CancelPendingTap();
        }

        scroller.SyncOffset(ImGui.GetScrollY());
        if (smoothWheel)
        {
            Glide(region, frame);
        }

        if (!Enabled)
        {
            if (region.Pressed || scroller.IsControlling)
            {
                region.Reset();
            }

            return new Surface(region, 0f, false, false);
        }

        if (InputShield.Active)
        {
            if (region.Pressed || scroller.IsControlling)
            {
                region.CancelGesture();
            }

            return new Surface(region, 0f, false, false);
        }

        var io = ImGui.GetIO();
        var deltaSeconds = io.DeltaTime;
        var pointerY = io.MousePos.Y;
        var down = ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var widgetOwnsClick = ImGui.IsAnyItemActive();
        var hovered = ImGui.IsWindowHovered();
        var shouldBlock = false;

        if (io.MouseWheel != 0f)
        {
            UiInteract.CancelPendingTap();
            if (!region.Pressed)
            {
                scroller.CancelMomentum();
            }
        }

        if (region.Pressed && region.EdgePress)
        {
            if (down)
            {
                DragThumb(region, pointerY);
                shouldBlock = region.Grabbing;
            }
            else
            {
                shouldBlock = region.Grabbing;
                region.CancelGesture();
            }
        }
        else if (region.Pressed)
        {
            if (down)
            {
                var wasDragging = scroller.IsDragging;
                scroller.Move(pointerY, deltaSeconds);
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
                region.Pressed = false;
                scroller.Tick(deltaSeconds);
            }
        }
        else if (down && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && hovered && !widgetOwnsClick &&
                 !UiInteract.InputBlocked && !UiInteract.PointerOverDragSurface)
        {
            scroller.Press(pointerY);
            region.Pressed = true;
            region.Wheeling = false;
            region.EdgePress = grabStripWidth > 0f && ImGui.GetScrollMaxY() > 0f &&
                               HoversEdgeStrip(grabStripWidth);
        }
        else
        {
            scroller.Tick(deltaSeconds);
        }

        if (grabStripWidth > 0f && hovered && ImGui.GetScrollMaxY() > 0f && HoversEdgeStrip(grabStripWidth))
        {
            shouldBlock = true;
        }

        if (scroller.IsControlling)
        {
            ImGui.SetScrollY(scroller.Offset);
        }

        if (shouldBlock)
        {
            UiInteract.BlockThisFrame();
        }

        if (scroller.PullDistance > 0f)
        {
            ImGui.Dummy(new Vector2(0f, scroller.PullDistance));
        }

        return new Surface(region, scroller.PullDistance, scroller.IsDragging, region.Grabbing);
    }

    public static bool ParentTakesWheel(ImGuiWindowPtr parent)
    {
        var window = parent;
        while (!window.IsNull && (window.Flags & ImGuiWindowFlags.ChildWindow) != 0 &&
               (window.ScrollMax.Y == 0f || (window.Flags & ImGuiWindowFlags.NoScrollWithMouse) != 0))
        {
            window = window.ParentWindow;
        }

        return !window.IsNull && (window.Flags & ImGuiWindowFlags.NoScrollWithMouse) == 0;
    }

    private static void Glide(Region region, int frame)
    {
        var io = ImGui.GetIO();
        if (io.MouseWheel == 0f && !region.Wheeling)
        {
            return;
        }

        var window = ImGuiP.GetCurrentWindowRead();
        var maxY = MathF.Max(0f, window.ScrollMax.Y);
        var down = ImGui.IsMouseDown(ImGuiMouseButton.Left);
        if (region.Wheeling &&
            (down || MathF.Abs(window.Scroll.Y - MathF.Min(region.WheelLast, maxY)) > WheelYieldPixels))
        {
            region.Wheeling = false;
        }

        if (io.MouseWheel != 0f && !io.KeyShift && ReceivesWheel(window, frame))
        {
            var parentScale = window.ParentWindow.IsNull ? 1f : window.ParentWindow.FontWindowScale;
            var font = io.FontDefault.IsNull ? io.Fonts.Fonts[0] : io.FontDefault;
            var fontSize = io.FontGlobalScale * font.FontSize * font.Scale * window.FontWindowScale * parentScale;
            var view = window.InnerRect.Max.Y - window.InnerRect.Min.Y;
            var step = io.MouseWheel *
                       MathF.Floor(MathF.Min(ImGuiWheelStepFontSizes * fontSize, view * ImGuiWheelStepMaxShare));
            if (down)
            {
                ImGui.SetScrollY(Math.Clamp(window.Scroll.Y - step, 0f, maxY));
                return;
            }

            if (!region.Wheeling)
            {
                region.Wheel.SnapTo(window.Scroll.Y);
                region.WheelTarget = window.Scroll.Y;
                region.Wheeling = true;
            }

            region.WheelTarget -= step;
        }

        if (!region.Wheeling)
        {
            return;
        }

        region.WheelTarget = Math.Clamp(region.WheelTarget, 0f, maxY);
        region.Wheel.Value = MathF.Min(region.Wheel.Value, maxY);
        var next = region.Wheel.Step(region.WheelTarget, Motion.PageSettle,
            MathF.Min(io.DeltaTime, TransitionTiming.MaxFrameSeconds));
        if (MathF.Abs(next - region.WheelTarget) <= WheelRestEpsilon)
        {
            next = region.WheelTarget;
            region.Wheeling = false;
        }

        region.WheelLast = next;
        ImGui.SetScrollY(next);
    }

    private static bool ReceivesWheel(ImGuiWindowPtr window, int frame)
    {
        var context = ImGui.GetCurrentContext();
        var target = context.WheelingWindow.IsNull ? context.HoveredWindow : context.WheelingWindow;
        while (!target.IsNull && target != window && (target.Flags & ImGuiWindowFlags.ChildWindow) != 0 &&
               !GlidesAt(target, frame) &&
               (target.ScrollMax.Y == 0f || (target.Flags & ImGuiWindowFlags.NoScrollWithMouse) != 0))
        {
            target = target.ParentWindow;
        }

        return target == window;
    }

    private static bool GlidesAt(ImGuiWindowPtr window, int frame) =>
        window.ScrollMax.Y > 0f && Regions.TryGetValue(window.ChildId, out var region) && region.Smooth && frame - region.LastFrame <= 1;

    private static void DragThumb(Region region, float pointerY)
    {
        var scale = UiScale.Current;
        var scrollY = ImGui.GetScrollY();
        if (!region.Grabbing)
        {
            if (!region.Scroller.ExceedsDragThreshold(pointerY))
            {
                return;
            }

            region.Grabbing = true;
            region.GrabPointerY = pointerY;
            region.GrabScrollY = scrollY;
            UiInteract.CancelPendingTap();
        }

        var maxY = ImGui.GetScrollMaxY();
        var thumb = ScrollThumb.Measure(ImGui.GetWindowPos().Y, ImGui.GetWindowSize().Y, scrollY, maxY, scale);
        var target = region.GrabScrollY + thumb.ScrollDelta(pointerY - region.GrabPointerY);
        ImGui.SetScrollY(Math.Clamp(target, 0f, MathF.Max(0f, maxY)));
    }

    private static void EvictStale(int frame)
    {
        foreach (var (key, region) in Regions)
        {
            if (frame - region.LastFrame > EvictAfterFrames)
            {
                stale.Add(key);
            }
        }

        foreach (var key in stale)
        {
            Regions.Remove(key);
        }

        stale.Clear();
    }
}
