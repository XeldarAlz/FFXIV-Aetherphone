using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Lander;

internal enum LanderState : byte
{
    Flying,
    Landed,
    Crashed,
    Over,
}

internal enum LanderCrash : byte
{
    None,
    MissedPad,
    TooFast,
    TooSteep,
    Hull,
}

internal sealed class LanderBoard
{
    public const float Gravity = 1.62f;
    public const float Thrust = 4.4f;
    public const float RotateSpeed = 2.4f;
    public const float MaxTilt = 1.75f;
    public const float SafeDescent = 1.8f;
    public const float SafeDrift = 1f;
    public const float SafeTilt = 0.1745f;
    public const float PerfectDescent = 0.6f;
    public const float PerfectDrift = 0.25f;
    public const float PerfectTilt = 0.035f;
    public const float FootSpread = 0.75f;
    public const float FootDrop = 0.95f;
    public const float HullRadius = 0.55f;
    public const float HullLift = -0.25f;
    public const float NozzleDrop = 0.62f;
    public const float SpawnY = 5f;
    public const float SpawnMargin = 10f;
    public const float BaseFuel = 20f;
    public const float FuelPerLevel = 1.2f;
    public const float MinFuel = 9f;
    public const float LowFuelFraction = 0.2f;
    public const float BaseDrift = 1f;
    public const float DriftPerLevel = 0.25f;
    public const float MaxStartDrift = 3.5f;
    public const float LandedSeconds = 2.8f;
    public const float CrashSeconds = 2.2f;
    public const int StartLives = 3;
    public const int BasePoints = 50;
    public const int SoftPoints = 100;
    public const int FuelPoints = 10;
    public const float FixedStep = 1f / 120f;
    public const float Ceiling = -6f;
    public const float EdgeMargin = 0.9f;
    private const float MaxCatchUpSeconds = 0.1f;
    private const int HullSamples = 10;
    private const float SettleRate = 9f;

    private readonly LanderTerrain terrain = new();
    private GameRandom random;
    private FixedStepClock clock = new(FixedStep, MaxCatchUpSeconds);
    private float spawnX;
    private float spawnDrift;
    private bool lowFuelWarned;

    public LanderTerrain Terrain => terrain;

    public LanderState State { get; private set; }

    public Vector2 Position { get; private set; }

    public Vector2 Velocity { get; private set; }

    public float Angle { get; private set; }

    public float Fuel { get; private set; }

    public float FuelCapacity { get; private set; }

    public bool Thrusting { get; private set; }

    public int Rotating { get; private set; }

    public int Level { get; private set; }

    public int Lives { get; private set; }

    public int Score { get; private set; }

    public int Landings { get; private set; }

    public int Perfects { get; private set; }

    public int BestLanding { get; private set; }

    public float PlaySeconds { get; private set; }

    public float StateSeconds { get; private set; }

    public int PadIndex { get; private set; } = -1;

    public bool LandedThisStep { get; private set; }

    public bool PerfectThisStep { get; private set; }

    public int LastLandingPoints { get; private set; }

    public LanderCrash CrashThisStep { get; private set; }

    public Vector2 CrashPoint { get; private set; }

    public bool LevelStartedThisStep { get; private set; }

    public bool RespawnedThisStep { get; private set; }

    public bool FuelLowThisStep { get; private set; }

    public bool FuelEmptyThisStep { get; private set; }

    public float FuelFraction => FuelCapacity <= 0f ? 0f : Fuel / FuelCapacity;

    public float Descent => Velocity.Y;

    public float Drift => MathF.Abs(Velocity.X);

    public float Tilt => MathF.Abs(Angle);

    public Vector2 Up => new(MathF.Sin(Angle), -MathF.Cos(Angle));

    public float Altitude => terrain.GroundY(Position.X) - (Position.Y + FootDrop);

    public static float FuelFor(int level) => MathF.Max(MinFuel, BaseFuel - FuelPerLevel * (level - 1));

    public static float StartDrift(int level) => MathF.Min(MaxStartDrift, BaseDrift + DriftPerLevel * (level - 1));

