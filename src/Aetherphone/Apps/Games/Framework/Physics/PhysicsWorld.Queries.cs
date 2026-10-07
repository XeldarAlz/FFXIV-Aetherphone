namespace Aetherphone.Apps.Games.Framework.Physics;

internal sealed partial class PhysicsWorld
{
    public bool Raycast(Vector2 origin, Vector2 direction, float maxDistance, out RaycastHit hit,
        ushort mask = AllCategories)
    {
        hit = default;
        var lengthSquared = direction.LengthSquared();
        if (lengthSquared <= 0f || maxDistance <= 0f)
        {
            return false;
        }

        var unit = direction / MathF.Sqrt(lengthSquared);
        var end = origin + unit * maxDistance;
        var rayMin = Vector2.Min(origin, end);
        var rayMax = Vector2.Max(origin, end);
        var best = maxDistance;
        var found = false;
        for (var body = 1; body < nextBody; body++)
        {
            if (!IsQueryable(body, mask) || !OverlapsBounds(body, rayMin, rayMax))
            {
                continue;
            }

            if (!RaycastBody(body, origin, unit, best, out var distance, out var normal))
            {
                continue;
            }

            best = distance;
            found = true;
            hit = new RaycastHit(body, origin + unit * distance, normal, distance);
        }

        return found;
    }

    public int OverlapCircle(Vector2 center, float radius, Span<int> results, ushort mask = AllCategories)
    {
        var count = 0;
        var reach = new Vector2(radius);
        for (var body = 1; body < nextBody && count < results.Length; body++)
        {
            if (!IsQueryable(body, mask) || !OverlapsBounds(body, center - reach, center + reach))
            {
                continue;
            }

            var manifold = default(Manifold);
            CollideWithCircle(body, positions[body], rotations[body], center, radius, 0f, ref manifold);
            if (manifold.Count == 0 || manifold.Points[manifold.DeepestSlot()].Separation >= 0f)
            {
                continue;
            }

            results[count] = body;
            count++;
        }

        return count;
    }

    public void PredictPath(Vector2 start, Vector2 velocity, float gravityScale, float linearDamping,
        int stepsPerPoint, Span<Vector2> path)
    {
        var position = start;
        var damping = 1f / (1f + StepSeconds * MathF.Max(0f, linearDamping));
        for (var pointIndex = 0; pointIndex < path.Length; pointIndex++)
        {
            for (var step = 0; step < stepsPerPoint; step++)
            {
                velocity = (velocity + StepSeconds * (gravity * gravityScale)) * damping;
                position += StepSeconds * velocity;
            }

            path[pointIndex] = position;
        }
    }

    private bool IsQueryable(int body, ushort mask) =>
        alive[body] && shapes[body] != ShapeKind.None && (flags[body] & BodyFlags.Sensor) == 0 &&
        (categories[body] & mask) != 0;

    private bool OverlapsBounds(int body, Vector2 minimum, Vector2 maximum) =>
        boundsMin[body].X <= maximum.X && boundsMax[body].X >= minimum.X &&
        boundsMin[body].Y <= maximum.Y && boundsMax[body].Y >= minimum.Y;

    private bool RaycastBody(int body, Vector2 origin, Vector2 direction, float maxDistance, out float distance,
        out Vector2 normal)
    {
        switch (shapes[body])
        {
            case ShapeKind.Circle:
                return ShapeRaycast.Circle(positions[body], radii[body], origin, direction, maxDistance, out distance,
                    out normal);
            case ShapeKind.Box:
                return ShapeRaycast.Box(positions[body], rotations[body], halfExtents[body], origin, direction,
                    maxDistance, out distance, out normal);
            default:
                return ShapeRaycast.Chain(ChainPoints(body), chainClosed[body], origin, direction, maxDistance,
                    out distance, out normal);
        }
    }
}
