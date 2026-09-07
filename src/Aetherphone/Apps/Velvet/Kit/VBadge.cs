using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet.Kit;

internal static class VBadge
{
    public static void Dot(ImDrawListPtr drawList, Vector2 center, Vector4 color)
    {
        var scale = UiScale.Current;
        drawList.AddCircleFilled(center, 3.5f * scale + 1.5f * scale, VelvetTheme.GroundBottom.Packed(), 16);
        drawList.AddCircleFilled(center, 3.5f * scale, color.Packed(), 16);
    }
}
