using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class Squircle
{
    private const float Exponent = 4.2f;
    private const float Fullness = 1.10f;
    private const int CornerSegments = 8;
    private const float MinSegmentSquared = 0.01f;
    private static readonly Vector2[] UnitCorner = BuildUnitCorner();
    private static readonly Vector2[] PathScratch = new Vector2[(CornerSegments + 1) * 4];

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

    public static void StrokeCorner(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, int quadrant,
        Vector4 startColor, Vector4 endColor, float thickness)
    {
        var box = MathF.Max(0f, MathF.Min(radius, MathF.Min(max.X - min.X, max.Y - min.Y) * 0.5f));
        if (box <= 0.5f)
        {
            return;
        }

        var corner = UnitCorner;
        var center = quadrant switch
        {
            0 => new Vector2(min.X + box, min.Y + box),
            1 => new Vector2(max.X - box, min.Y + box),
            2 => new Vector2(max.X - box, max.Y - box),
            _ => new Vector2(min.X + box, max.Y - box)
        };
        var signX = quadrant is 1 or 2 ? 1f : -1f;
        var signY = quadrant is 2 or 3 ? 1f : -1f;
        var previous = new Vector2(center.X + signX * corner[0].X * box, center.Y + signY * corner[0].Y * box);
        for (var index = 1; index < corner.Length; index++)
        {
            var point = corner[index];
            var current = new Vector2(center.X + signX * point.X * box, center.Y + signY * point.Y * box);
            var blend = ImGui.GetColorU32(Vector4.Lerp(startColor, endColor, (float)index / (corner.Length - 1)));
            drawList.AddLine(previous, current, blend, thickness);
            previous = current;
        }
    }

    public static void FillOutsideCorners(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, uint color)
    {
        var box = CornerBox(min, max, radius);
        if (box <= 0.5f)
        {
            return;
        }

        FillCorner(drawList, min, new Vector2(min.X + box, min.Y + box), box, -1f, -1f, color);
        FillCorner(drawList, new Vector2(max.X, min.Y), new Vector2(max.X - box, min.Y + box), box, 1f, -1f, color);
        FillCorner(drawList, max, new Vector2(max.X - box, max.Y - box), box, 1f, 1f, color);
        FillCorner(drawList, new Vector2(min.X, max.Y), new Vector2(min.X + box, max.Y - box), box, -1f, 1f, color);
    }

    private static void FillCorner(ImDrawListPtr drawList, Vector2 apex, Vector2 anchor, float box, float signX,
        float signY, uint color)
    {
        var corner = UnitCorner;
        drawList.PathClear();
        drawList.PathLineTo(apex);
        for (var index = 0; index < corner.Length; index++)
        {
            var point = corner[index];
            drawList.PathLineTo(new Vector2(anchor.X + signX * point.X * box, anchor.Y + signY * point.Y * box));
        }

        drawList.PathFillConvex(color);
    }

    public static void FillCap(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, uint color, bool top)
    {
        var box = CornerBox(min, max, radius);
        if (box <= 1.5f)
        {
            drawList.AddRectFilled(min, max, color, MathF.Max(0f, radius),
                top ? ImDrawFlags.RoundCornersTop : ImDrawFlags.RoundCornersBottom);
            return;
        }

        const float overlap = 1.5f;
        if (top)
        {
            var bodyTop = min.Y + box;
            if (max.Y > bodyTop)
            {
                drawList.AddRectFilled(new Vector2(min.X, bodyTop), max, color);
            }
        }
        else
        {
            var bodyBottom = max.Y - box;
            if (bodyBottom > min.Y)
            {
                drawList.AddRectFilled(min, new Vector2(max.X, bodyBottom), color);
            }
        }

        TraceCapPath(drawList, min, max, box, top, overlap);
        drawList.PathFillConvex(color);
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
        var corner = UnitCorner;
        var topLeft = new Vector2(min.X + box, min.Y + box);
        var topRight = new Vector2(max.X - box, min.Y + box);
        var bottomRight = new Vector2(max.X - box, max.Y - box);
        var bottomLeft = new Vector2(min.X + box, max.Y - box);
        var count = 0;
        for (var index = 0; index < corner.Length; index++)
        {
            var point = corner[index];
            AppendDistinct(new Vector2(topLeft.X - point.X * box, topLeft.Y - point.Y * box), ref count);
        }

        for (var index = corner.Length - 1; index >= 0; index--)
        {
            var point = corner[index];
            AppendDistinct(new Vector2(topRight.X + point.X * box, topRight.Y - point.Y * box), ref count);
        }

        for (var index = 0; index < corner.Length; index++)
        {
            var point = corner[index];
            AppendDistinct(new Vector2(bottomRight.X + point.X * box, bottomRight.Y + point.Y * box), ref count);
        }

        for (var index = corner.Length - 1; index >= 0; index--)
        {
            var point = corner[index];
            AppendDistinct(new Vector2(bottomLeft.X - point.X * box, bottomLeft.Y + point.Y * box), ref count);
        }

        while (count > 1 && Vector2.DistanceSquared(PathScratch[count - 1], PathScratch[0]) < MinSegmentSquared)
        {
            count--;
        }

        drawList.PathClear();
        for (var index = 0; index < count; index++)
        {
            drawList.PathLineTo(PathScratch[index]);
        }
    }

    private static void AppendDistinct(Vector2 point, ref int count)
    {
        if (count > 0 && Vector2.DistanceSquared(PathScratch[count - 1], point) < MinSegmentSquared)
        {
            return;
        }

        PathScratch[count] = point;
        count++;
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
