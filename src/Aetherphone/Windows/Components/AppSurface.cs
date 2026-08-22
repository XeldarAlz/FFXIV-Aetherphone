using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal static class AppSurface
{
    public static bool ActiveFreshVisit { get; private set; }

    public static SurfaceScope Begin(Rect area, bool disableMouseWheelScroll = false)
    {
        var scale = UiScale.Current;
        ImGui.SetCursorScreenPos(area.Min);
        var key = ImGui.GetID("##appSurface");
        var padding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(16f * scale, 8f * scale));
        var flags = DragScrollHost.ScrollFlags(ImGuiWindowFlags.NoBackground);
        if (disableMouseWheelScroll)
        {
            flags |= ImGuiWindowFlags.NoScrollWithMouse;
        }

        var child = ImRaii.Child("##appSurface", area.Size, false, flags);
        var freshVisit = ResetScrollOnNewVisit();
        ActiveFreshVisit = freshVisit;
        return new SurfaceScope(child, padding, DragScrollHost.Begin(key), freshVisit);
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

    public ref struct SurfaceScope
    {
        private ImRaii.ChildDisposable child;
        private readonly IDisposable padding;
        private readonly DragScrollHost.Surface surface;
        private readonly bool freshVisit;

        internal SurfaceScope(ImRaii.ChildDisposable child, IDisposable padding, DragScrollHost.Surface surface,
            bool freshVisit)
        {
            this.child = child;
            this.padding = padding;
            this.surface = surface;
            this.freshVisit = freshVisit;
        }

        public readonly float Pull => surface.Pull;

        public readonly bool Dragging => surface.Dragging;

        public readonly bool FreshVisit => freshVisit;

        public readonly void JumpToTop() => surface.JumpToTop();

        public readonly void CancelDrag() => surface.CancelDrag();

        public void Dispose()
        {
            ActiveFreshVisit = false;
            child.Dispose();
            padding?.Dispose();
        }
    }
}
