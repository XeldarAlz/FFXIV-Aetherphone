using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class UpdraftIcon
{
    public static void Draw(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint hole)
    {
        var cloudY = center.Y + extent * 0.5f;
        drawList.AddCircleFilled(new Vector2(center.X - extent * 0.5f, cloudY + extent * 0.12f), extent * 0.3f, ink, 24);
        drawList.AddCircleFilled(new Vector2(center.X - extent * 0.06f, cloudY - extent * 0.02f), extent * 0.4f, ink, 28);
        drawList.AddCircleFilled(new Vector2(center.X + extent * 0.46f, cloudY + extent * 0.1f), extent * 0.32f, ink, 24);
        drawList.AddRectFilled(new Vector2(center.X - extent * 0.8f, cloudY + extent * 0.1f),
            new Vector2(center.X + extent * 0.8f, cloudY + extent * 0.42f), ink, extent * 0.16f);
        var streak = extent * 0.09f;
        drawList.AddRectFilled(new Vector2(center.X - extent * 0.78f - streak, center.Y - extent * 0.62f),
            new Vector2(center.X - extent * 0.78f + streak, center.Y - extent * 0.08f), ink, streak);
        drawList.AddRectFilled(new Vector2(center.X + extent * 0.8f - streak, center.Y - extent * 0.86f),
            new Vector2(center.X + extent * 0.8f + streak, center.Y - extent * 0.36f), ink, streak);
        var bird = new Vector2(center.X, center.Y - extent * 0.38f);
        var radius = extent * 0.36f;
        drawList.AddCircleFilled(bird, radius, ink, 28);
        drawList.AddTriangleFilled(new Vector2(bird.X + radius * 0.8f, bird.Y - radius * 0.2f),
            new Vector2(bird.X + radius * 1.35f, bird.Y + radius * 0.02f), new Vector2(bird.X + radius * 0.8f, bird.Y + radius * 0.26f),
            ink);
        drawList.AddCircleFilled(new Vector2(bird.X + radius * 0.36f, bird.Y - radius * 0.28f), radius * 0.2f, hole, 14);
        drawList.PathClear();
        drawList.PathArcTo(new Vector2(bird.X - radius * 0.22f, bird.Y + radius * 0.05f), radius * 0.42f, MathF.PI * 0.1f,
            MathF.PI * 0.95f, 12);
        drawList.PathStroke(hole, ImDrawFlags.None, MathF.Max(1f, radius * 0.16f));
    }
}
