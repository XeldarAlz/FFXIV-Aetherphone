using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Trailblaze;

internal enum TrailblazeState : byte
{
    Running,
    Dying,
    Over,
}

internal enum TrailblazeDeath : byte
{
    None,
    Crash,
    Fall,
}

internal enum TrailblazePower : byte
{
    None,
    Magnet,
    Double,
    Wings,
}

internal enum TrailblazeMove : byte
{
    Left,
    Right,
    Jump,
    Slide,
}

internal enum TrailblazeStunt : byte
{
    None,
    Leap,
    Duck,
}

internal struct TrailblazeHazard
{
    public float Z;
    public float Length;
    public byte Lane;
    public TrailblazeCell Kind;
    public bool Passed;
}

internal struct TrailblazeCoin
{
    public float Z;
    public float LaneX;
    public float Height;
    public bool Magnetized;
}

internal struct TrailblazePickup
{
    public float Z;
    public byte Lane;
    public TrailblazePower Kind;
}

internal sealed class TrailblazeBoard
{
    public const int LaneCount = TrailblazeChunks.Lanes;
    public const float RowSpacing = 4.5f;
    public const float ChunkLength = TrailblazeChunks.Rows * RowSpacing;
    public const float RunwayLength = 24f;
    public const float BaseSpeed = 12f;
    public const float MaxSpeed = 28f;
    public const float SpeedRamp = 1500f;
    public const float Gravity = 38f;
    public const float JumpVelocity = 12f;
    public const float SlamVelocity = 26f;
    public const float SlideSeconds = 0.6f;
    public const float LaneChangeRate = 9f;
    public const float BodyHeight = 1.5f;
    public const float SlideBodyHeight = 0.6f;
    public const float BodyHalfDepth = 0.4f;
    public const float PlayerHalfWidth = 0.3f;
    public const float HazardHalfWidth = 0.45f;
    public const float BarrierBottom = 0.95f;
    public const float BarrierTop = 2.6f;
    public const float BarrierDepth = 0.5f;
    public const float CartHeight = 2.4f;
    public const float CartInset = 0.4f;
    public const float GapLength = 3f;
    public const float GapMargin = 0.35f;
    public const float CoinSpacing = 1.5f;
    public const float CoinHeight = 0.55f;
    public const float ArcPeak = 1.35f;
    public const float PickupHeight = 1f;
    public const float FlyHeight = 3.6f;
    public const float FlyRiseSpeed = 9f;
    public const float WingsSeconds = 6f;
    public const float MagnetSeconds = 8f;
    public const float DoubleSeconds = 10f;
    public const float GraceSeconds = 1f;
    public const float MagnetReach = 16f;
    public const int CoinPoints = 4;
    public const int StuntPoints = 25;
    public const int MilestoneMetres = 500;
    public const float ChainWindowSeconds = 1f;
    public const int HistoryCapacity = 64;
    private const float LoadAhead = 110f;
    private const float RecycleBehind = 8f;
    private const float MaxStepSeconds = 1f / 120f;
    private const float JumpBufferSeconds = 0.15f;
    private const float DyingSeconds = 1.1f;
    private const float CrashHop = 7f;
    private const float FallFloor = -6f;
    private const float CoinReachLane = 0.5f;
    private const float CoinReachDepth = 0.8f;
    private const float PickupReachLane = 0.6f;
    private const float PickupReachDepth = 0.9f;
    private const float MagnetPull = 12f;
    private const int HazardCapacity = 160;
    private const int CoinCapacity = 512;
    private const int PickupCapacity = 16;
    private const int EventCapacity = 16;
    private const int FirstTierUpChunk = 2;
    private const int SecondTierUpChunk = 6;
    private const int CapTierWeight = 3;
    private const ulong LootSalt = 0x7A11B1A2E5C0FFEEUL;

