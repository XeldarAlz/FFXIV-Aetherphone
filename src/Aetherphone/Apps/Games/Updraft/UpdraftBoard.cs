using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Updraft;

internal sealed class UpdraftBoard
{
    public const float FieldWidth = 9f;
    public const float ViewHeight = 16f;
    public const float MetresPerUnit = 2f;
    public const float BirdRadius = 0.34f;
    public const float Gravity = 28f;
    public const float PlainBounce = 14f;
    public const float SpringBounce = 20f;
    public const float GoldenBounce = 30f;
    public const float MaxSpeed = 8f;
    public const float Acceleration = 50f;
    public const float MaxWind = 2f;
    public const float MaxPathGap = 2.2f;
    public const float MinHalfWidth = 0.7f;
    public const float FeatherSeconds = 3.5f;
    public const float FeatherLift = 8f;
    public const float StormTop = 0.2f;
    public const float StormBottom = 0.7f;
    public const int CloudCapacity = 128;
    public const int PickupCapacity = 48;
    public const int WindCapacity = 6;
    public const int EventCapacity = 24;
    public const int CrystalPoints = 5;
    public const int MaxChain = 5;
    public const int MilestoneMetres = 100;
    public const float StepSeconds = 1f / 120f;
    public const float StartCamera = -2.5f;
    private const float MaxCatchUpSeconds = 0.25f;
    private const float MouseGain = 2.5f;
    private const float KeyboardSpeed = 6.5f;
    private const float KeyboardAcceleration = 24f;
    private const float StunSeconds = 0.35f;
    private const float ZapDrop = 4f;
    private const float StormCooldown = 1.2f;
    private const float CameraFollow = 0.42f;
    private const float DeathMargin = 0.8f;
    private const float RecycleMargin = 2f;
    private const float Lookahead = 12f;
    private const float ChunkHeight = 8f;
    private const float RampHeight = 700f;
    private const float StormClearance = 2.4f;
    private const float PickupRadius = 0.38f;
    private const float ChainWindow = 1.4f;
    private const float LaunchHalfWidth = 2.2f;
    private const float LandingSlop = 0.06f;
    private const float LandingGrip = 0.5f;
    private const float DissolveSeconds = 0.45f;
    private const float SquashDecay = 4f;
    private const float FeatherEase = 30f;
    private const int HistoryLength = 24;
    private const float GapMargin = 0.05f;
    private readonly UpdraftCloud[] clouds = new UpdraftCloud[CloudCapacity];
    private readonly UpdraftPickup[] pickups = new UpdraftPickup[PickupCapacity];
    private readonly UpdraftWind[] winds = new UpdraftWind[WindCapacity];
    private readonly UpdraftEvent[] events = new UpdraftEvent[EventCapacity];
    private readonly PathPoint[] history = new PathPoint[HistoryLength];
    private FixedStepClock clock = new(StepSeconds, MaxCatchUpSeconds);
    private UpdraftRandom random = new(1);
    private int eventCount;
    private int historyCount;
    private bool generating;
    private bool launchEventPending;
    private float lastPathX;
    private float lastPathY;
    private float lastCrystalTime;
    private int chain;
    private int nextMilestone;
    private float stun;
    private float elapsed;
    public float BirdX { get; private set; }
    public float BirdY { get; private set; }
    public float VelocityX { get; private set; }
    public float VelocityY { get; private set; }
    public float PreviousBirdX { get; private set; }
    public float PreviousBirdY { get; private set; }
    public float CameraBottom { get; private set; }
    public float PreviousCamera { get; private set; }
    public float MaxHeight { get; private set; }
    public float BestHeight { get; private set; }
    public float FeatherTime { get; private set; }
    public bool HasShield { get; private set; }
    public bool Launched { get; private set; }
    public bool GameOver { get; private set; }
    public bool PassedBest { get; private set; }
    public int Crystals { get; private set; }
    public int CrystalScore { get; private set; }
    public int OverflowCount { get; private set; }
    public int ActiveCloudCount { get; private set; }
    public UpdraftCloudKind LastBounceKind { get; private set; }
    public float Alpha => clock.Alpha;
    public int HeightMetres => (int)(MaxHeight * MetresPerUnit);
    public int Score => HeightMetres + CrystalScore;
    public float FeatherFraction => FeatherTime / FeatherSeconds;
    public bool Gliding => FeatherTime > 0f;
    public bool Boosting => VelocityY > PlainBounce * 1.08f && !Gliding;
    public ReadOnlySpan<UpdraftCloud> Clouds => clouds;
    public ReadOnlySpan<UpdraftPickup> Pickups => pickups;
    public ReadOnlySpan<UpdraftWind> Winds => winds;
    public ReadOnlySpan<UpdraftEvent> Events => events.AsSpan(0, eventCount);

