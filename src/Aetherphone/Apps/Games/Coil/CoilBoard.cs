using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Coil;

internal sealed class CoilBoard
{
    public const float FieldWidth = 1f;
    public const float FieldHeight = 1.4f;
    public const float MarbleRadius = 0.028f;
    public const float Diameter = MarbleRadius * 2f;
    public const float TrackMargin = MarbleRadius + 0.014f;
    public const float SampleSpacing = MarbleRadius * 0.5f;
    public const float MouthDistance = MarbleRadius * 1.7f;
    public const int StartLives = 3;
    public const int MarbleCapacity = 192;
    public const int ShotCapacity = 4;
    public const int MaxColours = 6;
    public const int PointsPerMarble = 10;
    public const int GapPoints = 100;
    public const int StageClearBase = 500;
    public const int TimeBonusPerSecond = 10;
    public const float StepSeconds = 1f / 120f;
    public const float FreezeSeconds = 5f;
    public const float SlowSeconds = 8f;
    public const float ReverseSeconds = 3f;
    public const float GuideSeconds = 10f;
    public const float StageClearSeconds = 3.2f;
    public const float ShotSpeed = 2.4f;
    public const float BlastRadius = MarbleRadius * 4.6f;
    private const float MaxCatchUpSeconds = 0.25f;
    private const float Touch = Diameter * 1.02f;
    private const float HitDistance = Diameter * 0.92f;
    private const float PullAcceleration = 1.6f;
    private const float PullMaxSpeed = 0.9f;
    private const float RushSpeed = 0.75f;
    private const float RushFraction = 0.3f;
    private const float DrainStartSpeed = 0.2f;
    private const float DrainMaxSpeed = 1.6f;
    private const float DrainAcceleration = 2.2f;
    private const float ReverseFactor = 2.5f;
    private const float SlowFactor = 0.5f;
    private const float DangerStart = 0.72f;
    private const float DangerSlowdown = 0.35f;
    private const float FireCooldown = 0.18f;
    private const float LagRate = 16f;
    private const float ArriveSeconds = 0.12f;
    private const float PowerChance = 0.045f;
    private const int PowerSpacing = 12;
    private const int PowerWarmup = 8;
    private const float RepeatChance = 0.42f;
    private const int MaxSpawnRun = 3;
    private const float TrackCrossDistance = MarbleRadius * 0.8f;
    private const int EventCapacity = 160;
    private const int ClearCapacity = 24;
    private const int SmallEventCapacity = 8;

    private readonly CoilMarble[] marbles = new CoilMarble[MarbleCapacity];
    private readonly Vector2[] positions = new Vector2[MarbleCapacity];
    private readonly bool[] separated = new bool[MarbleCapacity];
    private readonly bool[] marked = new bool[MarbleCapacity];
    private readonly int[] joins = new int[MarbleCapacity];
    private readonly CoilShot[] shots = new CoilShot[ShotCapacity];
    private readonly CoilBurst[] bursts = new CoilBurst[EventCapacity];
    private readonly CoilBurst[] swallowed = new CoilBurst[EventCapacity];
    private readonly CoilClear[] clears = new CoilClear[ClearCapacity];
    private readonly CoilGap[] gaps = new CoilGap[SmallEventCapacity];
    private readonly CoilTrigger[] triggers = new CoilTrigger[SmallEventCapacity];
    private readonly CoilTrack track = new();
    private FixedStepClock clock = new(StepSeconds, MaxCatchUpSeconds);
    private CoilRandom random;
    private int count;
    private int shotCount;
    private int burstCount;
    private int swallowedCount;
    private int clearCount;
    private int gapCount;
    private int triggerCount;
    private int presentMask;
    private int spawned;
    private int chainDepth;
    private int spawnRun;
    private int sinceLastPower;
    private byte lastSpawnColour;
    private bool rushActive;
    private float crawlSpeed;
    private float elapsed;
    private float fireCooldown;
    private float clearTimer;
    private float drainSpeed;

    public CoilBoard(int seed)
    {
        Reset(seed);
    }