    private readonly TrailblazeHazard[] hazards = new TrailblazeHazard[HazardCapacity];
    private readonly TrailblazeCoin[] coins = new TrailblazeCoin[CoinCapacity];
    private readonly TrailblazePickup[] pickups = new TrailblazePickup[PickupCapacity];
    private readonly Vector3[] coinEvents = new Vector3[EventCapacity];
    private readonly int[] history = new int[HistoryCapacity];
    private GameRandom chunkRandom;
    private GameRandom lootRandom;
    private ComboMeter chain = new(ChainWindowSeconds);
    private int openingChunk;
    private int hazardCount;
    private int coinCount;
    private int pickupCount;
    private int coinEventCount;
    private int queuedSteer;
    private bool queuedJump;
    private bool queuedSlide;
    private int previousChunk;
    private float nextChunkZ;
    private float slideLeft;
    private float jumpBuffer;
    private float tempo = 1f;
    private bool slideQueued;
    private bool airborne;
    private float magnetLeft;
    private float doubleLeft;
    private float wingsLeft;
    private float graceLeft;
    private float dyingSeconds;
    private float dyingSpeed;
    private int coinScore;
    private int stuntScore;
    private int milestonesReached;

    public TrailblazeState State { get; private set; }

    public TrailblazeDeath Death { get; private set; }

    public float Distance { get; private set; }

    public float LaneX { get; private set; }

    public int TargetLane { get; private set; }

    public float Height { get; private set; }

    public float VerticalVelocity { get; private set; }

    public float PlaySeconds { get; private set; }

    public int Gil { get; private set; }

    public int Stunts { get; private set; }

    public int Jumps { get; private set; }

    public int BestChain { get; private set; }

    public int ChunksLoaded { get; private set; }

    public bool JumpedThisStep { get; private set; }

    public bool LandedThisStep { get; private set; }

    public float LandingImpact { get; private set; }

    public bool SlidThisStep { get; private set; }

    public bool SlammedThisStep { get; private set; }

    public int SteeredThisStep { get; private set; }

    public int BumpedThisStep { get; private set; }

    public bool ChainTierUpThisStep { get; private set; }

    public TrailblazeStunt StuntThisStep { get; private set; }

    public Vector3 StuntPosition { get; private set; }

    public TrailblazePower PowerThisStep { get; private set; }

    public TrailblazePower PowerEndedThisStep { get; private set; }

    public int MilestoneThisStep { get; private set; }

    public bool DiedThisStep { get; private set; }

    public int CoinsThisStep => coinEventCount;

    public int Score => (int)Distance + coinScore + stuntScore;

    public int Metres => (int)Distance;

    public float Speed => State == TrailblazeState.Running ? RunSpeed(Distance) : dyingSpeed;

    public bool Sliding => slideLeft > 0f;

    public float SlideLeft => slideLeft;

    public bool Airborne => airborne;

    public bool Flying => wingsLeft > 0f;

    public bool Invulnerable => wingsLeft > 0f || graceLeft > 0f;

    public float GraceLeft => graceLeft;

    public float DyingProgress => Math.Clamp(dyingSeconds / DyingSeconds, 0f, 1f);

    public ComboMeter Chain => chain;

    public float BodyTop => Height + (Sliding ? SlideBodyHeight : BodyHeight);

    public int HazardCount => hazardCount;

    public int CoinCount => coinCount;

    public int PickupCount => pickupCount;

    public ref readonly TrailblazeHazard HazardAt(int index) => ref hazards[index];

    public ref readonly TrailblazeCoin CoinAt(int index) => ref coins[index];

    public ref readonly TrailblazePickup PickupAt(int index) => ref pickups[index];

    public Vector3 CoinEventAt(int index) => coinEvents[index];

    public int ChunkAt(int ordinal) => ordinal >= 0 && ordinal < Math.Min(ChunksLoaded, HistoryCapacity) ? history[ordinal] : -1;

    public static float RunSpeed(float distance) =>
        BaseSpeed + (MaxSpeed - BaseSpeed) * (1f - MathF.Exp(-MathF.Max(0f, distance) / SpeedRamp));

    public static float TempoAt(float speed) => MathF.Sqrt(MathF.Max(BaseSpeed, speed) / BaseSpeed);

    public static float AirtimeAt(float speed) => 2f * JumpVelocity / (Gravity * TempoAt(speed));