    public static float Wrap(float x)
    {
        var wrapped = x % FieldWidth;
        return wrapped < 0f ? wrapped + FieldWidth : wrapped;
    }

    public static float WrapDelta(float delta) => Wrap(delta + FieldWidth * 0.5f) - FieldWidth * 0.5f;

    public static float Apex(float bounce) => bounce * bounce / (2f * Gravity);

    public static float AirTime(float rise, float bounce)
    {
        var discriminant = bounce * bounce - 2f * Gravity * rise;
        if (discriminant < 0f)
        {
            return 0f;
        }

        return (bounce + MathF.Sqrt(discriminant)) / Gravity;
    }

    public static float HorizontalReach(float rise) =>
        MathF.Max(0f, (KeyboardSpeed - MaxWind) * (AirTime(rise, PlainBounce) - KeyboardSpeed / KeyboardAcceleration));

    public static float Difficulty(float height) => Math.Clamp(height / RampHeight, 0f, 1f);

    public static float ArcTop(float y, UpdraftCloudKind kind) => y + BirdRadius + Apex(BounceFor(kind));

    public static float BounceFor(UpdraftCloudKind kind) => kind switch
    {
        UpdraftCloudKind.Golden => GoldenBounce,
        UpdraftCloudKind.Spring => SpringBounce,
        _ => PlainBounce,
    };

    public void StartGame(int seed, float bestHeight)
    {
        ResetState(seed, bestHeight);
        generating = true;
        lastPathX = FieldWidth * 0.5f;
        lastPathY = 0f;
        SpawnCloud(UpdraftCloudKind.Plain, lastPathX, 0f, LaunchHalfWidth, true, 0f);
        RecordPath(lastPathX, 0f, UpdraftCloudKind.Plain);
        EnsureGenerated();
    }

    public void StartEmpty()
    {
        ResetState(1, 0f);
        generating = false;
    }

    public void Launch()
    {
        if (Launched || GameOver)
        {
            return;
        }

        Launched = true;
        VelocityY = PlainBounce;
        LastBounceKind = UpdraftCloudKind.Plain;
        launchEventPending = true;
    }

    public void PlaceBird(float x, float y, float velocityX, float velocityY)
    {
        BirdX = Wrap(x);
        BirdY = y;
        PreviousBirdX = BirdX;
        PreviousBirdY = y;
        VelocityX = velocityX;
        VelocityY = velocityY;
        Launched = true;
    }

    public int AddCloud(UpdraftCloudKind kind, float x, float y, float halfWidth, float driftSpeed = 0f) =>
        SpawnCloud(kind, x, y, halfWidth, false, driftSpeed);

    public int AddPickup(UpdraftPickupKind kind, float x, float y) => SpawnPickup(kind, x, y);

    public void AdvanceCamera(float cameraBottom)
    {
        CameraBottom = MathF.Max(CameraBottom, cameraBottom);
        Recycle();
        EnsureGenerated();
    }

    public void Tick(float deltaSeconds, in UpdraftInput input)
    {
        eventCount = 0;
        if (!Launched || GameOver)
        {
            return;
        }

        if (launchEventPending)
        {
            launchEventPending = false;
            AddEvent(UpdraftEventKind.Bounce, BirdX, BirdY - BirdRadius, 0, 0, UpdraftCloudKind.Plain);
        }

        var steps = clock.Advance(deltaSeconds);
        for (var step = 0; step < steps; step++)
        {
            Step(StepSeconds, input);
            if (GameOver)
            {
                return;
            }
        }
    }

