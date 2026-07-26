namespace Aetherphone.Apps.Games.Blade;

internal enum BladeState : byte
{
    Ready,
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
    private const float BladeArc = 0.17f;
    private const float FlightSeconds = 0.085f;
    private const float BaseSpeed = 1.45f;
    private const float SpeedPerLevel = 0.13f;
    private const float MaxSpeed = 4.1f;
    private const int BaseBlades = 5;
    private const int BladesPerLevel = 1;
    private const int MaxBlades = 10;
    private const int BossEvery = 5;
    private const float ClearedSeconds = 0.75f;
    private readonly float[] stuck = new float[MaxStuck];
    private readonly Random random = new();
    private float wheelAngle;
    private float direction = 1f;
    private float patternTime;
    private float clearedTimer;
    public BladeState State { get; private set; } = BladeState.Ready;
    public int StuckCount { get; private set; }
    public int Level { get; private set; } = 1;
    public int Score { get; private set; }
    public int Remaining { get; private set; }
    public int LevelBlades { get; private set; }
    public float FlightProgress { get; private set; }
    public bool InFlight { get; private set; }
    public float WheelAngle => wheelAngle;
    public float BlockedAngle { get; private set; }
    public bool IsBoss => Level % BossEvery == 0;
    public float StuckAngle(int index) => stuck[index] + wheelAngle;

    public void Reset()
    {
        State = BladeState.Ready;
        Level = 1;
        Score = 0;
        wheelAngle = 0f;
        patternTime = 0f;
        clearedTimer = 0f;
        InFlight = false;
        FlightProgress = 0f;
        BuildLevel();
    }

    public void Begin()
    {
        if (State != BladeState.Ready)
        {
            return;
        }

        State = BladeState.Playing;
    }

    public BladeThrow Step(float deltaSeconds)
    {
        patternTime += deltaSeconds;
        if (State == BladeState.Ready)
        {
            wheelAngle += SpeedOf() * direction * deltaSeconds * 0.45f;
            return BladeThrow.None;
        }

        if (State == BladeState.Cleared)
        {
            wheelAngle += SpeedOf() * direction * deltaSeconds * 0.6f;
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
            wheelAngle += SpeedOf() * direction * deltaSeconds * 0.25f;
            return BladeThrow.None;
        }

        wheelAngle += SpeedOf() * direction * deltaSeconds;
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

    private BladeThrow Resolve()
    {
        var local = Normalize(ImpactAngle - wheelAngle);
        for (var index = 0; index < StuckCount; index++)
        {
            if (MathF.Abs(Normalize(stuck[index] - local)) >= BladeArc)
            {
                continue;
            }

            BlockedAngle = ImpactAngle;
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
        if (Remaining > 0)
        {
            return BladeThrow.Stuck;
        }

        State = BladeState.Cleared;
        clearedTimer = ClearedSeconds;
        return BladeThrow.LevelCleared;
    }

    private void BuildLevel()
    {
        LevelBlades = Math.Min(MaxBlades, BaseBlades + (Level - 1) * BladesPerLevel);
        Remaining = LevelBlades;
        StuckCount = 0;
        direction = Level % 4 == 0 || Level % 4 == 3 ? -1f : 1f;
        patternTime = 0f;
        var obstacles = Math.Min(3, (Level - 1) / 3);
        for (var index = 0; index < obstacles; index++)
        {
            var angle = Normalize((float)random.NextDouble() * MathF.PI * 2f);
            if (!Fits(angle))
            {
                continue;
            }

            stuck[StuckCount] = angle;
            StuckCount++;
        }
    }

    private bool Fits(float angle)
    {
        for (var index = 0; index < StuckCount; index++)
        {
            if (MathF.Abs(Normalize(stuck[index] - angle)) < BladeArc * 2.4f)
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

    private static float Normalize(float angle)
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
}