    public static int PointsFor(float descent, float fuel, int multiplier, bool perfect)
    {
        var softness = 1f - Math.Clamp(descent / SafeDescent, 0f, 1f);
        var points = (BasePoints + (int)MathF.Round(SoftPoints * softness) + (int)MathF.Round(fuel * FuelPoints)) *
                     multiplier;
        return perfect ? points * 3 / 2 : points;
    }

    public Vector2 ToWorld(Vector2 local)
    {
        var cosine = MathF.Cos(Angle);
        var sine = MathF.Sin(Angle);
        return Position + new Vector2(local.X * cosine - local.Y * sine, local.X * sine + local.Y * cosine);
    }

    public void Reset(GameRandom seededRandom)
    {
        random = seededRandom;
        clock.Reset();
        Level = 1;
        Lives = StartLives;
        Score = 0;
        Landings = 0;
        Perfects = 0;
        BestLanding = 0;
        PlaySeconds = 0f;
        ClearEvents();
        StartLevel();
    }

    public void Step(float deltaSeconds, int rotate, bool thrust)
    {
        ClearEvents();
        if (deltaSeconds <= 0f || State == LanderState.Over)
        {
            return;
        }

        var ticks = clock.Advance(deltaSeconds);
        for (var tick = 0; tick < ticks && State != LanderState.Over; tick++)
        {
            Tick(FixedStep, Math.Sign(rotate), thrust);
        }
    }

    internal void Place(Vector2 position, Vector2 velocity, float angle)
    {
        Position = position;
        Velocity = velocity;
        Angle = angle;
        State = LanderState.Flying;
    }

    internal void SetFuel(float fuel)
    {
        Fuel = Math.Clamp(fuel, 0f, FuelCapacity);
    }

    private void Tick(float deltaSeconds, int rotate, bool thrust)
    {
        StateSeconds += deltaSeconds;
        switch (State)
        {
            case LanderState.Landed:
                Angle -= Angle * MathF.Min(1f, SettleRate * deltaSeconds);
                Thrusting = false;
                Rotating = 0;
                if (StateSeconds >= LandedSeconds)
                {
                    Level++;
                    StartLevel();
                }

                return;
            case LanderState.Crashed:
                Thrusting = false;
                Rotating = 0;
                if (StateSeconds < CrashSeconds)
                {
                    return;
                }

                if (Lives > 0)
                {
                    Respawn();
                }
                else
                {
                    State = LanderState.Over;
                }

                return;
            case LanderState.Over:
                return;
        }

        PlaySeconds += deltaSeconds;
        Rotating = rotate;
        Angle = Math.Clamp(Angle + rotate * RotateSpeed * deltaSeconds, -MaxTilt, MaxTilt);
        var hadFuel = Fuel > 0f;
        Thrusting = thrust && hadFuel;
        var acceleration = new Vector2(0f, Gravity);
        if (Thrusting)
        {
            Fuel = MathF.Max(0f, Fuel - deltaSeconds);
            acceleration += Up * Thrust;
            if (!lowFuelWarned && FuelFraction <= LowFuelFraction)
            {
                lowFuelWarned = true;
                FuelLowThisStep = true;
            }

            if (Fuel <= 0f)
            {
                FuelEmptyThisStep = true;
            }
        }

        var velocity = Velocity + acceleration * deltaSeconds;
        var position = Position + velocity * deltaSeconds;
        if (position.X < EdgeMargin || position.X > LanderTerrain.Width - EdgeMargin)
        {
            position.X = Math.Clamp(position.X, EdgeMargin, LanderTerrain.Width - EdgeMargin);
            velocity.X = 0f;
        }

        if (position.Y < Ceiling)
        {
            position.Y = Ceiling;
            velocity.Y = MathF.Max(0f, velocity.Y);
        }

        Position = position;
        Velocity = velocity;
        CheckContact();
    }