    private void ResetState(int seed, float bestHeight)
    {
        random = new UpdraftRandom(seed);
        Array.Clear(clouds);
        Array.Clear(pickups);
        Array.Clear(winds);
        ActiveCloudCount = 0;
        eventCount = 0;
        historyCount = 0;
        BirdX = FieldWidth * 0.5f;
        BirdY = BirdRadius;
        PreviousBirdX = BirdX;
        PreviousBirdY = BirdY;
        VelocityX = 0f;
        VelocityY = 0f;
        CameraBottom = StartCamera;
        PreviousCamera = StartCamera;
        MaxHeight = 0f;
        BestHeight = bestHeight;
        FeatherTime = 0f;
        stun = 0f;
        elapsed = 0f;
        HasShield = false;
        Launched = false;
        launchEventPending = false;
        GameOver = false;
        PassedBest = false;
        Crystals = 0;
        CrystalScore = 0;
        OverflowCount = 0;
        LastBounceKind = UpdraftCloudKind.Plain;
        lastCrystalTime = -100f;
        chain = 0;
        nextMilestone = MilestoneMetres;
        clock.Reset();
    }

    private void Step(float deltaSeconds, in UpdraftInput input)
    {
        elapsed += deltaSeconds;
        PreviousBirdX = BirdX;
        PreviousBirdY = BirdY;
        PreviousCamera = CameraBottom;
        UpdateTimers(deltaSeconds);
        Steer(deltaSeconds, input);
        BirdX = Wrap(BirdX + (VelocityX + WindAt(BirdY)) * deltaSeconds);
        if (FeatherTime > 0f)
        {
            VelocityY = Approach(VelocityY, FeatherLift, FeatherEase * deltaSeconds);
        }
        else
        {
            VelocityY -= Gravity * deltaSeconds;
        }

        var previousY = BirdY;
        BirdY += VelocityY * deltaSeconds;
        MoveClouds(deltaSeconds);
        CheckStorms();
        CheckLanding(previousY);
        CollectPickups();
        UpdateHeight();
        AdvanceCamera(BirdY - ViewHeight * CameraFollow);
        if (BirdY >= CameraBottom - DeathMargin)
        {
            return;
        }

        GameOver = true;
        AddEvent(UpdraftEventKind.Fell, BirdX, BirdY, Score, 0, UpdraftCloudKind.Plain);
    }

    private void UpdateTimers(float deltaSeconds)
    {
        FeatherTime = MathF.Max(0f, FeatherTime - deltaSeconds);
        stun = MathF.Max(0f, stun - deltaSeconds);
        for (var index = 0; index < CloudCapacity; index++)
        {
            ref var cloud = ref clouds[index];
            if (!cloud.Active)
            {
                continue;
            }

            cloud.Squash = MathF.Max(0f, cloud.Squash - SquashDecay * deltaSeconds);
            cloud.Cooldown = MathF.Max(0f, cloud.Cooldown - deltaSeconds);
            if (!cloud.Broken)
            {
                continue;
            }

            cloud.Dissolve += deltaSeconds / DissolveSeconds;
            if (cloud.Dissolve >= 1f)
            {
                cloud.Active = false;
                ActiveCloudCount--;
            }
        }
    }

    private void Steer(float deltaSeconds, in UpdraftInput input)
    {
        if (stun > 0f)
        {
            return;
        }

        if (input.HasTarget)
        {
            var desired = Math.Clamp(WrapDelta(input.TargetX - BirdX) * MouseGain, -MaxSpeed, MaxSpeed);
            VelocityX = Approach(VelocityX, desired, Acceleration * deltaSeconds);
            return;
        }

        VelocityX = Approach(VelocityX, Math.Clamp(input.Axis, -1f, 1f) * KeyboardSpeed, KeyboardAcceleration * deltaSeconds);
    }

