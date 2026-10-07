using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Framework.Physics;

namespace Aetherphone.Apps.Games.Fling;

internal enum FlingPhase : byte
{
    Aiming,
    Flying,
    Over,
}

internal enum FlingEventKind : byte
{
    Launch,
    Split,
    Impact,
    Crack,
    Break,
    GoblinPop,
    BirdGone,
    NextBird,
    BirdBonus,
    Won,
    Lost,
}

internal readonly struct FlingEvent
{
    public readonly FlingEventKind Kind;
    public readonly Vector2 Position;
    public readonly int Value;
    public readonly int Index;
    public readonly FlingMaterial Material;
    public readonly FlingBird Bird;
    public readonly float Size;
    public readonly float Angle;

    public FlingEvent(FlingEventKind kind, Vector2 position, int value = 0, int index = -1,
        FlingMaterial material = FlingMaterial.Wood, FlingBird bird = FlingBird.Normal, float size = 0f,
        float angle = 0f)
    {
        Kind = kind;
        Position = position;
        Value = value;
        Index = index;
        Material = material;
        Bird = bird;
        Size = size;
        Angle = angle;
    }
}

internal sealed class FlingBoard
{
    public const float MaxPull = 1.8f;
    public const float MinPull = 0.35f;
    public const float LaunchSpeed = 18f;
    public const float SplitAngle = 0.22f;
    public const float SplitSpacing = 0.6f;
    private const ushort BirdCategory = 2;
    private const ushort BirdMask = (ushort)(PhysicsWorld.AllCategories & ~BirdCategory);
    public const int MaxPieces = 64;
    public const int MaxFlying = 3;
    public const int PreviewStepsPerPoint = 3;
    public const int PreviewPoints = 20;
    public const int TrailCapacity = 96;
    public const float WoodStrength = 1.9f;
    public const float GlassStrength = 0.75f;
    public const float StoneStrength = 6f;
    public const float GoblinStrength = 1.2f;
    public const float KnightStrength = 2.8f;
    public const float DamageFloor = 0.25f;
    public const float GraceSeconds = 1f;
    public const float WorldLeft = -16f;
    public const float WorldRight = 64f;
    public const float FallLimit = 4f;
    private const float GroundHalfWidth = 40f;
    private const float GroundHalfHeight = 1f;
    private const float GroundCenterX = 24f;
    private const float ImpactThreshold = 0.5f;
    private const float ImpactEventMinimum = 1f;
    private const float MinTurnSeconds = 1.2f;
    private const float MaxTurnSeconds = 9f;
    private const float QuietSeconds = 0.6f;
    private const float SettleSpeed = 0.3f;
    private const float LandedLinearDamping = 0.6f;
    private const float LandedAngularDamping = 2f;
    private const float MaxCatchUpSeconds = 0.1f;
    private const float QueueStart = 1.1f;
    private const float QueueSpacing = 0.85f;
    private const float RetiredX = -2000f;
    private const float RetiredBirdX = -3000f;
    private const float RetiredY = -2000f;
    private const float RetiredSpacing = 10f;
    private const int TrailEveryTicks = 4;
    private const int BodyCapacity = 96;
    public static readonly Vector2 SlingAnchor = new(0f, -2.1f);
    private static readonly PhysicsMaterial WoodMaterial = new(0.6f, 0.05f, 0.7f);
    private static readonly PhysicsMaterial GlassMaterial = new(0.9f, 0.05f, 0.4f);
    private static readonly PhysicsMaterial StoneMaterial = new(2.4f, 0.02f, 0.85f);
    private static readonly PhysicsMaterial RockMaterial = new(1f, 0.05f, 0.9f);
    private static readonly PhysicsMaterial GoblinMaterial = new(0.8f, 0.2f, 0.6f);
    private static readonly PhysicsMaterial NormalBirdMaterial = new(1.4f, 0.3f, 0.6f);
    private static readonly PhysicsMaterial SplitterBirdMaterial = new(1.2f, 0.3f, 0.6f);
    private static readonly PhysicsMaterial HeavyBirdMaterial = new(4.5f, 0.15f, 0.7f);

