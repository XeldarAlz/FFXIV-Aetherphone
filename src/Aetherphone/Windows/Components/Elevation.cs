using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class Elevation
{
    private const int Layers = 8;
    private const float HorizontalBias = 0.55f;

    public static void Card(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, float scale,
        float opacity = 1f) =>
        Draw(drawList, min, max, rounding, scale, 11f, 4f, 0.18f, opacity);

    public static void Floating(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, float scale,
        float opacity = 1f) =>
        Draw(drawList, min, max, rounding, scale, 17f, 6f, 0.24f, opacity);

    public static void Icon(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, float radius,
        float opacity = 1f) =>
        Draw(drawList, min, max, rounding, 1f, radius * 0.34f, radius * 0.15f, 0.24f, opacity);

    public static void Draw(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, float scale, float spread,
        float yOffset, float strength, float opacity = 1f)
    {
        if (opacity <= 0f || strength <= 0f)
        {
            return;
        }

        var maxSpread = spread * scale;
        var drop = new Vector2(-yOffset * scale * HorizontalBias, yOffset * scale);
        var layerColor = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, strength / Layers * opacity));
        for (var index = 0; index < Layers; index++)
        {
            var inset = maxSpread * (1f - index / (float)Layers);
            var layerMin = min - new Vector2(inset, inset) + drop;
            var layerMax = max + new Vector2(inset, inset) + drop;
            drawList.AddRectFilled(layerMin, layerMax, layerColor, rounding + inset);
        }
    }
}
