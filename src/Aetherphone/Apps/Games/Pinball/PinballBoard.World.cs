using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Framework.Physics;

namespace Aetherphone.Apps.Games.Pinball;

internal enum TablePart : byte
{
    None,
    Ball,
    Wall,
    Post,
    Bumper,
    Sling,
    Target,
    Flipper,
    Spinner,
    TopLane,
    Rollover,
    Saucer,
    RampEntrance,
    RampMade,
    RampExit,
    Gate,
    Orbit,
}

internal sealed partial class PinballBoard
{
    public const float Gravity = 9.5f;
    public const float FlipSpeed = 30f;
    public const float ReturnSpeed = 14f;
    public const float FlipTorque = 900f;
    public const float ReturnTorque = 400f;
    public const float BumperKick = 8f;
    public const float SlingKick = 7f;
    public const float NudgeKick = 2.2f;
    public const float NudgeSideways = 0.6f;
    private const ushort PlayfieldLayer = 1;
    private const ushort RampLayer = 2;
    private const ushort BothLayers = PlayfieldLayer | RampLayer;
    private const ushort GateLayer = 4;
    private const ushort PlayfieldMask = PlayfieldLayer | GateLayer;
    private const ushort NoLayer = 0;
    private const int WorldBodyCapacity = 96;
    private const int WorldContactCapacity = 384;
    private const int WorldJointCapacity = 8;
    private const int WorldChainCapacity = 256;
    private const int WorldEventCapacity = 256;
    private const int TagShift = 8;
    private const int TagIndexMask = 0xFF;
    private const float MaxCatchUpSeconds = 0.1f;
    private const float BallLinearDamping = 0.02f;
    private const float BallAngularDamping = 0.05f;
    private const float SlingMinImpulse = 0.02f;
    private const float FlipperHitImpulse = 0.25f;
    private const float SpinPerSpeed = 2.4f;
    private const float SpinnerFriction = 1.4f;
    private const float SpinnerStopSpeed = 0.6f;
    private const float AutopilotHoldSeconds = 0.2f;
    private const float TargetClearance = 0.42f;
    private const float RaiseRetrySeconds = 0.2f;
    private const float OutOfBoundsMargin = 0.5f;
    private static readonly PhysicsMaterial WallMaterial = new(1f, 0.3f, 0.15f);
    private static readonly PhysicsMaterial RubberMaterial = new(1f, 0.55f, 0.4f);
    private static readonly PhysicsMaterial KickerMaterial = new(1f, 0.4f, 0.3f);
    private static readonly PhysicsMaterial BumperMaterial = new(1f, 0.5f, 0.2f);
    private static readonly PhysicsMaterial TargetMaterial = new(1f, 0.25f, 0.3f);
    private static readonly PhysicsMaterial FlipperMaterial = new(4f, 0.15f, 0.7f);
    private static readonly PhysicsMaterial BallMaterial = new(2f, 0.2f, 0.25f);
    private static readonly PhysicsMaterial RampMaterial = new(1f, 0.15f, 0.1f);
    private static readonly PhysicsMaterial GateMaterial = new(1f, 0.2f, 0.1f);

    private readonly PhysicsWorld world;
    private readonly int[] flipperBodies = new int[PinballTable.FlipperCount];
    private readonly int[] flipperHinges = new int[PinballTable.FlipperCount];
    private readonly float[] flipperAngles = new float[PinballTable.FlipperCount];
    private readonly float[] flipperPrevious = new float[PinballTable.FlipperCount];
    private readonly float[] autopilotHold = new float[PinballTable.FlipperCount];
    private readonly int[] targetBodies = new int[PinballTable.TargetCount];
    private FixedStepClock clock = new(PhysicsWorld.StepSeconds, MaxCatchUpSeconds);

    public PhysicsWorld World => world;

    public BallState BallStateAt(int slot) => balls[slot].State;

    public BallLayer BallLayerAt(int slot) => balls[slot].Layer;

    public int BallIdAt(int slot) => balls[slot].Id;

    public int BallBodyAt(int slot) => balls[slot].Body;