    public CoilState State { get; private set; }
    public int Stage { get; private set; }
    public int Score { get; private set; }
    public int Lives { get; private set; }
    public int Quota { get; private set; }
    public int Colours { get; private set; }
    public int ClearBonus { get; private set; }
    public byte LoadedColour { get; private set; }
    public byte NextColour { get; private set; }
    public CoilPower ArmedPower { get; private set; }
    public float FreezeLeft { get; private set; }
    public float SlowLeft { get; private set; }
    public float ReverseLeft { get; private set; }
    public float GuideLeft { get; private set; }
    public bool LandedThisFrame { get; private set; }
    public Vector2 LandPosition { get; private set; }
    public bool StageStartedThisFrame { get; private set; }
    public bool StageClearedThisFrame { get; private set; }
    public bool DrainStartedThisFrame { get; private set; }
    public bool LifeLostThisFrame { get; private set; }
    public bool GameOverThisFrame { get; private set; }
    public CoilTrack Track => track;
    public Vector2 Launcher => track.Launcher;
    public int MarbleCount => count;
    public ReadOnlySpan<CoilMarble> Marbles => marbles.AsSpan(0, count);
    public ReadOnlySpan<Vector2> Positions => positions.AsSpan(0, count);
    public ReadOnlySpan<CoilShot> Shots => shots.AsSpan(0, shotCount);
    public ReadOnlySpan<CoilBurst> Bursts => bursts.AsSpan(0, burstCount);
    public ReadOnlySpan<CoilBurst> Swallowed => swallowed.AsSpan(0, swallowedCount);
    public ReadOnlySpan<CoilClear> Clears => clears.AsSpan(0, clearCount);
    public ReadOnlySpan<CoilGap> Gaps => gaps.AsSpan(0, gapCount);
    public ReadOnlySpan<CoilTrigger> Triggers => triggers.AsSpan(0, triggerCount);
    public float StageClearProgress => State == CoilState.StageClear ? 1f - clearTimer / StageClearSeconds : 0f;
    public float LeadProgress => count == 0 ? 0f : Math.Clamp(marbles[count - 1].Arc / track.Length, 0f, 1f);
    public float Danger => Math.Clamp((LeadProgress - DangerStart) / (1f - DangerStart), 0f, 1f);
    private bool CanFire => State == CoilState.Playing && fireCooldown <= 0f && shotCount < ShotCapacity;

    public static int ShapeFor(int stage) => (stage - 1) % CoilShapes.Count;

    public static int LoopFor(int stage) => (stage - 1) / CoilShapes.Count;

    public static int ColoursFor(int stage)
    {
        if (LoopFor(stage) > 0)
        {
            return MaxColours;
        }

        var shape = ShapeFor(stage);
        if (shape < 3)
        {
            return 4;
        }

        return shape < 8 ? 5 : MaxColours;
    }

    public static int QuotaFor(int stage) => Math.Min(130, 34 + 3 * ShapeFor(stage) + 12 * LoopFor(stage));

    public static float CrawlSpeedFor(int stage) =>
        0.042f * (1f + 0.06f * ShapeFor(stage)) * (1f + 0.22f * LoopFor(stage));

    public static float ParSecondsFor(int stage) => QuotaFor(stage) * 1.5f;

    public static int TimeBonus(int stage, float elapsedSeconds) =>
        Math.Max(0, (int)((ParSecondsFor(stage) - elapsedSeconds) * TimeBonusPerSecond));

    public void Reset(int seed)
    {
        random = new CoilRandom(seed);
        Score = 0;
        Lives = StartLives;
        Stage = 1;
        chainDepth = 0;
        LoadStage();
        State = CoilState.Ready;
    }

    public void Begin()
    {
        if (State == CoilState.Ready)
        {
            State = CoilState.Playing;
        }
    }

    public void BeginFrame()
    {
        burstCount = 0;
        swallowedCount = 0;
        clearCount = 0;
        gapCount = 0;
        triggerCount = 0;
        LandedThisFrame = false;
        StageStartedThisFrame = false;
        StageClearedThisFrame = false;
        DrainStartedThisFrame = false;
        LifeLostThisFrame = false;
        GameOverThisFrame = false;
    }

    public void Tick(float deltaSeconds)
    {
        var steps = clock.Advance(deltaSeconds);
        for (var step = 0; step < steps; step++)
        {
            Step(StepSeconds);
        }
    }

