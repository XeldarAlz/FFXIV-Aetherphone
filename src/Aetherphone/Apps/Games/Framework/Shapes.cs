using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Framework;

internal static class Shapes
{
    public const int DefaultSegments = 24;
    private const float MinRadius = 0.2f;

    private static int[] orderBuffer = new int[32];
    private static int[] triangleBuffer = new int[90];

    public static void EllipsePath(ImDrawListPtr drawList, Vector2 center, Vector2 radii,
        int segments = DefaultSegments)
    {
        drawList.PathClear();
        for (var segment = 0; segment < segments; segment++)
        {
            var angle = segment * MathF.Tau / segments;
            drawList.PathLineTo(center + new Vector2(MathF.Cos(angle) * radii.X, MathF.Sin(angle) * radii.Y));
        }
    }

    public static void EllipsePath(ImDrawListPtr drawList, Vector2 center, Vector2 radii, float angle,
        int segments = DefaultSegments)
    {
        var cosine = MathF.Cos(angle);
        var sine = MathF.Sin(angle);
        drawList.PathClear();
        for (var segment = 0; segment < segments; segment++)
        {
            var theta = segment * MathF.Tau / segments;
            var localX = MathF.Cos(theta) * radii.X;
            var localY = MathF.Sin(theta) * radii.Y;
            drawList.PathLineTo(new Vector2(center.X + localX * cosine - localY * sine,
                center.Y + localX * sine + localY * cosine));
        }
    }

    public static void FillEllipse(ImDrawListPtr drawList, Vector2 center, Vector2 radii, uint color,
        int segments = DefaultSegments)
    {
        if (!Visible(radii))
        {
            return;
        }

        EllipsePath(drawList, center, radii, segments);
        drawList.PathFillConvex(color);
    }

    public static void FillEllipse(ImDrawListPtr drawList, Vector2 center, Vector2 radii, float angle, uint color,
        int segments = DefaultSegments)
    {
        if (!Visible(radii))
        {
            return;
        }

        EllipsePath(drawList, center, radii, angle, segments);
        drawList.PathFillConvex(color);
    }

    public static void FillEllipse(ImDrawListPtr drawList, Vector2 center, float radiusX, float radiusY, uint color,
        int segments = DefaultSegments) =>
        FillEllipse(drawList, center, new Vector2(radiusX, radiusY), color, segments);

    public static void FillEllipse(ImDrawListPtr drawList, Vector2 center, float radiusX, float radiusY, float angle,
        uint color, int segments = DefaultSegments) =>
        FillEllipse(drawList, center, new Vector2(radiusX, radiusY), angle, color, segments);

    public static void StrokeEllipse(ImDrawListPtr drawList, Vector2 center, Vector2 radii, uint color,
        float thickness, int segments = DefaultSegments)
    {
        if (!Visible(radii))
        {
            return;
        }

        EllipsePath(drawList, center, radii, segments);
        drawList.PathStroke(color, ImDrawFlags.Closed, thickness);
    }

    public static void StrokeEllipse(ImDrawListPtr drawList, Vector2 center, Vector2 radii, float angle, uint color,
        float thickness, int segments = DefaultSegments)
    {
        if (!Visible(radii))
        {
            return;
        }

        EllipsePath(drawList, center, radii, angle, segments);
        drawList.PathStroke(color, ImDrawFlags.Closed, thickness);
    }

    public static void FillConcave(ImDrawListPtr drawList, ReadOnlySpan<Vector2> polygon, uint color)
    {
        if (polygon.Length < 3)
        {
            return;
        }

        if (orderBuffer.Length < polygon.Length)
        {
            orderBuffer = new int[polygon.Length];
        }

        var needed = Polygon.TriangleIndexCount(polygon.Length);
        if (triangleBuffer.Length < needed)
        {
            triangleBuffer = new int[needed];
        }

        var written = Polygon.Triangulate(polygon, orderBuffer, triangleBuffer);
        FillTriangles(drawList, polygon, triangleBuffer.AsSpan(0, written), color);
    }

    public static void FillTriangles(ImDrawListPtr drawList, ReadOnlySpan<Vector2> points, ReadOnlySpan<int> triangles,
        uint color)
    {
        for (var index = 0; index + 2 < triangles.Length; index += 3)
        {
            drawList.AddTriangleFilled(points[triangles[index]], points[triangles[index + 1]],
                points[triangles[index + 2]], color);
        }
    }

    public static void FillTriangles(ImDrawListPtr drawList, in Camera2D camera, ReadOnlySpan<Vector2> points,
        ReadOnlySpan<int> triangles, Vector2 offset, uint color)
    {
        for (var index = 0; index + 2 < triangles.Length; index += 3)
        {
            drawList.AddTriangleFilled(camera.ToScreen(points[triangles[index]] + offset),
                camera.ToScreen(points[triangles[index + 1]] + offset),
                camera.ToScreen(points[triangles[index + 2]] + offset), color);
        }
    }

    private static bool Visible(Vector2 radii) => radii.X > MinRadius && radii.Y > MinRadius;
}
