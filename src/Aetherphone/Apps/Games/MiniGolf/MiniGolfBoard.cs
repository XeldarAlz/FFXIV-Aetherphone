using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Framework.Physics;

namespace Aetherphone.Apps.Games.MiniGolf;

internal enum GolfPhase : byte
{
    Aiming,
    Rolling,
    Sinking,
    Drowning,
    Holed,
}

[Flags]
internal enum GolfEvents : ushort
{
    None = 0,
    Shot = 1,
    Wall = 2,
    Post = 4,
    Mill = 8,
    Splash = 16,
    Tunnel = 32,
    Sand = 64,
    LipOut = 128,
    Drop = 256,
    Holed = 512,
    Stopped = 1024,
    Reset = 2048,
}

internal sealed class MiniGolfBoard
{
    public const float BallRadius = 0.14f;
    public const float CupRadius = 0.22f;
    public const float TunnelRadius = 0.3f;
    public const float MillThickness = 0.08f;
    public const float MaxShotSpeed = 8.5f;
    public const float CaptureSpeed = 2.4f;
    public const int MaxStrokes = 10;
    public const float SinkSeconds = 0.35f;
    public const float DrownSeconds = 0.55f;
    private const int MaxMills = 4;
    private const int MaxTunnels = 4;
    private const float GreenDecel = 1.0f;
    private const float GreenDamping = 0.6f;
    private const float SandDecel = 5.5f;
    private const float SandDamping = 2.4f;
    private const float StopSpeed = 0.08f;
    private const float WakeSpeed = 0.3f;
    private const float CupPull = 7f;
    private const float CaptureReach = CupRadius - BallRadius * 0.3f;
    private const float PullReach = CupRadius + BallRadius * 0.5f;
    private const float SwallowReach = TunnelRadius * 0.6f;
    private const float ImpactSound = 1.2f;
    private const float PinnedSeconds = 0.4f;
    private const int TagWall = 1;
    private const int TagPost = 2;
    private const int TagMill = 3;
    private const int TagTunnel = 16;
    private static readonly PhysicsMaterial BallMaterial = new(1f, 0.7f, 0.05f);
    private static readonly PhysicsMaterial WallMaterial = new(1f, 0.72f, 0.05f);
    private static readonly PhysicsMaterial PostMaterial = new(1f, 1f, 0.05f);
    private static readonly PhysicsMaterial MillMaterial = new(1f, 0.5f, 0.1f);

    private readonly PhysicsWorld world = new(bodyCapacity: 96, contactCapacity: 256, jointCapacity: 4,
        chainPointCapacity: 512, eventCapacity: 64);
    private readonly int[] mills = new int[MaxMills];
    private readonly int[] mouths = new int[MaxTunnels];
    private readonly bool[] touchingMouths = new bool[MaxTunnels];
    private MiniGolfHole? hole;
    private int ball;
    private int millCount;
    private int tunnelCount;
    private bool overCup;
    private float fastestOverCup;
    private float slowSeconds;
    private bool inSand;

    public GolfPhase Phase { get; private set; }

    public int Strokes { get; private set; }

    public Vector2 LastRest { get; private set; }

    public GolfEvents Events { get; private set; }

    public Vector2 EventPoint { get; private set; }

    public Vector2 TunnelFrom { get; private set; }

    public float ImpactStrength { get; private set; }

    public float PhaseProgress { get; private set; }

    public Vector2 SinkFrom { get; private set; }

    public int Penalties { get; private set; }

    public MiniGolfHole Hole => hole!;

    public PhysicsWorld World => world;

    public int MillCount => millCount;

    public bool InSand => inSand;

    public bool CanShoot => Phase == GolfPhase.Aiming && Strokes < MaxStrokes;

    public Vector2 BallPosition => world.Position(ball);

    public Vector2 BallRenderPosition => world.RenderPosition(ball);

    public Vector2 BallVelocity => world.Velocity(ball);

    public float BallSpeed => world.Velocity(ball).Length();

    public int MillBody(int index) => mills[index];

