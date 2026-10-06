using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Blade;

internal enum BladeState : byte
{
    Playing,
    Cleared,
    Over,
}

internal enum BladeThrow : byte
{
    None,
    Stuck,
    Blocked,
    LevelCleared,
}

internal sealed class BladeBoard
{
    public const float ImpactAngle = MathF.PI * 0.5f;
    public const int MaxStuck = 24;
    public const int MaxApples = 3;
    public const float BladeArc = 0.17f;
    public const float AppleArc = 0.2f;
    public const int ApplePoints = 3;
    public const int BossEvery = 5;
    public const float FlightSeconds = 0.085f;
    public const float ClearedSeconds = 0.75f;
    private const float BaseSpeed = 1.45f;
    private const float SpeedPerLevel = 0.13f;
    private const float MaxSpeed = 4.1f;
    private const int BaseBlades = 5;
    private const int BladesPerLevel = 1;
    private const int MaxBlades = 10;
    private const float ClearedSpin = 0.6f;
    private const float OverSpin = 0.25f;
    private const float ObstacleSpacing = 2.4f;
    private const int LevelsPerObstacle = 3;
    private const int MaxObstacles = 3;
    private const int LevelsPerApple = 4;
    private readonly float[] stuck = new float[MaxStuck];
    private readonly float[] apples = new float[MaxApples];
    private GameRandom random;
    private float wheelAngle;
    private float patternTime;
    private float clearedTimer;
    private bool reversed;

    public BladeState State { get; private set; }

    public int StuckCount { get; private set; }

    public int AppleCount { get; private set; }

    public int Level { get; private set; } = 1;

    public int Score { get; private set; }

    public int Apples { get; private set; }

    public int Remaining { get; private set; }

    public int LevelBlades { get; private set; }

    public float FlightProgress { get; private set; }

    public bool InFlight { get; private set; }

    public float Direction { get; private set; } = 1f;

    public bool ReversedThisStep { get; private set; }

    public bool AppleHitThisStep { get; private set; }

    public float AppleHitAngle { get; private set; }

    public float WheelAngle => wheelAngle;

    public bool IsBoss => Level % BossEvery == 0;

    public float StuckAngle(int index) => stuck[index] + wheelAngle;

    public float AppleAngle(int index) => apples[index] + wheelAngle;

    public void Reset(GameRandom seededRandom)
    {
        random = seededRandom;
        State = BladeState.Playing;
        Level = 1;
        Score = 0;
        Apples = 0;
        wheelAngle = 0f;
        patternTime = 0f;
        clearedTimer = 0f;
        InFlight = false;
        FlightProgress = 0f;
        ReversedThisStep = false;
        AppleHitThisStep = false;
        BuildLevel();
    }

    public void StartLevel(int level)
    {
        Level = Math.Max(1, level);
        State = BladeState.Playing;
        InFlight = false;
        FlightProgress = 0f;
        BuildLevel();
    }

    public void PlaceApple(float localAngle)
    {
        if (AppleCount >= MaxApples)
        {
            return;
        }

        apples[AppleCount] = Normalize(localAngle);
        AppleCount++;
    }

    public BladeThrow Step(float deltaSeconds)
    {
        ReversedThisStep = false;
        AppleHitThisStep = false;
        if (deltaSeconds <= 0f)
        {
            return BladeThrow.None;
        }

        patternTime += deltaSeconds;
        if (State == BladeState.Cleared)
        {
            wheelAngle += SpeedOf() * Direction * deltaSeconds * ClearedSpin;
            clearedTimer -= deltaSeconds;
            if (clearedTimer <= 0f)
            {
                Level++;
                BuildLevel();
                State = BladeState.Playing;
            }

            return BladeThrow.None;
        }

        if (State == BladeState.Over)
        {
            wheelAngle += SpeedOf() * Direction * deltaSeconds * OverSpin;
            return BladeThrow.None;
        }

        wheelAngle += SpeedOf() * Direction * deltaSeconds;
        if (!InFlight)
        {
            return BladeThrow.None;
        }

        FlightProgress += deltaSeconds / FlightSeconds;
        if (FlightProgress < 1f)
        {
            return BladeThrow.None;
        }

        InFlight = false;
        FlightProgress = 0f;
        return Resolve();
    }

