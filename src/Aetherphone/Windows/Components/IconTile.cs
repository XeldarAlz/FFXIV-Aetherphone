using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal static class IconTile
{
    public static void Draw(Vector2 center, float size, Vector4 tint, FontAwesomeIcon icon)
    {
        var drawList = ImGui.GetWindowDrawList();
        var half = size * 0.5f;
        Squircle.Fill(drawList, center - new Vector2(half, half), center + new Vector2(half, half),
            size * Metrics.Radius.TileFactor, ImGui.GetColorU32(tint));
        ProgressRing.CenterIcon(center, icon, new Vector4(1f, 1f, 1f, 1f), size * 0.50f);
    }
}
