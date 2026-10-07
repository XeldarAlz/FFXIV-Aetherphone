using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Framework.Physics;

namespace Aetherphone.Apps.Games.Pegfall;

internal enum PegKind : byte
{
    Blue,
    Orange,
    Green,
}

internal enum PegPower : byte
{
    None,
    Multiball,
    Magnet,
}

internal enum PegState : byte
{
    Idle,
    Lit,
    Cleared,
}

internal enum PegfallPhase : byte
{
    Aiming,
    Flying,
    Clearing,
    Over,
}

internal enum PegfallEventKind : byte
{
    Fire,
    PegHit,
    PegRebound,
    PegCleared,
    Multiball,
    Magnet,
    FreeBall,
    Drain,
    LastOrange,
    Fever,
    FeverBucket,
    ShotEnd,
    BallBonus,
    Won,
    Lost,
    Unstuck,
}

internal readonly struct PegfallEvent
{
    public readonly PegfallEventKind Kind;
    public readonly Vector2 Position;
    public readonly int Value;
    public readonly int Peg;
    public readonly bool TierUp;

    public PegfallEvent(PegfallEventKind kind, Vector2 position, int value = 0, int peg = -1, bool tierUp = false)
    {
        Kind = kind;
        Position = position;
        Value = value;
        Peg = peg;
        TierUp = tierUp;
    }
}

internal sealed class PegfallBoard
{
    public const float Width = 10f;
    public const float Height = 15f;
    public const float BallRadius = 0.17f;
    public const float PegRadius = 0.2f;
    public const int StartingBalls = 10;
    public const int MaxBalls = 3;
    public const int MaxPegs = 128;
    public const int MaxOranges = 25;
    public const int GreenPegs = 2;
    public const int MinimumPegsForGreens = 20;
    public const float OrangeShare = 0.4f;
    public const float LaunchSpeed = 10.5f;
    public const float MuzzleLength = 0.62f;
    public const float MinAimY = 0.12f;
    public const float BucketTop = 14.3f;
    public const float BucketInnerHalfWidth = 0.95f;
    public const float RimHalfWidth = 0.1f;
    public const float RimHalfHeight = 0.32f;
    public const float BucketTravel = 3.7f;
    public const float BucketBaseSpeed = 0.8f;
    public const float DrainY = Height + 0.6f;
    public const float MagnetSeconds = 3f;
    public const float BallRestitution = 0.62f;
    public const int BluePoints = 10;
    public const int OrangePoints = 100;
    public const int GreenPoints = 10;
    public const int UnusedBallPoints = 5000;
    public const int PreviewStepsPerPoint = 2;
    public const int EventCapacity = 96;
    private const float MagnetAcceleration = 12f;
    private const float StuckSpeed = 0.5f;
    private const float StuckSeconds = 1.4f;
    private const float StuckReach = 0.08f;
    private const float ClearInterval = 0.05f;
    private const float SweepInterval = 0.022f;
    private const float DividerHalfWidth = 0.06f;
    private const float DividerHalfHeight = 0.55f;
    private const float WallOverhang = 1.5f;
    private const float ParkedY = Height + 40f;
    private const float InverseStep = 1f / PhysicsWorld.StepSeconds;
    private const float MaxCatchUpSeconds = 0.1f;
    private const int MaxTouching = 8;
    private const int BodyCapacity = 160;
    public static readonly Vector2 Launcher = new(Width * 0.5f, 0.9f);
    public static readonly Vector2 Gravity = new(0f, 7.5f);
    private static readonly int[] FeverBonuses = { 10000, 25000, 50000, 25000, 10000 };
    private static readonly PhysicsMaterial BallMaterial = new(1f, BallRestitution, 0.08f);
    private static readonly PhysicsMaterial PegMaterial = new(1f, BallRestitution, 0.08f);
    private static readonly PhysicsMaterial WallMaterial = new(1f, 0.55f, 0.05f);
    private static readonly Vector2 RimHalfExtents = new(RimHalfWidth, RimHalfHeight);

