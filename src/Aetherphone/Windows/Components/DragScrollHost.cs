using Aetherphone.Core.Animation;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

/// <summary>Adds phone-style drag-to-scroll and momentum to an ImGui scroll child.</summary>
internal static class DragScrollHost
{
    private sealed class Region
    {
        public readonly KineticScroller Scroller = new();
        public int LastFrame = -2;
        public bool Pressed;
    }

    private const int EvictAfterFrames = 240;

    private static readonly Dictionary<uint, Region> Regions = new();
    private static readonly List<uint> stale = new();
    private static float currentPull;
    private static bool currentDragging;

    public static float CurrentPull => currentPull;
    public static bool CurrentDragging => currentDragging;

    /// <summary>Whether drag-to-scroll is live; set per-frame to the phone's lock state.</summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>Hides the ImGui scrollbar on a scroll child while drag-to-scroll is live; keeps it when unlocked.</summary>
    public static ImGuiWindowFlags ScrollFlags(ImGuiWindowFlags baseFlags) =>
        Enabled ? baseFlags | ImGuiWindowFlags.NoScrollbar : baseFlags;

    public static void Begin(uint key)
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
            scroller.Reset();
            region.Pressed = false;
            UiInteract.CancelPendingTap();
        }

        scroller.SyncOffset(ImGui.GetScrollY());

        if (!Enabled)
        {
            if (region.Pressed || scroller.IsControlling)
            {
                scroller.Reset();
                region.Pressed = false;
            }

            currentPull = 0f;
            currentDragging = false;
            return;
        }

        var io = ImGui.GetIO();
        var dt = io.DeltaTime;
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
                scroller.Move(pointerY, dt);
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
                scroller.Tick(dt);
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
            scroller.Tick(dt);
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

        currentPull = scroller.PullDistance;
        currentDragging = scroller.IsDragging;
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