    public Vector2 BallPositionAt(int slot) =>
        Vector2.Lerp(balls[slot].Previous, balls[slot].Position, clock.Alpha);

    public Vector2 BallVelocityAt(int slot) => balls[slot].Velocity;

    public float FlipperAngle(int flipper) =>
        flipperPrevious[flipper] + (flipperAngles[flipper] - flipperPrevious[flipper]) * clock.Alpha;

    public float FlipperLift(int flipper) =>
        Math.Clamp(MathF.Abs(FlipperAngle(flipper) - PinballTable.RestAngle(flipper)) / PinballTable.FlipperSwing, 0f,
            1f);

    public float HingeAngle(int flipper) => world.HingeAngle(flipperHinges[flipper]);

    public int FlipperBody(int flipper) => flipperBodies[flipper];

    private static int Tag(TablePart part, int index) => ((int)part << TagShift) | index;

    private void BuildWorld()
    {
        world.Clear();
        world.Gravity = new Vector2(0f, Gravity);
        clock.Reset();
        AddChain(PinballTable.Cabinet, WallMaterial, PlayfieldLayer);
        AddChain(PinballTable.LaneWall, WallMaterial, PlayfieldLayer);
        AddSegment(PinballTable.PlungerFloorStart, PinballTable.PlungerFloorEnd, WallMaterial, TablePart.Wall, 0,
            PlayfieldLayer);
        AddSegment(PinballTable.GateStart, PinballTable.GateEnd, GateMaterial, TablePart.Gate, 0, GateLayer);
        AddChain(PinballTable.LeftOrbitWall, WallMaterial, PlayfieldLayer);
        AddChain(PinballTable.LeftDeflector, WallMaterial, PlayfieldLayer);
        AddChain(PinballTable.RightOrbitWall, WallMaterial, PlayfieldLayer);
        AddChain(PinballTable.RampMouthRail, WallMaterial, PlayfieldLayer);
        AddChain(PinballTable.RightDeflector, WallMaterial, PlayfieldLayer);
        AddChain(PinballTable.LeftDivider, RubberMaterial, PlayfieldLayer);
        AddChain(PinballTable.RightDivider, RubberMaterial, PlayfieldLayer);
        AddChain(PinballTable.RampOuterRail, RampMaterial, RampLayer);
        AddChain(PinballTable.RampInnerRail, RampMaterial, RampLayer);
        for (var guide = 0; guide < PinballTable.LaneGuides.Length; guide++)
        {
            var x = PinballTable.LaneGuides[guide];
            AddSegment(new Vector2(x, PinballTable.LaneGuideTop), new Vector2(x, PinballTable.LaneGuideBottom),
                RubberMaterial, TablePart.Wall, 0, PlayfieldLayer);
        }

        for (var sling = 0; sling < PinballTable.SlingCount; sling++)
        {
            var points = PinballTable.Sling(sling);
            AddChain(points, RubberMaterial, PlayfieldLayer);
            AddSegment(points[0], points[2], KickerMaterial, TablePart.Sling, sling, PlayfieldLayer);
        }

        for (var post = 0; post < PinballTable.Posts.Length; post++)
        {
            var body = world.CreateCircle(BodyType.Static, PinballTable.Posts[post], PinballTable.PostRadius,
                RubberMaterial);
            world.SetTag(body, Tag(TablePart.Post, post));
        }

        for (var bumper = 0; bumper < PinballTable.BumperCount; bumper++)
        {
            var body = world.CreateCircle(BodyType.Static, PinballTable.Bumpers[bumper], PinballTable.BumperRadius,
                BumperMaterial);
            world.SetTag(body, Tag(TablePart.Bumper, bumper));
        }

        for (var target = 0; target < PinballTable.TargetCount; target++)
        {
            var body = world.CreateBox(BodyType.Static, PinballTable.Targets[target], PinballTable.TargetHalfExtents,
                0f, TargetMaterial);
            world.SetTag(body, Tag(TablePart.Target, target));
            targetBodies[target] = body;
        }

        BuildFlippers();
        BuildSensors();
    }