    private readonly PhysicsWorld world = new(BodyCapacity, 512, 4, 16, 128);
    private readonly int[] pegBodies = new int[MaxPegs];
    private readonly PegKind[] pegKinds = new PegKind[MaxPegs];
    private readonly PegPower[] pegPowers = new PegPower[MaxPegs];
    private readonly PegState[] pegStates = new PegState[MaxPegs];
    private readonly Vector2[] pegHomes = new Vector2[MaxPegs];
    private readonly int[] pegRings = new int[MaxPegs];
    private readonly float[] pegAngles = new float[MaxPegs];
    private readonly float[] pegLitTimes = new float[MaxPegs];
    private readonly int[] shuffle = new int[MaxPegs];
    private readonly int[] litOrder = new int[MaxPegs];
    private readonly int[] ballBodies = new int[MaxBalls];
    private readonly Vector2[] ballPrevious = new Vector2[MaxBalls];
    private readonly float[] ballStuck = new float[MaxBalls];
    private readonly PegfallEvent[] events = new PegfallEvent[EventCapacity];
    private PegLayout layout;
    private GameRandom random;
    private FixedStepClock clock = new(PhysicsWorld.StepSeconds, MaxCatchUpSeconds);
    private ComboMeter combo = ComboMeter.Untimed();
    private int pegCount;
    private int ballCount;
    private int litCount;
    private int clearCursor;
    private int sweepCursor;
    private int eventCount;
    private int leftRim;
    private int rightRim;
    private float elapsed;
    private float clearTimer;
    private float magnetSeconds;
    private float bucketX;
    private float bucketPrevious;
    private bool dividersPlaced;

    public PegfallBoard()
    {
        world.Gravity = Gravity;
        Load(PegfallLevels.Get(1), GameRandom.FromSeed(1));
    }

    public PegfallPhase Phase { get; private set; }

    public int Score { get; private set; }

    public int ShotScore { get; private set; }

    public int BestShot { get; private set; }

    public int BallsLeft { get; private set; }

    public int OrangeTotal { get; private set; }

    public int OrangeLeft { get; private set; }

    public int PegsHit { get; private set; }

    public int Shots { get; private set; }

    public int Catches { get; private set; }

    public int BallBonus { get; private set; }

    public bool Fever { get; private set; }

    public int FeverSlot { get; private set; } = -1;

    public bool Won { get; private set; }

    public ComboMeter Combo => combo;

    public int Multiplier => combo.Multiplier;

    public int PegCount => pegCount;

    public int BallCount => ballCount;

    public int EventCount => eventCount;

    public float Elapsed => elapsed;

    public float Alpha => clock.Alpha;

    public bool MagnetActive => magnetSeconds > 0f;

    public float MagnetFraction => Math.Clamp(magnetSeconds / MagnetSeconds, 0f, 1f);

    public int TwoStarScore => layout.TwoStarScore;

    public int ThreeStarScore => layout.ThreeStarScore;

    public bool CanFire => Phase == PegfallPhase.Aiming && BallsLeft > 0;

    public int Stars
    {
        get
        {
            if (!Won)
            {
                return 0;
            }

            if (Score >= layout.ThreeStarScore)
            {
                return 3;
            }

            return Score >= layout.TwoStarScore ? 2 : 1;
        }
    }

    public static int FeverSlotCount => FeverBonuses.Length;

    public static int FeverBonus(int slot) => FeverBonuses[Math.Clamp(slot, 0, FeverBonuses.Length - 1)];

    public static float FeverSlotWidth => Width / FeverBonuses.Length;

    public static Vector2 ClampAim(Vector2 direction)
    {
        if (direction.LengthSquared() < 0.000001f)
        {
            return Vector2.UnitY;
        }

        var unit = Vector2.Normalize(direction);
        if (unit.Y >= MinAimY)
        {
            return unit;
        }

        var side = unit.X < 0f ? -1f : 1f;
        return new Vector2(side * MathF.Sqrt(1f - MinAimY * MinAimY), MinAimY);
    }

    public static Vector2 Muzzle(Vector2 aim) => Launcher + aim * MuzzleLength;

    public ref readonly PegfallEvent Event(int index) => ref events[index];

    public void ClearEvents()
    {
        eventCount = 0;
    }

    public PegKind KindOf(int peg) => pegKinds[peg];

    public PegPower PowerOf(int peg) => pegPowers[peg];

    public PegState StateOf(int peg) => pegStates[peg];

    public bool Rotates(int peg) => pegRings[peg] >= 0;

