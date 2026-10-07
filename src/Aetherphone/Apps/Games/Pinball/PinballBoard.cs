using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Framework.Physics;

namespace Aetherphone.Apps.Games.Pinball;

internal enum PinballPhase : byte
{
    Playing,
    BallEnd,
    GameOver,
}

internal enum BallState : byte
{
    None,
    Lane,
    Rolling,
    Held,
}

internal enum BallLayer : byte
{
    Playfield,
    Ramp,
}

internal struct PinballBall
{
    public int Body;
    public int Id;
    public BallState State;
    public BallLayer Layer;
    public bool PlayerLaunched;
    public bool Kicked;
    public byte OrbitSide;
    public float OrbitSeconds;
    public float HoldSeconds;
    public float SaucerGrace;
    public float StillSeconds;
    public Vector2 Previous;
    public Vector2 Position;
    public Vector2 Velocity;
}

internal sealed partial class PinballBoard
{
    public const int BallsPerGame = 3;
    public const int MaxBalls = 4;
    public const int MaxMultiplier = 5;
    public const int MaxEvents = 96;
    public const int LocksForMultiball = 3;
    public const int MultiballBalls = 3;
    public const int MaxTiltWarnings = 2;
    public const float BallSaveSeconds = 8f;
    public const float SkillWindowSeconds = 4f;
    public const float SaucerHoldSeconds = 1.1f;
    public const float SaucerGraceSeconds = 0.6f;
    public const float BallEndSeconds = 1.8f;
    public const float SaveRelaunchSeconds = 0.7f;
    public const float MultiballLaunchSeconds = 0.9f;
    public const float BankResetSeconds = 0.6f;
    public const float PullSeconds = 1.1f;
    public const float MinLaunchSpeed = 12f;
    public const float MaxLaunchSpeed = 19f;
    public const float AutoLaunchSpeed = 16f;
    public const float NudgeTilt = 0.45f;
    public const float TiltDecayPerSecond = 0.3f;
    public const float TiltAfterWarning = 0.35f;
    public const int SlingPoints = 110;
    public const int BumperPoints = 1000;
    public const int SpinnerPoints = 250;
    public const int TopLanePoints = 2500;
    public const int LitLanePoints = 500;
    public const int LanesCompletePoints = 10000;
    public const int TargetPoints = 5000;
    public const int BankPoints = 25000;
    public const int InlanePoints = 1000;
    public const int OutlanePoints = 2500;
    public const int RampPoints = 25000;
    public const int LockPoints = 15000;
    public const int MultiballPoints = 50000;
    public const int SaucerPoints = 10000;
    public const int SkillShotPoints = 50000;
    public const int JackpotBase = 100000;
    public const int JackpotStep = 50000;
    public const int ExtraBallScore = 1500000;
    public const int BonusPerBumper = 100;
    public const int BonusPerLane = 500;
    public const int BonusPerTarget = 750;
    public const int BonusPerRamp = 2500;
    public const int BonusPerLock = 2000;
    private const int AllLanes = (1 << PinballTable.TopLaneCount) - 1;
    private const float StillSpeed = 0.08f;
    private const float StillKickSeconds = 3f;
    private const float StillKickHeight = 9.6f;

    private readonly PinballBall[] balls = new PinballBall[MaxBalls];
    private readonly bool[] targetDown = new bool[PinballTable.TargetCount];
    private readonly float[] bumperFlash = new float[PinballTable.BumperCount];
    private readonly float[] slingFlash = new float[PinballTable.SlingCount];
    private readonly float[] rolloverFlash = new float[PinballTable.RolloverCount];
    private readonly float[] laneFlash = new float[PinballTable.TopLaneCount];
    private readonly PinballEvent[] events = new PinballEvent[MaxEvents];
    private GameRandom random;
    private int nextBallId;
    private int pendingLaunches;
    private float launchDelay;
    private float phaseSeconds;
    private float bankResetSeconds;
    private bool bankResetPending;
    private bool freshBall;
    private bool leftHeld;
    private bool rightHeld;
    private float spinnerSpeed;
    private int spinnerHalfTurns;
    private float saucerFlash;
    private float rampFlash;

