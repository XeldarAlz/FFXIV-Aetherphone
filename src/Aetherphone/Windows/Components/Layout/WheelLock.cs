using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class WheelLock
{
    public static void ReleaseStale()
    {
        var context = ImGui.GetCurrentContext();
        if (context.IsNull)
        {
            return;
        }

        ref var locked = ref context.WheelingWindow;
        if (locked.IsNull || locked.Active || locked.RootWindow != ImGuiP.GetCurrentWindowRead().RootWindow)
        {
            return;
        }

        locked = default;
        context.WheelingWindowTimer = 0f;
    }
}