    private readonly PhysicsWorld world = new(BodyCapacity, 1024, 4, 4, 256);
    private readonly int[] pieceBodies = new int[MaxPieces];
    private readonly FlingPieceKind[] pieceKinds = new FlingPieceKind[MaxPieces];
    private readonly FlingMaterial[] pieceMaterials = new FlingMaterial[MaxPieces];
    private readonly Vector2[] pieceHalves = new Vector2[MaxPieces];
    private readonly float[] pieceDamage = new float[MaxPieces];
    private readonly bool[] pieceAlive = new bool[MaxPieces];
    private readonly Vector2[] piecePrevious = new Vector2[MaxPieces];
    private readonly float[] piecePreviousAngle = new float[MaxPieces];
    private readonly int[] pieceSeeds = new int[MaxPieces];
    private readonly int[] flyingBodies = new int[MaxFlying];
    private readonly FlingBird[] flyingKinds = new FlingBird[MaxFlying];
    private readonly bool[] flyingTouched = new bool[MaxFlying];
    private readonly Vector2[] flyingPrevious = new Vector2[MaxFlying];
    private readonly Vector2[] trail = new Vector2[TrailCapacity];
    private readonly FlingEvent[] events = new FlingEvent[128];
    private FlingLevel level;
    private GameRandom random;
    private FixedStepClock clock = new(PhysicsWorld.StepSeconds, MaxCatchUpSeconds);
    private int pieceCount;
    private int flyingCount;
    private int nextBird;
    private int trailCount;
    private int trailTicks;
    private int eventCount;
    private int retiredBirds;
    private float elapsed;
    private float flightSeconds;
    private float quietSeconds;
    private bool abilityUsed;
    private bool leadTouched;

    public FlingBoard()
    {
        world.ImpactThreshold = ImpactThreshold;
        Load(FlingLevels.Get(1), GameRandom.FromSeed(1));
    }

    public FlingPhase Phase { get; private set; }

    public int Score { get; private set; }

    public int Destroyed { get; private set; }

    public int Popped { get; private set; }

    public int Shots { get; private set; }

    public int BirdBonus { get; private set; }

    public int GoblinsLeft { get; private set; }

    public bool Won { get; private set; }

    public int GoblinCount => level.GoblinCount;

    public int BirdCount => level.Birds.Length;

    public int BirdsLeft => level.Birds.Length - nextBird;

    public float FocusX => level.FocusX;

    public float RightEdge => level.RightEdge;

    public int TwoStarScore => level.TwoStarScore;

    public int ThreeStarScore => level.ThreeStarScore;

    public float Elapsed => elapsed;

    public float FlightSeconds => flightSeconds;

    public float Alpha => clock.Alpha;

    public bool Settling => elapsed < GraceSeconds;

    public bool CanAim => Phase == FlingPhase.Aiming && nextBird < level.Birds.Length;

    public bool AbilityReady =>
        Phase == FlingPhase.Flying && !abilityUsed && !leadTouched && flyingCount > 0 &&
        flyingKinds[0] == FlingBird.Splitter;

    public int Stars
    {
        get
        {
            if (!Won)
            {
                return 0;
            }

            if (Score >= level.ThreeStarScore)
            {
                return 3;
            }

            return Score >= level.TwoStarScore ? 2 : 1;
        }
    }

    public int PieceCount => pieceCount;

    public int FlyingCount => flyingCount;

    public int TrailCount => trailCount;

    public int EventCount => eventCount;

    public static float BirdRadius(FlingBird bird) => bird switch
    {
        FlingBird.Splitter => 0.25f,
        FlingBird.Heavy => 0.42f,
        _ => 0.3f,
    };

    public static float StrengthOf(FlingPieceKind kind, FlingMaterial material) => kind switch
    {
        FlingPieceKind.Goblin => GoblinStrength,
        FlingPieceKind.Knight => KnightStrength,
        _ => material switch
        {
            FlingMaterial.Glass => GlassStrength,
            FlingMaterial.Stone => StoneStrength,
            FlingMaterial.Rock => float.PositiveInfinity,
            _ => WoodStrength,
        },
    };

    public static bool ResolveImpact(float damage, float impulse, float strength, out float newDamage)
    {
        newDamage = damage;
        if (impulse < strength * DamageFloor)
        {
            return false;
        }

        newDamage = damage + impulse / strength;
        return impulse >= strength || newDamage >= 1f;
    }

