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

        public void Reset()
        {
            Scroller.Reset();
            Pressed = false;
        }

        public void CancelGesture()
        {
            Scroller.CancelGesture();
            Pressed = false;
        }
    }

    public readonly struct Surface
    {
        private readonly Region? region;

        internal Surface(Region? region, float pull, bool dragging)
        {
            this.region = region;
            Pull = pull;
            Dragging = dragging;
        }

        public float Pull { get; }

        public bool Dragging { get; }

        public void JumpToTop()
        {
            ImGui.SetScrollY(0f);
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
                if (frame - region.LastFrame <= 1 && region.Scroller.IsDragging)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public static ImGuiWindowFlags ScrollFlags(ImGuiWindowFlags baseFlags) =>
        Enabled ? baseFlags | ImGuiWindowFlags.NoScrollbar : baseFlags;

    public static Surface Begin(uint key)
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

            return new Surface(region, 0f, false);
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

        if (region.Pressed)
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
                 !UiInteract.InputBlocked)
        {
            scroller.Press(pointerY);
            region.Pressed = true;
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

        return new Surface(region, scroller.PullDistance, scroller.IsDragging);
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
