using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Spiral;

internal enum SpiralSegment : byte
{
    Gap,
    Solid,
    Red,
}

internal enum SpiralState : byte
{
    Playing,
    Over,
}

internal enum SpiralLanding : byte
{
    None,
    Bounced,
    Smashed,
    Died,
}

internal readonly struct SpiralPass
{
    public readonly int Ring;
    public readonly int Points;
    public readonly int Streak;

    public SpiralPass(int ring, int points, int streak)
    {
        Ring = ring;
        Points = points;
        Streak = streak;
    }
}

internal sealed class SpiralBoard
{
    public const int Segments = 12;
    public const float SegmentArc = MathF.Tau / Segments;
    public const float BallAngle = MathF.PI * 0.5f;
    public const float RingSpacing = 3.4f;
    public const float BallRadius = 0.36f;
    public const float BallOrbit = 2f;
    public const float InnerRadius = 0.9f;
    public const float OuterRadius = 3f;
    public const float Thickness = 0.42f;
    public const float Gravity = 40f;
    public const float BounceHeight = 1.7f;
    public const float MaxFallSpeed = 24f;
    public const float SmashExitSpeed = 7f;
    public const int FireballStreak = 3;
    public const int SmashPoints = 10;
    public const int LevelRings = 20;
    public const int SafeRings = 3;
    public const int RingCapacity = 32;
    public const int LookAhead = 16;
    public const int BallSegment = 3;
    private const int PassCapacity = 4;
    private const float StepSeconds = 1f / 240f;
    private const int MinimumSolid = 2;

    public static readonly float BounceSpeed = MathF.Sqrt(2f * Gravity * BounceHeight);

    private readonly SpiralSegment[] layout = new SpiralSegment[RingCapacity * Segments];
    private readonly SpiralPass[] passes = new SpiralPass[PassCapacity];
    private GameRandom random;
    private int generatedUntil;

    public SpiralState State { get; private set; }

    public int Score { get; private set; }

    public int Level { get; private set; }

    public int RingsPassed { get; private set; }

    public int Streak { get; private set; }

    public int BestStreak { get; private set; }

    public int Smashes { get; private set; }

    public bool Fireball { get; private set; }

    public float Angle { get; private set; }

    public float BallY { get; private set; }

    public float BallVelocity { get; private set; }

    public int CurrentRing { get; private set; }

    public SpiralLanding Landing { get; private set; }

    public int LandingRing { get; private set; }

    public int LandingSegment { get; private set; }

    public int PassCount { get; private set; }

    public bool FireballStarted { get; private set; }

    public bool LevelUp { get; private set; }

    public SpiralPass Pass(int index) => passes[index];

    public static float RingY(int ring) => ring * RingSpacing;

    public static int IndexAt(float localAngle)
    {
        var wrapped = Wrap(localAngle);
        return Math.Min(Segments - 1, (int)(wrapped / SegmentArc));
    }

    public static float Wrap(float angle)
    {
        var wrapped = angle % MathF.Tau;
        return wrapped < 0f ? wrapped + MathF.Tau : wrapped;
    }

    public SpiralSegment SegmentAt(int ring, int segment) =>
        layout[(ring % RingCapacity) * Segments + ((segment % Segments) + Segments) % Segments];

    public int SegmentIndexUnderBall => IndexAt(BallAngle - Angle);

    public SpiralSegment SegmentUnderBall(int ring) => SegmentAt(ring, SegmentIndexUnderBall);

    public void Reset(GameRandom seededRandom)
    {
        random = seededRandom;
        State = SpiralState.Playing;
        Score = 0;
        Level = 1;
        RingsPassed = 0;
        Streak = 0;
        BestStreak = 0;
        Smashes = 0;
        Fireball = false;
        Angle = 0f;
        CurrentRing = 0;
        BallY = RingY(0) - BallRadius;
        BallVelocity = -BounceSpeed;
        generatedUntil = 0;
        EnsureGenerated();
        BeginFrame();
    }

    public void BeginFrame()
    {
        Landing = SpiralLanding.None;
        PassCount = 0;
        FireballStarted = false;
        LevelUp = false;
    }

    public void SetRing(int ring, ReadOnlySpan<SpiralSegment> segments)
    {
        var slot = (ring % RingCapacity) * Segments;
        for (var segment = 0; segment < Segments; segment++)
        {
            layout[slot + segment] = segments[segment % segments.Length];
        }
    }

    public void Rotate(float radians)
    {
        if (State != SpiralState.Playing)
        {
            return;
        }

        Angle = Wrap(Angle + radians);
    }

    public void Step(float deltaSeconds)
    {
        if (deltaSeconds <= 0f || State != SpiralState.Playing)
        {
            return;
        }

        var steps = new Substeps(deltaSeconds, StepSeconds);
        for (var step = 0; step < steps.Count && State == SpiralState.Playing; step++)
        {
            Advance(steps.Step);
        }
    }

