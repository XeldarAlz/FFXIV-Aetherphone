using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class SiegeIcon
{
    private const int Segments = 16;
    private static readonly float[] LeafAngles = { -0.62f, 0f, 0.62f };
    private static readonly Vector2 BodyOffset = new(0.14f, -0.16f);
    private static readonly Vector2 SeedOffset = new(-0.6f, 0.5f);
    private static readonly Vector2 TrailDirection = Vector2.Normalize(new Vector2(-0.45f, 1f));

    public static void Draw(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint hole)
    {
        var body = center + BodyOffset * extent;
        var radiusX = extent * 0.44f;
        var radiusY = extent * 0.48f;
        var crown = body - new Vector2(0f, radiusY * 0.88f);
        for (var index = 0; index < LeafAngles.Length; index++)
        {
            var angle = LeafAngles[index];
            var direction = new Vector2(MathF.Sin(angle), -MathF.Cos(angle));
            var length = extent * (index == 1 ? 0.3f : 0.24f);
            Ellipse(drawList, crown + direction * length, length, extent * 0.1f, angle - MathF.PI * 0.5f, ink);
        }

        drawList.AddCircleFilled(body + new Vector2(-radiusX * 0.42f, radiusY * 0.98f), extent * 0.1f, ink, 10);
        drawList.AddCircleFilled(body + new Vector2(radiusX * 0.42f, radiusY * 0.98f), extent * 0.1f, ink, 10);
        Ellipse(drawList, body, radiusX, radiusY, 0f, ink);
        drawList.AddCircleFilled(body + new Vector2(-radiusX * 0.38f, -radiusY * 0.12f), extent * 0.075f, hole, 10);
        drawList.AddCircleFilled(body + new Vector2(radiusX * 0.38f, -radiusY * 0.12f), extent * 0.075f, hole, 10);
        Ellipse(drawList, body + new Vector2(0f, radiusY * 0.36f), extent * 0.11f, extent * 0.06f, 0f, hole);
        var seed = center + SeedOffset * extent;
        var tail = seed + TrailDirection * extent * 0.5f;
        drawList.AddTriangleFilled(seed + new Vector2(-TrailDirection.Y, TrailDirection.X) * extent * 0.12f,
            seed - new Vector2(-TrailDirection.Y, TrailDirection.X) * extent * 0.12f, tail, ink);
        drawList.AddCircleFilled(seed, extent * 0.16f, ink, 14);
        drawList.AddCircleFilled(seed - new Vector2(extent * 0.05f, extent * 0.05f), extent * 0.05f, hole, 8);
    }

    private static void Ellipse(ImDrawListPtr drawList, Vector2 center, float radiusX, float radiusY, float angle,
        uint color)
    {
        var cos = MathF.Cos(angle);
        var sin = MathF.Sin(angle);
        for (var index = 0; index < Segments; index++)
        {
            var theta = index * MathF.Tau / Segments;
            var x = MathF.Cos(theta) * radiusX;
            var y = MathF.Sin(theta) * radiusY;
            drawList.PathLineTo(center + new Vector2(x * cos - y * sin, x * sin + y * cos));
        }

        drawList.PathFillConvex(color);
    }
}