    public Vector2 Mouth(Vector2 direction) => track.Launcher + direction * MouthDistance;

    public bool Fire(Vector2 direction)
    {
        if (!CanFire || direction.LengthSquared() < 0.0001f)
        {
            return false;
        }

        direction = Vector2.Normalize(direction);
        ref var shot = ref shots[shotCount++];
        shot.Position = Mouth(direction);
        shot.Velocity = direction * ShotSpeed;
        shot.Colour = LoadedColour;
        shot.Kind = ArmedPower;
        shot.OnTrack = false;
        shot.CrossArc = 0f;
        shot.Gaps = 0;
        ArmedPower = CoilPower.None;
        LoadedColour = NextColour;
        NextColour = RollAmmo();
        fireCooldown = FireCooldown;
        return true;
    }

    public bool Swap()
    {
        if (State != CoilState.Playing || LoadedColour == NextColour)
        {
            return false;
        }

        (LoadedColour, NextColour) = (NextColour, LoadedColour);
        return true;
    }

    public bool PredictImpact(Vector2 direction, out Vector2 impact)
    {
        direction = Vector2.Normalize(direction);
        var origin = Mouth(direction);
        var best = float.MaxValue;
        var reach = HitDistance * HitDistance;
        for (var index = 0; index < count; index++)
        {
            if (!OnTrack(index))
            {
                continue;
            }

            var relative = positions[index] - origin;
            var along = Vector2.Dot(relative, direction);
            if (along < 0f)
            {
                continue;
            }

            var across = relative.LengthSquared() - along * along;
            if (across > reach)
            {
                continue;
            }

            best = MathF.Min(best, along - MathF.Sqrt(reach - across));
        }

        if (best < float.MaxValue)
        {
            impact = origin + direction * MathF.Max(0f, best);
            return true;
        }

        impact = origin + direction * ExitDistance(origin, direction);
        return false;
    }

    private static float ExitDistance(Vector2 origin, Vector2 direction)
    {
        var horizontal = direction.X > 0f ? (FieldWidth - origin.X) / direction.X :
            direction.X < 0f ? -origin.X / direction.X : float.MaxValue;
        var vertical = direction.Y > 0f ? (FieldHeight - origin.Y) / direction.Y :
            direction.Y < 0f ? -origin.Y / direction.Y : float.MaxValue;
        return MathF.Min(horizontal, vertical);
    }

    private void LoadStage()
    {
        track.Build(ShapeFor(Stage), FieldWidth, FieldHeight, TrackMargin, SampleSpacing);
        Quota = QuotaFor(Stage);
        Colours = ColoursFor(Stage);
        crawlSpeed = CrawlSpeedFor(Stage);
        count = 0;
        shotCount = 0;
        spawned = 0;
        spawnRun = 0;
        sinceLastPower = 0;
        rushActive = true;
        elapsed = 0f;
        fireCooldown = 0f;
        clearTimer = 0f;
        chainDepth = 0;
        ClearPowers();
        presentMask = 0;
        LoadedColour = RollAmmo();
        NextColour = RollAmmo();
        clock.Reset();
        StageStartedThisFrame = true;
    }

    private void ClearPowers()
    {
        ArmedPower = CoilPower.None;
        FreezeLeft = 0f;
        SlowLeft = 0f;
        ReverseLeft = 0f;
        GuideLeft = 0f;
    }

    private void Step(float deltaSeconds)
    {
        switch (State)
        {
            case CoilState.Playing:
                StepPlaying(deltaSeconds);
                return;
            case CoilState.Draining:
                StepDraining(deltaSeconds);
                return;
            case CoilState.StageClear:
                clearTimer -= deltaSeconds;
                if (clearTimer > 0f)
                {
                    return;
                }

                Stage++;
                LoadStage();
                State = CoilState.Playing;
                return;
        }
    }

