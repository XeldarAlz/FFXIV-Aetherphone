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
        DragScrollHost.Begin(key);
        return new SurfaceScope(child, padding);
    }

    public ref struct SurfaceScope
    {
        private ImRaii.ChildDisposable child;
        private readonly IDisposable padding;

        internal SurfaceScope(ImRaii.ChildDisposable child, IDisposable padding)
        {
            this.child = child;
            this.padding = padding;
        }

        public void Dispose()
        {
            child.Dispose();
            padding?.Dispose();
        }
    }
}