    private void BuildFlippers()
    {
        var halfExtents = new Vector2(PinballTable.FlipperLength * 0.5f, PinballTable.FlipperHalfThickness);
        for (var flipper = 0; flipper < PinballTable.FlipperCount; flipper++)
        {
            var pivot = PinballTable.Pivot(flipper);
            var angle = PinballTable.RestAngle(flipper);
            var center = pivot + PinballTable.FlipperDirection(flipper, angle) * (PinballTable.FlipperLength * 0.5f);
            var body = world.CreateBox(BodyType.Dynamic, center, halfExtents, angle, FlipperMaterial);
            world.SetTag(body, Tag(TablePart.Flipper, flipper));
            world.SetGravityScale(body, 0f);
            var hinge = world.CreateHinge(PhysicsWorld.Ground, body, pivot);
            if (flipper == PinballTable.LeftFlipper)
            {
                world.SetHingeLimits(hinge, -PinballTable.FlipperSwing, 0f);
            }
            else
            {
                world.SetHingeLimits(hinge, 0f, PinballTable.FlipperSwing);
            }

            flipperBodies[flipper] = body;
            flipperHinges[flipper] = hinge;
            flipperAngles[flipper] = angle;
            flipperPrevious[flipper] = angle;
            autopilotHold[flipper] = 0f;
        }
    }

    private void BuildSensors()
    {
        AddSensorBox(PinballTable.Spinner, PinballTable.SpinnerHalfExtents, TablePart.Spinner, 0, PlayfieldLayer);
        for (var lane = 0; lane < PinballTable.TopLaneCount; lane++)
        {
            AddSensorBox(PinballTable.TopLanes[lane], PinballTable.LaneSensorHalfExtents, TablePart.TopLane, lane,
                PlayfieldLayer);
        }

        for (var rollover = 0; rollover < PinballTable.RolloverCount; rollover++)
        {
            AddSensorBox(PinballTable.Rollovers[rollover], PinballTable.RolloverHalfExtents, TablePart.Rollover,
                rollover, PlayfieldLayer);
        }

        for (var orbit = 0; orbit < PinballTable.OrbitCount; orbit++)
        {
            AddSensorBox(PinballTable.OrbitSensors[orbit], PinballTable.OrbitSensorHalfExtents, TablePart.Orbit, orbit,
                PlayfieldLayer);
        }

        var saucer = world.CreateCircle(BodyType.Static, PinballTable.Saucer, PinballTable.SaucerCatchRadius,
            PhysicsMaterial.Default, BodyFlags.Sensor);
        world.SetTag(saucer, Tag(TablePart.Saucer, 0));
        AddSensorBox(PinballTable.RampEntrance, PinballTable.RampSensorHalfExtents, TablePart.RampEntrance, 0,
            BothLayers);
        AddSensorBox(PinballTable.RampMade, PinballTable.RampSensorHalfExtents, TablePart.RampMade, 0, RampLayer);
        var exit = world.CreateCircle(BodyType.Static, PinballTable.RampExit, PinballTable.SaucerCatchRadius,
            PhysicsMaterial.Default, BodyFlags.Sensor);
        world.SetTag(exit, Tag(TablePart.RampExit, 0));
        world.SetCollisionFilter(exit, RampLayer, PhysicsWorld.AllCategories);
    }

    private void AddChain(ReadOnlySpan<Vector2> points, in PhysicsMaterial material, ushort layer)
    {
        var body = world.CreatePolyline(points, material);
        world.SetTag(body, Tag(TablePart.Wall, 0));
        world.SetCollisionFilter(body, layer, PhysicsWorld.AllCategories);
    }

    private void AddSegment(Vector2 start, Vector2 end, in PhysicsMaterial material, TablePart part, int index,
        ushort layer)
    {
        var body = world.CreateSegment(start, end, material);
        world.SetTag(body, Tag(part, index));
        world.SetCollisionFilter(body, layer, PhysicsWorld.AllCategories);
    }

