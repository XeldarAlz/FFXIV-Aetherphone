using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Stage;

internal static class NeonTubeMesh
{
    public const int RingCount = 5;
    public const int PaletteSize = RingCount + 1;

    private const int SectionVertices = 1 + RingCount * 2;
    private const int BodyIndices = RingCount * 2 * 6;
    private const int CapSteps = 8;
    private const int MaxFanSteps = 8;
    private const int MinCornerSteps = 2;
    private const float FanStep = MathF.PI / 8f;
    private const float SharpTurn = 0.4f;
    private const float StraightTurn = 0.0005f;
    private const float HotCore = 0.6f;
    private const float TubeEdge = 0.3f;
    private const float Unlimited = float.MaxValue;

    private static readonly float[] RingRadii = { 0.5f, 0.85f, 1.5f, 2.4f, 3.4f };
    private static readonly float[] HaloAlpha = { 0.5f, 0.24f, 0.08f, 0f };
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    public static void Palette(Vector4 color, float lit, Span<uint> palette)
    {
        var level = color.W * Math.Clamp(lit, 0f, 1f);
        palette[0] = ImGui.GetColorU32(Vector4.Lerp(color, White, HotCore) with { W = level });
        palette[1] = ImGui.GetColorU32(Vector4.Lerp(color, White, TubeEdge) with { W = level });
        for (var halo = 0; halo < HaloAlpha.Length; halo++)
        {
            palette[halo + 2] = ImGui.GetColorU32(color with { W = level * HaloAlpha[halo] });
        }
    }

    public static unsafe void Stroke(ImDrawListPtr drawList, ReadOnlySpan<Vector2> points, bool closed, float width,
        ReadOnlySpan<uint> palette)
    {
        var count = points.Length;
        if (count < 2 || width <= 0f)
        {
            return;
        }

        var segmentCount = closed ? count : count - 1;
        Span<Corner> corners = stackalloc Corner[count];
        var sharpCount = 0;
        for (var index = 0; index < count; index++)
        {
            if (!closed && (index == 0 || index == count - 1))
            {
                continue;
            }

            corners[index] = new Corner(points[(index + count - 1) % count], points[index],
                points[(index + 1) % count]);
            if (corners[index].Sharp)
            {
                sharpCount++;
            }
        }

        var fanCount = sharpCount + (closed ? 0 : 2);
        var vertexBudget = (1 + segmentCount + sharpCount) * SectionVertices + fanCount * FanVertices(MaxFanSteps);
        var indexBudget = segmentCount * BodyIndices + fanCount * FanIndices(MaxFanSteps);
        Span<float> radii = stackalloc float[RingCount];
        for (var ring = 0; ring < RingCount; ring++)
        {
            radii[ring] = RingRadii[ring] * width;
        }

        drawList.PushTextureID(ImGui.GetFontTexIdWhitePixel());
        drawList.PrimReserve(indexBudget, vertexBudget);
        var mesh = new TubeWriter(drawList, palette, radii);
        if (closed)
        {
            TraceClosed(ref mesh, points, corners);
        }
        else
        {
            TraceOpen(ref mesh, points, corners);
        }

        mesh.Commit(drawList, indexBudget, vertexBudget);
        drawList.PopTextureID();
    }

    private static void TraceOpen(ref TubeWriter mesh, ReadOnlySpan<Vector2> points, ReadOnlySpan<Corner> corners)
    {
        var last = points.Length - 1;
        var firstNormal = Normal(points[0], points[1]);
        var lastNormal = Normal(points[last - 1], points[last]);
        mesh.Fan(points[0], firstNormal, -firstNormal, MathF.PI, CapSteps);
        var section = mesh.Section(points[0], Spoke.Straight(firstNormal), Spoke.Straight(-firstNormal));
        for (var index = 1; index < last; index++)
        {
            section = Arrive(ref mesh, section, points[index], corners[index]);
        }

        var end = mesh.Section(points[last], Spoke.Straight(lastNormal), Spoke.Straight(-lastNormal));
        mesh.Body(section, end);
        mesh.Fan(points[last], lastNormal, -lastNormal, -MathF.PI, CapSteps);
    }

