namespace Aetherphone.Apps.Games.Framework.Physics;

internal static class ShapeRaycast
{
    private const float ParallelEpsilon = 1e-9f;

    public static bool Circle(Vector2 center, float radius, Vector2 origin, Vector2 direction, float maxDistance,
        out float distance, out Vector2 normal)
    {
        distance = 0f;
        normal = Vector2.Zero;
        var offset = origin - center;
        var along = Vector2.Dot(offset, direction);
        var outside = offset.LengthSquared() - radius * radius;
        if (outside <= 0f || along > 0f)
        {
            return false;
        }

        var discriminant = along * along - outside;
        if (discriminant < 0f)
        {
            return false;
        }

        var hitDistance = -along - MathF.Sqrt(discriminant);
        if (hitDistance < 0f || hitDistance > maxDistance)
        {
            return false;
        }

        distance = hitDistance;
        normal = (offset + direction * hitDistance) / radius;
        return true;
    }

    public static bool Box(Vector2 center, Vector2 rotation, Vector2 halfExtents, Vector2 origin, Vector2 direction,
        float maxDistance, out float distance, out Vector2 normal)
    {
        distance = 0f;
        normal = Vector2.Zero;
        var localOrigin = PhysicsMath.InverseRotate(rotation, origin - center);
        var localDirection = PhysicsMath.InverseRotate(rotation, direction);
        var entry = float.MinValue;
        var exit = float.MaxValue;
        var entryNormal = Vector2.Zero;
        if (!Slab(localOrigin.X, localDirection.X, halfExtents.X, Vector2.UnitX, ref entry, ref exit, ref entryNormal) ||
            !Slab(localOrigin.Y, localDirection.Y, halfExtents.Y, Vector2.UnitY, ref entry, ref exit, ref entryNormal))
        {
            return false;
        }

        if (entry < 0f || entry > maxDistance)
        {
            return false;
        }

        distance = entry;
        normal = PhysicsMath.Rotate(rotation, entryNormal);
        return true;
    }

    public static bool Chain(ReadOnlySpan<Vector2> points, bool closed, Vector2 origin, Vector2 direction,
        float maxDistance, out float distance, out Vector2 normal)
    {
        distance = maxDistance;
        normal = Vector2.Zero;
        var found = false;
        var segmentCount = closed ? points.Length : points.Length - 1;
        for (var segment = 0; segment < segmentCount; segment++)
        {
            var start = points[segment];
            var span = points[segment + 1 < points.Length ? segment + 1 : 0] - start;
            var denominator = PhysicsMath.Cross(direction, span);
            if (MathF.Abs(denominator) < ParallelEpsilon)
            {
                continue;
            }

            var offset = start - origin;
            var along = PhysicsMath.Cross(offset, span) / denominator;
            var fraction = PhysicsMath.Cross(offset, direction) / denominator;
            if (along < 0f || along > distance || fraction < 0f || fraction > 1f)
            {
                continue;
            }

            var faceNormal = Vector2.Normalize(PhysicsMath.RightPerpendicular(span));
            distance = along;
            normal = Vector2.Dot(faceNormal, direction) > 0f ? -faceNormal : faceNormal;
            found = true;
        }

        return found;
    }

    private static bool Slab(float origin, float direction, float half, Vector2 axis, ref float entry, ref float exit,
        ref Vector2 entryNormal)
    {
        if (MathF.Abs(direction) < ParallelEpsilon)
        {
            return origin >= -half && origin <= half;
        }

        var inverse = 1f / direction;
        var near = (-half - origin) * inverse;
        var far = (half - origin) * inverse;
        var nearNormal = -axis;
        if (near > far)
        {
            (near, far) = (far, near);
            nearNormal = axis;
        }

        if (near > entry)
        {
            entry = near;
            entryNormal = nearNormal;
        }

        exit = MathF.Min(exit, far);
        return entry <= exit;
    }
}