    public static Vector2 ClampPull(Vector2 pull)
    {
        var length = pull.Length();
        if (length > MaxPull)
        {
            pull *= MaxPull / length;
        }

        var lowest = -SlingAnchor.Y - BirdRadius(FlingBird.Heavy);
        if (pull.Y > lowest)
        {
            pull.Y = lowest;
        }

        return pull;
    }

    public static Vector2 Pouch(Vector2 pull) => SlingAnchor + ClampPull(pull);

    public static Vector2 LaunchVelocity(Vector2 pull) => -ClampPull(pull) / MaxPull * LaunchSpeed;

    public static Vector2 QueuePosition(int queueIndex, FlingBird bird) =>
        new(SlingAnchor.X - QueueStart - queueIndex * QueueSpacing, -BirdRadius(bird));

    public FlingBird BirdAt(int queueIndex) => level.Birds[Math.Min(nextBird + queueIndex, level.Birds.Length - 1)];

    public FlingPieceKind PieceKind(int piece) => pieceKinds[piece];

    public FlingMaterial PieceMaterial(int piece) => pieceMaterials[piece];

    public bool PieceAlive(int piece) => pieceAlive[piece];

    public Vector2 PieceHalfExtents(int piece) => pieceHalves[piece];

    public float PieceDamage(int piece) => pieceDamage[piece];

    public int PieceSeed(int piece) => pieceSeeds[piece];

    public Vector2 PiecePosition(int piece) => world.Position(pieceBodies[piece]);

    public float PieceAngle(int piece) => world.Angle(pieceBodies[piece]);

    public Vector2 RenderPiecePosition(int piece) =>
        Vector2.Lerp(piecePrevious[piece], world.Position(pieceBodies[piece]), clock.Alpha);

    public float RenderPieceAngle(int piece)
    {
        var previous = piecePreviousAngle[piece];
        return previous + (world.Angle(pieceBodies[piece]) - previous) * clock.Alpha;
    }

    public FlingBird FlyingKind(int bird) => flyingKinds[bird];

    public Vector2 BirdPosition(int bird) => world.Position(flyingBodies[bird]);

    public Vector2 BirdVelocity(int bird) => world.Velocity(flyingBodies[bird]);

    public float BirdAngle(int bird) => world.Angle(flyingBodies[bird]);

    public Vector2 RenderBirdPosition(int bird) =>
        Vector2.Lerp(flyingPrevious[bird], world.Position(flyingBodies[bird]), clock.Alpha);

    public Vector2 TrailPoint(int index) => trail[index];

    public ref readonly FlingEvent Event(int index) => ref events[index];

    public void ClearEvents()
    {
        eventCount = 0;
    }

    public void Load(in FlingLevel flingLevel, GameRandom seededRandom)
    {
        level = flingLevel;
        random = seededRandom;
        world.Clear();
        world.CreateBox(BodyType.Static, new Vector2(GroundCenterX, GroundHalfHeight),
            new Vector2(GroundHalfWidth, GroundHalfHeight), 0f, RockMaterial);
        pieceCount = Math.Min(level.Pieces.Length, MaxPieces);
        GoblinsLeft = 0;
        for (var piece = 0; piece < pieceCount; piece++)
        {
            var spec = level.Pieces[piece];
            pieceKinds[piece] = spec.Kind;
            pieceMaterials[piece] = spec.Material;
            pieceHalves[piece] = spec.HalfExtents;
            pieceDamage[piece] = 0f;
            pieceAlive[piece] = true;
            piecePrevious[piece] = spec.Center;
            piecePreviousAngle[piece] = 0f;
            pieceSeeds[piece] = random.Next(int.MaxValue);
            pieceBodies[piece] = CreatePiece(spec);
            world.SetTag(pieceBodies[piece], piece + 1);
            if (spec.IsGoblin)
            {
                GoblinsLeft++;
            }
        }

        clock.Reset();
        flyingCount = 0;
        nextBird = 0;
        trailCount = 0;
        trailTicks = 0;
        eventCount = 0;
        retiredBirds = 0;
        elapsed = 0f;
        flightSeconds = 0f;
        quietSeconds = 0f;
        abilityUsed = false;
        leadTouched = false;
        Score = 0;
        Destroyed = 0;
        Popped = 0;
        Shots = 0;
        BirdBonus = 0;
        Won = false;
        Phase = FlingPhase.Aiming;
    }