    private static void TraceClosed(ref TubeWriter mesh, ReadOnlySpan<Vector2> points, ReadOnlySpan<Corner> corners)
    {
        var opening = corners[0];
        var first = mesh.Section(points[0], opening.Side(1f, opening.Outgoing), opening.Side(-1f, opening.Outgoing));
        var section = first;
        for (var index = 1; index < points.Length; index++)
        {
            section = Arrive(ref mesh, section, points[index], corners[index]);
        }

        if (!opening.Sharp)
        {
            mesh.Body(section, first);
            return;
        }

        var arriving = mesh.Section(points[0], opening.Side(1f, opening.Incoming), opening.Side(-1f, opening.Incoming));
        mesh.Body(section, arriving);
        mesh.Bend(points[0], opening);
    }

    private static int Arrive(ref TubeWriter mesh, int section, Vector2 point, in Corner corner)
    {
        if (!corner.Sharp)
        {
            var shared = mesh.Section(point, corner.Side(1f, corner.Incoming), corner.Side(-1f, corner.Incoming));
            mesh.Body(section, shared);
            return shared;
        }

        var arriving = mesh.Section(point, corner.Side(1f, corner.Incoming), corner.Side(-1f, corner.Incoming));
        mesh.Body(section, arriving);
        mesh.Bend(point, corner);
        return mesh.Section(point, corner.Side(1f, corner.Outgoing), corner.Side(-1f, corner.Outgoing));
    }

    private static int FanVertices(int steps) => 1 + (steps + 1) * RingCount;

    private static int FanIndices(int steps) => steps * (3 + (RingCount - 1) * 6);

    private static Vector2 Direction(Vector2 from, Vector2 to)
    {
        var delta = to - from;
        var length = delta.Length();
        return length > 0f ? delta / length : Vector2.UnitX;
    }

    private static Vector2 Normal(Vector2 from, Vector2 to)
    {
        var direction = Direction(from, to);
        return new Vector2(-direction.Y, direction.X);
    }

    private static Vector2 Rotate(Vector2 vector, float angle)
    {
        var cosine = MathF.Cos(angle);
        var sine = MathF.Sin(angle);
        return new Vector2(vector.X * cosine - vector.Y * sine, vector.X * sine + vector.Y * cosine);
    }

    private readonly struct Spoke(Vector2 direction, float scale, float limit)
    {
        public readonly Vector2 Direction = direction;
        public readonly float Scale = scale;
        public readonly float Limit = limit;

        public static Spoke Straight(Vector2 direction) => new(direction, 1f, Unlimited);

        public Vector2 Place(Vector2 point, float radius) => point + Direction * MathF.Min(radius * Scale, Limit);
    }

    private readonly struct Corner
    {
        public readonly Vector2 Incoming;
        public readonly Vector2 Outgoing;
        public readonly Vector2 Bisector;
        public readonly float Miter;
        public readonly float InnerLimit;
        public readonly float InnerSide;
        public readonly float Turn;
        public readonly bool Sharp;

        public Corner(Vector2 previous, Vector2 point, Vector2 next)
        {
            var arriving = Direction(previous, point);
            var leaving = Direction(point, next);
            Incoming = new Vector2(-arriving.Y, arriving.X);
            Outgoing = new Vector2(-leaving.Y, leaving.X);
            var cosine = Math.Clamp(Vector2.Dot(arriving, leaving), -1f, 1f);
            Turn = MathF.Acos(cosine);
            var sum = Incoming + Outgoing;
            var sumLength = sum.Length();
            Bisector = sumLength > 0f ? sum / sumLength : Incoming;
            Miter = sumLength > 0f ? 2f / sumLength : 1f;
            var cross = arriving.X * leaving.Y - arriving.Y * leaving.X;
            InnerSide = Turn < StraightTurn ? 0f : cross > 0f ? 1f : -1f;
            var halfSine = MathF.Sqrt((1f - cosine) * 0.5f);
            var reach = 0.5f * MathF.Min(Vector2.Distance(previous, point), Vector2.Distance(point, next));
            InnerLimit = halfSine > 0f ? reach / halfSine : Unlimited;
            Sharp = Turn > SharpTurn;
        }

        public Spoke Side(float side, Vector2 outerNormal)
        {
            if (side == InnerSide)
            {
                return new Spoke(Bisector * side, Miter, InnerLimit);
            }

            return Sharp ? Spoke.Straight(outerNormal * side) : new Spoke(Bisector * side, Miter, Unlimited);
        }
    }