    private void StepPlaying(float deltaSeconds)
    {
        elapsed += deltaSeconds;
        fireCooldown = MathF.Max(0f, fireCooldown - deltaSeconds);
        FreezeLeft = MathF.Max(0f, FreezeLeft - deltaSeconds);
        SlowLeft = MathF.Max(0f, SlowLeft - deltaSeconds);
        ReverseLeft = MathF.Max(0f, ReverseLeft - deltaSeconds);
        GuideLeft = MathF.Max(0f, GuideLeft - deltaSeconds);
        MoveChain(deltaSeconds, ChainSpeed());
        Spawn();
        DecayVisuals(deltaSeconds);
        UpdatePositions();
        EnsureAmmo();
        StepShots(deltaSeconds);
        if (count > 0 && marbles[count - 1].Arc >= track.Length)
        {
            BeginDrain();
            return;
        }

        if (spawned >= Quota && count == 0)
        {
            BeginStageClear();
        }
    }

    private float ChainSpeed()
    {
        var lead = count > 0 ? marbles[count - 1].Arc : 0f;
        if (rushActive)
        {
            var rushEnd = track.Length * RushFraction;
            if (lead < rushEnd && spawned < Quota)
            {
                var remaining = Math.Clamp((1f - lead / rushEnd) * 2.2f, 0f, 1f);
                return crawlSpeed + (RushSpeed - crawlSpeed) * remaining * remaining;
            }

            rushActive = false;
        }

        if (FreezeLeft > 0f)
        {
            return 0f;
        }

        if (ReverseLeft > 0f)
        {
            return count > 0 && marbles[0].Arc <= -Diameter ? 0f : -crawlSpeed * ReverseFactor;
        }

        var speed = SlowLeft > 0f ? crawlSpeed * SlowFactor : crawlSpeed;
        return speed * (1f - DangerSlowdown * Danger);
    }

    private void MoveChain(float deltaSeconds, float speed)
    {
        if (count == 0)
        {
            chainDepth = 0;
            return;
        }

        for (var index = 1; index < count; index++)
        {
            separated[index] = marbles[index].Arc - marbles[index - 1].Arc > Touch;
        }

        var anyPull = false;
        var start = 0;
        while (start < count)
        {
            var end = start;
            while (end + 1 < count && !separated[end + 1])
            {
                end++;
            }

            var pull = 0f;
            var delta = 0f;
            if (start == 0)
            {
                delta = speed * deltaSeconds;
            }
            else if (marbles[start - 1].Colour == marbles[start].Colour)
            {
                pull = MathF.Max(marbles[start].PullSpeed - PullAcceleration * deltaSeconds, -PullMaxSpeed);
                delta = pull * deltaSeconds;
                anyPull = true;
            }

            for (var index = start; index <= end; index++)
            {
                marbles[index].Arc += delta;
                marbles[index].PullSpeed = pull;
            }

            start = end + 1;
        }

        var joinCount = 0;
        for (var index = 1; index < count; index++)
        {
            var minimum = marbles[index - 1].Arc + Diameter;
            if (marbles[index].Arc < minimum)
            {
                marbles[index].Arc = minimum;
            }

            if (separated[index] && marbles[index].Arc - marbles[index - 1].Arc <= Touch)
            {
                joins[joinCount++] = index;
            }
        }

        if (!anyPull && joinCount == 0)
        {
            chainDepth = 0;
            return;
        }

        for (var joinIndex = joinCount - 1; joinIndex >= 0; joinIndex--)
        {
            var index = joins[joinIndex];
            if (index >= count || marbles[index - 1].Colour != marbles[index].Colour || !Touching(index - 1))
            {
                continue;
            }

            UpdatePositions();
            TryClear(index, true);
        }
    }

    private bool Touching(int index) => marbles[index + 1].Arc - marbles[index].Arc <= Touch;

    private bool OnTrack(int index) => marbles[index].Arc >= 0f && marbles[index].Arc <= track.Length;

    private void Spawn()
    {
        if (spawned >= Quota || count >= MarbleCapacity || (count > 0 && marbles[0].Arc < 0f))
        {
            return;
        }

        var arc = count > 0 && marbles[0].Arc <= Touch ? marbles[0].Arc - Diameter : -Diameter;
        for (var index = count; index > 0; index--)
        {
            marbles[index] = marbles[index - 1];
        }

        marbles[0] = new CoilMarble
        {
            Arc = arc,
            Colour = SpawnColour(),
            Power = SpawnPower(),
        };
        count++;
        spawned++;
    }