    public float LitAge(int peg) => elapsed - pegLitTimes[peg];

    public Vector2 PegPosition(int peg) => pegRings[peg] >= 0 ? RingPoint(peg, elapsed) : pegHomes[peg];

    public Vector2 RenderPegPosition(int peg) =>
        pegRings[peg] >= 0 ? RingPoint(peg, RenderTime) : pegHomes[peg];

    public int RingCount => layout.Rings.Length;

    public PegRing Ring(int ring) => layout.Rings[ring];

    public Vector2 BallPosition(int ball) => world.Position(ballBodies[ball]);

    public Vector2 BallVelocity(int ball) => world.Velocity(ballBodies[ball]);

    public Vector2 RenderBallPosition(int ball) =>
        Vector2.Lerp(ballPrevious[ball], world.Position(ballBodies[ball]), clock.Alpha);

    public float RenderBucketX => bucketPrevious + (bucketX - bucketPrevious) * clock.Alpha;

    public float BucketX => bucketX;

    private float RenderTime => elapsed + (clock.Alpha - 1f) * PhysicsWorld.StepSeconds;

    public void Load(in PegLayout pegLayout, GameRandom seededRandom)
    {
        layout = pegLayout;
        random = seededRandom;
        world.Clear();
        Span<Vector2> walls = stackalloc Vector2[4];
        walls[0] = new Vector2(0f, Height + WallOverhang);
        walls[1] = Vector2.Zero;
        walls[2] = new Vector2(Width, 0f);
        walls[3] = new Vector2(Width, Height + WallOverhang);
        world.CreatePolyline(walls, WallMaterial);
        pegCount = Math.Min(layout.Count, MaxPegs);
        for (var peg = 0; peg < pegCount; peg++)
        {
            var spot = layout.Pegs[peg];
            pegHomes[peg] = spot.Position;
            pegRings[peg] = spot.Ring;
            pegAngles[peg] = spot.Angle;
            pegKinds[peg] = PegKind.Blue;
            pegPowers[peg] = PegPower.None;
            pegStates[peg] = PegState.Idle;
            pegLitTimes[peg] = 0f;
            var type = spot.Rotates ? BodyType.Kinematic : BodyType.Static;
            var body = world.CreateCircle(type, spot.Position, PegRadius, PegMaterial);
            world.SetTag(body, peg + 1);
            pegBodies[peg] = body;
        }

        AssignColors();
        elapsed = 0f;
        bucketX = BucketCenterAt(0f);
        bucketPrevious = bucketX;
        leftRim = world.CreateBox(BodyType.Kinematic, RimCenter(bucketX, -1f), RimHalfExtents, 0f, WallMaterial);
        rightRim = world.CreateBox(BodyType.Kinematic, RimCenter(bucketX, 1f), RimHalfExtents, 0f, WallMaterial);
        clock.Reset();
        combo = ComboMeter.Untimed();
        ballCount = 0;
        litCount = 0;
        clearCursor = 0;
        sweepCursor = 0;
        eventCount = 0;
        clearTimer = 0f;
        magnetSeconds = 0f;
        dividersPlaced = false;
        BallsLeft = StartingBalls;
        Score = 0;
        ShotScore = 0;
        BestShot = 0;
        PegsHit = 0;
        Shots = 0;
        Catches = 0;
        BallBonus = 0;
        Fever = false;
        FeverSlot = -1;
        Won = false;
        OrangeLeft = OrangeTotal;
        Phase = PegfallPhase.Aiming;
    }

    public bool Fire(Vector2 direction)
    {
        if (!CanFire)
        {
            return false;
        }

        var aim = ClampAim(direction);
        var muzzle = Muzzle(aim);
        BallsLeft--;
        Shots++;
        ShotScore = 0;
        SpawnBall(muzzle, aim * LaunchSpeed);
        Phase = PegfallPhase.Flying;
        Push(new PegfallEvent(PegfallEventKind.Fire, muzzle));
        return true;
    }

    public void Step(float deltaSeconds)
    {
        if (Phase == PegfallPhase.Over)
        {
            return;
        }

        var ticks = clock.Advance(deltaSeconds);
        for (var tick = 0; tick < ticks; tick++)
        {
            Tick();
            if (Phase == PegfallPhase.Over)
            {
                return;
            }
        }
    }

