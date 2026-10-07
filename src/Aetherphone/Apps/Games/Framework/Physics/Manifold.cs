using System.Runtime.CompilerServices;

namespace Aetherphone.Apps.Games.Framework.Physics;

internal struct ManifoldPoint
{
    public Vector2 Point;
    public Vector2 Normal;
    public float Separation;
    public uint Id;
}

[InlineArray(Manifold.Capacity)]
internal struct ManifoldPointBuffer
{
    private ManifoldPoint element;
}

internal struct Manifold
{
    public const int Capacity = 4;

    public ManifoldPointBuffer Points;
    public int Count;

    public void Add(Vector2 point, Vector2 normal, float separation, uint id)
    {
        var slot = Count;
        if (Count >= Capacity)
        {
            slot = ShallowestSlot();
            if (Points[slot].Separation <= separation)
            {
                return;
            }
        }
        else
        {
            Count++;
        }

        ref var target = ref Points[slot];
        target.Point = point;
        target.Normal = normal;
        target.Separation = separation;
        target.Id = id;
    }

    public int DeepestSlot()
    {
        var slot = 0;
        for (var index = 1; index < Count; index++)
        {
            if (Points[index].Separation < Points[slot].Separation)
            {
                slot = index;
            }
        }

        return slot;
    }

    private int ShallowestSlot()
    {
        var slot = 0;
        for (var index = 1; index < Count; index++)
        {
            if (Points[index].Separation > Points[slot].Separation)
            {
                slot = index;
            }
        }

        return slot;
    }
}

[InlineArray(ConvexPolygon.Capacity)]
internal struct PolygonBuffer
{
    private Vector2 element;
}

internal struct ConvexPolygon
{
    public const int Capacity = 4;

    public PolygonBuffer Vertices;
    public PolygonBuffer Normals;
    public int Count;

    public static ConvexPolygon Box(Vector2 center, Vector2 rotation, Vector2 halfExtents)
    {
        var polygon = default(ConvexPolygon);
        var axisX = rotation;
        var axisY = new Vector2(-rotation.Y, rotation.X);
        var spanX = axisX * halfExtents.X;
        var spanY = axisY * halfExtents.Y;
        polygon.Count = 4;
        polygon.Vertices[0] = center - spanX - spanY;
        polygon.Vertices[1] = center + spanX - spanY;
        polygon.Vertices[2] = center + spanX + spanY;
        polygon.Vertices[3] = center - spanX + spanY;
        polygon.Normals[0] = -axisY;
        polygon.Normals[1] = axisX;
        polygon.Normals[2] = axisY;
        polygon.Normals[3] = -axisX;
        return polygon;
    }

    public static ConvexPolygon Segment(Vector2 start, Vector2 end)
    {
        var polygon = default(ConvexPolygon);
        var normal = Vector2.Normalize(PhysicsMath.RightPerpendicular(end - start));
        polygon.Count = 2;
        polygon.Vertices[0] = start;
        polygon.Vertices[1] = end;
        polygon.Normals[0] = normal;
        polygon.Normals[1] = -normal;
        return polygon;
    }
}