    private byte SpawnColour()
    {
        if (spawnRun > 0 && spawnRun < MaxSpawnRun && random.NextFloat() < RepeatChance)
        {
            spawnRun++;
            return lastSpawnColour;
        }

        var colour = (byte)random.Next(Colours);
        if (spawnRun > 0 && colour == lastSpawnColour)
        {
            colour = (byte)((colour + 1 + random.Next(Colours - 1)) % Colours);
        }

        lastSpawnColour = colour;
        spawnRun = 1;
        return colour;
    }

    private CoilPower SpawnPower()
    {
        sinceLastPower++;
        if (spawned < PowerWarmup || sinceLastPower < PowerSpacing || random.NextFloat() >= PowerChance)
        {
            return CoilPower.None;
        }

        sinceLastPower = 0;
        return (CoilPower)(1 + random.Next(6));
    }

    private void DecayVisuals(float deltaSeconds)
    {
        var lagBlend = MathF.Min(1f, LagRate * deltaSeconds);
        var arriveStep = deltaSeconds / ArriveSeconds;
        for (var index = 0; index < count; index++)
        {
            ref var marble = ref marbles[index];
            marble.Lag -= marble.Lag * lagBlend;
            marble.Arrive = MathF.Max(0f, marble.Arrive - arriveStep);
        }
    }

    private void UpdatePositions()
    {
        presentMask = 0;
        for (var index = 0; index < count; index++)
        {
            positions[index] = track.PositionAt(marbles[index].Arc);
            presentMask |= 1 << marbles[index].Colour;
        }
    }

    private void EnsureAmmo()
    {
        if (presentMask == 0)
        {
            return;
        }

        if ((presentMask & (1 << LoadedColour)) == 0)
        {
            LoadedColour = RollAmmo();
        }

        if ((presentMask & (1 << NextColour)) == 0)
        {
            NextColour = RollAmmo();
        }
    }

    private byte RollAmmo()
    {
        if (presentMask == 0)
        {
            return (byte)random.Next(Colours);
        }

        var available = 0;
        for (var colour = 0; colour < MaxColours; colour++)
        {
            if ((presentMask & (1 << colour)) != 0)
            {
                available++;
            }
        }

        var pick = random.Next(available);
        for (var colour = 0; colour < MaxColours; colour++)
        {
            if ((presentMask & (1 << colour)) == 0)
            {
                continue;
            }

            if (pick == 0)
            {
                return (byte)colour;
            }

            pick--;
        }

        return 0;
    }

    private void StepShots(float deltaSeconds)
    {
        var substeps = new Substeps(deltaSeconds, MarbleRadius * 0.5f / ShotSpeed);
        for (var shotIndex = shotCount - 1; shotIndex >= 0; shotIndex--)
        {
            for (var substep = 0; substep < substeps.Count; substep++)
            {
                ref var shot = ref shots[shotIndex];
                shot.Position += shot.Velocity * substeps.Step;
                var hit = FindHit(shot.Position);
                if (hit >= 0)
                {
                    var landed = shot;
                    RemoveShot(shotIndex);
                    Land(landed, hit);
                    break;
                }

                TrackGaps(ref shot);
                if (OutOfField(shot.Position))
                {
                    RemoveShot(shotIndex);
                    break;
                }
            }
        }
    }

    private static bool OutOfField(Vector2 position) =>
        position.X < -Diameter || position.X > FieldWidth + Diameter || position.Y < -Diameter ||
        position.Y > FieldHeight + Diameter;

    private void RemoveShot(int shotIndex)
    {
        shots[shotIndex] = shots[shotCount - 1];
        shotCount--;
    }

    private int FindHit(Vector2 position)
    {
        var best = -1;
        var bestDistance = HitDistance * HitDistance;
        for (var index = 0; index < count; index++)
        {
            if (!OnTrack(index))
            {
                continue;
            }

            var distance = Vector2.DistanceSquared(positions[index], position);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            best = index;
        }

        return best;
    }