    public static void Generate(ref GameRandom random, int ring, Span<SpiralSegment> output)
    {
        for (var segment = 0; segment < Segments; segment++)
        {
            output[segment] = SpiralSegment.Solid;
        }

        var gapWidth = ring < 8 ? 3 : ring < 30 ? random.Next(2, 4) : random.Next(1, 3);
        var gapStart = ring == 0 ? BallSegment + 4 + random.Next(4) : random.Next(Segments);
        Carve(output, gapStart, gapWidth);
        var secondChance = ring < 30 ? 0.3f : 0.18f;
        if (ring > 0 && random.Chance(secondChance))
        {
            Carve(output, gapStart + Segments / 2 + random.Next(-1, 2), random.Next(1, 3));
        }

        if (ring < SafeRings)
        {
            return;
        }

        var reds = random.Next(MaxRed(ring) + 1);
        if (random.Chance(0.35f))
        {
            var edge = random.Chance(0.5f) ? gapStart - 1 : gapStart + gapWidth;
            PaintRed(output, edge);
        }

        for (var red = 0; red < reds; red++)
        {
            PaintRed(output, random.Next(Segments));
        }
    }

    private static int MaxRed(int ring) => Math.Min(5, 1 + ring / 10);

    private static void Carve(Span<SpiralSegment> output, int start, int width)
    {
        for (var offset = 0; offset < width; offset++)
        {
            output[((start + offset) % Segments + Segments) % Segments] = SpiralSegment.Gap;
        }
    }

    private static void PaintRed(Span<SpiralSegment> output, int segment)
    {
        var index = (segment % Segments + Segments) % Segments;
        if (output[index] != SpiralSegment.Solid || CountSolid(output) <= MinimumSolid)
        {
            return;
        }

        output[index] = SpiralSegment.Red;
    }

    private static int CountSolid(ReadOnlySpan<SpiralSegment> output)
    {
        var count = 0;
        for (var segment = 0; segment < output.Length; segment++)
        {
            if (output[segment] == SpiralSegment.Solid)
            {
                count++;
            }
        }

        return count;
    }

    private void EnsureGenerated()
    {
        while (generatedUntil < CurrentRing + LookAhead)
        {
            var slot = (generatedUntil % RingCapacity) * Segments;
            Generate(ref random, generatedUntil, layout.AsSpan(slot, Segments));
            generatedUntil++;
        }
    }

    private void Advance(float deltaSeconds)
    {
        BallVelocity = MathF.Min(MaxFallSpeed, BallVelocity + Gravity * deltaSeconds);
        var nextY = BallY + BallVelocity * deltaSeconds;
        var ringTop = RingY(CurrentRing);
        var crossing = BallVelocity > 0f && BallY + BallRadius <= ringTop && nextY + BallRadius >= ringTop;
        if (crossing && SegmentUnderBall(CurrentRing) != SpiralSegment.Gap)
        {
            Land(ringTop);
            return;
        }

        BallY = nextY;
        if (BallY - BallRadius > ringTop + Thickness)
        {
            PassRing();
        }
    }

    private void Land(float ringTop)
    {
        LandingRing = CurrentRing;
        LandingSegment = SegmentIndexUnderBall;
        if (Fireball)
        {
            Smash();
            return;
        }

        BallY = ringTop - BallRadius;
        if (SegmentUnderBall(CurrentRing) == SpiralSegment.Red)
        {
            Landing = SpiralLanding.Died;
            BallVelocity = 0f;
            State = SpiralState.Over;
            return;
        }

        Landing = SpiralLanding.Bounced;
        BallVelocity = -BounceSpeed;
        Streak = 0;
    }

    private void Smash()
    {
        Landing = SpiralLanding.Smashed;
        Smashes++;
        Score += SmashPoints * Level;
        Fireball = false;
        Streak = 0;
        BallVelocity = MathF.Max(BallVelocity * 0.55f, SmashExitSpeed);
        CurrentRing++;
        CountRing();
    }

    private void PassRing()
    {
        var ring = CurrentRing;
        CurrentRing++;
        Streak++;
        BestStreak = Math.Max(BestStreak, Streak);
        var points = Streak * Level;
        Score += points;
        if (PassCount < PassCapacity)
        {
            passes[PassCount++] = new SpiralPass(ring, points, Streak);
        }

        if (Streak >= FireballStreak && !Fireball)
        {
            Fireball = true;
            FireballStarted = true;
        }

        CountRing();
    }

    private void CountRing()
    {
        RingsPassed++;
        if (RingsPassed % LevelRings == 0)
        {
            Level++;
            LevelUp = true;
        }

        EnsureGenerated();
    }
}
