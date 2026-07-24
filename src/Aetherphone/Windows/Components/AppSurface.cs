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

        /// <summary>How far this surface is pulled past its top edge, in pixels.</summary>
        public readonly float Pull => surface.Pull;

        /// <summary>Whether the pointer is dragging this surface right now.</summary>
        public readonly bool Dragging => surface.Dragging;

        /// <summary>Snaps this surface back to the top, dropping any drag, momentum, or pull.</summary>
        public readonly void JumpToTop() => surface.JumpToTop();

        public void Dispose()
        {
            child.Dispose();
            padding?.Dispose();
        }
    }
}
