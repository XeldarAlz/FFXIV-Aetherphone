using Aetherphone.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal static class AppSurface
{
    public static SurfaceScope Begin(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.SetCursorScreenPos(area.Min);
        var key = ImGui.GetID("##appSurface");
        var padding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(16f * scale, 8f * scale));
        var child = ImRaii.Child("##appSurface", area.Size, false,
            DragScrollHost.ScrollFlags(ImGuiWindowFlags.NoBackground));
        return new SurfaceScope(child, padding, DragScrollHost.Begin(key));
    }

    public ref struct SurfaceScope
    {
        private ImRaii.ChildDisposable child;
        private readonly IDisposable padding;
        private readonly DragScrollHost.Surface surface;

        internal SurfaceScope(ImRaii.ChildDisposable child, IDisposable padding, DragScrollHost.Surface surface)
        {
            this.child = child;
            this.padding = padding;
            this.surface = surface;
        }

        public readonly float Pull => surface.Pull;

        public readonly bool Dragging => surface.Dragging;

        public readonly void JumpToTop() => surface.JumpToTop();

        /// <summary>Hands an in-progress gesture to a widget inside this surface (see SkywatcherApp's
        /// scrubber), leaving the scroll offset alone instead of letting it also become a scroll.</summary>
        public readonly void CancelDrag() => surface.CancelDrag();

        public void Dispose()
        {
            child.Dispose();
            padding?.Dispose();
        }
    }
}
