using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class LanderIcon
{
    private static readonly Vector2[] Cabin =
    {
        new(-0.2f, -0.92f), new(0.2f, -0.92f), new(0.42f, -0.7f), new(0.42f, -0.38f), new(0.24f, -0.24f),
        new(-0.24f, -0.24f), new(-0.42f, -0.38f), new(-0.42f, -0.7f),
    };

    private static readonly Vector2[] Stage =
    {
        new(-0.56f, -0.24f), new(0.56f, -0.24f), new(0.46f, 0.16f), new(-0.46f, 0.16f),
    };

    private static readonly Vector2[] Flame =
    {
        new(-0.16f, 0.24f), new(0.16f, 0.24f), new(0f, 0.72f),
    };

    public static void Draw(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint hole)
    {
        var stroke = MathF.Max(1f, extent * 0.1f);
        Fill(drawList, Cabin, center, extent, ink);
        Fill(drawList, Stage, center, extent, ink);
        Fill(drawList, Flame, center, extent, ink);
        drawList.AddCircleFilled(center + new Vector2(-0.06f, -0.6f) * extent, extent * 0.14f, hole, 14);
        drawList.AddLine(center + new Vector2(-0.56f, -0.04f) * extent, center + new Vector2(0.56f, -0.04f) * extent,
            hole, MathF.Max(1f, extent * 0.06f));
        for (var side = -1; side <= 1; side += 2)
        {
            var hip = center + new Vector2(0.4f * side, 0.06f) * extent;
            var foot = center + new Vector2(0.76f * side, 0.56f) * extent;
            drawList.AddLine(hip, foot, ink, stroke);
            drawList.AddLine(foot - new Vector2(0.14f, 0f) * extent, foot + new Vector2(0.14f, 0f) * extent, ink,
                stroke * 1.3f);
        }

        var padTop = center.Y + extent * 0.66f;
        drawList.AddRectFilled(new Vector2(center.X - extent * 0.95f, padTop),
            new Vector2(center.X + extent * 0.95f, padTop + extent * 0.14f), ink, extent * 0.05f);
        drawList.AddCircleFilled(new Vector2(center.X - extent * 0.85f, padTop - extent * 0.08f), extent * 0.07f, ink, 8);
        drawList.AddCircleFilled(new Vector2(center.X + extent * 0.85f, padTop - extent * 0.08f), extent * 0.07f, ink, 8);
    }

    private static void Fill(ImDrawListPtr drawList, ReadOnlySpan<Vector2> points, Vector2 center, float extent,
        uint color)
    {
        for (var index = 0; index < points.Length; index++)
        {
            drawList.PathLineTo(center + points[index] * extent);
        }

        drawList.PathFillConvex(color);
    }
}
