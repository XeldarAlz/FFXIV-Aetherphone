using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class BroadsideIcon
{
    private const float BodyLeft = -0.95f;
    private const float BodyRight = 0.62f;
    private const float BodyTop = 0.02f;
    private const float BodyBottom = 0.56f;
    private const float SightRadius = 0.36f;
    private static readonly float[] Ribs = { -0.5f, -0.12f, 0.26f };

    public static void Draw(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint hole)
    {
        var bodyMin = center + new Vector2(BodyLeft, BodyTop) * extent;
        var bodyMax = center + new Vector2(BodyRight, BodyBottom) * extent;
        var middleY = (BodyTop + BodyBottom) * 0.5f;
        var tail = BodyLeft + 0.14f;
        drawList.AddTriangleFilled(center + new Vector2(tail + 0.2f, BodyTop + 0.08f) * extent,
            center + new Vector2(BodyLeft - 0.08f, BodyTop - 0.34f) * extent,
            center + new Vector2(tail, middleY) * extent, ink);
        drawList.AddTriangleFilled(center + new Vector2(tail + 0.2f, BodyBottom - 0.08f) * extent,
            center + new Vector2(BodyLeft - 0.08f, BodyBottom + 0.34f) * extent,
            center + new Vector2(tail, middleY) * extent, ink);
        drawList.AddRectFilled(bodyMin, bodyMax, ink, (BodyBottom - BodyTop) * 0.5f * extent);
        var thickness = MathF.Max(1f, extent * 0.07f);
        for (var rib = 0; rib < Ribs.Length; rib++)
        {
            var x = Ribs[rib];
            drawList.AddLine(center + new Vector2(x, BodyTop + 0.1f) * extent,
                center + new Vector2(x, BodyBottom - 0.1f) * extent, hole, thickness);
        }

        drawList.AddRectFilled(center + new Vector2(-0.3f, BodyBottom + 0.02f) * extent,
            center + new Vector2(0.14f, BodyBottom + 0.14f) * extent, ink, extent * 0.05f);
        var sight = center + new Vector2(0.56f, -0.42f) * extent;
        var radius = SightRadius * extent;
        drawList.AddCircleFilled(sight, radius * 1.3f, hole, 24);
        drawList.AddCircle(sight, radius, ink, 24, MathF.Max(1f, extent * 0.1f));
        var tick = radius * 0.55f;
        var reach = radius * 1.25f;
        var tickThickness = MathF.Max(1f, extent * 0.09f);
        drawList.AddLine(sight + new Vector2(0f, -reach), sight + new Vector2(0f, -tick), ink, tickThickness);
        drawList.AddLine(sight + new Vector2(0f, reach), sight + new Vector2(0f, tick), ink, tickThickness);
        drawList.AddLine(sight + new Vector2(-reach, 0f), sight + new Vector2(-tick, 0f), ink, tickThickness);
        drawList.AddLine(sight + new Vector2(reach, 0f), sight + new Vector2(tick, 0f), ink, tickThickness);
        drawList.AddCircleFilled(sight, extent * 0.07f, ink, 10);
    }
}