    private void MoveClouds(float deltaSeconds)
    {
        for (var index = 0; index < CloudCapacity; index++)
        {
            ref var cloud = ref clouds[index];
            if (!cloud.Active || cloud.DriftSpeed == 0f)
            {
                continue;
            }

            cloud.X = Wrap(cloud.X + cloud.DriftSpeed * deltaSeconds);
        }
    }

    private void CheckStorms()
    {
        for (var index = 0; index < CloudCapacity; index++)
        {
            ref var cloud = ref clouds[index];
            if (!cloud.Active || cloud.Broken || cloud.Kind != UpdraftCloudKind.Storm || cloud.Cooldown > 0f)
            {
                continue;
            }

            var horizontal = MathF.Abs(WrapDelta(BirdX - cloud.X));
            var vertical = BirdY - cloud.Y;
            if (horizontal > cloud.HalfWidth + BirdRadius * 0.4f || vertical > StormTop + BirdRadius ||
                vertical < -StormBottom - BirdRadius)
            {
                continue;
            }

            HitStorm(ref cloud);
            return;
        }
    }

    private void HitStorm(ref UpdraftCloud cloud)
    {
        cloud.Squash = 1f;
        if (HasShield)
        {
            HasShield = false;
            cloud.Broken = true;
            BirdY = MathF.Max(BirdY, cloud.Y + BirdRadius);
            VelocityY = MathF.Max(VelocityY, PlainBounce);
            AddEvent(UpdraftEventKind.ShieldBlock, cloud.X, cloud.Y, 0, 0, UpdraftCloudKind.Storm);
            return;
        }

        cloud.Cooldown = StormCooldown;
        FeatherTime = 0f;
        stun = StunSeconds;
        VelocityY = MathF.Min(VelocityY, -ZapDrop);
        AddEvent(UpdraftEventKind.Zap, BirdX, BirdY, 0, 0, UpdraftCloudKind.Storm);
    }

    private void CheckLanding(float previousY)
    {
        if (VelocityY > 0f)
        {
            return;
        }

        var bottomBefore = previousY - BirdRadius;
        var bottomAfter = BirdY - BirdRadius;
        for (var index = 0; index < CloudCapacity; index++)
        {
            ref var cloud = ref clouds[index];
            if (!cloud.Active || cloud.Broken || cloud.Kind == UpdraftCloudKind.Storm)
            {
                continue;
            }

            if (bottomBefore < cloud.Y - LandingSlop || bottomAfter > cloud.Y)
            {
                continue;
            }

            if (MathF.Abs(WrapDelta(BirdX - cloud.X)) > cloud.HalfWidth + BirdRadius * LandingGrip)
            {
                continue;
            }

            Bounce(ref cloud);
            return;
        }
    }

    private void Bounce(ref UpdraftCloud cloud)
    {
        BirdY = cloud.Y + BirdRadius;
        VelocityY = BounceFor(cloud.Kind);
        cloud.Squash = 1f;
        LastBounceKind = cloud.Kind;
        AddEvent(UpdraftEventKind.Bounce, BirdX, cloud.Y, 0, 0, cloud.Kind);
        if (cloud.Kind != UpdraftCloudKind.Fragile)
        {
            return;
        }

        cloud.Broken = true;
        AddEvent(UpdraftEventKind.Break, cloud.X, cloud.Y, 0, 0, UpdraftCloudKind.Fragile);
    }

    private void CollectPickups()
    {
        var reach = BirdRadius + PickupRadius;
        for (var index = 0; index < PickupCapacity; index++)
        {
            ref var pickup = ref pickups[index];
            if (!pickup.Active)
            {
                continue;
            }

            var horizontal = WrapDelta(BirdX - pickup.X);
            var vertical = BirdY - pickup.Y;
            if (horizontal * horizontal + vertical * vertical > reach * reach)
            {
                continue;
            }

            pickup.Active = false;
            Collect(pickup);
        }
    }