    public static int TierCap(int chunkOrdinal)
    {
        if (chunkOrdinal < FirstTierUpChunk)
        {
            return 0;
        }

        return chunkOrdinal < SecondTierUpChunk ? 1 : 2;
    }

    public float PowerLeft(TrailblazePower power) => power switch
    {
        TrailblazePower.Magnet => magnetLeft,
        TrailblazePower.Double => doubleLeft,
        TrailblazePower.Wings => wingsLeft,
        _ => 0f,
    };

    public static float PowerSeconds(TrailblazePower power) => power switch
    {
        TrailblazePower.Magnet => MagnetSeconds,
        TrailblazePower.Double => DoubleSeconds,
        TrailblazePower.Wings => WingsSeconds,
        _ => 1f,
    };

    public void Reset(GameRandom seededRandom, int firstChunk = 0)
    {
        chunkRandom = seededRandom;
        var fork = seededRandom;
        lootRandom = GameRandom.FromSeed((((ulong)fork.NextUInt() << 32) | fork.NextUInt()) ^ LootSalt);
        openingChunk = Math.Clamp(firstChunk, 0, TrailblazeChunks.Count - 1);
        State = TrailblazeState.Running;
        Death = TrailblazeDeath.None;
        Distance = 0f;
        LaneX = 1f;
        TargetLane = 1;
        Height = 0f;
        VerticalVelocity = 0f;
        PlaySeconds = 0f;
        Gil = 0;
        Stunts = 0;
        Jumps = 0;
        BestChain = 0;
        ChunksLoaded = 0;
        chain = new ComboMeter(ChainWindowSeconds);
        hazardCount = 0;
        coinCount = 0;
        pickupCount = 0;
        queuedSteer = 0;
        queuedJump = false;
        queuedSlide = false;
        previousChunk = -1;
        nextChunkZ = RunwayLength;
        slideLeft = 0f;
        jumpBuffer = 0f;
        tempo = 1f;
        slideQueued = false;
        airborne = false;
        magnetLeft = 0f;
        doubleLeft = 0f;
        wingsLeft = 0f;
        graceLeft = 0f;
        dyingSeconds = 0f;
        dyingSpeed = 0f;
        coinScore = 0;
        stuntScore = 0;
        milestonesReached = 0;
        ClearEvents();
        LoadChunks();
    }

    public void Move(TrailblazeMove move)
    {
        if (State != TrailblazeState.Running)
        {
            return;
        }

        switch (move)
        {
            case TrailblazeMove.Left:
                queuedSteer = Math.Max(-2, queuedSteer - 1);
                return;
            case TrailblazeMove.Right:
                queuedSteer = Math.Min(2, queuedSteer + 1);
                return;
            case TrailblazeMove.Jump:
                queuedJump = true;
                return;
            default:
                queuedSlide = true;
                return;
        }
    }

    public void Step(float deltaSeconds)
    {
        ClearEvents();
        if (deltaSeconds <= 0f || State == TrailblazeState.Over)
        {
            return;
        }

        ApplyQueuedMoves();
        var steps = new Substeps(deltaSeconds, MaxStepSeconds);
        for (var step = 0; step < steps.Count && State != TrailblazeState.Over; step++)
        {
            if (State == TrailblazeState.Dying)
            {
                StepDying(steps.Step);
                continue;
            }

            StepRunning(steps.Step);
        }
    }

    public bool LaneBlocked(int lane)
    {
        if (Invulnerable)
        {
            return false;
        }

        for (var index = 0; index < hazardCount; index++)
        {
            ref readonly var hazard = ref hazards[index];
            if (hazard.Z > Distance + BodyHalfDepth)
            {
                return false;
            }

            if (hazard.Kind == TrailblazeCell.Cart && hazard.Lane == lane && Overlaps(hazard))
            {
                return true;
            }
        }

        return false;
    }

    private void ClearEvents()
    {
        JumpedThisStep = false;
        LandedThisStep = false;
        LandingImpact = 0f;
        SlidThisStep = false;
        SlammedThisStep = false;
        SteeredThisStep = 0;
        BumpedThisStep = 0;
        ChainTierUpThisStep = false;
        StuntThisStep = TrailblazeStunt.None;
        PowerThisStep = TrailblazePower.None;
        PowerEndedThisStep = TrailblazePower.None;
        MilestoneThisStep = 0;
        DiedThisStep = false;
        coinEventCount = 0;
    }

