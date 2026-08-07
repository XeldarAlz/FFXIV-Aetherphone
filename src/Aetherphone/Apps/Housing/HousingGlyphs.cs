using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Housing;

internal static class HousingGlyphs
{
    public static void Estate(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 ink, Vector4 hole)
    {
        var packedInk = ImGui.GetColorU32(ink);
        var packedHole = ImGui.GetColorU32(hole);
        Span<Vector2> roof = stackalloc Vector2[3]
        {
            new(center.X, center.Y - radius),
            new(center.X + radius, center.Y - radius * 0.08f),
            new(center.X - radius, center.Y - radius * 0.08f),
        };
        Fill(drawList, packedInk, roof);
        var bodyMin = new Vector2(center.X - radius * 0.70f, center.Y - radius * 0.12f);
        var bodyMax = new Vector2(center.X + radius * 0.70f, center.Y + radius * 0.86f);
        drawList.AddRectFilled(bodyMin, bodyMax, packedInk, radius * 0.16f);
        var doorMin = new Vector2(center.X - radius * 0.22f, center.Y + radius * 0.24f);
        var doorMax = new Vector2(center.X + radius * 0.22f, center.Y + radius * 0.86f);
        drawList.AddRectFilled(doorMin, doorMax, packedHole, radius * 0.10f);
    }

    public static void Circle(ImDrawListPtr drawList, Vector2 center, float radius, uint fill) =>
        drawList.AddCircleFilled(center, radius, fill, 28);

    public static void Diamond(ImDrawListPtr drawList, Vector2 center, float radius, uint fill)
    {
        Span<Vector2> points = stackalloc Vector2[4]
        {
            new(center.X, center.Y - radius),
            new(center.X + radius, center.Y),
            new(center.X, center.Y + radius),
            new(center.X - radius, center.Y),
        };
        Fill(drawList, fill, points);
    }

    public static void Hexagon(ImDrawListPtr drawList, Vector2 center, float radius, uint fill)
    {
        Span<Vector2> points = stackalloc Vector2[6];
        FillHexagonPoints(points, center, radius);
        Fill(drawList, fill, points);
    }

    public static void DashedRing(ImDrawListPtr drawList, Vector2 center, float radius, uint color, float thickness,
        int dashes = 9)
    {
        var step = MathF.Tau / dashes;
        for (var index = 0; index < dashes; index++)
        {
            var from = index * step;
            drawList.PathClear();
            drawList.PathArcTo(center, radius, from, from + step * 0.55f, 6);
            drawList.PathStroke(color, ImDrawFlags.None, thickness);
        }
    }

    public static void WatchNotch(ImDrawListPtr drawList, Vector2 center, float radius, uint color)
    {
        var tip = new Vector2(center.X + radius * 1.25f, center.Y - radius * 1.25f);
        Span<Vector2> points = stackalloc Vector2[3]
        {
            new(tip.X - radius * 0.72f, tip.Y),
            new(tip.X, tip.Y),
            new(tip.X, tip.Y + radius * 0.72f),
        };
        Fill(drawList, color, points);
    }

    public static void RefreshArrow(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 color,
        float thickness, float rotation)
    {
        var packed = ImGui.GetColorU32(color);
        const float sweep = MathF.Tau * 0.70f;
        var start = rotation - MathF.PI * 0.30f;
        var end = start + sweep;
        drawList.PathClear();
        drawList.PathArcTo(center, radius, start, end, 24);
        drawList.PathStroke(packed, ImDrawFlags.None, thickness);
        var outward = new Vector2(MathF.Cos(end), MathF.Sin(end));
        var tangent = new Vector2(-outward.Y, outward.X);
        var tip = center + outward * radius;
        var head = radius * 0.66f;
        Span<Vector2> arrow = stackalloc Vector2[3]
        {
            tip + tangent * head * 0.85f,
            tip + outward * head * 0.52f - tangent * head * 0.16f,
            tip - outward * head * 0.52f - tangent * head * 0.16f,
        };
        Fill(drawList, packed, arrow);
    }

    public static void Slash(ImDrawListPtr drawList, Vector2 center, float radius, uint color, float thickness)
    {
        drawList.AddLine(new Vector2(center.X - radius, center.Y + radius),
            new Vector2(center.X + radius, center.Y - radius), color, thickness);
    }

    private static void FillHexagonPoints(Span<Vector2> points, Vector2 center, float radius)
    {
        for (var index = 0; index < 6; index++)
        {
            var angle = -MathF.PI / 2f + index * (MathF.Tau / 6f);
            points[index] = new Vector2(center.X + MathF.Cos(angle) * radius, center.Y + MathF.Sin(angle) * radius);
        }
    }

    private static void Fill(ImDrawListPtr drawList, uint color, ReadOnlySpan<Vector2> points)
    {
        drawList.PathClear();
        for (var index = 0; index < points.Length; index++)
        {
            drawList.PathLineTo(points[index]);
        }

        drawList.PathFillConvex(color);
    }
}