    public bool Throw()
    {
        if (State != BladeState.Playing || InFlight || Remaining <= 0)
        {
            return false;
        }

        InFlight = true;
        FlightProgress = 0f;
        return true;
    }

    public static float Normalize(float angle)
    {
        var wrapped = angle % (MathF.PI * 2f);
        if (wrapped > MathF.PI)
        {
            return wrapped - MathF.PI * 2f;
        }

        if (wrapped < -MathF.PI)
        {
            return wrapped + MathF.PI * 2f;
        }

        return wrapped;
    }

    private BladeThrow Resolve()
    {
        var local = Normalize(ImpactAngle - wheelAngle);
        for (var index = 0; index < StuckCount; index++)
        {
            if (MathF.Abs(Normalize(stuck[index] - local)) >= BladeArc)
            {
                continue;
            }

            State = BladeState.Over;
            return BladeThrow.Blocked;
        }

        if (StuckCount < MaxStuck)
        {
            stuck[StuckCount] = local;
            StuckCount++;
        }

        Score++;
        Remaining--;
        SliceApple(local);
        if (IsBoss && !reversed && Remaining == LevelBlades / 2)
        {
            Direction = -Direction;
            reversed = true;
            ReversedThisStep = true;
        }

        if (Remaining > 0)
        {
            return BladeThrow.Stuck;
        }

        State = BladeState.Cleared;
        clearedTimer = ClearedSeconds;
        return BladeThrow.LevelCleared;
    }

    private void SliceApple(float local)
    {
        for (var index = 0; index < AppleCount; index++)
        {
            if (MathF.Abs(Normalize(apples[index] - local)) >= AppleArc)
            {
                continue;
            }

            AppleHitThisStep = true;
            AppleHitAngle = apples[index] + wheelAngle;
            Score += ApplePoints;
            Apples++;
            apples[index] = apples[AppleCount - 1];
            AppleCount--;
            return;
        }
    }

    private void BuildLevel()
    {
        LevelBlades = Math.Min(MaxBlades, BaseBlades + (Level - 1) * BladesPerLevel);
        Remaining = LevelBlades;
        StuckCount = 0;
        AppleCount = 0;
        reversed = false;
        Direction = Level % 4 == 0 || Level % 4 == 3 ? -1f : 1f;
        patternTime = 0f;
        var obstacles = Math.Min(MaxObstacles, (Level - 1) / LevelsPerObstacle);
        for (var index = 0; index < obstacles; index++)
        {
            var angle = Normalize(random.NextFloat() * MathF.PI * 2f);
            if (!Fits(angle))
            {
                continue;
            }

            stuck[StuckCount] = angle;
            StuckCount++;
        }

        var appleCount = Math.Min(MaxApples, 1 + Level / LevelsPerApple);
        for (var index = 0; index < appleCount; index++)
        {
            var angle = Normalize(random.NextFloat() * MathF.PI * 2f);
            if (!Fits(angle))
            {
                continue;
            }

            apples[AppleCount] = angle;
            AppleCount++;
        }
    }

    private bool Fits(float angle)
    {
        for (var index = 0; index < StuckCount; index++)
        {
            if (MathF.Abs(Normalize(stuck[index] - angle)) < BladeArc * ObstacleSpacing)
            {
                return false;
            }
        }

        for (var index = 0; index < AppleCount; index++)
        {
            if (MathF.Abs(Normalize(apples[index] - angle)) < BladeArc * ObstacleSpacing)
            {
                return false;
            }
        }

        return true;
    }

    private float SpeedOf()
    {
        var speed = MathF.Min(MaxSpeed, BaseSpeed + SpeedPerLevel * (Level - 1));
        if (!IsBoss)
        {
            return speed;
        }

        return speed * (0.45f + 0.75f * MathF.Abs(MathF.Sin(patternTime * 0.9f)));
    }
}