    private void ApplyQueuedMoves()
    {
        while (queuedSteer != 0 && State == TrailblazeState.Running)
        {
            var direction = Math.Sign(queuedSteer);
            queuedSteer -= direction;
            Steer(direction);
        }

        if (queuedJump)
        {
            queuedJump = false;
            TryJump();
        }

        if (queuedSlide)
        {
            queuedSlide = false;
            TrySlide();
        }
    }

    private void Steer(int direction)
    {
        var target = TargetLane + direction;
        if (target < 0 || target >= LaneCount || LaneBlocked(target))
        {
            BumpedThisStep = direction;
            return;
        }

        TargetLane = target;
        SteeredThisStep = direction;
    }

    private void TryJump()
    {
        if (Flying)
        {
            return;
        }

        if (airborne)
        {
            jumpBuffer = JumpBufferSeconds;
            return;
        }

        Jump();
    }

    private void Jump()
    {
        slideLeft = 0f;
        slideQueued = false;
        jumpBuffer = 0f;
        tempo = TempoAt(RunSpeed(Distance));
        VerticalVelocity = JumpVelocity * tempo;
        airborne = true;
        Jumps++;
        JumpedThisStep = true;
    }

    private void TrySlide()
    {
        if (Flying)
        {
            return;
        }

        if (airborne)
        {
            VerticalVelocity = MathF.Min(VerticalVelocity, -SlamVelocity);
            slideQueued = true;
            jumpBuffer = 0f;
            SlammedThisStep = true;
            return;
        }

        StartSlide();
    }

    private void StartSlide()
    {
        slideQueued = false;
        slideLeft = SlideSeconds / TempoAt(RunSpeed(Distance));
        SlidThisStep = true;
    }

    private void StepRunning(float deltaSeconds)
    {
        PlaySeconds += deltaSeconds;
        UpdateTimers(deltaSeconds);
        Distance += RunSpeed(Distance) * deltaSeconds;
        LaneX = MoveToward(LaneX, TargetLane, LaneChangeRate * deltaSeconds);
        UpdateVertical(deltaSeconds);
        CheckMilestone();
        LoadChunks();
        CollectPickups();
        CollectCoins(deltaSeconds);
        CheckHazards();
        Recycle();
    }

    private void UpdateTimers(float deltaSeconds)
    {
        chain.Update(deltaSeconds);
        slideLeft = MathF.Max(0f, slideLeft - deltaSeconds);
        jumpBuffer = MathF.Max(0f, jumpBuffer - deltaSeconds);
        graceLeft = MathF.Max(0f, graceLeft - deltaSeconds);
        magnetLeft = Tick(magnetLeft, deltaSeconds, TrailblazePower.Magnet);
        doubleLeft = Tick(doubleLeft, deltaSeconds, TrailblazePower.Double);
        if (wingsLeft <= 0f)
        {
            return;
        }

        wingsLeft = Tick(wingsLeft, deltaSeconds, TrailblazePower.Wings);
        if (wingsLeft > 0f)
        {
            return;
        }

        graceLeft = GraceSeconds;
        tempo = TempoAt(RunSpeed(Distance));
        VerticalVelocity = 0f;
        airborne = true;
    }

    private float Tick(float left, float deltaSeconds, TrailblazePower power)
    {
        if (left <= 0f)
        {
            return 0f;
        }

        var next = left - deltaSeconds;
        if (next > 0f)
        {
            return next;
        }

        PowerEndedThisStep = power;
        return 0f;
    }

