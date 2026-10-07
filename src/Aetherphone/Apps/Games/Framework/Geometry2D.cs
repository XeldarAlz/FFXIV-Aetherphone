namespace Aetherphone.Apps.Games.Framework;

internal static class Geometry2D
{
    private const float DegenerateSquared = 1e-6f;
    private const float ParallelCross = 1e-9f;

    public static float Cross(Vector2 left, Vector2 right) => left.X * right.Y - left.Y * right.X;

    public static float ClosestAlong(Vector2 point, Vector2 from, Vector2 to)
    {
        var segment = to - from;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared <= DegenerateSquared)
        {
            return 0f;
        }

        return Math.Clamp(Vector2.Dot(point - from, segment) / lengthSquared, 0f, 1f);
    }

    public static Vector2 ClosestPoint(Vector2 point, Vector2 from, Vector2 to) =>
        from + (to - from) * ClosestAlong(point, from, to);

    public static float SegmentDistance(Vector2 point, Vector2 from, Vector2 to) =>
        Vector2.Distance(point, ClosestPoint(point, from, to));

    public static float ChainDistance(Vector2 point, ReadOnlySpan<Vector2> chain, bool closed)
    {
        var best = float.MaxValue;
        var count = closed ? chain.Length : chain.Length - 1;
        for (var index = 0; index < count; index++)
        {
            best = MathF.Min(best, SegmentDistance(point, chain[index], chain[(index + 1) % chain.Length]));
        }

        return best;
    }

    public static bool SegmentCircle(Vector2 from, Vector2 to, Vector2 center, float radius) =>
        Vector2.DistanceSquared(ClosestPoint(center, from, to), center) <= radius * radius;

    public static bool SweepCircle(Vector2 start, Vector2 travel, Vector2 center, float radius, out float along)
    {
        along = 0f;
        var offset = start - center;
        var outside = offset.LengthSquared() - radius * radius;
        if (outside <= 0f)
        {
            return true;
        }

        var lengthSquared = travel.LengthSquared();
        var approach = Vector2.Dot(offset, travel);
        if (approach >= 0f || lengthSquared <= DegenerateSquared)
        {
            return false;
        }

        var discriminant = approach * approach - lengthSquared * outside;
        if (discriminant < 0f)
        {
            return false;
        }

        along = (-approach - MathF.Sqrt(discriminant)) / lengthSquared;
        return along <= 1f;
    }

    public static bool SegmentSegment(Vector2 startA, Vector2 endA, Vector2 startB, Vector2 endB) =>
        SegmentSegment(startA, endA, startB, endB, out _);

    public static bool SegmentSegment(Vector2 startA, Vector2 endA, Vector2 startB, Vector2 endB, out float alongA)
    {
        alongA = 0f;
        var spanA = endA - startA;
        var spanB = endB - startB;
        var denominator = Cross(spanA, spanB);
        if (MathF.Abs(denominator) < ParallelCross)
        {
            return false;
        }

        var offset = startB - startA;
        var hitA = Cross(offset, spanB) / denominator;
        var hitB = Cross(offset, spanA) / denominator;
        if (hitA < 0f || hitA > 1f || hitB < 0f || hitB > 1f)
        {
            return false;
        }

        alongA = hitA;
        return true;
    }

    public static bool PointInPolygon(Vector2 point, ReadOnlySpan<Vector2> polygon)
    {
        var inside = false;
        var previous = polygon.Length - 1;
        for (var index = 0; index < polygon.Length; index++)
        {
            var current = polygon[index];
            var last = polygon[previous];
            if ((current.Y > point.Y) != (last.Y > point.Y) &&
                point.X < (last.X - current.X) * (point.Y - current.Y) / (last.Y - current.Y) + current.X)
            {
                inside = !inside;
            }

            previous = index;
        }

        return inside;
    }

    public static float SignedArea(ReadOnlySpan<Vector2> polygon)
    {
        var area = 0f;
        for (var index = 0; index < polygon.Length; index++)
        {
            var current = polygon[index];
            var next = polygon[(index + 1) % polygon.Length];
            area += current.X * next.Y - next.X * current.Y;
        }

        return area * 0.5f;
    }

    public static bool InTriangle(Vector2 point, Vector2 first, Vector2 second, Vector2 third) =>
        Cross(second - first, point - first) >= 0f && Cross(third - second, point - second) >= 0f &&
        Cross(first - third, point - third) >= 0f;
}