    public int PreviewPath(Vector2 direction, Span<Vector2> path, out bool bounced, out Vector2 bounceStart,
        out Vector2 bounceVelocity)
    {
        var aim = ClampAim(direction);
        var previous = Muzzle(aim);
        world.PredictPath(previous, aim * LaunchSpeed, 1f, 0f, PreviewStepsPerPoint, path);
        for (var index = 0; index < path.Length; index++)
        {
            var point = path[index];
            if (TryPreviewContact(point, out var normal))
            {
                var velocity = (point - previous) / (PreviewStepsPerPoint * PhysicsWorld.StepSeconds);
                bounced = true;
                bounceStart = point;
                bounceVelocity = velocity - (1f + BallRestitution) * Vector2.Dot(velocity, normal) * normal;
                return index + 1;
            }

            previous = point;
        }

        bounced = false;
        bounceStart = Vector2.Zero;
        bounceVelocity = Vector2.Zero;
        return path.Length;
    }

    public void PreviewBounce(Vector2 start, Vector2 velocity, Span<Vector2> path)
    {
        world.PredictPath(start, velocity, 1f, 0f, PreviewStepsPerPoint, path);
    }

    private void Tick()
    {
        var next = elapsed + PhysicsWorld.StepSeconds;
        DriveKinematics(next);
        for (var ball = 0; ball < ballCount; ball++)
        {
            ballPrevious[ball] = world.Position(ballBodies[ball]);
        }

        if (magnetSeconds > 0f)
        {
            ApplyMagnet();
        }

        world.ClearEvents();
        world.Tick();
        elapsed = next;
        HandleContacts();
        UpdateBalls();
        switch (Phase)
        {
            case PegfallPhase.Flying when ballCount == 0:
                EndShot();
                return;
            case PegfallPhase.Clearing:
                AdvanceClearing();
                return;
            default:
                return;
        }
    }

    private void DriveKinematics(float next)
    {
        for (var peg = 0; peg < pegCount; peg++)
        {
            if (pegRings[peg] < 0 || pegStates[peg] == PegState.Cleared)
            {
                continue;
            }

            var body = pegBodies[peg];
            world.SetVelocity(body, (RingPoint(peg, next) - world.Position(body)) * InverseStep);
        }

        bucketPrevious = bucketX;
        if (Fever)
        {
            Park(leftRim);
            Park(rightRim);
            return;
        }

        bucketX = BucketCenterAt(next);
        world.SetVelocity(leftRim, (RimCenter(bucketX, -1f) - world.Position(leftRim)) * InverseStep);
        world.SetVelocity(rightRim, (RimCenter(bucketX, 1f) - world.Position(rightRim)) * InverseStep);
    }

    private void Park(int rim)
    {
        var position = world.Position(rim);
        world.SetVelocity(rim, (new Vector2(position.X, ParkedY) - position) * InverseStep);
    }

    private void ApplyMagnet()
    {
        magnetSeconds = MathF.Max(0f, magnetSeconds - PhysicsWorld.StepSeconds);
        for (var ball = 0; ball < ballCount; ball++)
        {
            var body = ballBodies[ball];
            var position = world.Position(body);
            var target = NearestIdleOrange(position);
            if (target < 0)
            {
                return;
            }

            var offset = PegPosition(target) - position;
            var distance = offset.Length();
            if (distance < 0.0001f)
            {
                continue;
            }

            var impulse = offset / distance * (MagnetAcceleration * world.Mass(body) * PhysicsWorld.StepSeconds);
            world.ApplyImpulse(body, impulse);
        }
    }

    private void HandleContacts()
    {
        for (var index = 0; index < world.EventCount; index++)
        {
            ref readonly var contact = ref world.Event(index);
            if (contact.Kind != ContactEventKind.Hit)
            {
                continue;
            }

            var ball = BallSlotOf(contact.BodyA);
            var other = contact.BodyB;
            if (ball < 0)
            {
                ball = BallSlotOf(contact.BodyB);
                other = contact.BodyA;
            }

            if (ball < 0)
            {
                continue;
            }

            var tag = world.Tag(other);
            if (tag <= 0)
            {
                continue;
            }

            HitPeg(tag - 1, ball);
        }
    }

