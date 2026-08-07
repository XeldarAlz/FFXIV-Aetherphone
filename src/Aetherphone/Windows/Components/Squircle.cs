using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class Squircle
{
    private const float Exponent = 4.2f;
    private const int MinCornerSegments = 6;
    private const int MaxCornerSegments = 24;
    private const float SegmentError = 0.25f;
    private const float MinSegmentSquared = 0.01f;
    private const float CapOverlap = 1.5f;
    private const float DegenerateBox = 0.5f;
    private static readonly Vector2[][] UnitCorners = BuildUnitCorners();
    private static readonly Vector2[] PathScratch = new Vector2[(MaxCornerSegments + 1) * 4 + 4];

    public static void Fill(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, uint color)
    {
        var box = CornerBox(min, max, radius);
        if (box <= DegenerateBox)
        {
            drawList.AddRectFilled(min, max, color);
            return;
        }

        TracePath(drawList, min, max, box);
        drawList.PathFillConvex(color);
    }

    public static void FillImage(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, ImTextureID texture,
        uint tint)
    {
        var box = CornerBox(min, max, radius);
        if (box <= DegenerateBox)
        {
            drawList.AddImage(texture, min, max, Vector2.Zero, Vector2.One, tint);
            return;
        }

        drawList.AddDrawCmd();
        var firstCommand = drawList.CmdBuffer.Size - 1;
        var firstVertex = drawList.VtxBuffer.Size;
        TracePath(drawList, min, max, box);
        drawList.PathFillConvex(tint);
        MapLinearUv(drawList, firstVertex, min, max);
        BindTexture(drawList, firstCommand, texture);
        drawList.AddDrawCmd();
    }

    private static void BindTexture(ImDrawListPtr drawList, int firstCommand, ImTextureID texture)
    {
        var commands = drawList.CmdBuffer;
        for (var index = firstCommand; index < commands.Size; index++)
        {
            ref var command = ref commands.Ref(index);
            command.TextureId = texture;
        }
    }

    private static void MapLinearUv(ImDrawListPtr drawList, int firstVertex, Vector2 min, Vector2 max)
    {
        var width = max.X - min.X;
        var height = max.Y - min.Y;
        if (width <= 0f || height <= 0f)
        {
            return;
        }

        var vertices = drawList.VtxBuffer.AsSpan();
        for (var index = firstVertex; index < vertices.Length; index++)
        {
            ref var vertex = ref vertices[index];
            vertex.Uv = new Vector2(Math.Clamp((vertex.Pos.X - min.X) / width, 0f, 1f),
                Math.Clamp((vertex.Pos.Y - min.Y) / height, 0f, 1f));
        }
    }

    public static void Stroke(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, uint color,
        float thickness)
    {
        var box = CornerBox(min, max, radius);
        if (box <= DegenerateBox)
        {
            drawList.AddRect(min, max, color, 0f, ImDrawFlags.None, thickness);
            return;
        }

        TracePath(drawList, min, max, box);
        drawList.PathStroke(color, ImDrawFlags.Closed, thickness);
    }

    public static void StrokeCorner(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, int quadrant,
        Vector4 startColor, Vector4 endColor, float thickness)
    {
        var box = CornerBox(min, max, radius);
        if (box <= DegenerateBox)
        {
            return;
        }

        var corner = CornerFor(box);
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

    public static void FillOutsideCorners(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, uint color,
        float grow)
    {
        var limit = MathF.Min(max.X - min.X, max.Y - min.Y) * 0.5f;
        var box = MathF.Min(CornerBox(min, max, radius) + MathF.Max(grow, 0f), MathF.Max(limit, 0f));
        if (box <= DegenerateBox)
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
        var corner = CornerFor(box);
        drawList.PathClear();
        drawList.PathLineTo(apex);
        for (var index = 0; index < corner.Length; index++)
        {
            var point = corner[index];
            drawList.PathLineTo(new Vector2(anchor.X + signX * point.X * box, anchor.Y + signY * point.Y * box));
        }

        var flags = drawList.Flags;
        drawList.Flags = flags & ~ImDrawListFlags.AntiAliasedFill;
        drawList.PathFillConvex(color);
        drawList.Flags = flags;
    }

    public static void FillCap(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, uint color, bool top)
    {
        var box = CornerBox(min, max, radius);
        if (box <= DegenerateBox)
        {
            drawList.AddRectFilled(min, max, color);
            return;
        }

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

        TraceCapPath(drawList, min, max, box, top);
        drawList.PathFillConvex(color);
    }

    public static void FillSideCap(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, uint color,
        bool left)
    {
        var box = CornerBox(min, max, radius);
        if (box <= DegenerateBox)
        {
            drawList.AddRectFilled(min, max, color);
            return;
        }

        if (left)
        {
            var bodyLeft = min.X + box;
            if (max.X > bodyLeft)
            {
                drawList.AddRectFilled(new Vector2(bodyLeft, min.Y), max, color);
            }
        }
        else
        {
            var bodyRight = max.X - box;
            if (bodyRight > min.X)
            {
                drawList.AddRectFilled(min, new Vector2(bodyRight, max.Y), color);
            }
        }

        TraceSideCapPath(drawList, min, max, box, left);
        drawList.PathFillConvex(color);
    }

    public static void FillVerticalGradient(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius,
        uint topColor, uint bottomColor)
    {
        var box = CornerBox(min, max, radius);
        if (box <= DegenerateBox)
        {
            drawList.AddRectFilledMultiColor(min, max, topColor, topColor, bottomColor, bottomColor);
            return;
        }

        var capTop = min.Y + box;
        var capBottom = max.Y - box;
        var left = min.X - CapOverlap;
        var right = max.X + CapOverlap;

        drawList.PushClipRect(new Vector2(left, min.Y - CapOverlap), new Vector2(right, capTop), true);
        TraceCapPath(drawList, min, max, box, true);
        drawList.PathFillConvex(topColor);
        drawList.PopClipRect();

        if (capBottom > capTop)
        {
            drawList.PushClipRect(new Vector2(left, capTop), new Vector2(right, capBottom), true);
            drawList.AddRectFilledMultiColor(new Vector2(min.X, capTop - CapOverlap),
                new Vector2(max.X, capBottom + CapOverlap), topColor, topColor, bottomColor, bottomColor);
            drawList.PopClipRect();
        }

        drawList.PushClipRect(new Vector2(left, capBottom), new Vector2(right, max.Y + CapOverlap), true);
        TraceCapPath(drawList, min, max, box, false);
        drawList.PathFillConvex(bottomColor);
        drawList.PopClipRect();
    }

    private static float CornerBox(Vector2 min, Vector2 max, float radius)
    {
        var limit = MathF.Min(max.X - min.X, max.Y - min.Y) * 0.5f;
        return MathF.Max(0f, MathF.Min(radius, limit));
    }

    private static void TracePath(ImDrawListPtr drawList, Vector2 min, Vector2 max, float box)
    {
        var count = 0;
        AppendCorner(new Vector2(min.X + box, min.Y + box), -1f, -1f, box, false, ref count);
        AppendCorner(new Vector2(max.X - box, min.Y + box), 1f, -1f, box, true, ref count);
        AppendCorner(new Vector2(max.X - box, max.Y - box), 1f, 1f, box, false, ref count);
        AppendCorner(new Vector2(min.X + box, max.Y - box), -1f, 1f, box, true, ref count);
        EmitScratch(drawList, count, true);
    }

    private static void TraceCapPath(ImDrawListPtr drawList, Vector2 min, Vector2 max, float box, bool top)
    {
        var count = 0;
        if (top)
        {
            AppendDistinct(new Vector2(min.X, min.Y + box + CapOverlap), ref count);
            AppendCorner(new Vector2(min.X + box, min.Y + box), -1f, -1f, box, false, ref count);
            AppendCorner(new Vector2(max.X - box, min.Y + box), 1f, -1f, box, true, ref count);
            AppendDistinct(new Vector2(max.X, min.Y + box + CapOverlap), ref count);
            EmitScratch(drawList, count, false);
            return;
        }

        AppendDistinct(new Vector2(max.X, max.Y - box - CapOverlap), ref count);
        AppendCorner(new Vector2(max.X - box, max.Y - box), 1f, 1f, box, false, ref count);
        AppendCorner(new Vector2(min.X + box, max.Y - box), -1f, 1f, box, true, ref count);
        AppendDistinct(new Vector2(min.X, max.Y - box - CapOverlap), ref count);
        EmitScratch(drawList, count, false);
    }

    private static void TraceSideCapPath(ImDrawListPtr drawList, Vector2 min, Vector2 max, float box, bool left)
    {
        var count = 0;
        if (left)
        {
            AppendDistinct(new Vector2(min.X + box + CapOverlap, min.Y), ref count);
            AppendCorner(new Vector2(min.X + box, min.Y + box), -1f, -1f, box, true, ref count);
            AppendCorner(new Vector2(min.X + box, max.Y - box), -1f, 1f, box, false, ref count);
            AppendDistinct(new Vector2(min.X + box + CapOverlap, max.Y), ref count);
            EmitScratch(drawList, count, false);
            return;
        }

        AppendDistinct(new Vector2(max.X - box - CapOverlap, max.Y), ref count);
        AppendCorner(new Vector2(max.X - box, max.Y - box), 1f, 1f, box, true, ref count);
        AppendCorner(new Vector2(max.X - box, min.Y + box), 1f, -1f, box, false, ref count);
        AppendDistinct(new Vector2(max.X - box - CapOverlap, min.Y), ref count);
        EmitScratch(drawList, count, false);
    }

    private static void AppendCorner(Vector2 anchor, float signX, float signY, float box, bool reverse, ref int count)
    {
        var corner = CornerFor(box);
        if (reverse)
        {
            for (var index = corner.Length - 1; index >= 0; index--)
            {
                var point = corner[index];
                AppendDistinct(new Vector2(anchor.X + signX * point.X * box, anchor.Y + signY * point.Y * box),
                    ref count);
            }

            return;
        }

        for (var index = 0; index < corner.Length; index++)
        {
            var point = corner[index];
            AppendDistinct(new Vector2(anchor.X + signX * point.X * box, anchor.Y + signY * point.Y * box), ref count);
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

    private static void EmitScratch(ImDrawListPtr drawList, int count, bool closed)
    {
        while (closed && count > 1 && Vector2.DistanceSquared(PathScratch[count - 1], PathScratch[0]) < MinSegmentSquared)
        {
            count--;
        }

        drawList.PathClear();
        for (var index = 0; index < count; index++)
        {
            drawList.PathLineTo(PathScratch[index]);
        }
    }

    private static Vector2[] CornerFor(float box) => UnitCorners[SegmentsFor(box) - MinCornerSegments];

    private static int SegmentsFor(float box)
    {
        if (box <= 1f)
        {
            return MinCornerSegments;
        }

        var cosine = MathF.Max(1f - SegmentError / box, -1f);
        var count = (int)MathF.Ceiling(MathF.PI * 0.5f / MathF.Acos(cosine));
        return Math.Clamp(count, MinCornerSegments, MaxCornerSegments);
    }

    private static Vector2[][] BuildUnitCorners()
    {
        var table = new Vector2[MaxCornerSegments - MinCornerSegments + 1][];
        for (var segments = MinCornerSegments; segments <= MaxCornerSegments; segments++)
        {
            table[segments - MinCornerSegments] = BuildUnitCorner(segments);
        }

        return table;
    }

    private static Vector2[] BuildUnitCorner(int segments)
    {
        var points = new Vector2[segments + 1];
        var power = 2f / Exponent;
        for (var index = 0; index <= segments; index++)
        {
            var angle = MathF.PI * 0.5f * index / segments;
            var cosine = MathF.Max(MathF.Cos(angle), 0f);
            var sine = MathF.Max(MathF.Sin(angle), 0f);
            points[index] = new Vector2(MathF.Pow(cosine, power), MathF.Pow(sine, power));
        }

        return points;
    }
}