    private void UpdateVertical(float deltaSeconds)
    {
        if (Flying)
        {
            Height = MoveToward(Height, FlyHeight, FlyRiseSpeed * deltaSeconds);
            VerticalVelocity = 0f;
            airborne = true;
            return;
        }

        if (!airborne)
        {
            return;
        }

        VerticalVelocity -= Gravity * tempo * tempo * deltaSeconds;
        Height += VerticalVelocity * deltaSeconds;
        if (Height > 0f)
        {
            return;
        }

        LandingImpact = MathF.Max(LandingImpact, -VerticalVelocity);
        Height = 0f;
        VerticalVelocity = 0f;
        airborne = false;
        LandedThisStep = true;
        if (slideQueued)
        {
            StartSlide();
            return;
        }

        if (jumpBuffer > 0f)
        {
            Jump();
        }
    }

    private void CheckMilestone()
    {
        var reached = (int)(Distance / MilestoneMetres);
        if (reached <= milestonesReached)
        {
            return;
        }

        milestonesReached = reached;
        MilestoneThisStep = reached * MilestoneMetres;
    }

    private void CollectPickups()
    {
        for (var index = 0; index < pickupCount;)
        {
            ref readonly var pickup = ref pickups[index];
            if (pickup.Z > Distance + PickupReachDepth)
            {
                return;
            }

            if (MathF.Abs(pickup.Z - Distance) > PickupReachDepth || MathF.Abs(pickup.Lane - LaneX) > PickupReachLane)
            {
                index++;
                continue;
            }

            Activate(pickup.Kind);
            RemoveAt(pickups, ref pickupCount, index);
        }
    }

    private void Activate(TrailblazePower power)
    {
        PowerThisStep = power;
        switch (power)
        {
            case TrailblazePower.Magnet:
                magnetLeft = MagnetSeconds;
                return;
            case TrailblazePower.Double:
                doubleLeft = DoubleSeconds;
                return;
            case TrailblazePower.Wings:
                wingsLeft = WingsSeconds;
                graceLeft = 0f;
                slideLeft = 0f;
                slideQueued = false;
                jumpBuffer = 0f;
                airborne = true;
                return;
            default:
                return;
        }
    }

    private void CollectCoins(float deltaSeconds)
    {
        var magnet = magnetLeft > 0f;
        var pull = 1f - MathF.Exp(-MagnetPull * deltaSeconds);
        var bodyTop = BodyTop;
        for (var index = 0; index < coinCount;)
        {
            ref var coin = ref coins[index];
            if (coin.Z > Distance + MagnetReach + CoinReachDepth)
            {
                return;
            }

            if (magnet && !coin.Magnetized && coin.Z > Distance - 1f && coin.Z < Distance + MagnetReach)
            {
                coin.Magnetized = true;
            }

            if (coin.Magnetized)
            {
                coin.LaneX += (LaneX - coin.LaneX) * pull;
                coin.Height += (Height + BodyHeight * 0.5f - coin.Height) * pull;
                coin.Z += (Distance - coin.Z) * pull;
            }

            if (!Touches(coin, bodyTop))
            {
                index++;
                continue;
            }

            Collect(coin);
            RemoveAt(coins, ref coinCount, index);
        }
    }

    private bool Touches(in TrailblazeCoin coin, float bodyTop)
    {
        if (MathF.Abs(coin.Z - Distance) > CoinReachDepth || MathF.Abs(coin.LaneX - LaneX) > CoinReachLane)
        {
            return false;
        }

        if (coin.Magnetized || Flying)
        {
            return true;
        }

        return coin.Height > Height - 0.45f && coin.Height < bodyTop + 0.35f;
    }

    private void Collect(in TrailblazeCoin coin)
    {
        var amount = doubleLeft > 0f ? 2 : 1;
        var tierBefore = chain.Multiplier;
        var multiplier = chain.Hit();
        Gil += amount;
        coinScore += CoinPoints * amount * multiplier;
        BestChain = Math.Max(BestChain, chain.Count);
        if (multiplier > tierBefore)
        {
            ChainTierUpThisStep = true;
        }

        if (coinEventCount < EventCapacity)
        {
            coinEvents[coinEventCount++] = new Vector3(coin.LaneX, coin.Height, coin.Z);
        }
    }