    private void HitPeg(int peg, int ball)
    {
        var state = pegStates[peg];
        if (state == PegState.Cleared)
        {
            return;
        }

        var position = PegPosition(peg);
        if (state == PegState.Lit)
        {
            Push(new PegfallEvent(PegfallEventKind.PegRebound, position, 0, peg));
            return;
        }

        pegStates[peg] = PegState.Lit;
        pegLitTimes[peg] = elapsed;
        litOrder[litCount] = peg;
        litCount++;
        PegsHit++;
        var kind = pegKinds[peg];
        var multiplierBefore = combo.Multiplier;
        if (kind == PegKind.Orange)
        {
            combo.Hit();
            OrangeLeft--;
        }

        var points = PointsFor(kind) * combo.Multiplier;
        Score += points;
        ShotScore += points;
        Push(new PegfallEvent(PegfallEventKind.PegHit, position, points, peg, combo.Multiplier > multiplierBefore));
        if (kind == PegKind.Green)
        {
            TriggerPower(peg, ball, position);
        }

        if (kind != PegKind.Orange)
        {
            return;
        }

        if (OrangeLeft == 0)
        {
            StartFever(position);
            return;
        }

        if (OrangeLeft == 1)
        {
            var last = NearestIdleOrange(position);
            Push(new PegfallEvent(PegfallEventKind.LastOrange, last >= 0 ? PegPosition(last) : position, 0, last));
        }
    }

    private static int PointsFor(PegKind kind) => kind switch
    {
        PegKind.Orange => OrangePoints,
        PegKind.Green => GreenPoints,
        _ => BluePoints,
    };

    private void TriggerPower(int peg, int ball, Vector2 position)
    {
        switch (pegPowers[peg])
        {
            case PegPower.Multiball:
            {
                if (ballCount < MaxBalls)
                {
                    var body = ballBodies[ball];
                    var source = world.Position(body);
                    var velocity = world.Velocity(body);
                    var spawnX = Math.Clamp(2f * position.X - source.X, BallRadius * 2f, Width - BallRadius * 2f);
                    SpawnBall(new Vector2(spawnX, source.Y), new Vector2(-velocity.X, velocity.Y));
                }

                Push(new PegfallEvent(PegfallEventKind.Multiball, position, 0, peg));
                return;
            }
            case PegPower.Magnet:
                magnetSeconds = MagnetSeconds;
                Push(new PegfallEvent(PegfallEventKind.Magnet, position, 0, peg));
                return;
            default:
                return;
        }
    }

    private void StartFever(Vector2 position)
    {
        Fever = true;
        Push(new PegfallEvent(PegfallEventKind.Fever, position));
        if (dividersPlaced)
        {
            return;
        }

        dividersPlaced = true;
        var halfExtents = new Vector2(DividerHalfWidth, DividerHalfHeight);
        for (var divider = 1; divider < FeverBonuses.Length; divider++)
        {
            var center = new Vector2(divider * FeverSlotWidth, BucketTop + DividerHalfHeight);
            world.CreateBox(BodyType.Static, center, halfExtents, 0f, WallMaterial);
        }
    }

    private void UpdateBalls()
    {
        for (var ball = ballCount - 1; ball >= 0; ball--)
        {
            var body = ballBodies[ball];
            var position = world.Position(body);
            var previous = ballPrevious[ball];
            var crossed = previous.Y < BucketTop && position.Y >= BucketTop;
            if (Fever && crossed && Phase == PegfallPhase.Flying)
            {
                LandFever(position);
                return;
            }

            if (!Fever && crossed && MathF.Abs(position.X - bucketX) <= BucketInnerHalfWidth)
            {
                Catches++;
                BallsLeft++;
                Push(new PegfallEvent(PegfallEventKind.FreeBall, new Vector2(bucketX, BucketTop)));
                RemoveBall(ball);
                continue;
            }

            if (position.Y > DrainY)
            {
                Push(new PegfallEvent(PegfallEventKind.Drain, new Vector2(position.X, Height)));
                RemoveBall(ball);
                continue;
            }

            UpdateStuck(ball, body, position);
        }
    }

