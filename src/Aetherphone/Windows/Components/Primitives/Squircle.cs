using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class Squircle
{
    public const float Exponent = 4.2f;
    private const int MinCornerSegments = 6;
    private const int MaxCornerSegments = 24;
    private const float SegmentError = 0.25f;
    private const float MinSegmentSquared = 0.01f;
    private const float CapOverlap = 1.5f;
    private const float DegenerateBox = 0.5f;
    private const uint AlphaMask = 0xFF000000;
    private static readonly Vector2[][] UnitCorners = BuildUnitCorners();
    private static readonly Vector2[] PathScratch = new Vector2[(MaxCornerSegments + 1) * 4 + 4];
    private static readonly Vector2[] ClipScratch = new Vector2[(MaxCornerSegments + 1) * 4 + 4];
    private static readonly Vector2[] OuterRing = new Vector2[(MaxCornerSegments + 1) * 4];
    private static readonly Vector2[] InnerRing = new Vector2[(MaxCornerSegments + 1) * 4];

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
        uint tint) =>
        FillImage(drawList, min, max, radius, texture, tint, Vector2.Zero, Vector2.One);

    public static void FillImage(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, ImTextureID texture,
        uint tint, Vector2 uv0, Vector2 uv1)
    {
        var box = CornerBox(min, max, radius);
        if (box <= DegenerateBox)
        {
            drawList.AddImage(texture, min, max, uv0, uv1, tint);
            return;
        }

        drawList.AddDrawCmd();
        var firstCommand = drawList.CmdBuffer.Size - 1;
        var firstVertex = drawList.VtxBuffer.Size;
        TracePath(drawList, min, max, box);
        drawList.PathFillConvex(tint);
        MapLinearUv(drawList, firstVertex, min, max, uv0, uv1);
        BindTexture(drawList, firstCommand, texture);
        drawList.AddDrawCmd();
    }

    public static void FillImageMapped(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius,
        ImTextureID texture, in TextureMap map, uint topColor, uint bottomColor)
    {
        if (((topColor | bottomColor) & AlphaMask) == 0)
        {
            return;
        }

        var box = CornerBox(min, max, radius);
        drawList.AddDrawCmd();
        var firstCommand = drawList.CmdBuffer.Size - 1;
        var firstVertex = drawList.VtxBuffer.Size;
        if (box <= DegenerateBox)
        {
            drawList.AddRectFilled(min, max, topColor | AlphaMask);
        }
        else
        {
            TracePath(drawList, min, max, box);
            drawList.PathFillConvex(topColor | AlphaMask);
        }

        var vertices = drawList.VtxBuffer.AsSpan();
        for (var index = firstVertex; index < vertices.Length; index++)
        {
            ref var vertex = ref vertices[index];
            vertex.Uv = map.At(vertex.Pos);
        }

        ShadeVertical(drawList, firstVertex, min.Y, max.Y, topColor, bottomColor);
        BindTexture(drawList, firstCommand, texture);
        drawList.AddDrawCmd();
    }

    public static void FillImageEdge(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, float band,
        ImTextureID texture, uint tint, Vector2 uv0, Vector2 uv1, float refraction)
    {
        var box = CornerBox(min, max, radius);
        var innerMin = new Vector2(min.X + band, min.Y + band);
        var innerMax = new Vector2(max.X - band, max.Y - band);
        if (box <= DegenerateBox || band <= 0f || innerMax.X - innerMin.X <= 1f || innerMax.Y - innerMin.Y <= 1f)
        {
            return;
        }

        var corner = CornerFor(box);
        var count = TraceRing(OuterRing, min, max, box, corner);
        TraceRing(InnerRing, innerMin, innerMax, CornerBox(innerMin, innerMax, MathF.Max(box - band, 0f)), corner);
        var size = max - min;
        var span = uv1 - uv0;
        for (var index = 0; index < count; index++)
        {
            var next = index + 1 == count ? 0 : index + 1;
            var outerA = OuterRing[index];
            var outerB = OuterRing[next];
            var innerA = InnerRing[index];
            var innerB = InnerRing[next];
            var bentA = outerA + (outerA - innerA) * refraction;
            var bentB = outerB + (outerB - innerB) * refraction;
            drawList.AddImageQuad(texture, outerA, outerB, innerB, innerA,
                MapUv(bentA, min, size, uv0, span), MapUv(bentB, min, size, uv0, span),
                MapUv(innerB, min, size, uv0, span), MapUv(innerA, min, size, uv0, span), tint);
        }
    }

    public static void FillEdge(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, float band, uint color)
    {
        var box = CornerBox(min, max, radius);
        var innerMin = new Vector2(min.X + band, min.Y + band);
        var innerMax = new Vector2(max.X - band, max.Y - band);
        if (box <= DegenerateBox || band <= 0f || innerMax.X - innerMin.X <= 1f || innerMax.Y - innerMin.Y <= 1f)
        {
            return;
        }

        var corner = CornerFor(box);
        var count = TraceRing(OuterRing, min, max, box, corner);
        TraceRing(InnerRing, innerMin, innerMax, CornerBox(innerMin, innerMax, MathF.Max(box - band, 0f)), corner);
        for (var index = 0; index < count; index++)
        {
            var next = index + 1 == count ? 0 : index + 1;
            drawList.AddQuadFilled(OuterRing[index], OuterRing[next], InnerRing[next], InnerRing[index], color);
        }
    }

    private static Vector2 MapUv(Vector2 point, Vector2 min, Vector2 size, Vector2 uv0, Vector2 span) =>
        new(uv0.X + (point.X - min.X) / size.X * span.X, uv0.Y + (point.Y - min.Y) / size.Y * span.Y);

    private static int TraceRing(Vector2[] target, Vector2 min, Vector2 max, float box, Vector2[] corner)
    {
        var count = 0;
        WriteCorner(target, new Vector2(min.X + box, min.Y + box), -1f, -1f, box, false, corner, ref count);
        WriteCorner(target, new Vector2(max.X - box, min.Y + box), 1f, -1f, box, true, corner, ref count);
        WriteCorner(target, new Vector2(max.X - box, max.Y - box), 1f, 1f, box, false, corner, ref count);
        WriteCorner(target, new Vector2(min.X + box, max.Y - box), -1f, 1f, box, true, corner, ref count);
        return count;
    }

    private static void WriteCorner(Vector2[] target, Vector2 anchor, float signX, float signY, float box, bool reverse,
        Vector2[] corner, ref int count)
    {
        if (reverse)
        {
            for (var index = corner.Length - 1; index >= 0; index--)
            {
                var point = corner[index];
                target[count] = new Vector2(anchor.X + signX * point.X * box, anchor.Y + signY * point.Y * box);
                count++;
            }

            return;
        }

        for (var index = 0; index < corner.Length; index++)
        {
            var point = corner[index];
            target[count] = new Vector2(anchor.X + signX * point.X * box, anchor.Y + signY * point.Y * box);
            count++;
        }
    }

    public static void StrokeNear(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, uint color,
        float thickness, Vector2 point, float reach)
    {
        if (reach <= 0f)
        {
            return;
        }

        var firstVertex = drawList.VtxBuffer.Size;
        Stroke(drawList, min, max, radius, color, thickness);
        var vertices = drawList.VtxBuffer.AsSpan();
        for (var index = firstVertex; index < vertices.Length; index++)
        {
            ref var vertex = ref vertices[index];
            var distance = Vector2.Distance(vertex.Pos, point);
            var weight = distance >= reach ? 0f : 1f - distance / reach;
            weight *= weight;
            var alpha = (uint)MathF.Round((vertex.Col >> 24) * weight);
            vertex.Col = (vertex.Col & ~AlphaMask) | (alpha << 24);
        }
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

    private static void MapLinearUv(ImDrawListPtr drawList, int firstVertex, Vector2 min, Vector2 max, Vector2 uv0,
        Vector2 uv1)
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
            var amountX = Math.Clamp((vertex.Pos.X - min.X) / width, 0f, 1f);
            var amountY = Math.Clamp((vertex.Pos.Y - min.Y) / height, 0f, 1f);
            vertex.Uv = new Vector2(uv0.X + (uv1.X - uv0.X) * amountX, uv0.Y + (uv1.Y - uv0.Y) * amountY);
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

    public static void StrokeDirectional(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, uint color,
        float thickness, Vector2 direction, float sharpness)
    {
        var length = direction.Length();
        if (length < 0.0001f)
        {
            return;
        }

        var unit = direction / length;
        var center = (min + max) * 0.5f;
        var firstVertex = drawList.VtxBuffer.Size;
        Stroke(drawList, min, max, radius, color, thickness);
        var vertices = drawList.VtxBuffer.AsSpan();
        for (var index = firstVertex; index < vertices.Length; index++)
        {
            ref var vertex = ref vertices[index];
            var offset = vertex.Pos - center;
            var distance = offset.Length();
            var facing = distance < 0.0001f ? 0f : (offset.X * unit.X + offset.Y * unit.Y) / distance;
            var weight = facing <= 0f ? 0f : MathF.Pow(facing, sharpness);
            var alpha = (uint)MathF.Round((vertex.Col >> 24) * weight);
            vertex.Col = (vertex.Col & ~AlphaMask) | (alpha << 24);
        }
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

        var firstVertex = drawList.VtxBuffer.Size;
        TracePath(drawList, min, max, box);
        drawList.PathFillConvex(topColor | AlphaMask);
        ShadeVertical(drawList, firstVertex, min.Y, max.Y, topColor, bottomColor);
    }

    public static void FillHorizontalGradient(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius,
        uint leftColor, uint rightColor)
    {
        var box = CornerBox(min, max, radius);
        if (box <= DegenerateBox)
        {
            drawList.AddRectFilledMultiColor(min, max, leftColor, rightColor, rightColor, leftColor);
            return;
        }

        var firstVertex = drawList.VtxBuffer.Size;
        TracePath(drawList, min, max, box);
        drawList.PathFillConvex(leftColor | AlphaMask);
        Shade(drawList, firstVertex, min.X, max.X, leftColor, rightColor, true);
    }

    public static void FillCircleVerticalGradient(ImDrawListPtr drawList, Vector2 center, float radius,
        uint topColor, uint bottomColor, int segments = 48)
    {
        var firstVertex = drawList.VtxBuffer.Size;
        drawList.PathArcTo(center, radius, 0f, MathF.PI * 2f, segments);
        drawList.PathFillConvex(topColor | AlphaMask);
        ShadeVertical(drawList, firstVertex, center.Y - radius, center.Y + radius, topColor, bottomColor);
    }

    private static void ShadeVertical(ImDrawListPtr drawList, int firstVertex, float top, float bottom,
        uint topColor, uint bottomColor) =>
        Shade(drawList, firstVertex, top, bottom, topColor, bottomColor, false);

    private static void Shade(ImDrawListPtr drawList, int firstVertex, float start, float end, uint startColor,
        uint endColor, bool horizontal)
    {
        var span = MathF.Max(end - start, 1f);
        var vertices = drawList.VtxBuffer.AsSpan();
        for (var index = firstVertex; index < vertices.Length; index++)
        {
            ref var vertex = ref vertices[index];
            var position = horizontal ? vertex.Pos.X : vertex.Pos.Y;
            var amount = Math.Clamp((position - start) / span, 0f, 1f);
            var mixed = MixColor(startColor, endColor, amount);
            vertex.Col = (vertex.Col & AlphaMask) == 0 ? mixed & ~AlphaMask : mixed;
        }
    }

    private static uint MixColor(uint from, uint to, float amount)
    {
        var mixed = 0u;
        for (var shift = 0; shift < 32; shift += 8)
        {
            var start = (from >> shift) & 0xFF;
            var end = (to >> shift) & 0xFF;
            var channel = (uint)MathF.Round(start + (end - (float)start) * amount);
            mixed |= channel << shift;
        }

        return mixed;
    }

    public static float CornerBox(Vector2 min, Vector2 max, float radius)
    {
        var limit = MathF.Min(max.X - min.X, max.Y - min.Y) * 0.5f;
        return MathF.Max(0f, MathF.Min(radius, limit));
    }

    public static float EdgeInset(float box, float depth)
    {
        if (box <= 0f || depth <= 0f)
        {
            return MathF.Max(box, 0f);
        }

        if (depth >= box)
        {
            return 0f;
        }

        var vertical = MathF.Pow(1f - depth / box, Exponent * 0.5f);
        var horizontal = MathF.Pow(MathF.Max(1f - vertical * vertical, 0f), 1f / Exponent);
        return box * (1f - horizontal);
    }

    public static void FillGlint(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, Vector2 normal,
        float center, float halfWidth, uint color)
    {
        if (halfWidth <= 0f)
        {
            return;
        }

        var box = CornerBox(min, max, radius);
        FillRamp(drawList, min, max, box, normal, center - halfWidth, center, color);
        FillRamp(drawList, min, max, box, -normal, -center - halfWidth, -center, color);
    }

    private static void FillRamp(ImDrawListPtr drawList, Vector2 min, Vector2 max, float box, Vector2 normal,
        float start, float end, uint color)
    {
        var count = TraceScratch(min, max, box);
        count = ClipHalfPlane(PathScratch, count, ClipScratch, normal, start);
        count = ClipHalfPlane(ClipScratch, count, PathScratch, -normal, -end);
        if (count < 3)
        {
            return;
        }

        var firstVertex = drawList.VtxBuffer.Size;
        EmitScratch(drawList, count, true);
        drawList.PathFillConvex(color);
        var span = MathF.Max(end - start, 0.0001f);
        var vertices = drawList.VtxBuffer.AsSpan();
        for (var index = firstVertex; index < vertices.Length; index++)
        {
            ref var vertex = ref vertices[index];
            var weight = Math.Clamp((Vector2.Dot(vertex.Pos, normal) - start) / span, 0f, 1f);
            var alpha = (uint)MathF.Round((vertex.Col >> 24) * weight);
            vertex.Col = (vertex.Col & ~AlphaMask) | (alpha << 24);
        }
    }

    private static int ClipHalfPlane(Vector2[] source, int count, Vector2[] target, Vector2 normal, float limit)
    {
        var written = 0;
        for (var index = 0; index < count; index++)
        {
            var current = source[index];
            var next = source[index + 1 == count ? 0 : index + 1];
            var currentDistance = Vector2.Dot(current, normal) - limit;
            var nextDistance = Vector2.Dot(next, normal) - limit;
            if (currentDistance >= 0f)
            {
                target[written] = current;
                written++;
            }

            if (currentDistance >= 0f == nextDistance >= 0f)
            {
                continue;
            }

            target[written] = Vector2.Lerp(current, next, currentDistance / (currentDistance - nextDistance));
            written++;
        }

        return written;
    }

    private static void TracePath(ImDrawListPtr drawList, Vector2 min, Vector2 max, float box) =>
        EmitScratch(drawList, TraceScratch(min, max, box), true);

    private static int TraceScratch(Vector2 min, Vector2 max, float box)
    {
        var count = 0;
        AppendCorner(new Vector2(min.X + box, min.Y + box), -1f, -1f, box, false, ref count);
        AppendCorner(new Vector2(max.X - box, min.Y + box), 1f, -1f, box, true, ref count);
        AppendCorner(new Vector2(max.X - box, max.Y - box), 1f, 1f, box, false, ref count);
        AppendCorner(new Vector2(min.X + box, max.Y - box), -1f, 1f, box, true, ref count);
        return count;
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