    public bool Launch(Vector2 pull)
    {
        var clamped = ClampPull(pull);
        if (!CanAim || clamped.Length() < MinPull)
        {
            return false;
        }

        var bird = level.Birds[nextBird];
        nextBird++;
        Shots++;
        var pouch = SlingAnchor + clamped;
        SpawnBird(bird, pouch, LaunchVelocity(clamped));
        abilityUsed = false;
        leadTouched = false;
        flightSeconds = 0f;
        quietSeconds = 0f;
        trailCount = 0;
        trailTicks = 0;
        Phase = FlingPhase.Flying;
        Push(new FlingEvent(FlingEventKind.Launch, pouch, 0, nextBird - 1, bird: bird));
        return true;
    }

    public bool Ability()
    {
        if (!AbilityReady)
        {
            return false;
        }

        abilityUsed = true;
        var body = flyingBodies[0];
        var position = world.Position(body);
        var velocity = world.Velocity(body);
        Push(new FlingEvent(FlingEventKind.Split, position, 0, 0, bird: FlingBird.Splitter));
        for (var side = -1; side <= 1; side += 2)
        {
            if (flyingCount >= MaxFlying)
            {
                break;
            }

            var angle = side * SplitAngle;
            var turned = new Vector2(velocity.X * MathF.Cos(angle) - velocity.Y * MathF.Sin(angle),
                velocity.X * MathF.Sin(angle) + velocity.Y * MathF.Cos(angle));
            var speed = turned.Length();
            var across = speed > 0.001f ? new Vector2(-turned.Y, turned.X) / speed : Vector2.UnitY;
            SpawnBird(FlingBird.Splitter, position + across * (side * SplitSpacing), turned);
        }

        return true;
    }

    public void PreviewPath(Vector2 pull, Span<Vector2> path)
    {
        var clamped = ClampPull(pull);
        world.PredictPath(SlingAnchor + clamped, LaunchVelocity(clamped), 1f, 0f, PreviewStepsPerPoint, path);
    }

    public void Step(float deltaSeconds)
    {
        if (Phase == FlingPhase.Over)
        {
            return;
        }

        var ticks = clock.Advance(deltaSeconds);
        for (var tick = 0; tick < ticks; tick++)
        {
            Tick();
            if (Phase == FlingPhase.Over)
            {
                return;
            }
        }
    }

    private void Tick()
    {
        for (var piece = 0; piece < pieceCount; piece++)
        {
            if (!pieceAlive[piece])
            {
                continue;
            }

            piecePrevious[piece] = world.Position(pieceBodies[piece]);
            piecePreviousAngle[piece] = world.Angle(pieceBodies[piece]);
        }

        for (var bird = 0; bird < flyingCount; bird++)
        {
            flyingPrevious[bird] = world.Position(flyingBodies[bird]);
        }

        world.ClearEvents();
        world.Tick();
        elapsed += PhysicsWorld.StepSeconds;
        HandleContacts();
        RemoveFallen();
        if (Phase == FlingPhase.Flying)
        {
            flightSeconds += PhysicsWorld.StepSeconds;
            SampleTrail();
            CheckTurnEnd();
            return;
        }

        if (GoblinsLeft == 0)
        {
            Win();
        }
    }

    private void HandleContacts()
    {
        var damaging = elapsed >= GraceSeconds;
        for (var index = 0; index < world.EventCount; index++)
        {
            ref readonly var contact = ref world.Event(index);
            if (contact.Kind != ContactEventKind.Hit)
            {
                continue;
            }

            var birdA = FlyingSlotOf(contact.BodyA);
            var birdB = FlyingSlotOf(contact.BodyB);
            if (birdA >= 0 || birdB >= 0)
            {
                OnBirdContact(birdA >= 0 ? birdA : birdB, contact.Point, contact.Impulse);
            }

            if (!damaging)
            {
                continue;
            }

            Impact(PieceOf(contact.BodyA), contact.Impulse);
            Impact(PieceOf(contact.BodyB), contact.Impulse);
        }
    }

