namespace Aetherphone.Apps.Games.Framework.Physics;

internal enum BodyType : byte
{
    Static,
    Dynamic,
    Kinematic,
}

internal enum ShapeKind : byte
{
    None,
    Circle,
    Box,
    Segment,
    Polyline,
}

[Flags]
internal enum BodyFlags : byte
{
    None = 0,
    Sensor = 1,
    Bullet = 2,
    FixedRotation = 4,
}

internal enum ContactEventKind : byte
{
    Hit,
    SensorEnter,
    SensorExit,
}

internal readonly struct PhysicsMaterial
{
    public static readonly PhysicsMaterial Default = new(1f, 0f, 0.6f);

    public readonly float Density;
    public readonly float Restitution;
    public readonly float Friction;

    public PhysicsMaterial(float density, float restitution, float friction)
    {
        Density = density;
        Restitution = restitution;
        Friction = friction;
    }
}

internal readonly struct ContactEvent
{
    public readonly int BodyA;
    public readonly int BodyB;
    public readonly Vector2 Point;
    public readonly Vector2 Normal;
    public readonly float Impulse;
    public readonly ContactEventKind Kind;

    public ContactEvent(int bodyA, int bodyB, Vector2 point, Vector2 normal, float impulse, ContactEventKind kind)
    {
        BodyA = bodyA;
        BodyB = bodyB;
        Point = point;
        Normal = normal;
        Impulse = impulse;
        Kind = kind;
    }

    public bool Involves(int body) => BodyA == body || BodyB == body;

    public int Other(int body) => BodyA == body ? BodyB : BodyA;
}

internal readonly struct RaycastHit
{
    public readonly int Body;
    public readonly Vector2 Point;
    public readonly Vector2 Normal;
    public readonly float Distance;

    public RaycastHit(int body, Vector2 point, Vector2 normal, float distance)
    {
        Body = body;
        Point = point;
        Normal = normal;
        Distance = distance;
    }
}