    private void CheckHazards()
    {
        for (var index = 0; index < hazardCount; index++)
        {
            ref var hazard = ref hazards[index];
            if (hazard.Z > Distance + BodyHalfDepth)
            {
                return;
            }

            if (!hazard.Passed && hazard.Z + hazard.Length < Distance - BodyHalfDepth)
            {
                Pass(ref hazard);
                continue;
            }

            if (Invulnerable || !Overlaps(hazard))
            {
                continue;
            }

            if (Hits(hazard))
            {
                Crash();
                return;
            }

            if (FallsInto(hazard))
            {
                Fall();
                return;
            }
        }
    }

    private bool Overlaps(in TrailblazeHazard hazard) =>
        Distance + BodyHalfDepth > hazard.Z && Distance - BodyHalfDepth < hazard.Z + hazard.Length;

    public static bool Collides(TrailblazeCell kind, int lane, float laneX, float height, float bodyTop)
    {
        if (MathF.Abs(laneX - lane) >= PlayerHalfWidth + HazardHalfWidth)
        {
            return false;
        }

        return kind switch
        {
            TrailblazeCell.Cart => height < CartHeight,
            TrailblazeCell.Barrier => height < BarrierTop && bodyTop > BarrierBottom,
            _ => false,
        };
    }

    public static bool Drops(int lane, float laneX, float height, bool inAir, float distance, float gapZ, float gapLength) =>
        !inAir && height <= 0f && MathF.Abs(laneX - lane) < 0.5f && distance > gapZ + GapMargin &&
        distance < gapZ + gapLength - GapMargin;

    private bool Hits(in TrailblazeHazard hazard) => Collides(hazard.Kind, hazard.Lane, LaneX, Height, BodyTop);

    private bool FallsInto(in TrailblazeHazard hazard) =>
        hazard.Kind == TrailblazeCell.Gap &&
        Drops(hazard.Lane, LaneX, Height, airborne, Distance, hazard.Z, hazard.Length);

    private void Pass(ref TrailblazeHazard hazard)
    {
        hazard.Passed = true;
        if (Flying || hazard.Kind == TrailblazeCell.Cart || MathF.Abs(LaneX - hazard.Lane) >= 0.5f)
        {
            return;
        }

        StuntThisStep = hazard.Kind == TrailblazeCell.Gap ? TrailblazeStunt.Leap : TrailblazeStunt.Duck;
        StuntPosition = new Vector3(hazard.Lane, Height, hazard.Z + hazard.Length);
        Stunts++;
        stuntScore += StuntPoints;
    }

    private void Crash()
    {
        Death = TrailblazeDeath.Crash;
        State = TrailblazeState.Dying;
        DiedThisStep = true;
        dyingSeconds = 0f;
        dyingSpeed = 0f;
        slideLeft = 0f;
        VerticalVelocity = CrashHop;
        airborne = true;
    }

    private void Fall()
    {
        Death = TrailblazeDeath.Fall;
        State = TrailblazeState.Dying;
        DiedThisStep = true;
        dyingSeconds = 0f;
        dyingSpeed = RunSpeed(Distance) * 0.35f;
        slideLeft = 0f;
        VerticalVelocity = 0f;
        airborne = true;
    }

    private void StepDying(float deltaSeconds)
    {
        dyingSeconds += deltaSeconds;
        Distance += dyingSpeed * deltaSeconds;
        dyingSpeed *= MathF.Exp(-5f * deltaSeconds);
        VerticalVelocity -= Gravity * deltaSeconds;
        Height += VerticalVelocity * deltaSeconds;
        if (Death == TrailblazeDeath.Crash && Height < 0f)
        {
            Height = 0f;
            VerticalVelocity = 0f;
        }

        Height = MathF.Max(FallFloor, Height);
        if (dyingSeconds >= DyingSeconds)
        {
            State = TrailblazeState.Over;
        }
    }

    private void LoadChunks()
    {
        while (nextChunkZ < Distance + LoadAhead)
        {
            var chunk = PickChunk();
            if (ChunksLoaded < HistoryCapacity)
            {
                history[ChunksLoaded] = chunk;
            }

            Spawn(chunk, nextChunkZ);
            previousChunk = chunk;
            nextChunkZ += ChunkLength;
            ChunksLoaded++;
        }
    }

