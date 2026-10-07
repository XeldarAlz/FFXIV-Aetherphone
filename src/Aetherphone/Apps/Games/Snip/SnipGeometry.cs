namespace Aetherphone.Apps.Games.Snip;

internal static class SnipGeometry
{
    public static Vector2 ClosestPoint(Vector2 point, Vector2 from, Vector2 to)
    {
        var segment = to - from;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.000001f)
        {
            return from;
        }

        var along = Math.Clamp(Vector2.Dot(point - from, segment) / lengthSquared, 0f, 1f);
        return from + segment * along;
    }

    public static float SegmentDistance(Vector2 point, Vector2 from, Vector2 to) =>
        Vector2.Distance(point, ClosestPoint(point, from, to));

    public static bool SegmentHitsCircle(Vector2 from, Vector2 to, Vector2 center, float radius) =>
        Vector2.DistanceSquared(ClosestPoint(center, from, to), center) <= radius * radius;
}
