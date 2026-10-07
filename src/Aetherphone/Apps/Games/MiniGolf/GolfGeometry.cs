namespace Aetherphone.Apps.Games.MiniGolf;

internal static class GolfGeometry
{
    public static bool InsidePolygon(Vector2 point, ReadOnlySpan<Vector2> polygon)
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

    public static float SegmentDistance(Vector2 point, Vector2 from, Vector2 to)
    {
        var segment = to - from;
        var lengthSquared = segment.LengthSquared();
        var along = lengthSquared <= 0.000001f
            ? 0f
            : Math.Clamp(Vector2.Dot(point - from, segment) / lengthSquared, 0f, 1f);
        return Vector2.Distance(point, from + segment * along);
    }

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

    public static int[] Triangulate(ReadOnlySpan<Vector2> polygon)
    {
        var count = polygon.Length;
        var order = new List<int>(count);
        var clockwise = SignedArea(polygon) < 0f;
        for (var index = 0; index < count; index++)
        {
            order.Add(clockwise ? count - 1 - index : index);
        }

        var triangles = new List<int>((count - 2) * 3);
        var guard = count * count;
        while (order.Count > 3 && guard-- > 0)
        {
            for (var index = 0; index < order.Count; index++)
            {
                var previous = order[(index + order.Count - 1) % order.Count];
                var current = order[index];
                var next = order[(index + 1) % order.Count];
                if (!IsEar(polygon, order, previous, current, next))
                {
                    continue;
                }

                triangles.Add(previous);
                triangles.Add(current);
                triangles.Add(next);
                order.RemoveAt(index);
                break;
            }
        }

        if (order.Count == 3)
        {
            triangles.Add(order[0]);
            triangles.Add(order[1]);
            triangles.Add(order[2]);
        }

        return triangles.ToArray();
    }

    private static float SignedArea(ReadOnlySpan<Vector2> polygon)
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

    private static bool IsEar(ReadOnlySpan<Vector2> polygon, List<int> order, int previous, int current, int next)
    {
        var a = polygon[previous];
        var b = polygon[current];
        var c = polygon[next];
        if (Cross(b - a, c - b) <= 0f)
        {
            return false;
        }

        for (var index = 0; index < order.Count; index++)
        {
            var vertex = order[index];
            if (vertex == previous || vertex == current || vertex == next)
            {
                continue;
            }

            if (InTriangle(polygon[vertex], a, b, c))
            {
                return false;
            }
        }

        return true;
    }

    private static bool InTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c) =>
        Cross(b - a, point - a) >= 0f && Cross(c - b, point - b) >= 0f && Cross(a - c, point - c) >= 0f;

    private static float Cross(Vector2 left, Vector2 right) => left.X * right.Y - left.Y * right.X;
}