    private void AddSensorBox(Vector2 center, Vector2 halfExtents, TablePart part, int index, ushort layer)
    {
        var body = world.CreateBox(BodyType.Static, center, halfExtents, 0f, PhysicsMaterial.Default,
            BodyFlags.Sensor);
        world.SetTag(body, Tag(part, index));
        world.SetCollisionFilter(body, layer, PhysicsWorld.AllCategories);
    }

    private bool ServeBall(bool fresh)
    {
        var slot = FreeSlot();
        if (slot < 0)
        {
            return false;
        }

        var body = world.CreateCircle(BodyType.Dynamic, PinballTable.LanePlunger, PinballTable.BallRadius,
            BallMaterial, BodyFlags.Bullet);
        world.SetTag(body, Tag(TablePart.Ball, slot));
        world.SetCollisionFilter(body, PlayfieldLayer, PlayfieldLayer);
        world.SetDamping(body, BallLinearDamping, BallAngularDamping);
        ref var ball = ref balls[slot];
        ball = default;
        ball.Body = body;
        ball.Id = nextBallId++;
        ball.State = BallState.Lane;
        ball.Layer = BallLayer.Playfield;
        ball.Position = PinballTable.LanePlunger;
        ball.Previous = PinballTable.LanePlunger;
        if (!fresh)
        {
            return true;
        }

        freshBall = true;
        SkillLane = random.Next(PinballTable.TopLaneCount);
        return true;
    }

    private bool LaunchWaiting(float speed, bool player)
    {
        var slot = player ? WaitingSlot() : LaneSlot();
        if (slot < 0)
        {
            return false;
        }

        ref var ball = ref balls[slot];
        ball.Velocity = new Vector2(0f, -speed);
        ball.PlayerLaunched = player;
        world.SetVelocity(ball.Body, ball.Velocity);
        Emit(PinballEventKind.Launch, ball.Position, (int)speed, player ? 1 : 0);
        return true;
    }

    private void RemoveBall(int slot)
    {
        world.DestroyBody(balls[slot].Body);
        balls[slot] = default;
    }

    private int FreeSlot()
    {
        for (var slot = 0; slot < MaxBalls; slot++)
        {
            if (balls[slot].State == BallState.None)
            {
                return slot;
            }
        }

        return -1;
    }

    private void HoldBody(int body)
    {
        world.SetTransform(body, PinballTable.Saucer, 0f);
        world.SetVelocity(body, Vector2.Zero);
        world.SetAngularVelocity(body, 0f);
        world.SetGravityScale(body, 0f);
        world.SetCollisionFilter(body, NoLayer, NoLayer);
    }

    private void ReleaseBody(int body, Vector2 velocity)
    {
        world.SetGravityScale(body, 1f);
        world.SetCollisionFilter(body, PlayfieldLayer, PlayfieldMask);
        world.SetVelocity(body, velocity);
    }

    private void SetLayer(int slot, BallLayer layer)
    {
        ref var ball = ref balls[slot];
        ball.Layer = layer;
        if (layer == BallLayer.Ramp)
        {
            world.SetCollisionFilter(ball.Body, RampLayer, RampLayer);
            return;
        }

        world.SetCollisionFilter(ball.Body, PlayfieldLayer, PlayfieldMask);
    }

    private void DropTargetBody(int index)
    {
        world.SetCollisionFilter(targetBodies[index], PlayfieldLayer, NoLayer);
    }

    private void RaiseTargets()
    {
        var blocked = false;
        for (var target = 0; target < PinballTable.TargetCount; target++)
        {
            if (!targetDown[target])
            {
                continue;
            }

            if (BallNear(PinballTable.Targets[target]))
            {
                blocked = true;
                continue;
            }

            targetDown[target] = false;
            world.SetCollisionFilter(targetBodies[target], PlayfieldLayer, PhysicsWorld.AllCategories);
        }

        bankResetPending = blocked;
        bankResetSeconds = blocked ? RaiseRetrySeconds : 0f;
    }