    public void Load(MiniGolfHole source, GameRandom random)
    {
        hole = source;
        world.Clear();
        world.Gravity = Vector2.Zero;
        var course = world.CreatePolyline(source.Course, WallMaterial, true);
        world.SetTag(course, TagWall);
        for (var index = 0; index < source.Walls.Length; index++)
        {
            world.SetTag(world.CreatePolyline(source.Walls[index], WallMaterial), TagWall);
        }

        for (var index = 0; index < source.Blocks.Length; index++)
        {
            world.SetTag(world.CreatePolyline(source.Blocks[index], WallMaterial, true), TagWall);
        }

        for (var index = 0; index < source.Posts.Length; index++)
        {
            var post = source.Posts[index];
            world.SetTag(world.CreateCircle(BodyType.Static, post.Center, post.Radius, PostMaterial), TagPost);
        }

        millCount = Math.Min(source.Mills.Length, MaxMills);
        for (var index = 0; index < millCount; index++)
        {
            var mill = source.Mills[index];
            var body = world.CreateBox(BodyType.Kinematic, mill.Hub, new Vector2(mill.Reach, MillThickness),
                random.Range(0f, MathF.PI), MillMaterial);
            world.SetAngularVelocity(body, mill.Speed);
            world.SetTag(body, TagMill);
            mills[index] = body;
        }

        tunnelCount = Math.Min(source.Tunnels.Length, MaxTunnels);
        for (var index = 0; index < tunnelCount; index++)
        {
            mouths[index] = world.CreateCircle(BodyType.Static, source.Tunnels[index].Entry, TunnelRadius,
                PhysicsMaterial.Default, BodyFlags.Sensor);
            world.SetTag(mouths[index], TagTunnel + index);
        }

        ball = world.CreateCircle(BodyType.Dynamic, source.Tee, BallRadius, BallMaterial,
            BodyFlags.Bullet | BodyFlags.FixedRotation);
        world.SetDamping(ball, GreenDamping, 0f);
        Array.Clear(touchingMouths);
        overCup = false;
        fastestOverCup = 0f;
        slowSeconds = 0f;
        inSand = false;
        Phase = GolfPhase.Aiming;
        Strokes = 0;
        Penalties = 0;
        LastRest = source.Tee;
        PhaseProgress = 0f;
        Events = GolfEvents.None;
    }

    public void BeginFrame()
    {
        Events = GolfEvents.None;
        ImpactStrength = 0f;
    }

    public void PlaceBall(Vector2 position)
    {
        world.SetTransform(ball, position, 0f);
        world.SetVelocity(ball, Vector2.Zero);
        LastRest = position;
        Phase = GolfPhase.Aiming;
    }

    public bool Shoot(Vector2 direction, float power)
    {
        if (!CanShoot || direction.LengthSquared() <= 0.000001f || power <= 0f)
        {
            return false;
        }

        Strokes++;
        LastRest = world.Position(ball);
        world.SetVelocity(ball, Vector2.Normalize(direction) * Math.Clamp(power, 0f, 1f) * MaxShotSpeed);
        Phase = GolfPhase.Rolling;
        Events |= GolfEvents.Shot;
        EventPoint = LastRest;
        ImpactStrength = power;
        return true;
    }

    public void Step(float deltaSeconds)
    {
        if (deltaSeconds <= 0f || hole is null)
        {
            return;
        }

        switch (Phase)
        {
            case GolfPhase.Sinking:
                AdvanceSink(deltaSeconds);
                world.Step(deltaSeconds);
                return;
            case GolfPhase.Drowning:
                AdvanceDrown(deltaSeconds);
                world.Step(deltaSeconds);
                return;
            case GolfPhase.Holed:
                world.Step(deltaSeconds);
                return;
        }

        ApplyRolling(deltaSeconds);
        world.Step(deltaSeconds);
        ReadContacts();
        var position = world.Position(ball);
        if (Swallow(position))
        {
            return;
        }

        if (HazardAt(position))
        {
            return;
        }

        CheckCup(position);
        CheckRest(position, deltaSeconds);
    }

