using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Flap;

internal enum FlapState : byte
{
    Ready,
    Playing,
    Dying,
    Over,
}

internal enum FlapDeath : byte
{
    None,
    Pipe,
    Ground,
}

internal struct FlapPipe
{
    public float X;
    public float GapCenter;
    public float GapHalf;
    public bool Scored;
}

internal sealed class FlapBoard
{
    public const float WorldWidth = 9f;
    public const float WorldHeight = 16f;
    public const float BirdRadius = 0.6f;
    public const float Gravity = 64f;
    public const float FlapImpulse = -16.8f;
    public const float MaxFall = 32f;
    public const float BaseSpeed = 3.96f;
    public const float SpeedPerPipe = 0.054f;
    public const float MaxSpeed = 6.48f;
    public const float PipeWidth = 1.53f;
    public const float PipeSpacing = 5.94f;
    public const float FirstPipeDistance = 9f;
    public const float GapHalfStart = 2.88f;
    public const float GapHalfMin = 2.16f;
    public const float GapShrinkPerPipe = 0.064f;
    public const float EdgeMargin = 1.12f;
    public const int PipeCapacity = 12;
    public const int BronzePipes = 10;
    public const int SilverPipes = 25;
    public const int GoldPipes = 50;
    private const float GenerateAhead = 3f * WorldWidth;
    private const float RecycleBehind = 2f * WorldWidth;
    private const float DeathHop = -8f;
    private const float DyingCapSeconds = 1.5f;

    private readonly FlapPipe[] pipes = new FlapPipe[PipeCapacity];
    private GameRandom random;
    private int pipeCount;
    private int spawned;
    private float dyingSeconds;

    public FlapState State { get; private set; }

    public FlapDeath Death { get; private set; }

    public int Score { get; private set; }

    public int Flaps { get; private set; }

    public float BirdX { get; private set; }

    public float BirdY { get; private set; }

    public float BirdVelocity { get; private set; }

    public float PlaySeconds { get; private set; }

    public bool ScoredThisStep { get; private set; }

    public bool DiedThisStep { get; private set; }

    public int PipeCount => pipeCount;

    public float Speed => MathF.Min(BaseSpeed + Score * SpeedPerPipe, MaxSpeed);

    public ref readonly FlapPipe PipeAt(int index) => ref pipes[index];

    public static int MedalTier(int pipesPassed)
    {
        if (pipesPassed >= GoldPipes)
        {
            return 3;
        }

        if (pipesPassed >= SilverPipes)
        {
            return 2;
        }

        return pipesPassed >= BronzePipes ? 1 : 0;
    }

    public void Reset(GameRandom seededRandom)
    {
        random = seededRandom;
        State = FlapState.Ready;
        Death = FlapDeath.None;
        Score = 0;
        Flaps = 0;
        BirdX = 0f;
        BirdY = WorldHeight * 0.5f;
        BirdVelocity = 0f;
        PlaySeconds = 0f;
        ScoredThisStep = false;
        DiedThisStep = false;
        pipeCount = 0;
        spawned = 0;
        dyingSeconds = 0f;
        Generate();
    }

    public void Flap()
    {
        if (State is FlapState.Dying or FlapState.Over)
        {
            return;
        }

        if (State == FlapState.Ready)
        {
            State = FlapState.Playing;
        }

        BirdVelocity = FlapImpulse;
        Flaps++;
    }

    public bool TryNextGap(out float gapCenter)
    {
        for (var index = 0; index < pipeCount; index++)
        {
            ref readonly var pipe = ref pipes[index];
            if (pipe.X + PipeWidth < BirdX - BirdRadius)
            {
                continue;
            }

            gapCenter = pipe.GapCenter;
            return true;
        }

        gapCenter = WorldHeight * 0.5f;
        return false;
    }