    private bool BallNear(Vector2 point)
    {
        for (var slot = 0; slot < MaxBalls; slot++)
        {
            ref readonly var ball = ref balls[slot];
            if (ball.State == BallState.None || ball.Layer != BallLayer.Playfield)
            {
                continue;
            }

            if (Vector2.DistanceSquared(ball.Position, point) < TargetClearance * TargetClearance)
            {
                return true;
            }
        }

        return false;
    }

    private void Steer()
    {
        for (var flipper = 0; flipper < PinballTable.FlipperCount; flipper++)
        {
            autopilotHold[flipper] -= PhysicsWorld.StepSeconds;
        }

        for (var slot = 0; slot < MaxBalls; slot++)
        {
            ref readonly var ball = ref balls[slot];
            if (ball.State != BallState.Rolling || ball.Layer != BallLayer.Playfield || ball.Velocity.Y < -3f)
            {
                continue;
            }

            var position = ball.Position;
            if (position.Y < 9.9f || position.Y > 10.95f)
            {
                continue;
            }

            if (position.X > 1.85f && position.X < 2.6f)
            {
                autopilotHold[PinballTable.LeftFlipper] = AutopilotHoldSeconds;
            }

            if (position.X > 2.8f && position.X < 3.55f)
            {
                autopilotHold[PinballTable.RightFlipper] = AutopilotHoldSeconds;
            }
        }
    }

    private void ApplyFlippers()
    {
        var active = Phase != PinballPhase.GameOver && !Tilted;
        var left = active && (Autopilot ? autopilotHold[PinballTable.LeftFlipper] > 0f : leftHeld);
        var right = active && (Autopilot ? autopilotHold[PinballTable.RightFlipper] > 0f : rightHeld);
        world.SetHingeMotor(flipperHinges[PinballTable.LeftFlipper], left ? -FlipSpeed : ReturnSpeed,
            left ? FlipTorque : ReturnTorque);
        world.SetHingeMotor(flipperHinges[PinballTable.RightFlipper], right ? FlipSpeed : -ReturnSpeed,
            right ? FlipTorque : ReturnTorque);
    }

    private void StepWorld()
    {
        for (var flipper = 0; flipper < PinballTable.FlipperCount; flipper++)
        {
            flipperPrevious[flipper] = flipperAngles[flipper];
        }

        for (var slot = 0; slot < MaxBalls; slot++)
        {
            balls[slot].Previous = balls[slot].Position;
        }

        world.ClearEvents();
        world.Tick();
        for (var flipper = 0; flipper < PinballTable.FlipperCount; flipper++)
        {
            flipperAngles[flipper] = world.Angle(flipperBodies[flipper]);
        }

        for (var slot = 0; slot < MaxBalls; slot++)
        {
            ref var ball = ref balls[slot];
            if (ball.State is not (BallState.Lane or BallState.Rolling))
            {
                continue;
            }

            ball.Position = world.Position(ball.Body);
            ball.Velocity = world.Velocity(ball.Body);
        }

        Dispatch();
    }

    private void Dispatch()
    {
        var count = world.EventCount;
        for (var index = 0; index < count; index++)
        {
            var contact = world.Event(index);
            var slot = SlotOf(contact.BodyA);
            var other = contact.BodyB;
            if (slot < 0)
            {
                slot = SlotOf(contact.BodyB);
                other = contact.BodyA;
            }

            if (slot < 0 || balls[slot].State != BallState.Rolling)
            {
                continue;
            }

            var tag = world.Tag(other);
            var part = (TablePart)(tag >> TagShift);
            var partIndex = tag & TagIndexMask;
            switch (contact.Kind)
            {
                case ContactEventKind.Hit:
                    OnHit(slot, part, partIndex, contact);
                    break;
                case ContactEventKind.SensorEnter:
                    OnSensorEnter(slot, part, partIndex);
                    break;
                default:
                    OnSensorExit(slot, part);
                    break;
            }
        }
    }

    private int SlotOf(int body)
    {
        if (!world.IsAlive(body))
        {
            return -1;
        }

        var tag = world.Tag(body);
        if ((TablePart)(tag >> TagShift) != TablePart.Ball)
        {
            return -1;
        }

        var slot = tag & TagIndexMask;
        return slot < MaxBalls && balls[slot].Body == body && balls[slot].State != BallState.None ? slot : -1;
    }