    public int PredictAim(Vector2 direction, float power, Span<Vector2> path)
    {
        if (path.Length < 2 || direction.LengthSquared() <= 0.000001f)
        {
            return 0;
        }

        var unit = Vector2.Normalize(direction);
        var speed = Math.Clamp(power, 0f, 1f) * MaxShotSpeed;
        var remaining = RollDistance(speed) * 0.55f;
        var origin = world.Position(ball);
        var count = 0;
        path[count++] = origin;
        while (remaining > 0.01f && count < path.Length)
        {
            if (!world.Raycast(origin + unit * 0.001f, unit, remaining + BallRadius, out var hit) ||
                hit.Distance - BallRadius >= remaining)
            {
                path[count++] = origin + unit * remaining;
                break;
            }

            var travel = MathF.Max(0f, hit.Distance - BallRadius);
            origin += unit * travel;
            path[count++] = origin;
            remaining -= travel;
            unit = Vector2.Reflect(unit, hit.Normal);
            remaining *= WallMaterial.Restitution;
        }

        return count;
    }

    public static float RollDistance(float speed)
    {
        var floor = GreenDecel / GreenDamping;
        var time = MathF.Log((speed + floor) / floor) / GreenDamping;
        return MathF.Max(0f, (speed + floor) * (1f - MathF.Exp(-GreenDamping * time)) / GreenDamping - floor * time);
    }

    public Vector2 SlopeAt(Vector2 position)
    {
        var push = Vector2.Zero;
        var slopes = hole!.Slopes;
        for (var index = 0; index < slopes.Length; index++)
        {
            if (slopes[index].Zone.Contains(position))
            {
                push += slopes[index].Push;
            }
        }

        return push;
    }

    private void ApplyRolling(float deltaSeconds)
    {
        var position = world.Position(ball);
        var velocity = world.Velocity(ball);
        var speed = velocity.Length();
        var sand = InSandAt(position);
        if (sand != inSand)
        {
            inSand = sand;
            world.SetDamping(ball, sand ? SandDamping : GreenDamping, 0f);
            if (sand && Phase == GolfPhase.Rolling)
            {
                Events |= GolfEvents.Sand;
                EventPoint = position;
            }
        }

        var decel = sand ? SandDecel : GreenDecel;
        var force = SlopeAt(position);
        if (Phase == GolfPhase.Aiming)
        {
            if (speed < WakeSpeed)
            {
                return;
            }

            Phase = GolfPhase.Rolling;
        }

        if (speed > decel * deltaSeconds)
        {
            force -= velocity / speed * decel;
        }

        var toCup = hole!.Cup - position;
        var distance = toCup.Length();
        if (distance < PullReach && distance > 0.0001f)
        {
            force += toCup / distance * CupPull * (1f - distance / PullReach);
        }

        if (force.LengthSquared() > 0f)
        {
            world.ApplyForce(ball, force * world.Mass(ball));
        }
    }

    private bool InSandAt(Vector2 position)
    {
        var sand = hole!.Sand;
        for (var index = 0; index < sand.Length; index++)
        {
            if (sand[index].Contains(position))
            {
                return true;
            }
        }

        return false;
    }

    private void ReadContacts()
    {
        for (var index = 0; index < world.EventCount; index++)
        {
            ref readonly var contact = ref world.Event(index);
            if (!contact.Involves(ball))
            {
                continue;
            }

            var other = contact.Other(ball);
            var tag = world.Tag(other);
            if (tag >= TagTunnel)
            {
                touchingMouths[tag - TagTunnel] = contact.Kind == ContactEventKind.SensorEnter;
                continue;
            }

            if (contact.Kind != ContactEventKind.Hit || contact.Impulse / world.Mass(ball) < ImpactSound)
            {
                continue;
            }

            Events |= tag switch
            {
                TagPost => GolfEvents.Post,
                TagMill => GolfEvents.Mill,
                _ => GolfEvents.Wall,
            };
            EventPoint = contact.Point;
            ImpactStrength = MathF.Max(ImpactStrength, contact.Impulse / world.Mass(ball));
        }
    }

