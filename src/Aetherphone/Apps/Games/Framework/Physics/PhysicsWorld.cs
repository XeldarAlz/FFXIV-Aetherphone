namespace Aetherphone.Apps.Games.Framework.Physics;

internal sealed partial class PhysicsWorld
{
    public const int Ground = 0;
    public const float StepSeconds = 1f / 120f;
    public const int VelocityIterations = 8;
    public const float Baumgarte = 0.2f;
    public const float LinearSlop = 0.005f;
    public const ushort DefaultCategory = 1;
    public const ushort AllCategories = ushort.MaxValue;
    private const float InverseStep = 120f;
    private const float SpeculativeDistance = 4f * LinearSlop;
    private const float RestitutionThreshold = 1f;
    private const float TimeToSleep = 0.5f;
    private const float SleepLinearSpeed = 0.05f;
    private const float SleepAngularSpeed = 0.05f;
    private const float MaxCatchUpSeconds = 0.1f;
    private const float MaxTranslation = 2f;
    private const float MaxRotation = MathF.PI * 0.25f;
    private const float MaxCorrection = 0.2f;

    private readonly BodyType[] types;
    private readonly ShapeKind[] shapes;
    private readonly BodyFlags[] flags;
    private readonly bool[] alive;
    private readonly bool[] awake;
    private readonly Vector2[] positions;
    private readonly float[] angles;
    private readonly Vector2[] rotations;
    private readonly Vector2[] velocities;
    private readonly float[] angularVelocities;
    private readonly Vector2[] pseudoVelocities;
    private readonly float[] pseudoAngularVelocities;
    private readonly Vector2[] forces;
    private readonly float[] torques;
    private readonly Vector2[] startPositions;
    private readonly float[] startAngles;
    private readonly float[] masses;
    private readonly float[] inverseMasses;
    private readonly float[] inverseInertias;
    private readonly float[] radii;
    private readonly Vector2[] halfExtents;
    private readonly float[] extents;
    private readonly int[] chainStarts;
    private readonly int[] chainCounts;
    private readonly bool[] chainClosed;
    private readonly float[] restitutions;
    private readonly float[] frictions;
    private readonly float[] gravityScales;
    private readonly float[] linearDampings;
    private readonly float[] angularDampings;
    private readonly ushort[] categories;
    private readonly ushort[] masks;
    private readonly int[] tags;
    private readonly float[] sleepTimers;
    private readonly int[] jointCounts;
    private readonly Vector2[] boundsMin;
    private readonly Vector2[] boundsMax;
    private readonly int[] islandParents;
    private readonly bool[] islandAwake;
    private readonly float[] islandSleepTimes;
    private readonly int[] freeBodies;
    private readonly int[] sweepOrder;
    private readonly Vector2[] chainLocalPoints;
    private readonly Vector2[] chainWorldPoints;
    private readonly ContactEvent[] events;
    private FixedStepClock clock;
    private Vector2 gravity = new(0f, 9.81f);
    private int nextBody;
    private int freeBodyCount;
    private int sweepCount;
    private int chainPointCount;
    private int eventStart;
    private int eventCount;