    private void OnHit(int slot, TablePart part, int index, in ContactEvent contact)
    {
        switch (part)
        {
            case TablePart.Bumper:
                KickFromBumper(slot, index);
                return;
            case TablePart.Sling when contact.Impulse >= SlingMinImpulse:
                KickFromSling(slot, index);
                return;
            case TablePart.Target:
                if (Vector2.Dot(balls[slot].Position - PinballTable.Targets[index], PinballTable.TargetFacing[index]) > 0f)
                {
                    HitTarget(index);
                }

                return;
            case TablePart.Flipper when contact.Impulse >= FlipperHitImpulse:
                Emit(PinballEventKind.FlipperHit, contact.Point, 0, index);
                return;
            default:
                return;
        }
    }

    private void OnSensorEnter(int slot, TablePart part, int index)
    {
        ref var ball = ref balls[slot];
        switch (part)
        {
            case TablePart.Spinner when ball.Layer == BallLayer.Playfield:
                Spin(ball.Velocity.Y);
                return;
            case TablePart.TopLane when ball.Layer == BallLayer.Playfield:
                RollTopLane(index);
                return;
            case TablePart.Rollover when ball.Layer == BallLayer.Playfield:
                Rollover(slot, index);
                return;
            case TablePart.Orbit when ball.Layer == BallLayer.Playfield:
                PassOrbit(slot, index);
                return;
            case TablePart.Saucer when ball.Layer == BallLayer.Playfield && ball.SaucerGrace <= 0f:
                CaptureBall(slot);
                return;
            case TablePart.RampEntrance when ball.Layer == BallLayer.Playfield && ball.Velocity.Y < 0f:
                SetLayer(slot, BallLayer.Ramp);
                return;
            case TablePart.RampMade when ball.Layer == BallLayer.Ramp:
                CompleteRamp(ball.Position);
                return;
            case TablePart.RampExit when ball.Layer == BallLayer.Ramp:
                SetLayer(slot, BallLayer.Playfield);
                return;
            default:
                return;
        }
    }

    private void OnSensorExit(int slot, TablePart part)
    {
        ref readonly var ball = ref balls[slot];
        if (part != TablePart.RampEntrance || ball.Layer != BallLayer.Ramp || ball.Velocity.Y <= 0f)
        {
            return;
        }

        SetLayer(slot, BallLayer.Playfield);
    }

    private void KickFromBumper(int slot, int index)
    {
        ref var ball = ref balls[slot];
        bumperFlash[index] = 1f;
        if (Tilted)
        {
            return;
        }

        var offset = ball.Position - PinballTable.Bumpers[index];
        var direction = offset.LengthSquared() > 0.0001f ? Vector2.Normalize(offset) : -Vector2.UnitY;
        var along = Vector2.Dot(ball.Velocity, direction);
        ball.Velocity += direction * (MathF.Max(BumperKick, along) - along);
        world.SetVelocity(ball.Body, ball.Velocity);
        DisarmSkill();
        Bonus += BonusPerBumper;
        Emit(PinballEventKind.Bumper, PinballTable.Bumpers[index] + direction * PinballTable.BumperRadius,
            AddScore(BumperPoints), index);
        Charge(BumperCharge);
        CountMystery();
    }

    private void KickFromSling(int slot, int index)
    {
        ref var ball = ref balls[slot];
        var normal = PinballTable.SlingFaceNormal(index);
        var face = PinballTable.Sling(index);
        if (Vector2.Dot(ball.Position - face[0], normal) <= 0f)
        {
            return;
        }

        slingFlash[index] = 1f;
        if (Tilted)
        {
            return;
        }

        var along = Vector2.Dot(ball.Velocity, normal);
        ball.Velocity += normal * (MathF.Max(SlingKick, along) - along);
        world.SetVelocity(ball.Body, ball.Velocity);
        DisarmSkill();
        Emit(PinballEventKind.Sling, ball.Position - normal * PinballTable.BallRadius, AddScore(SlingPoints), index);
        Charge(SlingCharge);
    }