    private int PickChunk() =>
        ChunksLoaded == 0 ? openingChunk : NextChunk(ref chunkRandom, ChunksLoaded, previousChunk);

    public static int NextChunk(ref GameRandom chunkRandom, int ordinal, int previous)
    {
        var cap = TierCap(ordinal);
        var total = 0;
        for (var chunk = 0; chunk < TrailblazeChunks.Count; chunk++)
        {
            total += Weight(chunk, cap, previous);
        }

        var roll = chunkRandom.Next(total);
        for (var chunk = 0; chunk < TrailblazeChunks.Count; chunk++)
        {
            roll -= Weight(chunk, cap, previous);
            if (roll < 0)
            {
                return chunk;
            }
        }

        return 0;
    }

    private static int Weight(int chunk, int cap, int previous)
    {
        var tier = TrailblazeChunks.Tier(chunk);
        if (chunk == previous || tier > cap)
        {
            return 0;
        }

        return tier == cap ? CapTierWeight : 1;
    }

    private void Spawn(int chunk, float startZ)
    {
        var hazardStart = hazardCount;
        var coinStart = coinCount;
        var pickupStart = pickupCount;
        for (var row = 0; row < TrailblazeChunks.Rows; row++)
        {
            var rowZ = startZ + (row + 0.5f) * RowSpacing;
            for (var lane = 0; lane < LaneCount; lane++)
            {
                SpawnCell(chunk, row, lane, startZ, rowZ);
                SpawnLoot(chunk, row, lane, startZ, rowZ);
            }
        }

        SortHazards(hazardStart);
        SortCoins(coinStart);
        SortPickups(pickupStart);
    }

    private void SpawnCell(int chunk, int row, int lane, float startZ, float rowZ)
    {
        switch (TrailblazeChunks.Cell(chunk, row, lane))
        {
            case TrailblazeCell.Barrier:
                AddHazard(rowZ - BarrierDepth * 0.5f, BarrierDepth, lane, TrailblazeCell.Barrier);
                return;
            case TrailblazeCell.Gap:
                AddHazard(rowZ - GapLength * 0.5f, GapLength, lane, TrailblazeCell.Gap);
                return;
            case TrailblazeCell.Cart:
            {
                if (row > 0 && TrailblazeChunks.Cell(chunk, row - 1, lane) == TrailblazeCell.Cart)
                {
                    return;
                }

                var end = RunEnd(chunk, row, lane, true);
                AddHazard(startZ + row * RowSpacing + CartInset, (end - row + 1) * RowSpacing - CartInset * 2f, lane,
                    TrailblazeCell.Cart);
                return;
            }
            default:
                return;
        }
    }

    private void SpawnLoot(int chunk, int row, int lane, float startZ, float rowZ)
    {
        switch (TrailblazeChunks.Loot(chunk, row, lane))
        {
            case TrailblazeLoot.Coins:
                AddCoin(rowZ - CoinSpacing, lane, CoinHeight);
                AddCoin(rowZ, lane, CoinHeight);
                AddCoin(rowZ + CoinSpacing, lane, CoinHeight);
                return;
            case TrailblazeLoot.Arc:
                SpawnArc(chunk, row, lane, startZ);
                return;
            case TrailblazeLoot.Power:
                AddPickup(rowZ, lane, PickPower());
                return;
            default:
                return;
        }
    }

    private void SpawnArc(int chunk, int row, int lane, float startZ)
    {
        if (row > 0 && TrailblazeChunks.Loot(chunk, row - 1, lane) == TrailblazeLoot.Arc)
        {
            return;
        }

        var end = RunEnd(chunk, row, lane, false);
        var first = startZ + (row + 0.5f) * RowSpacing;
        var last = startZ + (end + 0.5f) * RowSpacing;
        var count = Math.Max(2, (int)MathF.Round((last - first) / CoinSpacing) + 1);
        for (var coin = 0; coin < count; coin++)
        {
            var progress = coin / (float)(count - 1);
            AddCoin(first + (last - first) * progress, lane, CoinHeight + ArcPeak * 4f * progress * (1f - progress));
        }
    }