    public void Step(float deltaSeconds)
    {
        ScoredThisStep = false;
        DiedThisStep = false;
        if (deltaSeconds <= 0f)
        {
            return;
        }

        switch (State)
        {
            case FlapState.Playing:
                StepPlaying(deltaSeconds);
                return;
            case FlapState.Dying:
                StepDying(deltaSeconds);
                return;
            default:
                return;
        }
    }

    private void StepPlaying(float deltaSeconds)
    {
        PlaySeconds += deltaSeconds;
        Fall(deltaSeconds);
        if (BirdY < BirdRadius)
        {
            BirdY = BirdRadius;
            BirdVelocity = 0f;
        }

        if (BirdY + BirdRadius >= WorldHeight)
        {
            BirdY = WorldHeight - BirdRadius;
            Die(FlapDeath.Ground);
            return;
        }

        BirdX += Speed * deltaSeconds;
        ScorePassed();
        Recycle();
        Generate();
        if (HitsPipe())
        {
            Die(FlapDeath.Pipe);
        }
    }

    private void StepDying(float deltaSeconds)
    {
        dyingSeconds += deltaSeconds;
        Fall(deltaSeconds);
        if (BirdY + BirdRadius < WorldHeight && dyingSeconds < DyingCapSeconds)
        {
            return;
        }

        BirdY = MathF.Min(BirdY, WorldHeight - BirdRadius);
        BirdVelocity = 0f;
        State = FlapState.Over;
    }

    private void Fall(float deltaSeconds)
    {
        BirdVelocity = MathF.Min(BirdVelocity + Gravity * deltaSeconds, MaxFall);
        BirdY += BirdVelocity * deltaSeconds;
    }

    private void Die(FlapDeath cause)
    {
        Death = cause;
        DiedThisStep = true;
        if (cause == FlapDeath.Ground)
        {
            BirdVelocity = 0f;
            State = FlapState.Over;
            return;
        }

        State = FlapState.Dying;
        dyingSeconds = 0f;
        BirdVelocity = MathF.Min(BirdVelocity, DeathHop);
    }

    private void ScorePassed()
    {
        for (var index = 0; index < pipeCount; index++)
        {
            ref var pipe = ref pipes[index];
            if (pipe.Scored || pipe.X + PipeWidth >= BirdX)
            {
                continue;
            }

            pipe.Scored = true;
            Score++;
            ScoredThisStep = true;
        }
    }

    private void Recycle()
    {
        while (pipeCount > 0 && pipes[0].X + PipeWidth < BirdX - RecycleBehind)
        {
            Array.Copy(pipes, 1, pipes, 0, pipeCount - 1);
            pipeCount--;
        }
    }

    private void Generate()
    {
        while (pipeCount < PipeCapacity)
        {
            var nextX = pipeCount == 0 ? BirdX + FirstPipeDistance : pipes[pipeCount - 1].X + PipeSpacing;
            if (nextX > BirdX + GenerateAhead)
            {
                return;
            }

            Spawn(nextX);
        }
    }

    private void Spawn(float x)
    {
        var gapHalf = MathF.Max(GapHalfMin, GapHalfStart - spawned * GapShrinkPerPipe);
        var margin = EdgeMargin + gapHalf;
        var center = margin + random.NextFloat() * (WorldHeight - 2f * margin);
        pipes[pipeCount] = new FlapPipe
        {
            X = x,
            GapCenter = center,
            GapHalf = gapHalf,
            Scored = false,
        };
        pipeCount++;
        spawned++;
    }

    private bool HitsPipe()
    {
        for (var index = 0; index < pipeCount; index++)
        {
            ref readonly var pipe = ref pipes[index];
            if (BirdX + BirdRadius <= pipe.X || BirdX - BirdRadius >= pipe.X + PipeWidth)
            {
                continue;
            }

            if (BirdY - BirdRadius < pipe.GapCenter - pipe.GapHalf || BirdY + BirdRadius > pipe.GapCenter + pipe.GapHalf)
            {
                return true;
            }
        }

        return false;
    }
}
