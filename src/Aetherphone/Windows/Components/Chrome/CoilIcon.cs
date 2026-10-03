using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class CoilIcon
{
    private const int ChainLength = 9;

    public static void Draw(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint hole)
    {
        for (var link = 0; link < ChainLength; link++)
        {
            var angle = -MathF.PI * 0.5f + link * 0.74f;
            var orbit = extent * (0.98f - link * 0.062f);
            var marble = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * orbit;
            var size = extent * (0.2f - link * 0.009f);
            drawList.AddCircleFilled(marble, size, ink, 16);
            drawList.AddCircleFilled(marble - new Vector2(size * 0.32f, size * 0.32f), size * 0.3f, hole, 10);
        }

        drawList.AddCircleFilled(center, extent * 0.3f, ink, 24);
        drawList.AddCircleFilled(center, extent * 0.15f, hole, 16);
        drawList.AddCircleFilled(center + new Vector2(0f, -extent * 0.3f), extent * 0.11f, ink, 12);
    }
}
