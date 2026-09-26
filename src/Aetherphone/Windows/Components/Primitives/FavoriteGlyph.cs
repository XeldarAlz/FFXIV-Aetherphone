using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class FavoriteGlyph
{
    public static readonly Vector4 Fill = new(1f, 0.78f, 0.25f, 1f);

    public static void Star(ImDrawListPtr drawList, Vector2 center, float radius, bool filled, Vector4 fill,
        Vector4 outline, float scale)
    {
        Span<Vector2> points = stackalloc Vector2[10];
        var innerRadius = radius * 0.44f;
        for (var index = 0; index < 10; index++)
        {
            var pointRadius = (index & 1) == 0 ? radius : innerRadius;
            var angle = -MathF.PI / 2f + index * (MathF.PI / 5f);
            points[index] = new Vector2(center.X + MathF.Cos(angle) * pointRadius,
                center.Y + MathF.Sin(angle) * pointRadius);
        }

        if (filled)
        {
            var packed = ImGui.GetColorU32(fill);
            for (var index = 0; index < 10; index++)
            {
                drawList.AddTriangleFilled(center, points[index], points[(index + 1) % 10], packed);
            }

            return;
        }

        var line = ImGui.GetColorU32(outline);
        var thickness = Metrics.Stroke.Thin * scale;
        for (var index = 0; index < 10; index++)
        {
            drawList.AddLine(points[index], points[(index + 1) % 10], line, thickness);
        }
    }
}