    private void Collect(in UpdraftPickup pickup)
    {
        switch (pickup.Kind)
        {
            case UpdraftPickupKind.Crystal:
            {
                chain = elapsed - lastCrystalTime <= ChainWindow ? chain + 1 : 1;
                lastCrystalTime = elapsed;
                var points = CrystalPoints * Math.Min(chain, MaxChain);
                Crystals++;
                CrystalScore += points;
                AddEvent(UpdraftEventKind.Crystal, pickup.X, pickup.Y, points, chain, UpdraftCloudKind.Plain);
                return;
            }
            case UpdraftPickupKind.Feather:
                FeatherTime = FeatherSeconds;
                AddEvent(UpdraftEventKind.Feather, pickup.X, pickup.Y, 0, 0, UpdraftCloudKind.Plain);
                return;
            default:
                HasShield = true;
                AddEvent(UpdraftEventKind.Shield, pickup.X, pickup.Y, 0, 0, UpdraftCloudKind.Plain);
                return;
        }
    }

    private void UpdateHeight()
    {
        if (BirdY <= MaxHeight)
        {
            return;
        }

        MaxHeight = BirdY;
        while (HeightMetres >= nextMilestone)
        {
            AddEvent(UpdraftEventKind.Milestone, BirdX, BirdY, nextMilestone, 0, UpdraftCloudKind.Plain);
            nextMilestone += MilestoneMetres;
        }

        if (PassedBest || BestHeight <= 0f || MaxHeight <= BestHeight)
        {
            return;
        }

        PassedBest = true;
        AddEvent(UpdraftEventKind.PassedBest, BirdX, BirdY, HeightMetres, 0, UpdraftCloudKind.Plain);
    }

    private float WindAt(float y)
    {
        var total = 0f;
        for (var index = 0; index < WindCapacity; index++)
        {
            ref readonly var wind = ref winds[index];
            if (wind.Active && y >= wind.Bottom && y <= wind.Top)
            {
                total += wind.Speed;
            }
        }

        return Math.Clamp(total, -MaxWind, MaxWind);
    }

    private void Recycle()
    {
        var floor = CameraBottom - RecycleMargin;
        for (var index = 0; index < CloudCapacity; index++)
        {
            ref var cloud = ref clouds[index];
            if (cloud.Active && cloud.Y < floor)
            {
                cloud.Active = false;
                ActiveCloudCount--;
            }
        }

        for (var index = 0; index < PickupCapacity; index++)
        {
            ref var pickup = ref pickups[index];
            if (pickup.Active && pickup.Y < floor)
            {
                pickup.Active = false;
            }
        }

        for (var index = 0; index < WindCapacity; index++)
        {
            ref var wind = ref winds[index];
            if (wind.Active && wind.Top < floor)
            {
                wind.Active = false;
            }
        }
    }

    private void EnsureGenerated()
    {
        if (!generating)
        {
            return;
        }

        var target = CameraBottom + ViewHeight + Lookahead;
        while (lastPathY < target)
        {
            GenerateChunk();
        }
    }

    private void GenerateChunk()
    {
        var chunkBottom = lastPathY;
        var chunkTop = lastPathY + ChunkHeight;
        while (lastPathY < chunkTop)
        {
            PlaceStep();
        }

        PlaceWind(chunkBottom, lastPathY);
    }

    private void PlaceStep()
    {
        var height = lastPathY;
        var difficulty = Difficulty(height);
        var minimumGap = 1.2f + 0.5f * difficulty;
        var maximumGap = 1.8f + (MaxPathGap - GapMargin - 1.8f) * difficulty;
        var gap = random.Range(minimumGap, maximumGap);
        var y = lastPathY + gap;
        var halfWidth = random.Range(1f, 1.2f) - 0.3f * difficulty;
        var reach = MathF.Min(HorizontalReach(gap), FieldWidth * 0.5f) * 0.9f;
        var x = Wrap(lastPathX + random.Range(-reach, reach));
        var kind = PickPathKind(height, difficulty);
        var drift = kind == UpdraftCloudKind.Drifting ? random.Sign() * random.Range(0.8f, 1.6f + difficulty) : 0f;
        SpawnCloud(kind, x, y, halfWidth, true, drift);
        RecordPath(x, y, kind);
        PlaceDecoy(x, y, halfWidth, height, difficulty);
        PlaceStorm(lastPathX, lastPathY, y, height, difficulty);
        PlacePickups(x, y, height, difficulty, kind);
        lastPathX = x;
        lastPathY = y;
    }

