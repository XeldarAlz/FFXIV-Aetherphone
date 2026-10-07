namespace Aetherphone.Apps.Games.Framework.Physics;

internal sealed partial class PhysicsWorld
{
    public PhysicsDebug Debug => new(this);

    public readonly struct PhysicsDebug
    {
        public const int CircleSegments = 24;

        private readonly PhysicsWorld world;

        public PhysicsDebug(PhysicsWorld world)
        {
            this.world = world;
        }

        public int ContactCount => world.pointCount;

        public int Outline(int body, Span<Vector2> outline)
        {
            if (body == Ground || !world.IsAlive(body))
            {
                return 0;
            }

            switch (world.shapes[body])
            {
                case ShapeKind.Circle:
                    return CircleOutline(body, outline);
                case ShapeKind.Box:
                {
                    var box = ConvexPolygon.Box(world.positions[body], world.rotations[body], world.halfExtents[body]);
                    var count = Math.Min(box.Count, outline.Length);
                    for (var index = 0; index < count; index++)
                    {
                        outline[index] = box.Vertices[index];
                    }

                    return count;
                }
                default:
                {
                    var chain = world.ChainPoints(body);
                    var count = Math.Min(chain.Length, outline.Length);
                    chain[..count].CopyTo(outline);
                    return count;
                }
            }
        }

        public bool IsClosed(int body) =>
            world.shapes[body] is ShapeKind.Circle or ShapeKind.Box || world.chainClosed[body];

        public Vector2 ContactPosition(int index) => world.points[index].Point;

        public Vector2 ContactNormal(int index) => world.points[index].Normal;

        public void JointAnchors(int joint, out Vector2 anchorA, out Vector2 anchorB)
        {
            ref readonly var record = ref world.joints[joint];
            anchorA = world.WorldPoint(record.BodyA, record.LocalAnchorA);
            anchorB = world.WorldPoint(record.BodyB, record.LocalAnchorB);
        }

        private int CircleOutline(int body, Span<Vector2> outline)
        {
            var center = world.positions[body];
            var radius = world.radii[body];
            var angle = world.angles[body];
            var count = Math.Min(CircleSegments, outline.Length);
            for (var index = 0; index < count; index++)
            {
                var turn = angle + index * (MathF.PI * 2f / CircleSegments);
                outline[index] = center + new Vector2(MathF.Cos(turn), MathF.Sin(turn)) * radius;
            }

            return count;
        }
    }
}