    private void LandFever(Vector2 position)
    {
        var slot = Math.Clamp((int)(position.X / FeverSlotWidth), 0, FeverBonuses.Length - 1);
        var bonus = FeverBonuses[slot];
        FeverSlot = slot;
        Score += bonus;
        ShotScore += bonus;
        Push(new PegfallEvent(PegfallEventKind.FeverBucket, position, bonus, slot));
        while (ballCount > 0)
        {
            RemoveBall(ballCount - 1);
        }

        BeginClearing();
    }

    private void UpdateStuck(int ball, int body, Vector2 position)
    {
        if (world.Velocity(body).LengthSquared() > StuckSpeed * StuckSpeed)
        {
            ballStuck[ball] = 0f;
            return;
        }

        ballStuck[ball] += PhysicsWorld.StepSeconds;
        if (ballStuck[ball] < StuckSeconds)
        {
            return;
        }

        ballStuck[ball] = 0f;
        Span<int> touching = stackalloc int[MaxTouching];
        var count = world.OverlapCircle(position, BallRadius + StuckReach, touching);
        var freed = false;
        for (var index = 0; index < count; index++)
        {
            var tag = world.Tag(touching[index]);
            if (tag <= 0 || pegStates[tag - 1] == PegState.Cleared)
            {
                continue;
            }

            HitPeg(tag - 1, ball);
            ClearPeg(tag - 1);
            freed = true;
        }

        if (!freed)
        {
            world.ApplyImpulse(body, new Vector2(random.Sign() * 0.8f, -1.5f) * world.Mass(body));
        }

        Push(new PegfallEvent(PegfallEventKind.Unstuck, position));
    }

    private void EndShot()
    {
        Push(new PegfallEvent(PegfallEventKind.ShotEnd, new Vector2(Width * 0.5f, BucketTop), ShotScore));
        BeginClearing();
    }

    private void BeginClearing()
    {
        BestShot = Math.Max(BestShot, ShotScore);
        Phase = PegfallPhase.Clearing;
        clearCursor = 0;
        sweepCursor = 0;
        clearTimer = ClearInterval;
        magnetSeconds = 0f;
    }

    private void AdvanceClearing()
    {
        clearTimer -= PhysicsWorld.StepSeconds;
        while (clearTimer <= 0f)
        {
            if (!PopNext())
            {
                FinishClearing();
                return;
            }

            clearTimer += clearCursor >= litCount ? SweepInterval : ClearInterval;
        }
    }

    private bool PopNext()
    {
        while (clearCursor < litCount)
        {
            var peg = litOrder[clearCursor];
            clearCursor++;
            if (pegStates[peg] != PegState.Lit)
            {
                continue;
            }

            ClearPeg(peg);
            return true;
        }

        if (!Fever)
        {
            return false;
        }

        while (sweepCursor < pegCount)
        {
            var peg = sweepCursor;
            sweepCursor++;
            if (pegStates[peg] == PegState.Cleared)
            {
                continue;
            }

            ClearPeg(peg);
            return true;
        }

        return false;
    }

    private void FinishClearing()
    {
        litCount = 0;
        clearCursor = 0;
        if (Fever)
        {
            FinishLevel(true);
            return;
        }

        if (BallsLeft <= 0)
        {
            FinishLevel(false);
            return;
        }

        Phase = PegfallPhase.Aiming;
    }

    private void FinishLevel(bool won)
    {
        Won = won;
        Phase = PegfallPhase.Over;
        if (!won)
        {
            Push(new PegfallEvent(PegfallEventKind.Lost, new Vector2(Width * 0.5f, Height * 0.5f)));
            return;
        }

        BallBonus = BallsLeft * UnusedBallPoints;
        Score += BallBonus;
        if (BallBonus > 0)
        {
            Push(new PegfallEvent(PegfallEventKind.BallBonus, Launcher, BallBonus, BallsLeft));
        }

        Push(new PegfallEvent(PegfallEventKind.Won, new Vector2(Width * 0.5f, Height * 0.5f), Score));
    }

    private void ClearPeg(int peg)
    {
        var position = PegPosition(peg);
        pegStates[peg] = PegState.Cleared;
        if (pegBodies[peg] >= 0)
        {
            world.DestroyBody(pegBodies[peg]);
            pegBodies[peg] = -1;
        }

        Push(new PegfallEvent(PegfallEventKind.PegCleared, position, 0, peg));
    }

