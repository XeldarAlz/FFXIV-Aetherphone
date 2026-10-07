namespace Aetherphone.Apps.Games.Framework;

internal static class Polygon
{
    public static int TriangleIndexCount(int vertexCount) => vertexCount < 3 ? 0 : (vertexCount - 2) * 3;

    public static int[] Triangulate(ReadOnlySpan<Vector2> polygon)
    {
        var order = new int[polygon.Length];
        var triangles = new int[TriangleIndexCount(polygon.Length)];
        var written = Triangulate(polygon, order, triangles);
        return written == triangles.Length ? triangles : triangles.AsSpan(0, written).ToArray();
    }

    public static int Triangulate(ReadOnlySpan<Vector2> polygon, Span<int> order, Span<int> triangles)
    {
        var count = polygon.Length;
        if (count < 3 || order.Length < count || triangles.Length < TriangleIndexCount(count))
        {
            return 0;
        }

        var clockwise = Geometry2D.SignedArea(polygon) < 0f;
        for (var index = 0; index < count; index++)
        {
            order[index] = clockwise ? count - 1 - index : index;
        }

        var remaining = count;
        var written = 0;
        var guard = count * count;
        while (remaining > 3 && guard-- > 0)
        {
            for (var index = 0; index < remaining; index++)
            {
                var previous = order[(index + remaining - 1) % remaining];
                var current = order[index];
                var next = order[(index + 1) % remaining];
                if (!IsEar(polygon, order[..remaining], previous, current, next))
                {
                    continue;
                }

                triangles[written++] = previous;
                triangles[written++] = current;
                triangles[written++] = next;
                order.Slice(index + 1, remaining - index - 1).CopyTo(order[index..]);
                remaining--;
                break;
            }
        }

        if (remaining != 3)
        {
            return written;
        }

        triangles[written++] = order[0];
        triangles[written++] = order[1];
        triangles[written++] = order[2];
        return written;
    }

    private static bool IsEar(ReadOnlySpan<Vector2> polygon, ReadOnlySpan<int> order, int previous, int current,
        int next)
    {
        var first = polygon[previous];
        var second = polygon[current];
        var third = polygon[next];
        if (Geometry2D.Cross(second - first, third - second) <= 0f)
        {
            return false;
        }

        for (var index = 0; index < order.Length; index++)
        {
            var vertex = order[index];
            if (vertex == previous || vertex == current || vertex == next)
            {
                continue;
            }

            if (Geometry2D.InTriangle(polygon[vertex], first, second, third))
            {
                return false;
            }
        }

        return true;
    }
}