    private void CheckContact()
    {
        var footLeft = ToWorld(new Vector2(-FootSpread, FootDrop));
        var footRight = ToWorld(new Vector2(FootSpread, FootDrop));
        var leftTouch = footLeft.Y >= terrain.GroundY(footLeft.X);
        var rightTouch = footRight.Y >= terrain.GroundY(footRight.X);
        var hull = HullContact(out var hullPoint);
        if (!leftTouch && !rightTouch)
        {
            if (hull)
            {
                Crash(LanderCrash.Hull, hullPoint);
            }

            return;
        }

        var contact = leftTouch ? footLeft : footRight;
        var pad = terrain.PadUnder(MathF.Min(footLeft.X, footRight.X), MathF.Max(footLeft.X, footRight.X));
        if (pad < 0 || hull)
        {
            Crash(hull ? LanderCrash.Hull : LanderCrash.MissedPad, hull ? hullPoint : contact);
            return;
        }

        if (Tilt > SafeTilt)
        {
            Crash(LanderCrash.TooSteep, contact);
            return;
        }

        if (Descent > SafeDescent || Drift > SafeDrift)
        {
            Crash(LanderCrash.TooFast, contact);
            return;
        }

        Land(pad);
    }

    private bool HullContact(out Vector2 point)
    {
        var center = ToWorld(new Vector2(0f, HullLift));
        for (var sample = 0; sample < HullSamples; sample++)
        {
            var angle = sample * MathF.Tau / HullSamples;
            var candidate = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * HullRadius;
            if (candidate.Y >= terrain.GroundY(candidate.X))
            {
                point = candidate;
                return true;
            }
        }

        var nozzle = ToWorld(new Vector2(0f, NozzleDrop));
        if (nozzle.Y >= terrain.GroundY(nozzle.X))
        {
            point = nozzle;
            return true;
        }

        point = default;
        return false;
    }

    private void Land(int pad)
    {
        var spot = terrain.Pads[pad];
        var descent = MathF.Max(0f, Descent);
        var perfect = descent <= PerfectDescent && Drift <= PerfectDrift && Tilt <= PerfectTilt;
        var points = PointsFor(descent, Fuel, spot.Multiplier, perfect);
        Score += points;
        Landings++;
        BestLanding = Math.Max(BestLanding, points);
        if (perfect)
        {
            Perfects++;
        }

        LastLandingPoints = points;
        LandedThisStep = true;
        PerfectThisStep = perfect;
        PadIndex = pad;
        Position = new Vector2(Position.X, spot.Y - FootDrop);
        Velocity = Vector2.Zero;
        Thrusting = false;
        State = LanderState.Landed;
        StateSeconds = 0f;
    }

    private void Crash(LanderCrash reason, Vector2 point)
    {
        CrashThisStep = reason;
        CrashPoint = point;
        Lives = Math.Max(0, Lives - 1);
        Velocity = Vector2.Zero;
        Thrusting = false;
        State = LanderState.Crashed;
        StateSeconds = 0f;
    }

    private void StartLevel()
    {
        terrain.Generate(ref random, Level);
        FuelCapacity = FuelFor(Level);
        spawnX = random.Range(SpawnMargin, LanderTerrain.Width - SpawnMargin);
        var toward = spawnX < LanderTerrain.Width * 0.5f ? 1f : -1f;
        spawnDrift = toward * StartDrift(Level) * random.Range(0.8f, 1.2f);
        LevelStartedThisStep = true;
        Respawn();
    }

    private void Respawn()
    {
        Position = new Vector2(spawnX, SpawnY);
        Velocity = new Vector2(spawnDrift, 0.3f);
        Angle = 0f;
        Fuel = FuelCapacity;
        lowFuelWarned = false;
        PadIndex = -1;
        Thrusting = false;
        Rotating = 0;
        State = LanderState.Flying;
        StateSeconds = 0f;
        RespawnedThisStep = true;
    }

    private void ClearEvents()
    {
        LandedThisStep = false;
        PerfectThisStep = false;
        CrashThisStep = LanderCrash.None;
        LevelStartedThisStep = false;
        RespawnedThisStep = false;
        FuelLowThisStep = false;
        FuelEmptyThisStep = false;
    }
}