    private void TrackGaps(ref CoilShot shot)
    {
        var arc = track.NearestArc(shot.Position, out var distanceSquared);
        var near = distanceSquared < TrackCrossDistance * TrackCrossDistance;
        if (near && !shot.OnTrack)
        {
            shot.OnTrack = true;
            shot.CrossArc = arc;
            return;
        }

        if (near || !shot.OnTrack)
        {
            return;
        }

        shot.OnTrack = false;
        if (count < 2 || shot.CrossArc <= marbles[0].Arc || shot.CrossArc >= marbles[count - 1].Arc)
        {
            return;
        }

        shot.Gaps++;
        var points = GapPoints * shot.Gaps;
        Score += points;
        if (gapCount < SmallEventCapacity)
        {
            gaps[gapCount++] = new CoilGap(track.PositionAt(shot.CrossArc), points);
        }
    }

    private void Land(in CoilShot shot, int hitIndex)
    {
        LandedThisFrame = true;
        LandPosition = shot.Position;
        switch (shot.Kind)
        {
            case CoilPower.Blast:
                MarkWithin(shot.Position, BlastRadius);
                RemoveMarked(Math.Max(chainDepth, 1));
                return;
            case CoilPower.Prism:
                MarkColour(marbles[hitIndex].Colour);
                RemoveMarked(Math.Max(chainDepth, 1));
                return;
        }

        var tangent = track.TangentAt(marbles[hitIndex].Arc);
        var ahead = Vector2.Dot(shot.Position - positions[hitIndex], tangent) >= 0f;
        var inserted = Insert(hitIndex, ahead, shot.Colour, shot.Position);
        if (inserted < 0)
        {
            return;
        }

        UpdatePositions();
        TryClear(inserted, false);
    }

    internal int Insert(int hitIndex, bool ahead, byte colour, Vector2 from)
    {
        if (count >= MarbleCapacity)
        {
            return -1;
        }

        var index = ahead ? hitIndex + 1 : hitIndex;
        var arc = ahead ? marbles[hitIndex].Arc + Diameter : marbles[hitIndex].Arc;
        var pull = marbles[hitIndex].PullSpeed;
        for (var shift = count; shift > index; shift--)
        {
            marbles[shift] = marbles[shift - 1];
        }

        marbles[index] = new CoilMarble
        {
            Arc = arc,
            PullSpeed = pull,
            Arrive = 1f,
            ArriveFrom = from,
            Colour = colour,
        };
        count++;
        for (var next = index + 1; next < count; next++)
        {
            var minimum = marbles[next - 1].Arc + Diameter;
            if (marbles[next].Arc >= minimum)
            {
                break;
            }

            marbles[next].Lag += minimum - marbles[next].Arc;
            marbles[next].Arc = minimum;
        }

        return index;
    }

    private void TryClear(int index, bool fromCollision)
    {
        var colour = marbles[index].Colour;
        var first = index;
        while (first > 0 && marbles[first - 1].Colour == colour && Touching(first - 1))
        {
            first--;
        }

        var last = index;
        while (last < count - 1 && marbles[last + 1].Colour == colour && Touching(last))
        {
            last++;
        }

        if (last - first + 1 < 3)
        {
            return;
        }

        for (var mark = first; mark <= last; mark++)
        {
            marked[mark] = true;
        }

        RemoveMarked(fromCollision ? chainDepth + 1 : Math.Max(chainDepth, 1));
    }

    private void MarkWithin(Vector2 center, float radius)
    {
        var reach = radius * radius;
        for (var index = 0; index < count; index++)
        {
            marked[index] = OnTrack(index) && Vector2.DistanceSquared(positions[index], center) <= reach;
        }
    }

    private void MarkColour(byte colour)
    {
        for (var index = 0; index < count; index++)
        {
            marked[index] = OnTrack(index) && marbles[index].Colour == colour;
        }
    }

    private void RemoveMarked(int multiplier)
    {
        var removed = 0;
        var sum = Vector2.Zero;
        byte colour = 0;
        var write = 0;
        for (var read = 0; read < count; read++)
        {
            if (!marked[read])
            {
                marbles[write] = marbles[read];
                positions[write] = positions[read];
                write++;
                continue;
            }

            marked[read] = false;
            ref readonly var marble = ref marbles[read];
            var position = positions[read];
            colour = marble.Colour;
            sum += position;
            removed++;
            if (burstCount < EventCapacity)
            {
                bursts[burstCount++] = new CoilBurst(position, marble.Colour, marble.Power);
            }

            if (marble.Power != CoilPower.None)
            {
                Activate(marble.Power, position);
            }
        }

        count = write;
        if (removed == 0)
        {
            return;
        }

        chainDepth = multiplier;
        var points = removed * PointsPerMarble * multiplier;
        Score += points;
        if (clearCount < ClearCapacity)
        {
            clears[clearCount++] = new CoilClear(sum / removed, removed, points, multiplier, colour);
        }

        UpdatePositions();
        EnsureAmmo();
    }

