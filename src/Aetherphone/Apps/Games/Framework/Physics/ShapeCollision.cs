using System.Runtime.CompilerServices;

namespace Aetherphone.Apps.Games.Framework.Physics;

internal static class ShapeCollision
{
    private const int VertexFeature = 0;
    private const int FaceFeature = 1;
    private const int SegmentFeatureShift = 8;
    private const uint StartVertexRegion = 1u;
    private const uint EndVertexRegion = 2u;
    private const float ReferenceTolerance = 0.1f * PhysicsWorld.LinearSlop;
    private const float DegenerateDistance = 1e-6f;

    private struct ClipVertex
    {
        public Vector2 Point;
        public uint Id;
    }

    [InlineArray(2)]
    private struct ClipBuffer
    {
        private ClipVertex element;
    }

    public static void CircleCircle(Vector2 centerA, float radiusA, Vector2 centerB, float radiusB, float margin,
        ref Manifold manifold)
    {
        var delta = centerB - centerA;
        var reach = radiusA + radiusB + margin;
        var distanceSquared = delta.LengthSquared();
        if (distanceSquared > reach * reach)
        {
            return;
        }

        var distance = MathF.Sqrt(distanceSquared);
        var normal = distance > DegenerateDistance ? delta / distance : Vector2.UnitY;
        var separation = distance - radiusA - radiusB;
        manifold.Add(centerA + normal * (radiusA + separation * 0.5f), normal, separation, 0u);
    }

    public static void BoxCircle(Vector2 boxCenter, Vector2 boxRotation, Vector2 halfExtents, Vector2 circleCenter,
        float radius, float margin, ref Manifold manifold)
    {
        var local = PhysicsMath.InverseRotate(boxRotation, circleCenter - boxCenter);
        var closest = Vector2.Clamp(local, -halfExtents, halfExtents);
        Vector2 localNormal;
        Vector2 surface;
        float separation;
        if (closest == local)
        {
            var gapX = halfExtents.X - MathF.Abs(local.X);
            var gapY = halfExtents.Y - MathF.Abs(local.Y);
            if (gapX < gapY)
            {
                var sign = local.X >= 0f ? 1f : -1f;
                localNormal = new Vector2(sign, 0f);
                surface = new Vector2(sign * halfExtents.X, local.Y);
                separation = -gapX - radius;
            }
            else
            {
                var sign = local.Y >= 0f ? 1f : -1f;
                localNormal = new Vector2(0f, sign);
                surface = new Vector2(local.X, sign * halfExtents.Y);
                separation = -gapY - radius;
            }
        }
        else
        {
            var offset = local - closest;
            var distance = offset.Length();
            separation = distance - radius;
            if (separation > margin)
            {
                return;
            }

            localNormal = offset / distance;
            surface = closest;
        }

        var normal = PhysicsMath.Rotate(boxRotation, localNormal);
        var surfacePoint = boxCenter + PhysicsMath.Rotate(boxRotation, surface);
        manifold.Add(surfacePoint + normal * (separation * 0.5f), normal, separation, 0u);
    }

    public static void BoxBox(Vector2 centerA, Vector2 rotationA, Vector2 halfExtentsA, Vector2 centerB,
        Vector2 rotationB, Vector2 halfExtentsB, float margin, ref Manifold manifold)
    {
        var boxA = ConvexPolygon.Box(centerA, rotationA, halfExtentsA);
        var boxB = ConvexPolygon.Box(centerB, rotationB, halfExtentsB);
        Polygons(boxA, boxB, margin, 0u, ref manifold);
    }

