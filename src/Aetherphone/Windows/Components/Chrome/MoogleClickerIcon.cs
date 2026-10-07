using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class MoogleClickerIcon
{
    private const float HeadRadius = 0.6f;
    private const float PomRadius = 0.18f;
    private static readonly Vector2 HeadCenter = new(0f, 0.2f);
    private static readonly Vector2 PomCenter = new(0.16f, -0.84f);

    public static void Draw(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint hole)
    {
        for (var side = -1f; side <= 1f; side += 2f)
        {
            drawList.AddTriangleFilled(At(center, extent, side * 0.5f, 0.3f), At(center, extent, side * 1.02f, -0.18f),
                At(center, extent, side * 0.86f, 0.5f), ink);
            drawList.AddTriangleFilled(At(center, extent, side * 0.16f, -0.32f), At(center, extent, side * 0.6f, -0.62f),
                At(center, extent, side * 0.58f, -0.1f), ink);
        }

        drawList.AddCircleFilled(center + HeadCenter * extent, HeadRadius * extent, ink, 32);
        var thickness = MathF.Max(1f, extent * 0.08f);
        drawList.AddBezierQuadratic(At(center, extent, 0f, -0.36f), At(center, extent, 0.16f, -0.56f),
            center + PomCenter * extent, ink, thickness, 10);
        drawList.AddCircleFilled(center + PomCenter * extent, PomRadius * extent, ink, 20);
        drawList.AddCircleFilled(At(center, extent, -0.24f, 0.12f), extent * 0.075f, hole, 12);
        drawList.AddCircleFilled(At(center, extent, 0.24f, 0.12f), extent * 0.075f, hole, 12);
        drawList.AddCircleFilled(At(center, extent, 0f, 0.32f), extent * 0.12f, hole, 16);
    }

    private static Vector2 At(Vector2 center, float extent, float unitX, float unitY) =>
        new(center.X + unitX * extent, center.Y + unitY * extent);
}
