using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class LuckyDrawIcon
{
    private const float CardWidth = 0.7f;
    private const float CardHeight = 1f;
    private const float FanRadius = 0.95f;
    private const float PivotDrop = 0.9f;
    private const float Outline = 0.07f;
    private const float Stroke = 0.12f;
    private static readonly float[] Angles = { -0.46f, 0.46f, 0f };

    public static void Draw(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint hole)
    {
        var pivot = center + new Vector2(0f, extent * PivotDrop);
        for (var card = 0; card < Angles.Length; card++)
        {
            var angle = Angles[card];
            var cardCenter = pivot + Turn(new Vector2(0f, -extent * FanRadius), angle);
            DrawCard(drawList, cardCenter, angle, extent, ink, hole);
        }

        var front = pivot + new Vector2(0f, -extent * FanRadius);
        var thickness = MathF.Max(1f, extent * Stroke);
        var barLeft = front + new Vector2(-extent * 0.17f, -extent * 0.26f);
        var barRight = front + new Vector2(extent * 0.19f, -extent * 0.26f);
        var foot = front + new Vector2(-extent * 0.04f, extent * 0.3f);
        drawList.AddLine(barLeft, barRight, hole, thickness);
        drawList.AddLine(barRight, foot, hole, thickness);
        DrawSparkle(drawList, center + new Vector2(extent * 0.86f, -extent * 0.78f), extent * 0.26f, ink);
        DrawSparkle(drawList, center + new Vector2(-extent * 0.88f, -extent * 0.5f), extent * 0.14f, ink);
    }

    private static void DrawCard(ImDrawListPtr drawList, Vector2 cardCenter, float angle, float extent, uint ink,
        uint hole)
    {
        var halfWidth = CardWidth * extent * 0.5f;
        var halfHeight = CardHeight * extent * 0.5f;
        var topLeft = cardCenter + Turn(new Vector2(-halfWidth, -halfHeight), angle);
        var topRight = cardCenter + Turn(new Vector2(halfWidth, -halfHeight), angle);
        var bottomRight = cardCenter + Turn(new Vector2(halfWidth, halfHeight), angle);
        var bottomLeft = cardCenter + Turn(new Vector2(-halfWidth, halfHeight), angle);
        drawList.AddQuadFilled(topLeft, topRight, bottomRight, bottomLeft, ink);
        drawList.AddQuad(topLeft, topRight, bottomRight, bottomLeft, hole, MathF.Max(1f, extent * Outline));
    }

    private static void DrawSparkle(ImDrawListPtr drawList, Vector2 sparkle, float radius, uint ink)
    {
        var waist = radius * 0.28f;
        drawList.AddQuadFilled(sparkle + new Vector2(0f, -radius), sparkle + new Vector2(waist, 0f),
            sparkle + new Vector2(0f, radius), sparkle + new Vector2(-waist, 0f), ink);
        drawList.AddQuadFilled(sparkle + new Vector2(-radius, 0f), sparkle + new Vector2(0f, -waist),
            sparkle + new Vector2(radius, 0f), sparkle + new Vector2(0f, waist), ink);
    }

    private static Vector2 Turn(Vector2 offset, float angle)
    {
        var sine = MathF.Sin(angle);
        var cosine = MathF.Cos(angle);
        return new Vector2(offset.X * cosine - offset.Y * sine, offset.X * sine + offset.Y * cosine);
    }
}