    public static void ChainCircle(ReadOnlySpan<Vector2> points, bool closed, Vector2 center, float radius,
        float margin, ref Manifold manifold)
    {
        var segmentCount = closed ? points.Length : points.Length - 1;
        var reach = radius + margin;
        for (var segment = 0; segment < segmentCount; segment++)
        {
            var start = points[segment];
            var end = points[segment + 1 < points.Length ? segment + 1 : 0];
            if (OutsideReach(start, end, center, reach))
            {
                continue;
            }

            var span = end - start;
            var fraction = Vector2.Dot(center - start, span) / span.LengthSquared();
            var region = 0u;
            if (fraction <= 0f)
            {
                if (closed || segment > 0)
                {
                    continue;
                }

                region = StartVertexRegion;
                fraction = 0f;
            }
            else if (fraction >= 1f)
            {
                if ((closed || segment < segmentCount - 1) && InsideNextFace(points, segment, center))
                {
                    continue;
                }

                region = EndVertexRegion;
                fraction = 1f;
            }

            var closest = start + span * fraction;
            var offset = center - closest;
            var distanceSquared = offset.LengthSquared();
            if (distanceSquared > reach * reach)
            {
                continue;
            }

            var distance = MathF.Sqrt(distanceSquared);
            var normal = distance > DegenerateDistance
                ? offset / distance
                : Vector2.Normalize(PhysicsMath.RightPerpendicular(span));
            var separation = distance - radius;
            manifold.Add(closest + normal * (separation * 0.5f), normal, separation,
                ((uint)segment << SegmentFeatureShift) | region);
        }
    }

    public static void ChainBox(ReadOnlySpan<Vector2> points, bool closed, Vector2 boxCenter, Vector2 boxRotation,
        Vector2 halfExtents, float margin, ref Manifold manifold)
    {
        var box = ConvexPolygon.Box(boxCenter, boxRotation, halfExtents);
        var reach = halfExtents.Length() + margin;
        var segmentCount = closed ? points.Length : points.Length - 1;
        for (var segment = 0; segment < segmentCount; segment++)
        {
            var start = points[segment];
            var end = points[segment + 1 < points.Length ? segment + 1 : 0];
            if (OutsideReach(start, end, boxCenter, reach))
            {
                continue;
            }

            var edge = ConvexPolygon.Segment(start, end);
            Polygons(edge, box, margin, (uint)segment << SegmentFeatureShift, ref manifold);
        }
    }

    private static bool InsideNextFace(ReadOnlySpan<Vector2> points, int segment, Vector2 center)
    {
        var nextStart = points[segment + 1 < points.Length ? segment + 1 : 0];
        var nextEnd = points[(segment + 2) % points.Length];
        return Vector2.Dot(center - nextStart, nextEnd - nextStart) > 0f;
    }

    private static bool OutsideReach(Vector2 start, Vector2 end, Vector2 center, float reach) =>
        center.X + reach < MathF.Min(start.X, end.X) || center.X - reach > MathF.Max(start.X, end.X) ||
        center.Y + reach < MathF.Min(start.Y, end.Y) || center.Y - reach > MathF.Max(start.Y, end.Y);

    private static void Polygons(in ConvexPolygon polygonA, in ConvexPolygon polygonB, float margin,
        uint featureBase, ref Manifold manifold)
    {
        var separationA = MaxSeparation(polygonA, polygonB, out var edgeA);
        if (separationA > margin)
        {
            return;
        }

        var separationB = MaxSeparation(polygonB, polygonA, out var edgeB);
        if (separationB > margin)
        {
            return;
        }

        if (separationB > separationA + ReferenceTolerance)
        {
            ClipAgainst(polygonB, edgeB, polygonA, margin, true, featureBase, ref manifold);
            return;
        }

        ClipAgainst(polygonA, edgeA, polygonB, margin, false, featureBase, ref manifold);
    }

    private static float MaxSeparation(in ConvexPolygon reference, in ConvexPolygon other, out int edge)
    {
        var best = float.MinValue;
        edge = 0;
        for (var normalIndex = 0; normalIndex < reference.Count; normalIndex++)
        {
            var normal = reference.Normals[normalIndex];
            var vertex = reference.Vertices[normalIndex];
            var deepest = float.MaxValue;
            for (var vertexIndex = 0; vertexIndex < other.Count; vertexIndex++)
            {
                deepest = MathF.Min(deepest, Vector2.Dot(normal, other.Vertices[vertexIndex] - vertex));
            }

            if (deepest > best)
            {
                best = deepest;
                edge = normalIndex;
            }
        }

        return best;
    }