    private void Rollover(int slot, int index)
    {
        rolloverFlash[index] = 1f;
        if (Tilted || balls[slot].Kicked)
        {
            return;
        }

        if (index == PinballTable.LeftOutlane && KickbackLit && balls[slot].Velocity.Y > 0f)
        {
            FireKickback(slot);
            return;
        }

        var outlane = index == PinballTable.LeftOutlane || index == PinballTable.RightOutlane;
        Bonus += BonusPerLane;
        Emit(outlane ? PinballEventKind.Outlane : PinballEventKind.Inlane, PinballTable.Rollovers[index],
            AddScore(outlane ? OutlanePoints : InlanePoints), index);
        Charge(SwitchCharge);
        if (!outlane)
        {
            LightInlane(index);
        }
    }

    private void Spin(float verticalSpeed)
    {
        var spin = -verticalSpeed * SpinPerSpeed;
        if (MathF.Abs(spin) > MathF.Abs(spinnerSpeed))
        {
            spinnerSpeed = spin;
        }

        DisarmSkill();
    }

    private void AdvanceSpinner(float seconds)
    {
        if (MathF.Abs(spinnerSpeed) < SpinnerStopSpeed)
        {
            spinnerSpeed = 0f;
            return;
        }

        SpinnerAngle += spinnerSpeed * seconds;
        spinnerSpeed *= MathF.Max(0f, 1f - SpinnerFriction * seconds);
        var halfTurns = (int)MathF.Floor(SpinnerAngle / MathF.PI);
        if (halfTurns == spinnerHalfTurns)
        {
            return;
        }

        spinnerHalfTurns = halfTurns;
        if (Tilted)
        {
            return;
        }

        Emit(PinballEventKind.Spinner, PinballTable.Spinner, AddScore(SpinnerPoints), halfTurns);
        Charge(SpinnerCharge);
    }

    private void TrackBalls(float seconds)
    {
        for (var slot = 0; slot < MaxBalls; slot++)
        {
            ref var ball = ref balls[slot];
            switch (ball.State)
            {
                case BallState.Lane:
                    if (!PinballTable.InLane(ball.Position))
                    {
                        LeaveLane(slot);
                    }

                    continue;
                case BallState.Rolling:
                    if (OutOfPlay(ball.Position))
                    {
                        DrainBall(slot);
                        continue;
                    }

                    GuideKick(ref ball);
                    WatchStill(ref ball, seconds);
                    continue;
                default:
                    continue;
            }
        }
    }

    private static bool OutOfPlay(Vector2 position) =>
        position.Y > PinballTable.DrainY || position.X < -OutOfBoundsMargin ||
        position.X > PinballTable.Width + OutOfBoundsMargin || position.Y < -OutOfBoundsMargin;

    private void LeaveLane(int slot)
    {
        ref var ball = ref balls[slot];
        ball.State = BallState.Rolling;
        world.SetCollisionFilter(ball.Body, PlayfieldLayer, PlayfieldMask);
        if (!freshBall)
        {
            return;
        }

        freshBall = false;
        if (Attract)
        {
            return;
        }

        BallSaveLeft = BallSaveSeconds;
        if (!ball.PlayerLaunched)
        {
            return;
        }

        SkillArmed = true;
        SkillSeconds = SkillWindowSeconds;
    }

    private void WatchStill(ref PinballBall ball, float seconds)
    {
        if (ball.Velocity.LengthSquared() > StillSpeed * StillSpeed || ball.Layer != BallLayer.Playfield ||
            ball.Position.Y > StillKickHeight)
        {
            ball.StillSeconds = 0f;
            return;
        }

        ball.StillSeconds += seconds;
        if (ball.StillSeconds < StillKickSeconds)
        {
            return;
        }

        ball.StillSeconds = 0f;
        ball.Velocity = new Vector2(random.Range(-1.5f, 1.5f), -2.5f);
        world.SetVelocity(ball.Body, ball.Velocity);
    }
}