    public PhysicsWorld(int bodyCapacity = 256, int contactCapacity = 1024, int jointCapacity = 256,
        int chainPointCapacity = 1024, int eventCapacity = 256)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bodyCapacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(contactCapacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(jointCapacity);
        ArgumentOutOfRangeException.ThrowIfNegative(chainPointCapacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(eventCapacity);
        var slots = bodyCapacity + 1;
        types = new BodyType[slots];
        shapes = new ShapeKind[slots];
        flags = new BodyFlags[slots];
        alive = new bool[slots];
        awake = new bool[slots];
        positions = new Vector2[slots];
        angles = new float[slots];
        rotations = new Vector2[slots];
        velocities = new Vector2[slots];
        angularVelocities = new float[slots];
        pseudoVelocities = new Vector2[slots];
        pseudoAngularVelocities = new float[slots];
        forces = new Vector2[slots];
        torques = new float[slots];
        startPositions = new Vector2[slots];
        startAngles = new float[slots];
        masses = new float[slots];
        inverseMasses = new float[slots];
        inverseInertias = new float[slots];
        radii = new float[slots];
        halfExtents = new Vector2[slots];
        extents = new float[slots];
        chainStarts = new int[slots];
        chainCounts = new int[slots];
        chainClosed = new bool[slots];
        restitutions = new float[slots];
        frictions = new float[slots];
        gravityScales = new float[slots];
        linearDampings = new float[slots];
        angularDampings = new float[slots];
        categories = new ushort[slots];
        masks = new ushort[slots];
        tags = new int[slots];
        sleepTimers = new float[slots];
        jointCounts = new int[slots];
        boundsMin = new Vector2[slots];
        boundsMax = new Vector2[slots];
        islandParents = new int[slots];
        islandAwake = new bool[slots];
        islandSleepTimes = new float[slots];
        freeBodies = new int[slots];
        sweepOrder = new int[slots];
        chainLocalPoints = new Vector2[chainPointCapacity];
        chainWorldPoints = new Vector2[chainPointCapacity];
        events = new ContactEvent[eventCapacity];
        pairs = new ContactPair[contactCapacity];
        previousPairs = new ContactPair[contactCapacity];
        points = new ContactPoint[contactCapacity];
        previousPoints = new ContactPoint[contactCapacity];
        activePairs = new int[contactCapacity];
        var tableBits = TableBits(contactCapacity);
        pairTable = new int[1 << tableBits];
        pairTableShift = 64 - tableBits;
        bulletCandidates = new int[BulletCandidateCapacity];
        joints = new Joint[jointCapacity];
        freeJoints = new int[jointCapacity];
        ropes = new RopeRecord[RopeCapacity];
        ropeLinks = new int[RopeCapacity * MaxRopeSegments];
        ropeJoints = new int[RopeCapacity * MaxRopeSegments];
        clock = new FixedStepClock(StepSeconds, MaxCatchUpSeconds);
        Clear();
    }

    public Vector2 Gravity
    {
        get => gravity;
        set
        {
            gravity = value;
            WakeAll();
        }
    }

    public float ImpactThreshold { get; set; } = float.PositiveInfinity;

    public int BodyCapacity => types.Length - 1;

    public int BodyCount { get; private set; }

    public int EventCount => eventCount;

    public ref readonly ContactEvent Event(int index) => ref events[(eventStart + index) % events.Length];

    public void ClearEvents()
    {
        eventStart = 0;
        eventCount = 0;
    }

    public void Clear()
    {
        Array.Clear(alive);
        Array.Clear(awake);
        Array.Clear(shapes);
        Array.Clear(jointCounts);
        nextBody = 1;
        freeBodyCount = 0;
        sweepCount = 0;
        chainPointCount = 0;
        BodyCount = 0;
        alive[Ground] = true;
        types[Ground] = BodyType.Static;
        rotations[Ground] = Vector2.UnitX;
        ClearJoints();
        ClearContacts();
        ClearEvents();
        clock.Reset();
    }

    public int Step(float deltaSeconds)
    {
        ClearEvents();
        var steps = clock.Advance(deltaSeconds);
        for (var index = 0; index < steps; index++)
        {
            Tick();
        }

        Array.Clear(forces, 0, nextBody);
        Array.Clear(torques, 0, nextBody);
        return steps;
    }

    public void Tick()
    {
        BeginContacts();
        SaveStartPoses();
        UpdateBounds();
        FindPairs();
        WakeIslands();
        IntegrateVelocities();
        PrepareContacts();
        WarmStartContacts();
        PrepareJoints();
        for (var iteration = 0; iteration < VelocityIterations; iteration++)
        {
            SolveJointVelocities();
            SolveContactVelocities();
            SolveJointPositions();
            SolveContactPositions();
        }

        ApplyRestitution();
        IntegratePositions();
        SweepBullets();
        UpdateSleep();
        EmitEvents();
    }

    public int CreateCircle(BodyType type, Vector2 position, float radius, in PhysicsMaterial material,
        BodyFlags bodyFlags = BodyFlags.None)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        var body = AllocateBody(type, ShapeKind.Circle, position, 0f, material, bodyFlags);
        radii[body] = radius;
        extents[body] = radius;
        FinishBody(body, material.Density);
        return body;
    }