    private void OnBirdContact(int bird, Vector2 point, float impulse)
    {
        if (!flyingTouched[bird])
        {
            flyingTouched[bird] = true;
            world.SetDamping(flyingBodies[bird], LandedLinearDamping, LandedAngularDamping);
        }

        if (bird == 0)
        {
            leadTouched = true;
        }

        if (impulse < ImpactEventMinimum)
        {
            return;
        }

        Push(new FlingEvent(FlingEventKind.Impact, point, (int)(impulse * 10f), bird, bird: flyingKinds[bird]));
    }

    private void Impact(int piece, float impulse)
    {
        if (piece < 0 || !pieceAlive[piece])
        {
            return;
        }

        var kind = pieceKinds[piece];
        var material = pieceMaterials[piece];
        if (kind == FlingPieceKind.Block && material == FlingMaterial.Rock)
        {
            return;
        }

        var before = pieceDamage[piece];
        var broken = ResolveImpact(before, impulse, StrengthOf(kind, material), out var after);
        pieceDamage[piece] = after;
        if (broken)
        {
            Break(piece);
            return;
        }

        if (kind == FlingPieceKind.Block && CrackStage(after) > CrackStage(before))
        {
            Push(new FlingEvent(FlingEventKind.Crack, world.Position(pieceBodies[piece]), 0, piece, material,
                size: pieceHalves[piece].Length()));
        }
    }

    private static int CrackStage(float damage) => damage >= 0.66f ? 2 : damage >= 0.33f ? 1 : 0;

    private void Break(int piece)
    {
        var body = pieceBodies[piece];
        var position = world.Position(body);
        var angle = world.Angle(body);
        pieceAlive[piece] = false;
        Park(body, new Vector2(RetiredX - piece * RetiredSpacing, RetiredY));
        var kind = pieceKinds[piece];
        if (kind == FlingPieceKind.Block)
        {
            var points = FlingLevels.PointsFor(pieceMaterials[piece]);
            Score += points;
            Destroyed++;
            Push(new FlingEvent(FlingEventKind.Break, position, points, piece, pieceMaterials[piece],
                size: pieceHalves[piece].Length(), angle: angle));
            return;
        }

        Score += FlingLevels.GoblinPoints;
        Popped++;
        GoblinsLeft--;
        Push(new FlingEvent(FlingEventKind.GoblinPop, position, FlingLevels.GoblinPoints, piece,
            size: pieceHalves[piece].X));
    }

    private void Park(int body, Vector2 spot)
    {
        world.SetGravityScale(body, 0f);
        world.SetVelocity(body, Vector2.Zero);
        world.SetAngularVelocity(body, 0f);
        world.SetTransform(body, spot, 0f);
    }

    private void RemoveFallen()
    {
        for (var piece = 0; piece < pieceCount; piece++)
        {
            if (!pieceAlive[piece] || OnStage(world.Position(pieceBodies[piece])))
            {
                continue;
            }

            Break(piece);
        }

        for (var bird = flyingCount - 1; bird >= 0; bird--)
        {
            var position = world.Position(flyingBodies[bird]);
            if (OnStage(position))
            {
                continue;
            }

            RemoveBird(bird, position);
        }
    }

    private static bool OnStage(Vector2 position) =>
        position.X >= WorldLeft && position.X <= WorldRight && position.Y <= FallLimit;

    private void SampleTrail()
    {
        if (flyingCount == 0 || leadTouched || trailCount >= TrailCapacity)
        {
            return;
        }

        trailTicks++;
        if (trailTicks < TrailEveryTicks)
        {
            return;
        }

        trailTicks = 0;
        trail[trailCount] = world.Position(flyingBodies[0]);
        trailCount++;
    }

    private void CheckTurnEnd()
    {
        if (flightSeconds < MinTurnSeconds)
        {
            return;
        }

        var moving = false;
        for (var bird = 0; bird < flyingCount && !moving; bird++)
        {
            moving = world.Velocity(flyingBodies[bird]).LengthSquared() > SettleSpeed * SettleSpeed;
        }

        for (var piece = 0; piece < pieceCount && !moving; piece++)
        {
            if (!pieceAlive[piece] || !world.IsAwake(pieceBodies[piece]))
            {
                continue;
            }

            moving = world.Velocity(pieceBodies[piece]).LengthSquared() > SettleSpeed * SettleSpeed;
        }

        quietSeconds = moving ? 0f : quietSeconds + PhysicsWorld.StepSeconds;
        if (quietSeconds < QuietSeconds && flightSeconds < MaxTurnSeconds)
        {
            return;
        }

        EndTurn();
    }