    private void SpawnBall(Vector2 position, Vector2 velocity)
    {
        var body = world.CreateCircle(BodyType.Dynamic, position, BallRadius, BallMaterial, BodyFlags.Bullet);
        world.SetVelocity(body, velocity);
        ballBodies[ballCount] = body;
        ballPrevious[ballCount] = position;
        ballStuck[ballCount] = 0f;
        ballCount++;
    }

    private void RemoveBall(int ball)
    {
        world.DestroyBody(ballBodies[ball]);
        var last = ballCount - 1;
        ballBodies[ball] = ballBodies[last];
        ballPrevious[ball] = ballPrevious[last];
        ballStuck[ball] = ballStuck[last];
        ballCount = last;
    }

    private int BallSlotOf(int body)
    {
        for (var ball = 0; ball < ballCount; ball++)
        {
            if (ballBodies[ball] == body)
            {
                return ball;
            }
        }

        return -1;
    }

    private int NearestIdleOrange(Vector2 position)
    {
        var best = -1;
        var bestDistance = float.MaxValue;
        for (var peg = 0; peg < pegCount; peg++)
        {
            if (pegKinds[peg] != PegKind.Orange || pegStates[peg] != PegState.Idle)
            {
                continue;
            }

            var distance = Vector2.DistanceSquared(PegPosition(peg), position);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            best = peg;
        }

        return best;
    }

    private bool TryPreviewContact(Vector2 point, out Vector2 normal)
    {
        if (point.X < BallRadius)
        {
            normal = Vector2.UnitX;
            return true;
        }

        if (point.X > Width - BallRadius)
        {
            normal = -Vector2.UnitX;
            return true;
        }

        var reach = PegRadius + BallRadius;
        for (var peg = 0; peg < pegCount; peg++)
        {
            if (pegStates[peg] == PegState.Cleared)
            {
                continue;
            }

            var offset = point - PegPosition(peg);
            var distanceSquared = offset.LengthSquared();
            if (distanceSquared >= reach * reach || distanceSquared < 0.000001f)
            {
                continue;
            }

            normal = offset / MathF.Sqrt(distanceSquared);
            return true;
        }

        normal = Vector2.Zero;
        return false;
    }

    private void AssignColors()
    {
        OrangeTotal = Math.Clamp((int)MathF.Round(pegCount * OrangeShare), Math.Min(1, pegCount),
            Math.Min(MaxOranges, pegCount));
        var greens = pegCount >= MinimumPegsForGreens ? GreenPegs : 0;
        for (var peg = 0; peg < pegCount; peg++)
        {
            shuffle[peg] = peg;
        }

        var picks = Math.Min(pegCount, OrangeTotal + greens);
        for (var pick = 0; pick < picks; pick++)
        {
            var swap = pick + random.Next(pegCount - pick);
            (shuffle[pick], shuffle[swap]) = (shuffle[swap], shuffle[pick]);
        }

        for (var pick = 0; pick < OrangeTotal; pick++)
        {
            pegKinds[shuffle[pick]] = PegKind.Orange;
        }

        var firstPower = random.Chance(0.5f) ? PegPower.Multiball : PegPower.Magnet;
        for (var pick = OrangeTotal; pick < picks; pick++)
        {
            var peg = shuffle[pick];
            pegKinds[peg] = PegKind.Green;
            pegPowers[peg] = pick == OrangeTotal ? firstPower : Other(firstPower);
        }
    }

    private static PegPower Other(PegPower power) => power == PegPower.Multiball ? PegPower.Magnet : PegPower.Multiball;

    private Vector2 RingPoint(int peg, float time)
    {
        ref readonly var ring = ref layout.Rings[pegRings[peg]];
        return ring.PointAt(pegAngles[peg] + ring.Speed * time);
    }

    private float BucketCenterAt(float time) =>
        Width * 0.5f + BucketTravel * MathF.Sin(time * BucketBaseSpeed * layout.BucketSpeed);

    private static Vector2 RimCenter(float bucketCenter, float side) =>
        new(bucketCenter + side * (BucketInnerHalfWidth + RimHalfWidth), BucketTop + RimHalfHeight);

    private void Push(in PegfallEvent entry)
    {
        if (eventCount >= events.Length)
        {
            return;
        }

        events[eventCount] = entry;
        eventCount++;
    }
}
