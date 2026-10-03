using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Swoop;

internal enum SwoopLanding : byte
{
    Smooth,
    Thud,
}

internal struct SwoopCrystal
{
    public double X;
    public float Y;
    public float Phase;
}

internal sealed class SwoopBoard
{
    public const float DaySeconds = 60f;
    public const float StepSeconds = 1f / 120f;
    public const float BirdRadius = 1.1f;
    public const float CrystalRadius = 0.7f;
    public const float SampleSpacing = 0.8f;
    public const int SampleCapacity = 400;
    public const float SampleBehind = 70f;
    public const float SampleAhead = 240f;
    public const float LightGravity = 18f;
    public const float HeavyGravity = 36f;
    public const float GlideGravity = 11f;
    public const float CruiseSpeed = 11f;
    public const float CruiseThrust = 8f;
    public const float MinDaySpeed = 1.5f;
    public const float FeverThrust = 4f;
    public const float FeverTopSpeed = 38f;
    public const float MaxSpeed = 48f;
    public const float AirDrag = 0.0025f;
    public const float GroundDrag = 0.003f;
    public const float NightFriction = 5f;
    public const float SmoothAngle = 0.4f;
    public const float SmoothMaxSlope = 0.08f;
    public const float MinCountedAirSeconds = 0.35f;
    public const float SmoothBoost = 1.6f;
    public const float ThudKeep = 0.45f;
    public const float MildThudKeep = 0.75f;
    public const float ThudSeverityRange = 0.6f;
    public const float SlamKeep = 0.55f;
    public const float SlamSlope = 0.2f;
    public const float SlamPress = 30f;
    public const int FeverStreak = 3;
    public const int SmoothPoints = 50;
    public const int CrystalPoints = 25;
    public const float AirPointsPerSecond = 40f;
    public const float IslandBonusSeconds = 18f;
    public const float StopSpeed = 0.35f;
    public const float StillSeconds = 0.5f;
    public const float MaxNightSeconds = 20f;
    public const float StartX = 6f;
    public const int CrystalCapacity = 64;
    public const int PickupCapacity = 8;
    private const float MaxCatchUpSeconds = 0.25f;
    private const float CrystalSpawnAhead = 200f;
    private const float CrystalDespawnBehind = 60f;
    private const float FirstCrystalX = 70f;
    private const float MinCrystalGap = 30f;
    private const float MaxCrystalGap = 70f;
    private const float CrystalReach = BirdRadius + CrystalRadius + 0.3f;
    private const uint CrystalSeedMask = 0xA5A5F00Du;

    private readonly SwoopTerrain terrain = new();
    private readonly float[] samples = new float[SampleCapacity];
    private readonly SwoopCrystal[] crystals = new SwoopCrystal[CrystalCapacity];
    private readonly float[] pickupY = new float[PickupCapacity];
    private readonly double[] pickupX = new double[PickupCapacity];
    private SwoopRandom random = new(1u);
    private FixedStepClock stepClock = new(StepSeconds, MaxCatchUpSeconds);
    private int firstSample;
    private int endSample;
    private bool samplesValid;
    private int crystalCount;
    private double nextCrystalX;
    private double furthestX;
    private float distancePoints;
    private float stillSeconds;
    private bool slammed;

