using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class Squircle
{
    private const float Exponent = 4.2f;
    private const float Fullness = 1.10f;
    private const int CornerSegments = 8;
    private static readonly Vector2[] UnitCorner = BuildUnitCorner();

    public static void Fill(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, uint color)
    {
        var box = CornerBox(min, max, radius);
        if (box <= 1.5f)
        {
            drawList.AddRectFilled(min, max, color, MathF.Max(0f, radius), ImDrawFlags.RoundCornersAll);
            return;
        }

        TracePath(drawList, min, max, box);
        drawList.PathFillConvex(color);
    }

    public static void Stroke(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, uint color,
        float thickness)
    {
        var box = CornerBox(min, max, radius);
        if (box <= 1.5f)
        {
            drawList.AddRect(min, max, color, MathF.Max(0f, radius), ImDrawFlags.RoundCornersAll, thickness);
            return;
        }

        TracePath(drawList, min, max, box);
        drawList.PathStroke(color, ImDrawFlags.Closed, thickness);
    }

    public static void FillVerticalGradient(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius,
        uint topColor, uint bottomColor)
    {
        var box = CornerBox(min, max, radius);
        if (box <= 1.5f)
        {
            drawList.AddRectFilledMultiColor(min, max, topColor, topColor, bottomColor, bottomColor);
            return;
        }

        var capTop = min.Y + box;
        var capBottom = max.Y - box;
        if (capBottom > capTop)
        {
            drawList.AddRectFilledMultiColor(new Vector2(min.X, capTop), new Vector2(max.X, capBottom), topColor,
                topColor, bottomColor, bottomColor);
        }

        const float overlap = 1.5f;
        TraceCapPath(drawList, min, max, box, true, overlap);
        drawList.PathFillConvex(topColor);
        TraceCapPath(drawList, min, max, box, false, overlap);
        drawList.PathFillConvex(bottomColor);
    }

    private static float CornerBox(Vector2 min, Vector2 max, float radius)
    {
        var limit = MathF.Min(max.X - min.X, max.Y - min.Y) * 0.5f;
        return MathF.Max(0f, MathF.Min(radius * Fullness, limit));
    }

    private static void TracePath(ImDrawListPtr drawList, Vector2 min, Vector2 max, float box)
    {
        drawList.PathClear();
        var corner = UnitCorner;
        var topLeft = new Vector2(min.X + box, min.Y + box);
        var topRight = new Vector2(max.X - box, min.Y + box);
        var bottomRight = new Vector2(max.X - box, max.Y - box);
        var bottomLeft = new Vector2(min.X + box, max.Y - box);
        for (var index = 0; index < corner.Length; index++)
        {
            var point = corner[index];
            drawList.PathLineTo(new Vector2(topLeft.X - point.X * box, topLeft.Y - point.Y * box));
        }

        for (var index = corner.Length - 1; index >= 0; index--)
        {
            var point = corner[index];
            drawList.PathLineTo(new Vector2(topRight.X + point.X * box, topRight.Y - point.Y * box));
        }

        for (var index = 0; index < corner.Length; index++)
        {
            var point = corner[index];
            drawList.PathLineTo(new Vector2(bottomRight.X + point.X * box, bottomRight.Y + point.Y * box));
        }

        for (var index = corner.Length - 1; index >= 0; index--)
        {
            var point = corner[index];
            drawList.PathLineTo(new Vector2(bottomLeft.X - point.X * box, bottomLeft.Y + point.Y * box));
        }
    }

    private static void TraceCapPath(ImDrawListPtr drawList, Vector2 min, Vector2 max, float box, bool top,
        float overlap)
    {
        drawList.PathClear();
        var corner = UnitCorner;
        if (top)
        {
            var topLeft = new Vector2(min.X + box, min.Y + box);
            var topRight = new Vector2(max.X - box, min.Y + box);
            drawList.PathLineTo(new Vector2(min.X, min.Y + box + overlap));
            for (var index = 0; index < corner.Length; index++)
            {
                var point = corner[index];
                drawList.PathLineTo(new Vector2(topLeft.X - point.X * box, topLeft.Y - point.Y * box));
            }

            for (var index = corner.Length - 1; index >= 0; index--)
            {
                var point = corner[index];
                drawList.PathLineTo(new Vector2(topRight.X + point.X * box, topRight.Y - point.Y * box));
            }

            drawList.PathLineTo(new Vector2(max.X, min.Y + box + overlap));
            return;
        }

        var bottomRight = new Vector2(max.X - box, max.Y - box);
        var bottomLeft = new Vector2(min.X + box, max.Y - box);
        drawList.PathLineTo(new Vector2(max.X, max.Y - box - overlap));
        for (var index = 0; index < corner.Length; index++)
        {
            var point = corner[index];
            drawList.PathLineTo(new Vector2(bottomRight.X + point.X * box, bottomRight.Y + point.Y * box));
        }

        for (var index = corner.Length - 1; index >= 0; index--)
        {
            var point = corner[index];
            drawList.PathLineTo(new Vector2(bottomLeft.X - point.X * box, bottomLeft.Y + point.Y * box));
        }

        drawList.PathLineTo(new Vector2(min.X, max.Y - box - overlap));
    }

    private static Vector2[] BuildUnitCorner()
    {
        var points = new Vector2[CornerSegments + 1];
        var power = 2f / Exponent;
        for (var index = 0; index <= CornerSegments; index++)
        {
            var angle = MathF.PI * 0.5f * index / CornerSegments;
            var cosine = MathF.Max(MathF.Cos(angle), 0f);
            var sine = MathF.Max(MathF.Sin(angle), 0f);
            points[index] = new Vector2(MathF.Pow(cosine, power), MathF.Pow(sine, power));
        }

        return points;
    }
}