    private unsafe ref struct TubeWriter
    {
        private readonly ImDrawVert* vertices;
        private readonly ushort* indices;
        private readonly uint baseIndex;
        private readonly Vector2 whitePixel;
        private readonly ReadOnlySpan<uint> palette;
        private readonly ReadOnlySpan<float> radii;
        private int vertexCount;
        private int indexCount;

        public TubeWriter(ImDrawListPtr drawList, ReadOnlySpan<uint> palette, ReadOnlySpan<float> radii)
        {
            vertices = drawList.VtxWritePtr.Handle;
            indices = drawList.IdxWritePtr;
            baseIndex = drawList.VtxCurrentIdx;
            whitePixel = ImGui.GetFontTexUvWhitePixel();
            this.palette = palette;
            this.radii = radii;
        }

        public int Section(Vector2 point, in Spoke plus, in Spoke minus)
        {
            var start = vertexCount;
            Vertex(point, palette[0]);
            for (var ring = 0; ring < RingCount; ring++)
            {
                Vertex(plus.Place(point, radii[ring]), palette[ring + 1]);
            }

            for (var ring = 0; ring < RingCount; ring++)
            {
                Vertex(minus.Place(point, radii[ring]), palette[ring + 1]);
            }

            return start;
        }

        public void Body(int from, int to)
        {
            for (var side = 0; side < 2; side++)
            {
                var offset = 1 + side * RingCount;
                Quad(from, to, to + offset, from + offset);
                for (var ring = 0; ring < RingCount - 1; ring++)
                {
                    Quad(from + offset + ring, to + offset + ring, to + offset + ring + 1, from + offset + ring + 1);
                }
            }
        }

        public void Bend(Vector2 point, in Corner corner)
        {
            var outerSide = -corner.InnerSide;
            var steps = Math.Clamp((int)MathF.Ceiling(corner.Turn / FanStep), MinCornerSteps, MaxFanSteps);
            Fan(point, corner.Incoming * outerSide, corner.Outgoing * outerSide, corner.Turn * corner.InnerSide, steps);
        }

        public void Fan(Vector2 point, Vector2 from, Vector2 to, float angle, int steps)
        {
            var center = vertexCount;
            Vertex(point, palette[0]);
            for (var step = 0; step <= steps; step++)
            {
                var direction = step == 0 ? from : step == steps ? to : Rotate(from, angle * step / steps);
                var spoke = Spoke.Straight(direction);
                for (var ring = 0; ring < RingCount; ring++)
                {
                    Vertex(spoke.Place(point, radii[ring]), palette[ring + 1]);
                }
            }

            for (var step = 0; step < steps; step++)
            {
                var current = center + 1 + step * RingCount;
                var next = current + RingCount;
                Triangle(center, current, next);
                for (var ring = 0; ring < RingCount - 1; ring++)
                {
                    Quad(current + ring, next + ring, next + ring + 1, current + ring + 1);
                }
            }
        }

        public void Commit(ImDrawListPtr drawList, int indexBudget, int vertexBudget)
        {
            drawList.VtxWritePtr = new ImDrawVertPtr(vertices + vertexCount);
            drawList.IdxWritePtr = indices + indexCount;
            drawList.VtxCurrentIdx += (uint)vertexCount;
            drawList.PrimUnreserve(indexBudget - indexCount, vertexBudget - vertexCount);
        }

        private void Vertex(Vector2 position, uint color)
        {
            var vertex = vertices + vertexCount;
            vertex->Pos = position;
            vertex->Uv = whitePixel;
            vertex->Col = color;
            vertexCount++;
        }

        private void Triangle(int first, int second, int third)
        {
            Index(first);
            Index(second);
            Index(third);
        }

        private void Quad(int first, int second, int third, int fourth)
        {
            Triangle(first, second, third);
            Triangle(first, third, fourth);
        }

        private void Index(int local)
        {
            indices[indexCount] = (ushort)(baseIndex + (uint)local);
            indexCount++;
        }
    }
}
