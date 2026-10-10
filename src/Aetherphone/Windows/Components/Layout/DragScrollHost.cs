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

        public void Reset()
        {
            Scroller.Reset();
            Pressed = false;
            ClearGrab();
        }

        public void CancelGesture()
        {
            Scroller.CancelGesture();
            Pressed = false;
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

    public static ImGuiWindowFlags ScrollFlags(ImGuiWindowFlags baseFlags) =>
        Enabled ? baseFlags | ImGuiWindowFlags.NoScrollbar : baseFlags;

    public static bool HoversEdgeStrip(float stripWidth)
    {
        var windowMin = ImGui.GetWindowPos();
        var windowMax = windowMin + ImGui.GetWindowSize();
        return UiInteract.HoverWindowOnly(new Vector2(windowMax.X - stripWidth, windowMin.Y), windowMax, false);
    }

    public static Surface Begin(uint key, float grabStripWidth = 0f)
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

        var scroller = region.Scroller;
        scroller.Scale = UiScale.Current;
        scroller.SetBounds(ImGui.GetScrollMaxY());
        if (gapped)
        {
            region.Reset();
            UiInteract.CancelPendingTap();
        }

        scroller.SyncOffset(ImGui.GetScrollY());

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
            region.EdgePress = grabStripWidth > 0f && ImGui.GetScrollMaxY() > 0f &&
                               HoversEdgeStrip(grabStripWidth);
        }
        else
        {
            scroller.Tick(deltaSeconds);
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