    private UpdraftCloudKind PickPathKind(float height, float difficulty)
    {
        var golden = height > 30f ? 0.035f : 0f;
        var spring = height > 50f ? 0.06f : 0f;
        var fragile = height > 25f ? 0.08f + 0.32f * difficulty : 0f;
        var drifting = height > 12f ? 0.1f + 0.3f * difficulty : 0f;
        var roll = random.NextFloat();
        if (roll < golden)
        {
            return UpdraftCloudKind.Golden;
        }

        roll -= golden;
        if (roll < spring)
        {
            return UpdraftCloudKind.Spring;
        }

        roll -= spring;
        if (roll < fragile)
        {
            return UpdraftCloudKind.Fragile;
        }

        roll -= fragile;
        return roll < drifting ? UpdraftCloudKind.Drifting : UpdraftCloudKind.Plain;
    }

    private void PlaceDecoy(float pathX, float pathY, float pathHalfWidth, float height, float difficulty)
    {
        if (!random.Chance(0.4f - 0.2f * difficulty))
        {
            return;
        }

        var halfWidth = random.Range(0.8f, 1.1f) - 0.2f * difficulty;
        var spacing = random.Range(pathHalfWidth + halfWidth + 0.6f, FieldWidth * 0.5f);
        var x = Wrap(pathX + random.Sign() * spacing);
        var y = pathY + random.Range(-0.5f, 0.5f);
        var kind = height > 25f && random.Chance(0.5f) ? UpdraftCloudKind.Fragile : UpdraftCloudKind.Plain;
        var drift = 0f;
        if (height > 12f && random.Chance(0.3f))
        {
            kind = UpdraftCloudKind.Drifting;
            drift = random.Sign() * random.Range(0.8f, 1.6f + difficulty);
        }

        if (Overlaps(x, y, halfWidth))
        {
            return;
        }

        SpawnCloud(kind, x, y, halfWidth, false, drift);
    }

    private void PlaceStorm(float fromX, float fromY, float toY, float height, float difficulty)
    {
        if (height <= 80f || !random.Chance(0.06f + 0.26f * difficulty))
        {
            return;
        }

        var halfWidth = random.Range(0.8f, 1.05f);
        var y = (fromY + toY) * 0.5f + random.Range(-0.2f, 0.2f);
        for (var attempt = 0; attempt < 6; attempt++)
        {
            var x = Wrap(fromX + FieldWidth * 0.5f + random.Range(-1.5f, 1.5f));
            if (!StormKeepsPathClear(x, y, halfWidth) || Overlaps(x, y, halfWidth))
            {
                continue;
            }

            SpawnCloud(UpdraftCloudKind.Storm, x, y, halfWidth, false, 0f);
            return;
        }
    }

    private bool StormKeepsPathClear(float x, float y, float halfWidth)
    {
        var stormFloor = y - StormBottom - BirdRadius;
        var stormCeiling = y + StormTop + BirdRadius;
        var available = Math.Min(historyCount, HistoryLength);
        for (var offset = 1; offset < available; offset++)
        {
            ref readonly var from = ref history[(historyCount - 1 - offset) % HistoryLength];
            ref readonly var to = ref history[(historyCount - offset) % HistoryLength];
            if (from.Top < stormFloor || from.Y + BirdRadius > stormCeiling)
            {
                continue;
            }

            if (from.Drifting || to.Drifting || ArcDistance(x, from.X, to.X) < StormClearance + halfWidth)
            {
                return false;
            }
        }

        return true;
    }

    private void RecordPath(float x, float y, UpdraftCloudKind kind)
    {
        history[historyCount % HistoryLength] = new PathPoint
        {
            X = x,
            Y = y,
            Top = ArcTop(y, kind),
            Drifting = kind == UpdraftCloudKind.Drifting,
        };
        historyCount++;
    }