    public SwoopTerrain Terrain => terrain;
    public double X { get; private set; }
    public float Y { get; private set; }
    public float Speed { get; private set; }
    public float VelocityX { get; private set; }
    public float VelocityY { get; private set; }
    public float GroundSlope { get; private set; }
    public bool Grounded { get; private set; }
    public bool Holding { get; private set; }
    public float AirSeconds { get; private set; }
    public float BestAirSeconds { get; private set; }
    public float Clock { get; private set; }
    public bool Night { get; private set; }
    public float NightSeconds { get; private set; }
    public bool GameOver { get; private set; }
    public bool Fever { get; private set; }
    public int SmoothStreak { get; private set; }
    public int CurrentIsland { get; private set; }
    public int IslandsReached { get; private set; }
    public int BonusPoints { get; private set; }
    public int Score => (int)distancePoints + BonusPoints;
    public int Distance => (int)(furthestX - StartX);
    public int Multiplier => Fever ? 2 : 1;
    public float ClockFraction => Clock / DaySeconds;
    public float DayProgress => Night ? 1f : 1f - ClockFraction;
    public int FirstSample => firstSample;
    public int EndSample => endSample;
    public int CrystalCount => crystalCount;
    public bool LaunchedThisTick { get; private set; }
    public bool SmoothThisTick { get; private set; }
    public bool ThudThisTick { get; private set; }
    public bool AirCountedThisTick { get; private set; }
    public float LastAirSeconds { get; private set; }
    public float LastImpactSpeed { get; private set; }
    public bool FeverStartedThisTick { get; private set; }
    public bool FeverEndedThisTick { get; private set; }
    public bool IslandReachedThisTick { get; private set; }
    public float LastBonusSeconds { get; private set; }
    public bool NightFellThisTick { get; private set; }
    public bool EndedThisTick { get; private set; }
    public int PickupsThisTick { get; private set; }

    public float IslandProgress
    {
        get
        {
            var start = terrain.IslandStart(CurrentIsland);
            var end = terrain.IslandEnd(CurrentIsland);
            return Math.Clamp((float)((X - start) / (end - start)), 0f, 1f);
        }
    }

    public float Altitude => Y - (float)terrain.Height(X);

    public static double SampleX(int sample) => sample * (double)SampleSpacing;

    public float SampleHeight(int sample)
    {
        var slot = sample % SampleCapacity;
        if (slot < 0)
        {
            slot += SampleCapacity;
        }

        return samples[slot];
    }

    public ref readonly SwoopCrystal Crystal(int index) => ref crystals[index];

    public double PickupX(int index) => pickupX[index];

    public float PickupY(int index) => pickupY[index];

    public static SwoopLanding Classify(Vector2 velocity, float slope) =>
        LandingAngle(velocity, slope) <= SmoothAngle && slope <= SmoothMaxSlope ? SwoopLanding.Smooth : SwoopLanding.Thud;

    public static float LandingAngle(Vector2 velocity, float slope)
    {
        var speed = velocity.Length();
        if (speed < 0.001f)
        {
            return 0f;
        }

        var norm = MathF.Sqrt(1f + slope * slope);
        var along = (velocity.X + velocity.Y * slope) / norm;
        return MathF.Acos(Math.Clamp(along / speed, -1f, 1f));
    }

    public static float ThudRetention(float angle)
    {
        var severity = Math.Clamp((angle - SmoothAngle) / ThudSeverityRange, 0f, 1f);
        return ThudKeep + (MildThudKeep - ThudKeep) * (1f - severity);
    }

    public void Start(uint seed)
    {
        terrain.Generate(seed);
        random = new SwoopRandom(seed ^ CrystalSeedMask);
        stepClock.Reset();
        X = StartX;
        Y = (float)terrain.Height(X);
        Speed = CruiseSpeed;
        VelocityX = 0f;
        VelocityY = 0f;
        GroundSlope = 0f;
        Grounded = true;
        Holding = false;
        AirSeconds = 0f;
        BestAirSeconds = 0f;
        Clock = DaySeconds;
        Night = false;
        NightSeconds = 0f;
        GameOver = false;
        Fever = false;
        SmoothStreak = 0;
        CurrentIsland = 0;
        IslandsReached = 0;
        BonusPoints = 0;
        distancePoints = 0f;
        stillSeconds = 0f;
        slammed = false;
        furthestX = X;
        crystalCount = 0;
        nextCrystalX = FirstCrystalX;
        samplesValid = false;
        ClearEvents();
        RefreshSamples();
        SpawnCrystals();
    }

