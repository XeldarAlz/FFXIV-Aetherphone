using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal static class AppSurface
{
    public const float SidePadding = 16f;
    private const float TopPadding = 8f;
    private const float NavBarSnapTolerance = 0.5f;
    private const float ScrollbarSizeUnits = 4f;
    private const float ScrollbarRoundingUnits = 2f;
    private const float IndicatorHoldSeconds = 0.8f;
    private const float IndicatorStripUnits = 16f;
    private const float IndicatorWidthUnits = 3f;
    private const float IndicatorAlphaScale = 0.45f;
    private const float IndicatorMotionEpsilon = 0.5f;
    private const float MaxFrameSeconds = 0.1f;

    private static Spring indicator;
    private static double lastScrollMotion = -IndicatorHoldSeconds;
    private static int indicatorFrame = -1;

    public static float IndicatorAlpha { get; private set; }

    private static int depth;
    private static bool navBarArmed;
    private static float navBarBodyTop;
    private static float navBarInset;

    public static bool ActiveFreshVisit { get; private set; }

    public static Vector4? ScrollbarInk { get; set; }

    private static float ambientBottomInset;

    public static BottomInsetScope ReserveBottom(float inset) => new(inset);

    public static bool NavBarConsumed { get; private set; }

    public static float NavBarScrollY { get; private set; }

    public static void ArmNavBar(float bodyTop, float topInset)
    {
        navBarArmed = true;
        navBarBodyTop = bodyTop;
        navBarInset = topInset;
        NavBarConsumed = false;
        NavBarScrollY = 0f;
    }

    public static void DisarmNavBar() => navBarArmed = false;

    public static SurfaceScope Begin(Rect area, bool disableMouseWheelScroll = false) =>
        BeginCore(area, SidePadding, disableMouseWheelScroll);

    public static SurfaceScope Begin(Rect area, float sidePadding, bool disableMouseWheelScroll = false) =>
        BeginCore(area, sidePadding, disableMouseWheelScroll);

    public static SurfaceScope BeginEdgeToEdge(Rect area, bool disableMouseWheelScroll = false) =>
        BeginCore(area, 0f, disableMouseWheelScroll);

    private static SurfaceScope BeginCore(Rect area, float horizontalPadding, bool disableMouseWheelScroll)
    {
        var scale = UiScale.Current;
        var hostsNavBar = navBarArmed && depth == 0 &&
                          MathF.Abs(area.Min.Y - navBarBodyTop) <= NavBarSnapTolerance;
        if (hostsNavBar)
        {
            area = new Rect(new Vector2(area.Min.X, area.Min.Y - navBarInset), area.Max);
        }

        ImGui.SetCursorScreenPos(area.Min);
        var key = ImGui.GetID("##appSurface");
        var padding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding,
                new Vector2(horizontalPadding * scale, TopPadding * scale))
            .Push(ImGuiStyleVar.ScrollbarSize, ScrollbarSizeUnits * scale)
            .Push(ImGuiStyleVar.ScrollbarRounding, ScrollbarRoundingUnits * scale);
        var flags = DragScrollHost.ScrollFlags(ImGuiWindowFlags.NoBackground);
        if (disableMouseWheelScroll)
        {
            flags |= ImGuiWindowFlags.NoScrollWithMouse;
        }

        var scrollbar = ScrollbarInk is { } ink ? ScrollLayout.PushScrollbarInk(ink) : null;
        var child = ImRaii.Child("##appSurface", area.Size, false, flags);
        var freshVisit = ResetScrollOnNewVisit();
        ActiveFreshVisit = freshVisit;
        var surface = DragScrollHost.Begin(key, IndicatorStripUnits * scale);
        if (hostsNavBar)
        {
            ReserveNavBarBand(freshVisit);
        }

        depth++;
        return new SurfaceScope(child, padding, scrollbar, surface, freshVisit, ambientBottomInset);
    }

    private static void ReserveNavBarBand(bool freshVisit)
    {
        navBarArmed = false;
        NavBarConsumed = true;
        NavBarScrollY = freshVisit ? 0f : ImGui.GetScrollY();
        var style = ImGui.GetStyle();
        var reserve = MathF.Max(0f, navBarInset - ImGui.GetCursorPosY() - style.ItemSpacing.Y);
        ImGui.Dummy(new Vector2(0f, reserve));
    }

    private static void TrackIndicator(bool grabbing)
    {
        var frame = ImGui.GetFrameCount();
        var now = ImGui.GetTime();
        var scrollY = ImGui.GetScrollY();
        var storage = ImGui.GetStateStorage();
        var key = ImGui.GetID("##appSurfaceScrollY");
        var previous = storage.GetFloat(key, scrollY);
        if (MathF.Abs(previous - scrollY) > IndicatorMotionEpsilon)
        {
            lastScrollMotion = now;
        }

        storage.SetFloat(key, scrollY);
        var scale = UiScale.Current;
        var windowMin = ImGui.GetWindowPos();
        var windowSize = ImGui.GetWindowSize();
        var windowMax = windowMin + windowSize;
        if (grabbing || DragScrollHost.HoversEdgeStrip(IndicatorStripUnits * scale))
        {
            lastScrollMotion = now;
        }

        if (indicatorFrame != frame)
        {
            indicatorFrame = frame;
            var delta = MathF.Min(ImGui.GetIO().DeltaTime, MaxFrameSeconds);
            var wanted = now - lastScrollMotion < IndicatorHoldSeconds ? 1f : 0f;
            IndicatorAlpha = Math.Clamp(indicator.Step(wanted, Motion.Appear, delta), 0f, 1f);
        }

        if (!DragScrollHost.Enabled || IndicatorAlpha <= 0.01f)
        {
            return;
        }

        var maxY = ImGui.GetScrollMaxY();
        if (maxY <= 0f)
        {
            return;
        }

        var thumb = ScrollThumb.Measure(windowMin.Y, windowSize.Y, scrollY, maxY, scale);
        var right = windowMax.X - ScrollThumb.InsetUnits * scale;
        var width = IndicatorWidthUnits * scale;
        var ink = ScrollbarInk ?? new Vector4(1f, 1f, 1f, 1f);
        ImGui.GetWindowDrawList().AddRectFilled(new Vector2(right - width, thumb.Top),
            new Vector2(right, thumb.Top + thumb.Height),
            ImGui.GetColorU32(Palette.WithAlpha(ink, ink.W * IndicatorAlphaScale * IndicatorAlpha)), width * 0.5f);
    }

    public static bool ResetScrollOnNewVisit()
    {
        var visit = AppVisits.Active;
        if (visit == 0)
        {
            return false;
        }

        var storage = ImGui.GetStateStorage();
        var stampKey = ImGui.GetID("##appSurfaceVisit");
        if (storage.GetInt(stampKey, 0) == visit)
        {
            return false;
        }

        storage.SetInt(stampKey, visit);
        ImGui.SetScrollY(0f);
        return true;
    }

    public ref struct BottomInsetScope
    {
        private readonly float previous;

        internal BottomInsetScope(float inset)
        {
            previous = ambientBottomInset;
            ambientBottomInset = MathF.Max(0f, inset);
        }

        public void Dispose() => ambientBottomInset = previous;
    }

    public ref struct SurfaceScope
    {
        private ImRaii.ChildDisposable child;
        private readonly IDisposable padding;
        private readonly IDisposable? scrollbar;
        private readonly DragScrollHost.Surface surface;
        private readonly bool freshVisit;
        private readonly float bottomInset;

        internal SurfaceScope(ImRaii.ChildDisposable child, IDisposable padding, IDisposable? scrollbar,
            DragScrollHost.Surface surface, bool freshVisit, float bottomInset)
        {
            this.child = child;
            this.padding = padding;
            this.scrollbar = scrollbar;
            this.surface = surface;
            this.freshVisit = freshVisit;
            this.bottomInset = bottomInset;
        }

        public readonly float Pull => surface.Pull;

        public readonly bool Dragging => surface.Dragging;

        public readonly bool Scrolling => surface.Scrolling;

        public readonly bool FreshVisit => freshVisit;

        public readonly void JumpToTop() => surface.JumpToTop();

        public readonly void JumpTo(float scrollY) => surface.JumpTo(scrollY);

        public readonly void CancelDrag() => surface.CancelDrag();

        public void Dispose()
        {
            ActiveFreshVisit = false;
            if (bottomInset > 0f)
            {
                ImGui.Dummy(new Vector2(0f, bottomInset));
            }

            TrackIndicator(surface.Grabbing);
            depth = Math.Max(0, depth - 1);
            child.Dispose();
            padding?.Dispose();
            scrollbar?.Dispose();
        }
    }
}
