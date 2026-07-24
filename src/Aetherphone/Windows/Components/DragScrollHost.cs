using Aetherphone.Core.Animation;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

/// <summary>Adds phone-style drag-to-scroll and momentum to an ImGui scroll child.</summary>
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
    }

    /// <summary>A scroll region begun this frame: its gesture state and top-snap control.</summary>
    public readonly struct Surface
    {
        private readonly Region? region;

        internal Surface(Region? region, float pull, bool dragging)
        {
            this.region = region;
            Pull = pull;
            Dragging = dragging;
        }

        /// <summary>How far the region is pulled past its top edge, in pixels.</summary>
        public float Pull { get; }

        /// <summary>Whether the pointer is dragging this region right now.</summary>
        public bool Dragging { get; }

        /// <summary>Snaps the region back to the top, dropping any drag, momentum, or pull.</summary>
        public void JumpToTop()
        {
            ImGui.SetScrollY(0f);
            region?.Reset();
        }
    }

    private const int EvictAfterFrames = 240;

    private static readonly Dictionary<uint, Region> Regions = new();
    private static readonly List<uint> stale = new();

    /// <summary>Whether drag-to-scroll is live; set per-frame to the phone's lock state.</summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>Whether any live region is mid-drag; readable before this frame's regions have begun.</summary>
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

    /// <summary>Hides the ImGui scrollbar on a scroll child while drag-to-scroll is live; keeps it when unlocked.</summary>
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
        scroller.Scale = ImGuiHelpers.GlobalScale;
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