    public void Tick(float deltaSeconds, bool holding)
    {
        ClearEvents();
        if (GameOver)
        {
            return;
        }

        var steps = stepClock.Advance(deltaSeconds);
        for (var step = 0; step < steps; step++)
        {
            Step(holding);
            if (GameOver)
            {
                break;
            }
        }

        RefreshSamples();
    }

    internal void PlaceOnGround(double x, float speed)
    {
        X = x;
        Y = (float)terrain.Height(x);
        Speed = speed;
        Grounded = true;
        AirSeconds = 0f;
        furthestX = Math.Max(furthestX, x);
        CurrentIsland = Math.Max(0, terrain.IslandIndexAt(x));
        RefreshSamples();
    }

    internal void PlaceInAir(double x, float altitude, Vector2 velocity, float airSeconds)
    {
        X = x;
        Y = (float)terrain.Height(x) + altitude;
        VelocityX = velocity.X;
        VelocityY = velocity.Y;
        Speed = velocity.Length();
        Grounded = false;
        AirSeconds = airSeconds;
        furthestX = Math.Max(furthestX, x);
        CurrentIsland = Math.Max(0, terrain.IslandIndexAt(x));
        RefreshSamples();
    }

    internal void SetClock(float seconds)
    {
        Clock = seconds;
    }

    private void ClearEvents()
    {
        LaunchedThisTick = false;
        SmoothThisTick = false;
        ThudThisTick = false;
        AirCountedThisTick = false;
        FeverStartedThisTick = false;
        FeverEndedThisTick = false;
        IslandReachedThisTick = false;
        NightFellThisTick = false;
        EndedThisTick = false;
        PickupsThisTick = 0;
    }

    private void Step(bool holding)
    {
        var heavy = holding && !Night;
        Holding = heavy;
        var gravity = heavy ? HeavyGravity : Grounded ? LightGravity : GlideGravity;
        if (Grounded)
        {
            StepGround(gravity, heavy);
        }
        else
        {
            StepAir(gravity);
        }

        if (X > furthestX)
        {
            distancePoints += (float)(X - furthestX) * Multiplier;
            furthestX = X;
        }

        CollectCrystals();
        SpawnCrystals();
        AdvanceClock();
        CheckIslands();
        CheckStopped();
    }

    private void StepGround(float gravity, bool heavy)
    {
        terrain.Evaluate(X, out _, out var slopeValue, out var curvatureValue);
        var slope = (float)slopeValue;
        GroundSlope = slope;
        var norm = MathF.Sqrt(1f + slope * slope);
        var acceleration = -gravity * slope / norm;
        if (!Night)
        {
            if (Speed < CruiseSpeed)
            {
                acceleration += CruiseThrust;
            }

            if (Fever && Speed < FeverTopSpeed)
            {
                acceleration += FeverThrust;
            }
        }

        acceleration -= GroundDrag * Speed * MathF.Abs(Speed);
        Speed += acceleration * StepSeconds;
        if (Night)
        {
            Speed = Speed > 0f
                ? MathF.Max(0f, Speed - NightFriction * StepSeconds)
                : MathF.Min(0f, Speed + NightFriction * StepSeconds);
        }
        else
        {
            Speed = MathF.Max(Speed, MinDaySpeed);
        }

        Speed = MathF.Min(Speed, MaxSpeed);
        var press = Speed * Speed * (float)curvatureValue / (norm * norm * norm);
        if (!heavy || slope <= 0f)
        {
            slammed = false;
        }
        else if (!slammed && !Night && slope > SlamSlope && press > SlamPress)
        {
            Slam(press);
        }

        if (!heavy && gravity / norm + press < 0f && Speed > 0f)
        {
            Launch(slope, norm);
            return;
        }

        X += Speed / norm * StepSeconds;
        Y = (float)terrain.Height(X);
        VelocityX = Speed / norm;
        VelocityY = Speed * slope / norm;
    }