    private bool Swallow(Vector2 position)
    {
        var tunnels = hole!.Tunnels;
        for (var index = 0; index < tunnelCount; index++)
        {
            if (!touchingMouths[index] || Vector2.Distance(position, tunnels[index].Entry) > SwallowReach)
            {
                continue;
            }

            var velocity = world.Velocity(ball);
            TunnelFrom = tunnels[index].Entry;
            world.SetTransform(ball, tunnels[index].Exit, 0f);
            world.SetVelocity(ball, velocity);
            touchingMouths[index] = false;
            Events |= GolfEvents.Tunnel;
            EventPoint = tunnels[index].Exit;
            return true;
        }

        return false;
    }

    private bool HazardAt(Vector2 position)
    {
        var water = hole!.Water;
        for (var index = 0; index < water.Length; index++)
        {
            if (!water[index].Contains(position))
            {
                continue;
            }

            StartDrown(position, true);
            Events |= GolfEvents.Splash;
            return true;
        }

        if (hole.OnCourse(position))
        {
            return false;
        }

        StartDrown(position, false);
        Events |= GolfEvents.Reset;
        return true;
    }

    private void StartDrown(Vector2 position, bool penalty)
    {
        Phase = GolfPhase.Drowning;
        PhaseProgress = 0f;
        SinkFrom = position;
        EventPoint = position;
        world.SetVelocity(ball, Vector2.Zero);
        if (!penalty)
        {
            return;
        }

        Penalties++;
        Strokes = Math.Min(MaxStrokes, Strokes + 1);
    }

    private void AdvanceDrown(float deltaSeconds)
    {
        PhaseProgress = MathF.Min(1f, PhaseProgress + deltaSeconds / DrownSeconds);
        if (PhaseProgress < 1f)
        {
            return;
        }

        world.SetTransform(ball, LastRest, 0f);
        world.SetVelocity(ball, Vector2.Zero);
        inSand = InSandAt(LastRest);
        world.SetDamping(ball, inSand ? SandDamping : GreenDamping, 0f);
        Array.Clear(touchingMouths);
        Phase = GolfPhase.Aiming;
        Events |= GolfEvents.Stopped;
    }

    private void CheckCup(Vector2 position)
    {
        var distance = Vector2.Distance(position, hole!.Cup);
        var speed = world.Velocity(ball).Length();
        if (distance <= CaptureReach && speed <= CaptureSpeed)
        {
            Phase = GolfPhase.Sinking;
            PhaseProgress = 0f;
            SinkFrom = position;
            overCup = false;
            world.SetVelocity(ball, Vector2.Zero);
            world.SetCollisionFilter(ball, PhysicsWorld.DefaultCategory, 0);
            Events |= GolfEvents.Drop;
            EventPoint = hole.Cup;
            return;
        }

        var over = distance <= CupRadius;
        if (over)
        {
            overCup = true;
            fastestOverCup = MathF.Max(fastestOverCup, speed);
            return;
        }

        if (!overCup)
        {
            return;
        }

        overCup = false;
        if (fastestOverCup > CaptureSpeed)
        {
            Events |= GolfEvents.LipOut;
            EventPoint = hole.Cup;
        }

        fastestOverCup = 0f;
    }

    private void AdvanceSink(float deltaSeconds)
    {
        PhaseProgress = MathF.Min(1f, PhaseProgress + deltaSeconds / SinkSeconds);
        if (PhaseProgress < 1f)
        {
            return;
        }

        Phase = GolfPhase.Holed;
        Events |= GolfEvents.Holed;
        EventPoint = hole!.Cup;
    }

    private void CheckRest(Vector2 position, float deltaSeconds)
    {
        if (Phase != GolfPhase.Rolling)
        {
            return;
        }

        var speed = world.Velocity(ball).Length();
        if (speed >= StopSpeed)
        {
            slowSeconds = 0f;
            return;
        }

        slowSeconds += deltaSeconds;
        var decel = inSand ? SandDecel : GreenDecel;
        if (SlopeAt(position).Length() > decel && slowSeconds < PinnedSeconds)
        {
            return;
        }

        slowSeconds = 0f;

        world.SetVelocity(ball, Vector2.Zero);
        Phase = GolfPhase.Aiming;
        LastRest = position;
        Events |= GolfEvents.Stopped;
    }
}
