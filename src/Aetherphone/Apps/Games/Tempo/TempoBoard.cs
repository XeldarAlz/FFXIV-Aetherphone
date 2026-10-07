using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Tempo;

internal sealed class TempoBoard
{
    public const int RespawnTicks = 54;
    public const int MaxCheckpoints = 32;
    private const float MaxCatchUpSeconds = 0.1f;

    private readonly TempoRunner[] checkpoints = new TempoRunner[MaxCheckpoints];
    private FixedStepClock clock = new(TempoPhysics.TickSeconds, MaxCatchUpSeconds);
    private TempoRunner runner;
    private GameRandom random;
    private int deathTicks;
    private int lastBeat;

    public TempoLevel? Level { get; private set; }

    public ref readonly TempoRunner Runner => ref runner;

    public bool Practice { get; private set; }

    public bool Complete { get; private set; }

    public bool Dead { get; private set; }

    public int Attempts { get; private set; }

    public int Deaths { get; private set; }

    public int CheckpointCount { get; private set; }

    public float BestProgress { get; private set; }

    public TempoSignal Signals { get; private set; }

    public ulong ShatterSeed { get; private set; }

    public Vector2 DeathPoint { get; private set; }

    public int LastCoin { get; private set; }

    public int Beat => lastBeat;

    public float Alpha => clock.Alpha;

    public float DeathProgress => Dead ? 1f - deathTicks / (float)RespawnTicks : 0f;

    public float Progress => Level is null
        ? 0f
        : Math.Clamp((runner.X - TempoPhysics.StartX) / (Level.Length - TempoPhysics.StartX), 0f, 1f);

    public float BeatPhase
    {
        get
        {
            if (Level is null)
            {
                return 0f;
            }

            var beats = RenderX / Level.TilesPerBeat;
            return beats - MathF.Floor(beats);
        }
    }

    public float RenderX => runner.PreviousX + (runner.X - runner.PreviousX) * clock.Alpha;

    public float RenderY => runner.PreviousY + (runner.Y - runner.PreviousY) * clock.Alpha;

    public float RenderRotation => runner.PreviousRotation + (runner.Rotation - runner.PreviousRotation) * clock.Alpha;

    public Vector2 Checkpoint(int index) => new(checkpoints[index].X, checkpoints[index].Y);

    public void Load(TempoLevel level, GameRandom seed, bool practice)
    {
        Level = level;
        random = seed;
        Practice = practice;
        Complete = false;
        Dead = false;
        Attempts = 1;
        Deaths = 0;
        CheckpointCount = 0;
        BestProgress = 0f;
        Signals = TempoSignal.None;
        deathTicks = 0;
        runner = TempoPhysics.Start(level);
        lastBeat = BeatAt(runner.X);
        clock.Reset();
    }

    public int Step(float deltaSeconds, bool held, bool pressed)
    {
        Signals = TempoSignal.None;
        var ticks = clock.Advance(deltaSeconds);
        for (var tick = 0; tick < ticks; tick++)
        {
            Tick(held, pressed && tick == 0);
        }

        return ticks;
    }

    public int StepPlan(float deltaSeconds, TempoPlan plan)
    {
        Signals = TempoSignal.None;
        var ticks = clock.Advance(deltaSeconds);
        for (var tick = 0; tick < ticks; tick++)
        {
            var next = runner.Tick + 1;
            Tick(plan.Held(next), plan.Pressed(next));
        }

        return ticks;
    }

    public void Tick(bool held, bool pressed)
    {
        if (Level is null || Complete)
        {
            return;
        }

        if (Dead)
        {
            deathTicks--;
            if (deathTicks <= 0)
            {
                Respawn();
            }

            return;
        }

        var signals = TempoPhysics.Tick(ref runner, Level, held, pressed);
        var beat = BeatAt(runner.X);
        if (beat != lastBeat)
        {
            lastBeat = beat;
            signals |= TempoSignal.Beat;
        }

        if ((signals & TempoSignal.Coin) != 0)
        {
            LastCoin = runner.LastCoin;
        }

        BestProgress = MathF.Max(BestProgress, Progress);
        Signals |= signals;
        if ((signals & TempoSignal.Died) != 0)
        {
            Dead = true;
            Deaths++;
            deathTicks = RespawnTicks;
            DeathPoint = new Vector2(runner.X, runner.Y);
            ShatterSeed = random.NextUInt() | ((ulong)random.NextUInt() << 32);
            return;
        }

        if ((signals & TempoSignal.Finished) != 0)
        {
            Complete = true;
        }
    }

    public bool PlaceCheckpoint()
    {
        if (!Practice || Dead || Complete || !runner.Alive || CheckpointCount >= MaxCheckpoints)
        {
            return false;
        }

        checkpoints[CheckpointCount++] = runner;
        return true;
    }

    public bool RemoveCheckpoint()
    {
        if (!Practice || CheckpointCount == 0)
        {
            return false;
        }

        CheckpointCount--;
        return true;
    }

    private void Respawn()
    {
        Dead = false;
        Attempts++;
        runner = Practice && CheckpointCount > 0 ? checkpoints[CheckpointCount - 1] : TempoPhysics.Start(Level!);
        runner.PreviousX = runner.X;
        runner.PreviousY = runner.Y;
        runner.PreviousRotation = runner.Rotation;
        lastBeat = BeatAt(runner.X);
        Signals |= TempoSignal.Restarted;
    }

    private int BeatAt(float x) => Level is null ? 0 : (int)MathF.Floor(x / Level.TilesPerBeat);
}