    private void PlacePickups(float x, float y, float height, float difficulty, UpdraftCloudKind kind)
    {
        if (height > 40f && random.Chance(0.025f))
        {
            SpawnPickup(UpdraftPickupKind.Feather, x, y + 1.5f);
            return;
        }

        if (height > 70f && random.Chance(0.03f + 0.03f * difficulty))
        {
            SpawnPickup(UpdraftPickupKind.Shield, x, y + 1.5f);
            return;
        }

        if (kind == UpdraftCloudKind.Golden || !random.Chance(0.35f))
        {
            return;
        }

        var count = 1 + (int)(random.NextFloat() * 3f);
        for (var index = 0; index < count; index++)
        {
            SpawnPickup(UpdraftPickupKind.Crystal, x, y + 0.9f + index * 0.7f);
        }
    }

    private void PlaceWind(float bottom, float top)
    {
        if (bottom <= 110f || !random.Chance(0.25f + 0.25f * Difficulty(bottom)))
        {
            return;
        }

        for (var index = 0; index < WindCapacity; index++)
        {
            ref var wind = ref winds[index];
            if (wind.Active)
            {
                continue;
            }

            wind.Bottom = bottom + random.Range(0f, 2f);
            wind.Top = MathF.Min(top, wind.Bottom + random.Range(3f, 6f));
            wind.Speed = random.Sign() * random.Range(1.2f, MaxWind);
            wind.Active = true;
            return;
        }
    }

    public static float ArcDistance(float x, float from, float to)
    {
        var span = WrapDelta(to - from);
        var offset = WrapDelta(x - from);
        var inside = span >= 0f ? offset >= 0f && offset <= span : offset <= 0f && offset >= span;
        if (inside)
        {
            return 0f;
        }

        return MathF.Min(MathF.Abs(offset), MathF.Abs(WrapDelta(x - to)));
    }

    private bool Overlaps(float x, float y, float halfWidth)
    {
        for (var index = 0; index < CloudCapacity; index++)
        {
            ref readonly var cloud = ref clouds[index];
            if (!cloud.Active || MathF.Abs(cloud.Y - y) > 0.9f)
            {
                continue;
            }

            if (MathF.Abs(WrapDelta(cloud.X - x)) < cloud.HalfWidth + halfWidth + 0.3f)
            {
                return true;
            }
        }

        return false;
    }

    private int SpawnCloud(UpdraftCloudKind kind, float x, float y, float halfWidth, bool onPath, float driftSpeed)
    {
        for (var index = 0; index < CloudCapacity; index++)
        {
            ref var cloud = ref clouds[index];
            if (cloud.Active)
            {
                continue;
            }

            cloud = new UpdraftCloud
            {
                X = Wrap(x),
                Y = y,
                HalfWidth = MathF.Max(MinHalfWidth, halfWidth),
                DriftSpeed = driftSpeed,
                Look = (int)(random.NextUInt() & 0xFFFF),
                Kind = kind,
                Active = true,
                OnPath = onPath,
            };
            ActiveCloudCount++;
            return index;
        }

        OverflowCount++;
        return -1;
    }

    private int SpawnPickup(UpdraftPickupKind kind, float x, float y)
    {
        for (var index = 0; index < PickupCapacity; index++)
        {
            ref var pickup = ref pickups[index];
            if (pickup.Active)
            {
                continue;
            }

            pickup = new UpdraftPickup
            {
                X = Wrap(x),
                Y = y,
                Phase = random.NextFloat() * MathF.Tau,
                Kind = kind,
                Active = true,
            };
            return index;
        }

        OverflowCount++;
        return -1;
    }

    private void AddEvent(UpdraftEventKind kind, float x, float y, int value, int detail, UpdraftCloudKind cloudKind)
    {
        if (eventCount >= EventCapacity)
        {
            return;
        }

        events[eventCount] = new UpdraftEvent(kind, x, y, value, detail, cloudKind);
        eventCount++;
    }

    private struct PathPoint
    {
        public float X;
        public float Y;
        public float Top;
        public bool Drifting;
    }

    private static float Approach(float value, float target, float maxStep)
    {
        if (value < target)
        {
            return MathF.Min(target, value + maxStep);
        }

        return MathF.Max(target, value - maxStep);
    }
}