    public int CreateBox(BodyType type, Vector2 position, Vector2 halfExtents, float angle,
        in PhysicsMaterial material, BodyFlags bodyFlags = BodyFlags.None)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(halfExtents.X);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(halfExtents.Y);
        var body = AllocateBody(type, ShapeKind.Box, position, angle, material, bodyFlags);
        this.halfExtents[body] = halfExtents;
        extents[body] = halfExtents.Length();
        FinishBody(body, material.Density);
        return body;
    }

    public int CreateSegment(Vector2 start, Vector2 end, in PhysicsMaterial material,
        BodyFlags bodyFlags = BodyFlags.None)
    {
        Span<Vector2> ends = stackalloc Vector2[2];
        ends[0] = start;
        ends[1] = end;
        return CreateChain(ShapeKind.Segment, ends, false, material, bodyFlags);
    }

    public int CreatePolyline(ReadOnlySpan<Vector2> points, in PhysicsMaterial material, bool closed = false,
        BodyFlags bodyFlags = BodyFlags.None)
    {
        if (points.Length < 2)
        {
            throw new ArgumentException("A polyline needs at least two points.", nameof(points));
        }

        return CreateChain(ShapeKind.Polyline, points, closed && points.Length > 2, material, bodyFlags);
    }

    public void DestroyBody(int body)
    {
        if (body == Ground || !IsAlive(body))
        {
            return;
        }

        DestroyRopesTouching(body);
        for (var joint = 0; joint < nextJoint; joint++)
        {
            if (joints[joint].Kind != JointKind.None && (joints[joint].BodyA == body || joints[joint].BodyB == body))
            {
                DestroyJoint(joint);
            }
        }

        ReleaseContacts(body);
        RemoveFromSweep(body);
        if (chainCounts[body] > 0)
        {
            ReleaseChain(body);
        }

        alive[body] = false;
        awake[body] = false;
        shapes[body] = ShapeKind.None;
        freeBodies[freeBodyCount] = body;
        freeBodyCount++;
        BodyCount--;
    }

    public bool IsAlive(int body) => body >= 0 && body < alive.Length && alive[body];

    public BodyType Type(int body) => types[body];

    public ShapeKind Shape(int body) => shapes[body];

    public BodyFlags Flags(int body) => flags[body];

    public Vector2 Position(int body) => positions[body];

    public float Angle(int body) => angles[body];

    public Vector2 RenderPosition(int body) => Vector2.Lerp(startPositions[body], positions[body], clock.Alpha);

    public float RenderAngle(int body) => startAngles[body] + (angles[body] - startAngles[body]) * clock.Alpha;

    public Vector2 Velocity(int body) => velocities[body];

    public float AngularVelocity(int body) => angularVelocities[body];

    public float Radius(int body) => radii[body];

    public Vector2 HalfExtents(int body) => halfExtents[body];

    public float Mass(int body) => masses[body];

    public bool IsAwake(int body) => awake[body];

    public int Tag(int body) => tags[body];

    public ReadOnlySpan<Vector2> ChainPoints(int body) =>
        new(chainWorldPoints, chainStarts[body], chainCounts[body]);

    public bool IsClosedChain(int body) => chainClosed[body];

    public Vector2 WorldPoint(int body, Vector2 localPoint) =>
        positions[body] + PhysicsMath.Rotate(rotations[body], localPoint);

    public Vector2 LocalPoint(int body, Vector2 worldPoint) =>
        PhysicsMath.InverseRotate(rotations[body], worldPoint - positions[body]);

    public void SetTransform(int body, Vector2 position, float angle)
    {
        positions[body] = position;
        angles[body] = angle;
        rotations[body] = PhysicsMath.Rotation(angle);
        startPositions[body] = position;
        startAngles[body] = angle;
        if (chainCounts[body] > 0)
        {
            UpdateChainWorld(body);
        }

        ComputeBounds(body);
        if (types[body] == BodyType.Dynamic)
        {
            Wake(body);
            return;
        }

        WakeTouching(body);
    }

    public void SetVelocity(int body, Vector2 velocity)
    {
        if (types[body] == BodyType.Static)
        {
            return;
        }

        velocities[body] = velocity;
        Wake(body);
    }

    public void SetAngularVelocity(int body, float angularVelocity)
    {
        if (types[body] == BodyType.Static)
        {
            return;
        }

        angularVelocities[body] = angularVelocity;
        Wake(body);
    }

    public void ApplyImpulse(int body, Vector2 impulse)
    {
        if (types[body] != BodyType.Dynamic)
        {
            return;
        }

        velocities[body] += inverseMasses[body] * impulse;
        Wake(body);
    }

    public void ApplyImpulse(int body, Vector2 impulse, Vector2 worldPoint)
    {
        if (types[body] != BodyType.Dynamic)
        {
            return;
        }

        velocities[body] += inverseMasses[body] * impulse;
        angularVelocities[body] += inverseInertias[body] * PhysicsMath.Cross(worldPoint - positions[body], impulse);
        Wake(body);
    }

    public void ApplyForce(int body, Vector2 force)
    {
        if (types[body] != BodyType.Dynamic)
        {
            return;
        }

        forces[body] += force;
        Wake(body);
    }

    public void ApplyTorque(int body, float torque)
    {
        if (types[body] != BodyType.Dynamic)
        {
            return;
        }

        torques[body] += torque;
        Wake(body);
    }

    public void SetGravityScale(int body, float scale)
    {
        gravityScales[body] = scale;
        Wake(body);
    }

    public void SetDamping(int body, float linearDamping, float angularDamping)
    {
        linearDampings[body] = MathF.Max(0f, linearDamping);
        angularDampings[body] = MathF.Max(0f, angularDamping);
    }

    public void SetCollisionFilter(int body, ushort category, ushort mask)
    {
        categories[body] = category;
        masks[body] = mask;
        Wake(body);
        WakeTouching(body);
    }

    public void SetTag(int body, int tag)
    {
        tags[body] = tag;
    }

    public void Wake(int body)
    {
        if (types[body] != BodyType.Dynamic || !alive[body])
        {
            return;
        }

        awake[body] = true;
        sleepTimers[body] = 0f;
    }

    private void WakeAll()
    {
        for (var body = 1; body < nextBody; body++)
        {
            Wake(body);
        }
    }

    private int CreateChain(ShapeKind shape, ReadOnlySpan<Vector2> chain, bool closed, in PhysicsMaterial material,
        BodyFlags bodyFlags)
    {
        var segmentCount = closed ? chain.Length : chain.Length - 1;
        for (var segment = 0; segment < segmentCount; segment++)
        {
            var next = segment + 1 < chain.Length ? segment + 1 : 0;
            if (Vector2.DistanceSquared(chain[segment], chain[next]) < LinearSlop * LinearSlop)
            {
                throw new ArgumentException("Polyline points must be distinct.", nameof(chain));
            }
        }

        if (chainPointCount + chain.Length > chainLocalPoints.Length)
        {
            throw new InvalidOperationException("PhysicsWorld chain point capacity reached.");
        }

        var origin = chain[0];
        var body = AllocateBody(BodyType.Static, shape, origin, 0f, material, bodyFlags);
        chainStarts[body] = chainPointCount;
        chainCounts[body] = chain.Length;
        chainClosed[body] = closed;
        for (var index = 0; index < chain.Length; index++)
        {
            chainLocalPoints[chainPointCount + index] = chain[index] - origin;
            chainWorldPoints[chainPointCount + index] = chain[index];
        }

        chainPointCount += chain.Length;
        FinishBody(body, 0f);
        return body;
    }

    private int AllocateBody(BodyType type, ShapeKind shape, Vector2 position, float angle,
        in PhysicsMaterial material, BodyFlags bodyFlags)
    {
        int body;
        if (freeBodyCount > 0)
        {
            freeBodyCount--;
            body = freeBodies[freeBodyCount];
        }
        else
        {
            if (nextBody >= types.Length)
            {
                throw new InvalidOperationException("PhysicsWorld body capacity reached.");
            }

            body = nextBody;
            nextBody++;
        }

        alive[body] = true;
        types[body] = type;
        shapes[body] = shape;
        flags[body] = bodyFlags;
        awake[body] = type == BodyType.Dynamic;
        positions[body] = position;
        angles[body] = angle;
        rotations[body] = PhysicsMath.Rotation(angle);
        startPositions[body] = position;
        startAngles[body] = angle;
        velocities[body] = Vector2.Zero;
        angularVelocities[body] = 0f;
        pseudoVelocities[body] = Vector2.Zero;
        pseudoAngularVelocities[body] = 0f;
        forces[body] = Vector2.Zero;
        torques[body] = 0f;
        radii[body] = 0f;
        halfExtents[body] = Vector2.Zero;
        extents[body] = 0f;
        chainStarts[body] = 0;
        chainCounts[body] = 0;
        chainClosed[body] = false;
        restitutions[body] = MathF.Max(0f, material.Restitution);
        frictions[body] = MathF.Max(0f, material.Friction);
        gravityScales[body] = 1f;
        linearDampings[body] = 0f;
        angularDampings[body] = 0f;
        categories[body] = DefaultCategory;
        masks[body] = AllCategories;
        tags[body] = 0;
        sleepTimers[body] = 0f;
        jointCounts[body] = 0;
        sweepOrder[sweepCount] = body;
        sweepCount++;
        BodyCount++;
        return body;
    }

    private void FinishBody(int body, float density)
    {
        ComputeMass(body, density);
        ComputeBounds(body);
    }

    private void ComputeMass(int body, float density)
    {
        masses[body] = 0f;
        inverseMasses[body] = 0f;
        inverseInertias[body] = 0f;
        if (types[body] != BodyType.Dynamic)
        {
            return;
        }

        float mass;
        float inertia;
        if (shapes[body] == ShapeKind.Circle)
        {
            var radius = radii[body];
            mass = density * MathF.PI * radius * radius;
            inertia = mass * radius * radius * 0.5f;
        }
        else
        {
            var half = halfExtents[body];
            mass = density * 4f * half.X * half.Y;
            inertia = mass * (half.X * half.X + half.Y * half.Y) / 3f;
        }

        if (mass <= 0f)
        {
            mass = 1f;
            inertia = extents[body] * extents[body] * 0.5f;
        }

        masses[body] = mass;
        inverseMasses[body] = 1f / mass;
        if ((flags[body] & BodyFlags.FixedRotation) == 0 && inertia > 0f)
        {
            inverseInertias[body] = 1f / inertia;
        }
    }

    private void ComputeBounds(int body)
    {
        var position = positions[body];
        switch (shapes[body])
        {
            case ShapeKind.Circle:
            {
                var reach = new Vector2(radii[body]);
                boundsMin[body] = position - reach;
                boundsMax[body] = position + reach;
                return;
            }
            case ShapeKind.Box:
            {
                var rotation = rotations[body];
                var half = halfExtents[body];
                var reach = new Vector2(MathF.Abs(rotation.X) * half.X + MathF.Abs(rotation.Y) * half.Y,
                    MathF.Abs(rotation.Y) * half.X + MathF.Abs(rotation.X) * half.Y);
                boundsMin[body] = position - reach;
                boundsMax[body] = position + reach;
                return;
            }
            case ShapeKind.Segment:
            case ShapeKind.Polyline:
            {
                var chain = ChainPoints(body);
                var minimum = chain[0];
                var maximum = chain[0];
                for (var index = 1; index < chain.Length; index++)
                {
                    minimum = Vector2.Min(minimum, chain[index]);
                    maximum = Vector2.Max(maximum, chain[index]);
                }

                boundsMin[body] = minimum;
                boundsMax[body] = maximum;
                return;
            }
        }
    }

    private void UpdateChainWorld(int body)
    {
        var start = chainStarts[body];
        var position = positions[body];
        var rotation = rotations[body];
        for (var index = start; index < start + chainCounts[body]; index++)
        {
            chainWorldPoints[index] = position + PhysicsMath.Rotate(rotation, chainLocalPoints[index]);
        }
    }

    private void ReleaseChain(int body)
    {
        var start = chainStarts[body];
        var count = chainCounts[body];
        var tail = chainPointCount - start - count;
        Array.Copy(chainLocalPoints, start + count, chainLocalPoints, start, tail);
        Array.Copy(chainWorldPoints, start + count, chainWorldPoints, start, tail);
        chainPointCount -= count;
        chainCounts[body] = 0;
        for (var other = 1; other < nextBody; other++)
        {
            if (alive[other] && chainCounts[other] > 0 && chainStarts[other] > start)
            {
                chainStarts[other] -= count;
            }
        }
    }

    private void RemoveFromSweep(int body)
    {
        for (var index = 0; index < sweepCount; index++)
        {
            if (sweepOrder[index] != body)
            {
                continue;
            }

            Array.Copy(sweepOrder, index + 1, sweepOrder, index, sweepCount - index - 1);
            sweepCount--;
            return;
        }
    }

    private bool IsActive(int body)
    {
        if (types[body] == BodyType.Dynamic)
        {
            return awake[body];
        }

        return types[body] == BodyType.Kinematic && IsMoving(body);
    }

    private bool IsMoving(int body) => velocities[body] != Vector2.Zero || angularVelocities[body] != 0f;

    private bool IsSolvable(int body) => awake[body];

    private float SolverInverseMass(int body) => awake[body] ? inverseMasses[body] : 0f;

    private float SolverInverseInertia(int body) => awake[body] ? inverseInertias[body] : 0f;

    private void SaveStartPoses()
    {
        for (var body = 1; body < nextBody; body++)
        {
            if (!alive[body] || types[body] == BodyType.Static)
            {
                continue;
            }

            startPositions[body] = positions[body];
            startAngles[body] = angles[body];
        }
    }

    private void UpdateBounds()
    {
        for (var body = 1; body < nextBody; body++)
        {
            if (!alive[body] || !IsActive(body))
            {
                continue;
            }

            ComputeBounds(body);
            var motion = (velocities[body].Length() + MathF.Abs(angularVelocities[body]) * extents[body]) * StepSeconds;
            var margin = new Vector2(SpeculativeDistance + motion);
            boundsMin[body] -= margin;
            boundsMax[body] += margin;
        }
    }

    private void WakeIslands()
    {
        for (var body = 1; body < nextBody; body++)
        {
            islandParents[body] = body;
            islandAwake[body] = false;
        }

        for (var pairIndex = 0; pairIndex < pairCount; pairIndex++)
        {
            ref readonly var pair = ref pairs[pairIndex];
            if (pair.BodyA < 0 || pair.Sensor || pair.PointCount == 0)
            {
                continue;
            }

            LinkIsland(pair.BodyA, pair.BodyB);
        }

        for (var joint = 0; joint < nextJoint; joint++)
        {
            if (joints[joint].Kind != JointKind.None)
            {
                LinkIsland(joints[joint].BodyA, joints[joint].BodyB);
            }
        }

        for (var body = 1; body < nextBody; body++)
        {
            if (alive[body] && awake[body])
            {
                islandAwake[FindRoot(body)] = true;
            }
        }

        for (var body = 1; body < nextBody; body++)
        {
            if (alive[body] && types[body] == BodyType.Dynamic && !awake[body] && islandAwake[FindRoot(body)])
            {
                Wake(body);
            }
        }
    }

    private void LinkIsland(int bodyA, int bodyB)
    {
        var typeA = types[bodyA];
        var typeB = types[bodyB];
        if (typeA == BodyType.Dynamic && typeB == BodyType.Dynamic)
        {
            Union(bodyA, bodyB);
            return;
        }

        if (typeA == BodyType.Kinematic && typeB == BodyType.Dynamic && IsMoving(bodyA))
        {
            Wake(bodyB);
            return;
        }

        if (typeB == BodyType.Kinematic && typeA == BodyType.Dynamic && IsMoving(bodyB))
        {
            Wake(bodyA);
        }
    }

    private int FindRoot(int body)
    {
        while (islandParents[body] != body)
        {
            islandParents[body] = islandParents[islandParents[body]];
            body = islandParents[body];
        }

        return body;
    }

    private void Union(int first, int second)
    {
        var rootFirst = FindRoot(first);
        var rootSecond = FindRoot(second);
        if (rootFirst == rootSecond)
        {
            return;
        }

        if (rootFirst < rootSecond)
        {
            islandParents[rootSecond] = rootFirst;
            return;
        }

        islandParents[rootFirst] = rootSecond;
    }

    private void IntegrateVelocities()
    {
        for (var body = 1; body < nextBody; body++)
        {
            if (!alive[body] || !awake[body])
            {
                continue;
            }

            var velocity = velocities[body] + StepSeconds * (gravity * gravityScales[body] + inverseMasses[body] * forces[body]);
            var angularVelocity = angularVelocities[body] + StepSeconds * inverseInertias[body] * torques[body];
            velocities[body] = velocity * (1f / (1f + StepSeconds * linearDampings[body]));
            angularVelocities[body] = angularVelocity * (1f / (1f + StepSeconds * angularDampings[body]));
        }
    }

    private void IntegratePositions()
    {
        for (var body = 1; body < nextBody; body++)
        {
            if (!alive[body] || !IsActive(body))
            {
                continue;
            }

            var velocity = velocities[body];
            var speedSquared = velocity.LengthSquared();
            if (speedSquared * StepSeconds * StepSeconds > MaxTranslation * MaxTranslation)
            {
                velocity *= MaxTranslation / (MathF.Sqrt(speedSquared) * StepSeconds);
                velocities[body] = velocity;
            }

            var angularVelocity = angularVelocities[body];
            if (MathF.Abs(angularVelocity) * StepSeconds > MaxRotation)
            {
                angularVelocity = MathF.CopySign(MaxRotation * InverseStep, angularVelocity);
                angularVelocities[body] = angularVelocity;
            }

            positions[body] += StepSeconds * (velocity + pseudoVelocities[body]);
            angles[body] += StepSeconds * (angularVelocity + pseudoAngularVelocities[body]);
            rotations[body] = PhysicsMath.Rotation(angles[body]);
            pseudoVelocities[body] = Vector2.Zero;
            pseudoAngularVelocities[body] = 0f;
        }
    }

    private void UpdateSleep()
    {
        for (var body = 1; body < nextBody; body++)
        {
            islandSleepTimes[body] = float.MaxValue;
            if (!alive[body] || !awake[body])
            {
                continue;
            }

            var moving = velocities[body].LengthSquared() > SleepLinearSpeed * SleepLinearSpeed ||
                MathF.Abs(angularVelocities[body]) > SleepAngularSpeed;
            sleepTimers[body] = moving ? 0f : sleepTimers[body] + StepSeconds;
        }

        for (var body = 1; body < nextBody; body++)
        {
            if (!alive[body] || !awake[body])
            {
                continue;
            }

            var root = FindRoot(body);
            islandSleepTimes[root] = MathF.Min(islandSleepTimes[root], sleepTimers[body]);
        }

        for (var body = 1; body < nextBody; body++)
        {
            if (!alive[body] || !awake[body] || islandSleepTimes[FindRoot(body)] < TimeToSleep)
            {
                continue;
            }

            awake[body] = false;
            velocities[body] = Vector2.Zero;
            angularVelocities[body] = 0f;
        }
    }
}