    private static int RunEnd(int chunk, int row, int lane, bool cells)
    {
        var end = row;
        while (end + 1 < TrailblazeChunks.Rows && (cells
                   ? TrailblazeChunks.Cell(chunk, end + 1, lane) == TrailblazeCell.Cart
                   : TrailblazeChunks.Loot(chunk, end + 1, lane) == TrailblazeLoot.Arc))
        {
            end++;
        }

        return end;
    }

    private TrailblazePower PickPower()
    {
        var roll = lootRandom.Next(8);
        if (roll < 3)
        {
            return TrailblazePower.Magnet;
        }

        return roll < 6 ? TrailblazePower.Double : TrailblazePower.Wings;
    }

    private void AddHazard(float z, float length, int lane, TrailblazeCell kind)
    {
        if (hazardCount >= HazardCapacity)
        {
            return;
        }

        hazards[hazardCount++] = new TrailblazeHazard
        {
            Z = z,
            Length = length,
            Lane = (byte)lane,
            Kind = kind,
            Passed = false,
        };
    }

    private void AddCoin(float z, int lane, float height)
    {
        if (coinCount >= CoinCapacity)
        {
            return;
        }

        coins[coinCount++] = new TrailblazeCoin
        {
            Z = z,
            LaneX = lane,
            Height = height,
            Magnetized = false,
        };
    }

    private void AddPickup(float z, int lane, TrailblazePower kind)
    {
        if (pickupCount >= PickupCapacity)
        {
            return;
        }

        pickups[pickupCount++] = new TrailblazePickup
        {
            Z = z,
            Lane = (byte)lane,
            Kind = kind,
        };
    }

    private void SortHazards(int start)
    {
        for (var index = start + 1; index < hazardCount; index++)
        {
            var item = hazards[index];
            var slot = index - 1;
            while (slot >= start && hazards[slot].Z > item.Z)
            {
                hazards[slot + 1] = hazards[slot];
                slot--;
            }

            hazards[slot + 1] = item;
        }
    }

    private void SortCoins(int start)
    {
        for (var index = start + 1; index < coinCount; index++)
        {
            var item = coins[index];
            var slot = index - 1;
            while (slot >= start && coins[slot].Z > item.Z)
            {
                coins[slot + 1] = coins[slot];
                slot--;
            }

            coins[slot + 1] = item;
        }
    }

    private void SortPickups(int start)
    {
        for (var index = start + 1; index < pickupCount; index++)
        {
            var item = pickups[index];
            var slot = index - 1;
            while (slot >= start && pickups[slot].Z > item.Z)
            {
                pickups[slot + 1] = pickups[slot];
                slot--;
            }

            pickups[slot + 1] = item;
        }
    }

    private void Recycle()
    {
        var limit = Distance - RecycleBehind;
        var hazardsGone = 0;
        while (hazardsGone < hazardCount && hazards[hazardsGone].Z + hazards[hazardsGone].Length < limit)
        {
            hazardsGone++;
        }

        DropFront(hazards, ref hazardCount, hazardsGone);
        var coinsGone = 0;
        while (coinsGone < coinCount && coins[coinsGone].Z < limit)
        {
            coinsGone++;
        }

        DropFront(coins, ref coinCount, coinsGone);
        var pickupsGone = 0;
        while (pickupsGone < pickupCount && pickups[pickupsGone].Z < limit)
        {
            pickupsGone++;
        }

        DropFront(pickups, ref pickupCount, pickupsGone);
    }

    private static void DropFront<T>(T[] items, ref int count, int dropped)
    {
        if (dropped <= 0)
        {
            return;
        }

        Array.Copy(items, dropped, items, 0, count - dropped);
        count -= dropped;
    }

    private static void RemoveAt<T>(T[] items, ref int count, int index)
    {
        Array.Copy(items, index + 1, items, index, count - index - 1);
        count--;
    }

    private static float MoveToward(float value, float target, float maxStep)
    {
        if (MathF.Abs(target - value) <= maxStep)
        {
            return target;
        }

        return value + MathF.Sign(target - value) * maxStep;
    }
}