    private void EndTurn()
    {
        for (var bird = flyingCount - 1; bird >= 0; bird--)
        {
            RemoveBird(bird, world.Position(flyingBodies[bird]));
        }

        if (GoblinsLeft == 0)
        {
            Win();
            return;
        }

        if (nextBird >= level.Birds.Length)
        {
            Won = false;
            Phase = FlingPhase.Over;
            Push(new FlingEvent(FlingEventKind.Lost, new Vector2(level.FocusX, -2f)));
            return;
        }

        Phase = FlingPhase.Aiming;
        Push(new FlingEvent(FlingEventKind.NextBird, SlingAnchor, 0, nextBird, bird: level.Birds[nextBird]));
    }

    private void Win()
    {
        var spare = level.Birds.Length - nextBird;
        BirdBonus = spare * FlingLevels.SpareBirdPoints;
        Score += BirdBonus;
        for (var queueIndex = 0; queueIndex < spare; queueIndex++)
        {
            var bird = level.Birds[nextBird + queueIndex];
            Push(new FlingEvent(FlingEventKind.BirdBonus, QueuePosition(queueIndex, bird), FlingLevels.SpareBirdPoints,
                queueIndex, bird: bird));
        }

        Won = true;
        Phase = FlingPhase.Over;
        Push(new FlingEvent(FlingEventKind.Won, new Vector2(level.FocusX, -2f), Score));
    }

    private int CreatePiece(in FlingPiece spec)
    {
        if (spec.IsGoblin)
        {
            return world.CreateCircle(BodyType.Dynamic, spec.Center, spec.HalfExtents.X, GoblinMaterial);
        }

        if (spec.IsStatic)
        {
            return world.CreateBox(BodyType.Static, spec.Center, spec.HalfExtents, 0f, RockMaterial);
        }

        var material = spec.Material switch
        {
            FlingMaterial.Glass => GlassMaterial,
            FlingMaterial.Stone => StoneMaterial,
            _ => WoodMaterial,
        };
        return world.CreateBox(BodyType.Dynamic, spec.Center, spec.HalfExtents, 0f, material);
    }

    private void SpawnBird(FlingBird bird, Vector2 position, Vector2 velocity)
    {
        var material = bird switch
        {
            FlingBird.Splitter => SplitterBirdMaterial,
            FlingBird.Heavy => HeavyBirdMaterial,
            _ => NormalBirdMaterial,
        };
        var body = world.CreateCircle(BodyType.Dynamic, position, BirdRadius(bird), material, BodyFlags.Bullet);
        world.SetCollisionFilter(body, BirdCategory, BirdMask);
        world.SetVelocity(body, velocity);
        flyingBodies[flyingCount] = body;
        flyingKinds[flyingCount] = bird;
        flyingTouched[flyingCount] = false;
        flyingPrevious[flyingCount] = position;
        flyingCount++;
    }

    private void RemoveBird(int bird, Vector2 position)
    {
        Push(new FlingEvent(FlingEventKind.BirdGone, position, 0, bird, bird: flyingKinds[bird]));
        Park(flyingBodies[bird], new Vector2(RetiredBirdX - retiredBirds * RetiredSpacing, RetiredY));
        retiredBirds++;
        var last = flyingCount - 1;
        flyingBodies[bird] = flyingBodies[last];
        flyingKinds[bird] = flyingKinds[last];
        flyingTouched[bird] = flyingTouched[last];
        flyingPrevious[bird] = flyingPrevious[last];
        flyingCount = last;
        if (bird == 0)
        {
            leadTouched = true;
        }
    }

    private int FlyingSlotOf(int body)
    {
        for (var bird = 0; bird < flyingCount; bird++)
        {
            if (flyingBodies[bird] == body)
            {
                return bird;
            }
        }

        return -1;
    }

    private int PieceOf(int body)
    {
        var tag = world.Tag(body);
        if (tag <= 0)
        {
            return -1;
        }

        var piece = tag - 1;
        return piece < pieceCount && pieceAlive[piece] && pieceBodies[piece] == body ? piece : -1;
    }

    private void Push(in FlingEvent entry)
    {
        if (eventCount >= events.Length)
        {
            return;
        }

        events[eventCount] = entry;
        eventCount++;
    }
}
