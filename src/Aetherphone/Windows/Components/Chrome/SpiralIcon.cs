using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class SpiralIcon
{
    private const int RingCount = 3;
    private const int Steps = 24;
    private const float OuterX = 0.95f;
    private const float InnerX = 0.3f;
    private const float Squash = 0.3f;
    private const float RimDepth = 0.12f;
    private const float GapArc = 1.1f;
    private static readonly float[] RingHeights = { -0.32f, 0.2f, 0.72f };
    private static readonly float[] GapCenters = { 2.1f, 0.9f, 1.6f };

    public static void Draw(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint hole)
    {
        var poleHalf = extent * 0.13f;
        drawList.AddRectFilled(center + new Vector2(-poleHalf, -extent * 0.62f), center + new Vector2(poleHalf, extent),
            ink, poleHalf * 0.4f);
        for (var ring = RingCount - 1; ring >= 0; ring--)
        {
            DrawRing(drawList, center + new Vector2(0f, RingHeights[ring] * extent), extent, GapCenters[ring], ink, hole);
        }

        var ball = center + new Vector2(0.06f, -0.74f) * extent;
        var radius = extent * 0.2f;
        drawList.AddCircleFilled(ball, radius, ink, 18);
        drawList.AddCircleFilled(ball - new Vector2(radius * 0.32f, radius * 0.34f), radius * 0.3f, hole, 10);
    }

    private static void DrawRing(ImDrawListPtr drawList, Vector2 center, float extent, float gapCenter, uint ink,
        uint hole)
    {
        var gapStart = gapCenter - GapArc * 0.5f;
        var gapEnd = gapCenter + GapArc * 0.5f;
        var rim = new Vector2(0f, RimDepth * extent);
        for (var step = 0; step < Steps; step++)
        {
            var from = step * MathF.Tau / Steps;
            var to = (step + 1) * MathF.Tau / Steps;
            var middle = (from + to) * 0.5f;
            if (middle > gapStart && middle < gapEnd)
            {
                continue;
            }

            var outerFrom = Point(center, extent, OuterX, from);
            var outerTo = Point(center, extent, OuterX, to);
            if (MathF.Sin(middle) > 0f)
            {
                drawList.AddQuadFilled(outerFrom, outerTo, outerTo + rim, outerFrom + rim, ink);
            }

            drawList.AddQuadFilled(Point(center, extent, InnerX, from), outerFrom, outerTo, Point(center, extent, InnerX, to),
                ink);
            if (MathF.Sin(middle) > 0f)
            {
                drawList.AddLine(outerFrom, outerTo, hole, MathF.Max(1f, extent * 0.05f));
            }
        }
    }

    private static Vector2 Point(Vector2 center, float extent, float radius, float angle) =>
        center + new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius * Squash) * extent;
}