    public PinballBoard()
    {
        world = new PhysicsWorld(WorldBodyCapacity, WorldContactCapacity, WorldJointCapacity, WorldChainCapacity,
            WorldEventCapacity);
    }

    public bool Attract { get; private set; }

    public PinballPhase Phase { get; private set; }

    public int Score { get; private set; }

    public int BallNumber { get; private set; }

    public int ExtraBalls { get; private set; }

    public bool ExtraBallAwarded { get; private set; }

    public int Multiplier { get; private set; } = 1;

    public int Bonus { get; private set; }

    public int LanesLit { get; private set; }

    public int SkillLane { get; private set; }

    public bool SkillArmed { get; private set; }

    public float SkillSeconds { get; private set; }

    public int Locks { get; private set; }

    public bool MultiballActive { get; private set; }

    public bool JackpotLit { get; private set; }

    public int JackpotValue { get; private set; } = JackpotBase;

    public float BallSaveLeft { get; private set; }

    public float TiltMeter { get; private set; }

    public int TiltWarnings { get; private set; }

    public bool Tilted { get; private set; }

    public float PlungerPull { get; private set; }

    public float SpinnerAngle { get; private set; }

    public int Jackpots { get; private set; }

    public int Ramps { get; private set; }

    public int Multiballs { get; private set; }

    public bool Autopilot { get; set; }

    public int EventCount { get; private set; }

    public bool GameOver => Phase == PinballPhase.GameOver;

    public int BallsLeft => Phase == PinballPhase.GameOver
        ? 0
        : Math.Max(0, BallsPerGame - BallNumber + 1 + ExtraBalls);

    public bool ShootAgainLit => ExtraBalls > 0;

    public float SaucerFlash => saucerFlash;

    public float RampFlash => rampFlash;

    public PinballEvent Event(int index) => events[index];

    public bool TargetDown(int index) => targetDown[index];

    public bool BankDown(int bank)
    {
        var first = bank * PinballTable.BankSize;
        for (var index = first; index < first + PinballTable.BankSize; index++)
        {
            if (!targetDown[index])
            {
                return false;
            }
        }

        return true;
    }

    public float BumperFlash(int index) => bumperFlash[index];

    public float SlingFlash(int index) => slingFlash[index];

    public float RolloverFlash(int index) => rolloverFlash[index];

    public float LaneFlash(int index) => laneFlash[index];

    public bool LaneLit(int lane) => (LanesLit & (1 << lane)) != 0;

    public bool LockLit => !MultiballActive && !Tilted;

    public bool BallWaiting => WaitingSlot() >= 0;

    public bool BallInLane => LaneSlot() >= 0;