    internal void Activate(CoilPower power, Vector2 position)
    {
        switch (power)
        {
            case CoilPower.Freeze:
                FreezeLeft = FreezeSeconds;
                break;
            case CoilPower.Slow:
                SlowLeft = SlowSeconds;
                break;
            case CoilPower.Reverse:
                ReverseLeft = ReverseSeconds;
                break;
            case CoilPower.Guide:
                GuideLeft = GuideSeconds;
                break;
            case CoilPower.Blast:
            case CoilPower.Prism:
                ArmedPower = power;
                break;
            default:
                return;
        }

        if (triggerCount < SmallEventCapacity)
        {
            triggers[triggerCount++] = new CoilTrigger(position, power);
        }
    }

    private void BeginDrain()
    {
        State = CoilState.Draining;
        DrainStartedThisFrame = true;
        drainSpeed = DrainStartSpeed;
        shotCount = 0;
        chainDepth = 0;
        ClearPowers();
    }

    private void StepDraining(float deltaSeconds)
    {
        drainSpeed = MathF.Min(DrainMaxSpeed, drainSpeed + DrainAcceleration * deltaSeconds);
        for (var index = 0; index < count; index++)
        {
            marbles[index].Arc += drainSpeed * deltaSeconds;
        }

        DecayVisuals(deltaSeconds);
        while (count > 0 && marbles[count - 1].Arc > track.Length + MarbleRadius)
        {
            count--;
            if (swallowedCount < EventCapacity)
            {
                swallowed[swallowedCount++] = new CoilBurst(track.End, marbles[count].Colour, CoilPower.None);
            }
        }

        UpdatePositions();
        if (count > 0)
        {
            return;
        }

        Lives--;
        LifeLostThisFrame = true;
        if (Lives <= 0)
        {
            State = CoilState.GameOver;
            GameOverThisFrame = true;
            return;
        }

        LoadStage();
        State = CoilState.Playing;
    }

    private void BeginStageClear()
    {
        State = CoilState.StageClear;
        StageClearedThisFrame = true;
        clearTimer = StageClearSeconds;
        ClearBonus = StageClearBase + TimeBonus(Stage, elapsed);
        Score += ClearBonus;
        shotCount = 0;
        ClearPowers();
    }

    internal void StartAtStage(int stage)
    {
        Stage = stage;
        LoadStage();
        State = CoilState.Playing;
    }

    internal void LoadChain(ReadOnlySpan<byte> colours, float leadArc)
    {
        State = CoilState.Playing;
        rushActive = false;
        spawned = Quota;
        shotCount = 0;
        chainDepth = 0;
        count = colours.Length;
        for (var index = 0; index < count; index++)
        {
            marbles[index] = new CoilMarble
            {
                Arc = leadArc - (count - 1 - index) * Diameter,
                Colour = colours[index],
            };
        }

        UpdatePositions();
    }

    internal void SetMarble(int index, float arc, CoilPower power)
    {
        marbles[index].Arc = arc;
        marbles[index].Power = power;
        UpdatePositions();
    }

    internal void LandAt(int hitIndex, bool ahead, byte colour, CoilPower kind = CoilPower.None)
    {
        var tangent = track.TangentAt(marbles[hitIndex].Arc);
        var shot = new CoilShot
        {
            Position = positions[hitIndex] + tangent * (ahead ? MarbleRadius : -MarbleRadius),
            Colour = colour,
            Kind = kind,
        };
        Land(shot, hitIndex);
    }

    internal void SetAmmo(byte loaded, byte next)
    {
        LoadedColour = loaded;
        NextColour = next;
    }

    internal void RefreshAmmo()
    {
        UpdatePositions();
        LoadedColour = RollAmmo();
        NextColour = RollAmmo();
    }
}
