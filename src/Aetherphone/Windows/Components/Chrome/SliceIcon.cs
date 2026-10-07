using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class SliceIcon
{
    private const float GemScale = 0.82f;
    private const float Separation = 0.13f;
    private static readonly Vector2 CutNormal = new(0.514f, 0.857f);
    private static readonly Vector2[] UpperHalf =
    {
        new(0f, -1f), new(0.62f, -0.42f), new(0.626f, -0.375f), new(-0.568f, 0.341f), new(-0.7f, 0.2f),
        new(-0.62f, -0.42f),
    };

    private static readonly Vector2[] LowerHalf =
    {
        new(0.626f, -0.375f), new(0.7f, 0.2f), new(0f, 0.95f), new(-0.568f, 0.341f),
    };

    private static readonly Vector2[] Facet =
    {
        new(0f, -0.82f), new(0.42f, -0.4f), new(0f, -0.22f), new(-0.42f, -0.4f),
    };

    public static void Draw(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint hole)
    {
        var size = extent * GemScale;
        var upperCenter = center - CutNormal * (extent * Separation);
        var lowerCenter = center + CutNormal * (extent * Separation);
        Fill(drawList, UpperHalf, upperCenter, size, ink);
        Fill(drawList, LowerHalf, lowerCenter, size, ink);
        Fill(drawList, Facet, upperCenter, size, hole);
        var thickness = MathF.Max(1f, extent * 0.08f);
        var slashEnd = center + new Vector2(1.1f, -0.67f) * extent;
        drawList.AddLine(center + new Vector2(-1.1f, 0.67f) * extent, center + new Vector2(-0.72f, 0.44f) * extent, ink,
            thickness * 0.6f);
        drawList.AddLine(center + new Vector2(0.72f, -0.44f) * extent, slashEnd, ink, thickness);
        drawList.AddCircleFilled(slashEnd, thickness * 0.8f, ink, 10);
        for (var drop = 0; drop < 3; drop++)
        {
            var offset = new Vector2(0.34f + drop * 0.24f, 0.62f + drop * 0.12f);
            drawList.AddCircleFilled(center + offset * extent, extent * (0.1f - drop * 0.022f), ink, 12);
        }
    }

    private static void Fill(ImDrawListPtr drawList, ReadOnlySpan<Vector2> points, Vector2 center, float size, uint color)
    {
        drawList.PathClear();
        for (var index = 0; index < points.Length; index++)
        {
            drawList.PathLineTo(center + points[index] * size);
        }

        drawList.PathFillConvex(color);
    }
}
