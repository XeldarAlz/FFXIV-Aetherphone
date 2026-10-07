using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class ClaimIcon
{
    private const float Frame = 0.92f;
    private static readonly Vector2[] Line =
    {
        new(0.3f, 0.36f), new(0.3f, -0.28f), new(0.62f, -0.28f),
    };

    private static readonly Vector2[] Boss =
    {
        new(-0.62f, -0.62f), new(-0.3f, -0.32f), new(-0.08f, -0.7f), new(0.14f, -0.46f),
    };

    public static void Draw(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint hole)
    {
        var frame = extent * Frame;
        var stroke = MathF.Max(1f, extent * 0.11f);
        drawList.AddRect(center - new Vector2(frame, frame), center + new Vector2(frame, frame), ink, extent * 0.12f,
            ImDrawFlags.None, stroke);
        drawList.AddRectFilled(At(center, extent, -Frame, 0.36f), At(center, extent, Frame, Frame), ink,
            extent * 0.06f);
        drawList.AddRectFilled(At(center, extent, -Frame, -0.12f), At(center, extent, -0.3f, Frame), ink,
            extent * 0.06f);
        var facet = MathF.Max(1f, extent * 0.06f);
        drawList.AddLine(At(center, extent, -0.82f, 0.86f), At(center, extent, -0.4f, 0.0f), hole, facet);
        drawList.AddLine(At(center, extent, -0.4f, 0.0f), At(center, extent, 0.2f, 0.86f), hole, facet);
        drawList.AddLine(At(center, extent, 0.2f, 0.86f), At(center, extent, 0.6f, 0.46f), hole, facet);
        Polyline(drawList, Line, center, extent, ink, stroke);
        Polyline(drawList, Boss, center, extent, ink, stroke * 0.8f);
        var marker = At(center, extent, 0.62f, -0.28f);
        var size = extent * 0.2f;
        drawList.AddQuadFilled(marker + new Vector2(0f, -size), marker + new Vector2(size, 0f),
            marker + new Vector2(0f, size), marker + new Vector2(-size, 0f), ink);
        drawList.AddCircleFilled(marker, size * 0.35f, hole, 8);
    }

    private static Vector2 At(Vector2 center, float extent, float unitX, float unitY) =>
        center + new Vector2(unitX, unitY) * extent;

    private static void Polyline(ImDrawListPtr drawList, ReadOnlySpan<Vector2> points, Vector2 center, float extent,
        uint color, float thickness)
    {
        for (var index = 0; index < points.Length; index++)
        {
            drawList.PathLineTo(center + points[index] * extent);
        }

        drawList.PathStroke(color, ImDrawFlags.None, thickness);
    }
}