    private void Slam(float press)
    {
        slammed = true;
        LastImpactSpeed = MathF.Sqrt(press);
        Speed = ClampLandingSpeed(Speed * SlamKeep);
        SmoothStreak = 0;
        ThudThisTick = true;
        EndFever();
    }

    private void Launch(float slope, float norm)
    {
        VelocityX = Speed / norm;
        VelocityY = Speed * slope / norm;
        Grounded = false;
        AirSeconds = 0f;
        LaunchedThisTick = true;
        X += VelocityX * StepSeconds;
        Y += VelocityY * StepSeconds;
    }

    private void StepAir(float gravity)
    {
        VelocityY -= gravity * StepSeconds;
        var speed = MathF.Sqrt(VelocityX * VelocityX + VelocityY * VelocityY);
        var dragFactor = MathF.Max(0f, 1f - AirDrag * speed * StepSeconds);
        VelocityX *= dragFactor;
        VelocityY *= dragFactor;
        Speed = speed * dragFactor;
        X += VelocityX * StepSeconds;
        Y += VelocityY * StepSeconds;
        AirSeconds += StepSeconds;
        terrain.Evaluate(X, out var height, out var slope, out _);
        if (Y > height)
        {
            return;
        }

        Land((float)height, (float)slope);
    }

    private void Land(float height, float slope)
    {
        Y = height;
        GroundSlope = slope;
        Grounded = true;
        var velocity = new Vector2(VelocityX, VelocityY);
        var norm = MathF.Sqrt(1f + slope * slope);
        var along = (VelocityX + VelocityY * slope) / norm;
        LastImpactSpeed = MathF.Max(0f, (VelocityX * slope - VelocityY) / norm);
        var airSeconds = AirSeconds;
        AirSeconds = 0f;
        if (airSeconds < MinCountedAirSeconds)
        {
            Speed = ClampLandingSpeed(along);
            return;
        }

        var multiplier = Multiplier;
        LastAirSeconds = airSeconds;
        BestAirSeconds = MathF.Max(BestAirSeconds, airSeconds);
        AirCountedThisTick = true;
        BonusPoints += (int)(airSeconds * AirPointsPerSecond) * multiplier;
        if (Classify(velocity, slope) == SwoopLanding.Smooth)
        {
            Speed = ClampLandingSpeed(along + SmoothBoost);
            SmoothStreak++;
            SmoothThisTick = true;
            BonusPoints += SmoothPoints * multiplier;
            if (SmoothStreak >= FeverStreak && !Fever && !Night)
            {
                Fever = true;
                FeverStartedThisTick = true;
            }

            return;
        }

        Speed = ClampLandingSpeed(along * ThudRetention(LandingAngle(velocity, slope)));
        SmoothStreak = 0;
        ThudThisTick = true;
        EndFever();
    }

    private float ClampLandingSpeed(float speed)
    {
        var clamped = MathF.Min(speed, MaxSpeed);
        return Night ? clamped : MathF.Max(clamped, MinDaySpeed);
    }

    private void EndFever()
    {
        if (!Fever)
        {
            return;
        }

        Fever = false;
        FeverEndedThisTick = true;
    }

    private void AdvanceClock()
    {
        if (Night)
        {
            NightSeconds += StepSeconds;
            return;
        }

        Clock -= StepSeconds;
        if (Clock > 0f)
        {
            return;
        }

        Clock = 0f;
        Night = true;
        NightFellThisTick = true;
        SmoothStreak = 0;
        EndFever();
    }

    private void CheckIslands()
    {
        while (CurrentIsland < SwoopTerrain.MaxIslands - 1 && X >= terrain.IslandStart(CurrentIsland + 1))
        {
            CurrentIsland++;
            if (Night)
            {
                continue;
            }

            IslandsReached++;
            var before = Clock;
            Clock = MathF.Min(DaySeconds, Clock + IslandBonusSeconds);
            LastBonusSeconds = Clock - before;
            IslandReachedThisTick = true;
        }
    }