    private static void ClipAgainst(in ConvexPolygon reference, int edge, in ConvexPolygon incident, float margin,
        bool flip, uint featureBase, ref Manifold manifold)
    {
        var incidentEdge = IncidentEdge(reference, edge, incident);
        var next = edge + 1 < reference.Count ? edge + 1 : 0;
        var start = reference.Vertices[edge];
        var end = reference.Vertices[next];
        var normal = reference.Normals[edge];
        var tangent = new Vector2(-normal.Y, normal.X);
        var frontOffset = Vector2.Dot(normal, start);
        var sideOffsetStart = -Vector2.Dot(tangent, start);
        var sideOffsetEnd = Vector2.Dot(tangent, end);
        var firstClip = default(ClipBuffer);
        if (Clip(ref firstClip, incidentEdge, -tangent, sideOffsetStart, edge) < 2)
        {
            return;
        }

        var secondClip = default(ClipBuffer);
        if (Clip(ref secondClip, firstClip, tangent, sideOffsetEnd, next) < 2)
        {
            return;
        }

        var manifoldNormal = flip ? -normal : normal;
        for (var index = 0; index < 2; index++)
        {
            var clipPoint = secondClip[index].Point;
            var separation = Vector2.Dot(normal, clipPoint) - frontOffset;
            if (separation > margin)
            {
                continue;
            }

            var id = flip ? FlipFeature(secondClip[index].Id) : secondClip[index].Id;
            manifold.Add(clipPoint - normal * (separation * 0.5f), manifoldNormal, separation, featureBase | id);
        }
    }

    private static ClipBuffer IncidentEdge(in ConvexPolygon reference, int referenceEdge, in ConvexPolygon incident)
    {
        var normal = reference.Normals[referenceEdge];
        var edge = 0;
        var minimum = float.MaxValue;
        for (var index = 0; index < incident.Count; index++)
        {
            var alignment = Vector2.Dot(normal, incident.Normals[index]);
            if (alignment < minimum)
            {
                minimum = alignment;
                edge = index;
            }
        }

        var next = edge + 1 < incident.Count ? edge + 1 : 0;
        var clip = default(ClipBuffer);
        clip[0].Point = incident.Vertices[edge];
        clip[0].Id = Feature(referenceEdge, edge, FaceFeature, VertexFeature);
        clip[1].Point = incident.Vertices[next];
        clip[1].Id = Feature(referenceEdge, next, FaceFeature, VertexFeature);
        return clip;
    }

    private static int Clip(ref ClipBuffer output, in ClipBuffer input, Vector2 normal, float offset,
        int vertexIndex)
    {
        var count = 0;
        var distanceStart = Vector2.Dot(normal, input[0].Point) - offset;
        var distanceEnd = Vector2.Dot(normal, input[1].Point) - offset;
        if (distanceStart <= 0f)
        {
            output[count] = input[0];
            count++;
        }

        if (distanceEnd <= 0f)
        {
            output[count] = input[1];
            count++;
        }

        if (distanceStart * distanceEnd >= 0f)
        {
            return count;
        }

        var interpolation = distanceStart / (distanceStart - distanceEnd);
        output[count].Point = input[0].Point + interpolation * (input[1].Point - input[0].Point);
        output[count].Id = Feature(vertexIndex, (int)((input[0].Id >> 2) & 3u), VertexFeature, FaceFeature);
        return count + 1;
    }

    private static uint Feature(int indexA, int indexB, int typeA, int typeB) =>
        (uint)(indexA | (indexB << 2) | (typeA << 4) | (typeB << 5));

    private static uint FlipFeature(uint id) =>
        Feature((int)((id >> 2) & 3u), (int)(id & 3u), (int)((id >> 5) & 1u), (int)((id >> 4) & 1u));
}