    public int LiveBalls
    {
        get
        {
            var count = 0;
            for (var slot = 0; slot < MaxBalls; slot++)
            {
                if (balls[slot].State != BallState.None)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public int PendingLaunches => pendingLaunches;

    public void Reset(GameRandom seeded, bool attract)
    {
        random = seeded;
        Attract = attract;
        Autopilot = attract;
        Phase = PinballPhase.Playing;
        Score = 0;
        BallNumber = 1;
        ExtraBalls = 0;
        ExtraBallAwarded = false;
        Locks = 0;
        MultiballActive = false;
        JackpotLit = false;
        JackpotValue = JackpotBase;
        Jackpots = 0;
        Ramps = 0;
        Multiballs = 0;
        SpinnerAngle = 0f;
        spinnerSpeed = 0f;
        spinnerHalfTurns = 0;
        pendingLaunches = 0;
        launchDelay = 0f;
        phaseSeconds = 0f;
        bankResetPending = false;
        bankResetSeconds = 0f;
        nextBallId = 0;
        leftHeld = false;
        rightHeld = false;
        EventCount = 0;
        Array.Clear(targetDown);
        Array.Clear(bumperFlash);
        Array.Clear(slingFlash);
        Array.Clear(rolloverFlash);
        Array.Clear(laneFlash);
        saucerFlash = 0f;
        rampFlash = 0f;
        ResetBallState();
        ResetModes();
        BuildWorld();
        ServeBall(true);
        if (attract)
        {
            LaunchWaiting(AutoLaunchSpeed, false);
        }
    }

    public void SetFlippers(bool left, bool right)
    {
        var active = Phase != PinballPhase.GameOver && !Tilted;
        if (active && left && !leftHeld)
        {
            RotateLanes(-1);
        }

        if (active && right && !rightHeld)
        {
            RotateLanes(1);
        }

        leftHeld = left;
        rightHeld = right;
    }

    public void PullPlunger(float deltaSeconds)
    {
        if (deltaSeconds <= 0f || !BallWaiting)
        {
            return;
        }

        PlungerPull = MathF.Min(1f, PlungerPull + deltaSeconds / PullSeconds);
    }

    public void SetPlungerPull(float pull)
    {
        PlungerPull = BallWaiting ? Math.Clamp(pull, 0f, 1f) : 0f;
    }

    public bool ReleasePlunger()
    {
        var pull = PlungerPull;
        PlungerPull = 0f;
        if (pull <= 0.02f || Phase != PinballPhase.Playing)
        {
            return false;
        }

        var speed = MinLaunchSpeed + (MaxLaunchSpeed - MinLaunchSpeed) * pull;
        return LaunchWaiting(speed, true);
    }

    public bool Nudge()
    {
        if (Phase != PinballPhase.Playing || Tilted)
        {
            return false;
        }

        for (var slot = 0; slot < MaxBalls; slot++)
        {
            ref var ball = ref balls[slot];
            if (ball.State != BallState.Rolling || ball.Layer != BallLayer.Playfield)
            {
                continue;
            }

            var kick = new Vector2(random.Range(-NudgeSideways, NudgeSideways), -NudgeKick);
            world.SetVelocity(ball.Body, world.Velocity(ball.Body) + kick);
        }

        if (Attract)
        {
            return true;
        }

        TiltMeter += NudgeTilt;
        if (TiltMeter < 1f)
        {
            return true;
        }

        TiltMeter = TiltAfterWarning;
        if (TiltWarnings < MaxTiltWarnings)
        {
            TiltWarnings++;
            Emit(PinballEventKind.TiltWarning, new Vector2(PinballTable.Width * 0.5f, PinballTable.Height * 0.5f),
                TiltWarnings, 0);
            return true;
        }

        Tilted = true;
        SkillArmed = false;
        BallSaveLeft = 0f;
        Emit(PinballEventKind.Tilt, new Vector2(PinballTable.Width * 0.5f, PinballTable.Height * 0.5f), 0, 0);
        return true;
    }

    public bool HitTarget(int index)
    {
        if (targetDown[index])
        {
            return false;
        }

        targetDown[index] = true;
        DropTargetBody(index);
        var position = PinballTable.Targets[index];
        if (Tilted)
        {
            Emit(PinballEventKind.Target, position, 0, index);
            return true;
        }

        DisarmSkill();
        Bonus += BonusPerTarget;
        Emit(PinballEventKind.Target, position, AddScore(TargetPoints), index);
        Charge(SwitchCharge);
        var bank = index / PinballTable.BankSize;
        if (!BankDown(bank))
        {
            return true;
        }

        Emit(PinballEventKind.BankDown, PinballTable.Targets[bank * PinballTable.BankSize + 1], AddScore(BankPoints),
            bank);
        if (!BankDown(1 - bank))
        {
            return true;
        }

        if (JackpotLit)
        {
            JackpotValue += JackpotStep;
            Emit(PinballEventKind.JackpotRaised, PinballTable.RampEntrance, JackpotValue, 0);
        }
        else
        {
            JackpotLit = true;
            Emit(PinballEventKind.JackpotLit, PinballTable.RampEntrance, JackpotValue, 0);
        }

        bankResetPending = true;
        bankResetSeconds = BankResetSeconds;
        return true;
    }

    public void RollTopLane(int lane)
    {
        laneFlash[lane] = 1f;
        var position = PinballTable.TopLanes[lane];
        if (Tilted)
        {
            return;
        }

        if (SkillArmed)
        {
            SkillArmed = false;
            if (lane == SkillLane)
            {
                Emit(PinballEventKind.SkillShot, position, AddScore(SkillShotPoints), lane);
                MajorShot(position);
            }
        }

        Charge(SwitchCharge);
        var bit = 1 << lane;
        if ((LanesLit & bit) != 0)
        {
            Emit(PinballEventKind.TopLane, position, AddScore(LitLanePoints), lane);
            return;
        }

        LanesLit |= bit;
        Bonus += BonusPerLane;
        Emit(PinballEventKind.TopLane, position, AddScore(TopLanePoints), lane);
        if (LanesLit != AllLanes)
        {
            return;
        }

        LanesLit = 0;
        Multiplier = Math.Min(MaxMultiplier, Multiplier + 1);
        AddScore(LanesCompletePoints);
        Emit(PinballEventKind.LanesComplete, PinballTable.TopLanes[1], Multiplier, 0);
    }

    public void CompleteRamp(Vector2 position)
    {
        rampFlash = 1f;
        if (Tilted)
        {
            return;
        }

        DisarmSkill();
        Ramps++;
        Bonus += BonusPerRamp;
        Emit(PinballEventKind.RampMade, position, AddScore(RampPoints), Ramps);
        MajorShot(position);
        if (!JackpotLit)
        {
            return;
        }

        var value = AddScore(MultiballActive ? JackpotValue * 2 : JackpotValue);
        Jackpots++;
        JackpotLit = false;
        JackpotValue += JackpotStep;
        LightShow = LightShowSeconds;
        Emit(PinballEventKind.Jackpot, position, value, Jackpots);
        if (!MultiballActive)
        {
            return;
        }

        SuperJackpotLit = true;
        Emit(PinballEventKind.SuperJackpotLit, PinballTable.Saucer, JackpotValue * SuperJackpotFactor, 0);
    }

    public void CaptureBall(int slot)
    {
        ref var ball = ref balls[slot];
        if (ball.State != BallState.Rolling)
        {
            return;
        }

        ball.State = BallState.Held;
        ball.HoldSeconds = SaucerHoldSeconds;
        ball.Position = PinballTable.Saucer;
        ball.Previous = PinballTable.Saucer;
        ball.Velocity = Vector2.Zero;
        HoldBody(ball.Body);
        saucerFlash = 1f;
        DisarmSkill();
        if (Tilted)
        {
            Emit(PinballEventKind.SaucerHold, PinballTable.Saucer, 0, 0);
            return;
        }

        MajorShot(PinballTable.Saucer);
        if (MysteryLit)
        {
            AwardMystery();
        }

        if (MultiballActive)
        {
            if (SuperJackpotLit)
            {
                CollectSuperJackpot();
                return;
            }

            Emit(PinballEventKind.SaucerHold, PinballTable.Saucer, AddScore(SaucerPoints), 0);
            return;
        }

        Locks++;
        Bonus += BonusPerLock;
        if (Locks < LocksForMultiball)
        {
            Emit(PinballEventKind.Locked, PinballTable.Saucer, AddScore(LockPoints), Locks);
            return;
        }

        AddScore(LockPoints);
        MultiballActive = true;
        Multiballs++;
        JackpotLit = true;
        LightShow = LightShowSeconds;
        pendingLaunches += MultiballBalls - 1;
        launchDelay = MathF.Max(launchDelay, SaucerHoldSeconds * 0.5f);
        Emit(PinballEventKind.Multiball, PinballTable.Saucer, AddScore(MultiballPoints), MultiballBalls);
    }

    public void DrainBall(int slot)
    {
        ref var ball = ref balls[slot];
        if (ball.State == BallState.None)
        {
            return;
        }

        var position = ball.Position;
        RemoveBall(slot);
        Emit(PinballEventKind.Drain, position, 0, slot);
        if (Attract)
        {
            QueueLaunch(SaveRelaunchSeconds);
            return;
        }

        if (!Tilted && BallSaveLeft > 0f)
        {
            QueueLaunch(SaveRelaunchSeconds);
            Emit(PinballEventKind.BallSaved, position, 0, 0);
            return;
        }

        var live = LiveBalls + pendingLaunches;
        if (MultiballActive && live <= 1)
        {
            MultiballActive = false;
            SuperJackpotLit = false;
            Locks = 0;
        }

        if (live > 0)
        {
            return;
        }

        EndBall(position);
    }

    public void BeginFrame()
    {
        EventCount = 0;
    }

    public void Update(float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        var steps = clock.Advance(deltaSeconds);
        for (var step = 0; step < steps; step++)
        {
            Tick();
        }
    }

    private void Tick()
    {
        const float seconds = PhysicsWorld.StepSeconds;
        AdvanceTimers(seconds);
        if (Autopilot)
        {
            Steer();
        }

        ApplyFlippers();
        StepWorld();
        TrackBalls(seconds);
        AdvanceSpinner(seconds);
    }

    private void AdvanceTimers(float seconds)
    {
        Decay(bumperFlash, seconds * 6f);
        Decay(slingFlash, seconds * 7f);
        Decay(rolloverFlash, seconds * 3f);
        Decay(laneFlash, seconds * 3f);
        saucerFlash = MathF.Max(0f, saucerFlash - seconds * 2f);
        rampFlash = MathF.Max(0f, rampFlash - seconds * 2f);
        TiltMeter = MathF.Max(0f, TiltMeter - TiltDecayPerSecond * seconds);
        AdvanceModes(seconds);
        if (BallSaveLeft > 0f && !AnyBallInLane())
        {
            BallSaveLeft = MathF.Max(0f, BallSaveLeft - seconds);
        }

        if (SkillArmed)
        {
            SkillSeconds -= seconds;
            if (SkillSeconds <= 0f)
            {
                SkillArmed = false;
            }
        }

        if (bankResetPending)
        {
            bankResetSeconds -= seconds;
            if (bankResetSeconds <= 0f)
            {
                RaiseTargets();
            }
        }

        AdvanceHolds(seconds);
        AdvanceLaunches(seconds);
        if (Phase != PinballPhase.BallEnd)
        {
            return;
        }

        phaseSeconds -= seconds;
        if (phaseSeconds <= 0f)
        {
            NextBall();
        }
    }

    private void AdvanceHolds(float seconds)
    {
        for (var slot = 0; slot < MaxBalls; slot++)
        {
            ref var ball = ref balls[slot];
            if (ball.SaucerGrace > 0f)
            {
                ball.SaucerGrace -= seconds;
            }

            if (ball.OrbitSeconds > 0f)
            {
                ball.OrbitSeconds -= seconds;
            }

            if (ball.State != BallState.Held)
            {
                continue;
            }

            ball.HoldSeconds -= seconds;
            if (ball.HoldSeconds > 0f)
            {
                continue;
            }

            var kick = PinballTable.SaucerKick + new Vector2(random.Range(-0.3f, 0.3f), random.Range(-0.2f, 0.2f));
            ReleaseBody(ball.Body, kick);
            ball.State = BallState.Rolling;
            ball.Layer = BallLayer.Playfield;
            ball.SaucerGrace = SaucerGraceSeconds;
            ball.StillSeconds = 0f;
            saucerFlash = 1f;
            Emit(PinballEventKind.SaucerKick, PinballTable.Saucer, 0, slot);
        }
    }

    private void AdvanceLaunches(float seconds)
    {
        if (launchDelay > 0f)
        {
            launchDelay -= seconds;
        }

        if (pendingLaunches <= 0 || launchDelay > 0f || Phase != PinballPhase.Playing || AnyBallInLane())
        {
            return;
        }

        if (!ServeBall(false))
        {
            return;
        }

        pendingLaunches--;
        LaunchWaiting(AutoLaunchSpeed + random.Range(-0.6f, 0.6f), false);
        launchDelay = MultiballLaunchSeconds;
    }

    private void QueueLaunch(float delay)
    {
        pendingLaunches++;
        launchDelay = MathF.Max(launchDelay, delay);
    }

    private void EndBall(Vector2 position)
    {
        Phase = PinballPhase.BallEnd;
        phaseSeconds = BallEndSeconds;
        if (FeverActive)
        {
            FinishFever();
        }

        var award = Tilted ? 0 : Bonus * Multiplier;
        Credit(award);

        Emit(PinballEventKind.Bonus, position, award, Multiplier);
    }

    private void NextBall()
    {
        Tilted = false;
        TiltWarnings = 0;
        TiltMeter = 0f;
        Bonus = 0;
        Multiplier = 1;
        LanesLit = 0;
        SkillArmed = false;
        BallSaveLeft = 0f;
        ClearBallModes();
        if (ExtraBalls > 0)
        {
            ExtraBalls--;
            Emit(PinballEventKind.ShootAgain, PinballTable.LanePlunger, 0, BallNumber);
        }
        else
        {
            BallNumber++;
        }

        if (BallNumber > BallsPerGame)
        {
            BallNumber = BallsPerGame;
            Phase = PinballPhase.GameOver;
            return;
        }

        Phase = PinballPhase.Playing;
        ServeBall(true);
        Emit(PinballEventKind.NewBall, PinballTable.LanePlunger, BallNumber, 0);
    }

    private void RotateLanes(int direction)
    {
        if (BallInLane && !Attract)
        {
            SkillLane = (SkillLane + direction + PinballTable.TopLaneCount) % PinballTable.TopLaneCount;
        }

        if (LanesLit == 0 || LanesLit == AllLanes)
        {
            return;
        }

        LanesLit = direction > 0
            ? ((LanesLit << 1) | (LanesLit >> (PinballTable.TopLaneCount - 1))) & AllLanes
            : ((LanesLit >> 1) | (LanesLit << (PinballTable.TopLaneCount - 1))) & AllLanes;
    }

    private void DisarmSkill()
    {
        SkillArmed = false;
    }

    private void Emit(PinballEventKind kind, Vector2 position, int value, int index)
    {
        if (EventCount >= MaxEvents)
        {
            return;
        }

        events[EventCount++] = new PinballEvent(kind, position, value, index);
    }

    private void ResetBallState()
    {
        Multiplier = 1;
        Bonus = 0;
        LanesLit = 0;
        SkillArmed = false;
        SkillSeconds = 0f;
        BallSaveLeft = 0f;
        Tilted = false;
        TiltWarnings = 0;
        TiltMeter = 0f;
        PlungerPull = 0f;
        freshBall = false;
        for (var slot = 0; slot < MaxBalls; slot++)
        {
            balls[slot] = default;
        }
    }

    private bool AnyBallInLane() => LaneSlot() >= 0;

    private int LaneSlot()
    {
        for (var slot = 0; slot < MaxBalls; slot++)
        {
            if (balls[slot].State == BallState.Lane)
            {
                return slot;
            }
        }

        return -1;
    }

    private int WaitingSlot()
    {
        var slot = LaneSlot();
        if (slot < 0)
        {
            return -1;
        }

        ref readonly var ball = ref balls[slot];
        return ball.Position.Y > PinballTable.PlungerRestY - 0.3f && ball.Velocity.LengthSquared() < 0.25f
            ? slot
            : -1;
    }

    private static void Decay(float[] values, float amount)
    {
        for (var index = 0; index < values.Length; index++)
        {
            values[index] = MathF.Max(0f, values[index] - amount);
        }
    }
}