    private void CheckStopped()
    {
        if (!Night)
        {
            return;
        }

        if (Grounded && MathF.Abs(Speed) < StopSpeed)
        {
            stillSeconds += StepSeconds;
        }
        else if (MathF.Abs(Speed) > StopSpeed * 4f)
        {
            stillSeconds = 0f;
        }

        if (stillSeconds < StillSeconds && NightSeconds < MaxNightSeconds)
        {
            return;
        }

        GameOver = true;
        EndedThisTick = true;
        Speed = 0f;
        VelocityX = 0f;
        VelocityY = 0f;
    }

    private void CollectCrystals()
    {
        var centerY = Y + BirdRadius;
        for (var index = crystalCount - 1; index >= 0; index--)
        {
            ref var crystal = ref crystals[index];
            var deltaX = (float)(crystal.X - X);
            if (deltaX < -CrystalDespawnBehind)
            {
                RemoveCrystal(index);
                continue;
            }

            var deltaY = crystal.Y - centerY;
            if (deltaX * deltaX + deltaY * deltaY > CrystalReach * CrystalReach)
            {
                continue;
            }

            BonusPoints += CrystalPoints * Multiplier;
            if (PickupsThisTick < PickupCapacity)
            {
                pickupX[PickupsThisTick] = crystal.X;
                pickupY[PickupsThisTick] = crystal.Y;
                PickupsThisTick++;
            }

            RemoveCrystal(index);
        }
    }

    private void RemoveCrystal(int index)
    {
        crystals[index] = crystals[crystalCount - 1];
        crystalCount--;
    }

    private void SpawnCrystals()
    {
        while (nextCrystalX < X + CrystalSpawnAhead)
        {
            SpawnGroup(nextCrystalX);
            nextCrystalX += random.Range(MinCrystalGap, MaxCrystalGap);
        }
    }

    private void SpawnGroup(double x)
    {
        var pattern = random.Next(3);
        if (pattern == 0)
        {
            for (var index = 0; index < 5; index++)
            {
                var crystalX = x + index * 3.0;
                AddCrystal(crystalX, (float)terrain.Height(crystalX) + CrystalRadius + 0.4f);
            }

            return;
        }

        var count = pattern == 1 ? 7 : 8;
        var spacing = pattern == 1 ? 3.2 : 3.6;
        var lift = pattern == 1 ? 5f : 12f;
        var peak = float.MinValue;
        for (var index = 0; index < count; index++)
        {
            peak = MathF.Max(peak, (float)terrain.Height(x + index * spacing));
        }

        var middle = (count - 1) * 0.5f;
        for (var index = 0; index < count; index++)
        {
            var offset = (index - middle) / middle;
            AddCrystal(x + index * spacing, peak + lift + 5f * (1f - offset * offset));
        }
    }

    private void AddCrystal(double x, float y)
    {
        if (crystalCount >= CrystalCapacity)
        {
            return;
        }

        ref var crystal = ref crystals[crystalCount];
        crystal.X = x;
        crystal.Y = y;
        crystal.Phase = random.Range(0f, MathF.Tau);
        crystalCount++;
    }

    private void RefreshSamples()
    {
        var desiredFirst = (int)Math.Floor((X - SampleBehind) / SampleSpacing);
        var desiredEnd = (int)Math.Ceiling((X + SampleAhead) / SampleSpacing);
        if (!samplesValid || desiredFirst < firstSample || desiredFirst >= endSample)
        {
            firstSample = desiredFirst;
            endSample = desiredFirst;
            samplesValid = true;
        }

        firstSample = Math.Max(firstSample, desiredFirst);
        for (var sample = endSample; sample < desiredEnd; sample++)
        {
            var slot = sample % SampleCapacity;
            if (slot < 0)
            {
                slot += SampleCapacity;
            }

            samples[slot] = (float)terrain.Height(SampleX(sample));
        }

        endSample = Math.Max(endSample, desiredEnd);
    }
}
