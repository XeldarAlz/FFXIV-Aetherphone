using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Hop;

internal struct LaneEntity
{
    public float X;
    public int Length;
}

internal sealed class HopBoard
{
    public const int Columns = 13;
    public const int Rows = 15;
    public const int StartRow = 0;
    public const int RoadFirstRow = 1;
    public const int RoadLastRow = 5;
    public const int MedianRow = 6;
    public const int StreamFirstRow = 7;
    public const int StreamLastRow = 11;
    public const int BankRow = 12;
    public const int LaneCount = 5;
    public const int BayCount = 5;
    public const int MaxEntitiesPerLane = 3;
    public const int StartLives = 3;
    public const int HopPoints = 10;
    public const int BayPoints = 200;
    public const int BayTimeBonusMax = 90;
    public const int BayTimeBonusPerSecond = 2;
    public const int LevelClearBonus = 500;
    public const float LifeTimerSeconds = 45f;
    public const float DeathPauseSeconds = 1.2f;
    public const float LevelClearPauseSeconds = 1.5f;
    public const float LowTimerSeconds = 10f;
    public const float NearMissCells = 1f;
    public static readonly int[] BayColumns = { 0, 3, 6, 9, 12 };
    public static readonly float[] RoadSpeeds = { 1.5f, 2.1f, 1.7f, 2.4f, 1.9f };
    public static readonly int[] RoadBaseCounts = { 2, 3, 2, 3, 2 };
    public static readonly float[] StreamSpeeds = { 1.3f, 1.8f, 1.5f, 2.0f, 1.6f };
    public static readonly int[] PadCounts = { 2, 3, 2, 3, 2 };
    public static readonly int[] PadLengths = { 3, 2, 3, 2, 3 };
    private const float FixedStepSeconds = 1f / 60f;
    private const float MaxCatchUpSeconds = 0.5f;
    private const float SpeedRampPerLevel = 1.12f;
    private const int DensityRampLevels = 2;
    private const int VehicleMinLength = 1;
    private const int VehicleMaxLength = 2;
    private const float CollisionMargin = 0.2f;
    private const float PadGripSlack = 0.3f;
    private const float BayCatchHalfWidth = 1f;
    private const float HopFlashSeconds = 0.18f;
    private const float BankFlashSeconds = 0.9f;
    private const float BumpFlashSeconds = 0.4f;
    private const int StartColumn = 6;
    private readonly LaneEntity[] roadEntities = new LaneEntity[LaneCount * MaxEntitiesPerLane];
    private readonly LaneEntity[] streamEntities = new LaneEntity[LaneCount * MaxEntitiesPerLane];
    private readonly int[] roadCounts = new int[LaneCount];
    private readonly float[] roadLaneSpeeds = new float[LaneCount];
    private readonly float[] streamLaneSpeeds = new float[LaneCount];
    private readonly bool[] bays = new bool[BayCount];
    private GameRandom random;
    private FixedStepClock clock = new(FixedStepSeconds, MaxCatchUpSeconds);
    private int maxRowReached;
    private float deathTimer;
    private float clearTimer;
    private int nearMissVehicle = -1;
    public int Score { get; private set; }
    public int Level { get; private set; }
    public int Lives { get; private set; }
    public bool GameOver { get; private set; }
    public int BankedTotal { get; private set; }
    public int BankedThisLevel { get; private set; }
    public float TimerRemaining { get; private set; }
    public float TimerFraction => TimerRemaining / LifeTimerSeconds;
    public float X { get; private set; }
    public int Row { get; private set; }
    public float HopFlash { get; private set; }
    public int LastBankedBay { get; private set; } = -1;
    public int LastBankPoints { get; private set; }
    public float BankFlash { get; private set; }
    public float BumpFlash { get; private set; }
    public int BumpColumn { get; private set; } = -1;
    public bool Dying => deathTimer > 0f;
    public bool ClearingLevel => clearTimer > 0f;
    public bool Frozen => Dying || ClearingLevel;
    public bool HoppedThisFrame { get; private set; }
    public int BankedBayThisFrame { get; private set; } = -1;
    public bool BumpedThisFrame { get; private set; }
    public bool DiedThisFrame { get; private set; }
    public bool LevelClearedThisFrame { get; private set; }
    public bool LevelStartedThisFrame { get; private set; }
    public bool NearMissThisFrame { get; private set; }
    public float NearMissX { get; private set; }
    public float NearMissDirection { get; private set; }
    public bool OnLog => OnStream && IsSupported(Row - StreamFirstRow);
    public bool BayFilled(int bay) => bays[bay];
    public int RoadCount(int lane) => roadCounts[lane];
    public int PadCount(int lane) => PadCounts[lane];
    public float RoadSpeed(int lane) => roadLaneSpeeds[lane];
    public float StreamSpeed(int lane) => streamLaneSpeeds[lane];
    public LaneEntity RoadEntity(int lane, int index) => roadEntities[lane * MaxEntitiesPerLane + index];
    public LaneEntity Pad(int lane, int index) => streamEntities[lane * MaxEntitiesPerLane + index];

    public int AlignedBay
    {
        get
        {
            if (GameOver || Frozen || Row != StreamLastRow)
            {
                return -1;
            }

            var bay = BayAt(X);
            return bay >= 0 && !bays[bay] ? bay : -1;
        }
    }

    public static float Wrap(float value) => ((value % Columns) + Columns) % Columns;

    public static bool SpansOverlap(float aStart, float aLength, float bStart, float bLength)
    {
        var distance = Wrap(bStart - aStart);
        return distance < aLength || distance > Columns - bLength;
    }

    public static bool PadSupports(float hopperX, float padX, int padLength)
    {
        var distance = Wrap(hopperX + 0.5f - padX);
        return distance <= padLength + PadGripSlack || distance >= Columns - PadGripSlack;
    }

    public static bool NearMiss(float hopperX, float vehicleX, int vehicleLength, float direction)
    {
        var hopperStart = hopperX + CollisionMargin;
        var hopperLength = 1f - CollisionMargin * 2f;
        if (SpansOverlap(hopperStart, hopperLength, vehicleX, vehicleLength))
        {
            return false;
        }

        var ahead = Wrap(vehicleX - (hopperStart + hopperLength));
        var behind = Wrap(hopperStart - (vehicleX + vehicleLength));
        return (direction >= 0f ? ahead : behind) < NearMissCells;
    }

    public static int BayAt(float x)
    {
        for (var bay = 0; bay < BayCount; bay++)
        {
            if (MathF.Abs(x - BayColumns[bay]) <= BayCatchHalfWidth)
            {
                return bay;
            }
        }

        return -1;
    }

    public static int BankPoints(float timerRemaining) =>
        HopPoints + BayPoints + Math.Min(BayTimeBonusMax, (int)timerRemaining * BayTimeBonusPerSecond);

    public static int RoadCountForLevel(int lane, int level) =>
        Math.Min(MaxEntitiesPerLane, RoadBaseCounts[lane] + (level - 1) / DensityRampLevels);

    public static float SpeedRamp(int level) => MathF.Pow(SpeedRampPerLevel, level - 1);

    public static int PerLevelMaximum() =>
        BayCount * ((StreamLastRow - StartRow) * HopPoints + BankPoints(LifeTimerSeconds)) + LevelClearBonus;

    public void StartGame(GameRandom seededRandom)
    {
        random = seededRandom;
        Score = 0;
        Level = 1;
        Lives = StartLives;
        GameOver = false;
        BankedTotal = 0;
        BankedThisLevel = 0;
        deathTimer = 0f;
        clearTimer = 0f;
        nearMissVehicle = -1;
        LastBankedBay = -1;
        LastBankPoints = 0;
        BumpColumn = -1;
        BankFlash = 0f;
        BumpFlash = 0f;
        clock.Reset();
        for (var bay = 0; bay < BayCount; bay++)
        {
            bays[bay] = false;
        }

        ClearFrameEvents();
        BuildLanes();
        Respawn();
        LevelStartedThisFrame = true;
    }

    public void Hop(int dx, int dy)
    {
        if (GameOver || Frozen)
        {
            return;
        }

        HopFlash = HopFlashSeconds;
        HoppedThisFrame = true;
        if (dy > 0)
        {
            HopUp();
        }
        else if (dy < 0 && Row > StartRow)
        {
            Row--;
            SnapOffStream();
        }

        if (dx == 0)
        {
            return;
        }

        if (OnStream)
        {
            X += dx;
            KillIfOffEdge();
            return;
        }

        X = Math.Clamp(MathF.Floor(X + 0.5f) + dx, 0f, Columns - 1);
    }

    public void Tick(float deltaSeconds)
    {
        ClearFrameEvents();
        if (GameOver)
        {
            return;
        }

        var steps = clock.Advance(deltaSeconds);
        for (var step = 0; step < steps; step++)
        {
            Step(FixedStepSeconds);
            if (GameOver)
            {
                return;
            }
        }
    }

    private bool OnStream => Row >= StreamFirstRow && Row <= StreamLastRow;

    private bool OnRoad => Row >= RoadFirstRow && Row <= RoadLastRow;

    private void ClearFrameEvents()
    {
        HoppedThisFrame = false;
        BankedBayThisFrame = -1;
        BumpedThisFrame = false;
        DiedThisFrame = false;
        LevelClearedThisFrame = false;
        LevelStartedThisFrame = false;
        NearMissThisFrame = false;
    }

    private void BuildLanes()
    {
        var ramp = SpeedRamp(Level);
        for (var lane = 0; lane < LaneCount; lane++)
        {
            roadLaneSpeeds[lane] = RoadSpeeds[lane] * ramp * (lane % 2 == 0 ? 1f : -1f);
            var count = RoadCountForLevel(lane, Level);
            roadCounts[lane] = count;
            var spacing = Columns / (float)count;
            var phase = random.NextFloat() * Columns;
            for (var index = 0; index < count; index++)
            {
                roadEntities[lane * MaxEntitiesPerLane + index] = new LaneEntity
                {
                    X = Wrap(phase + index * spacing),
                    Length = random.Next(VehicleMinLength, VehicleMaxLength + 1),
                };
            }

            streamLaneSpeeds[lane] = StreamSpeeds[lane] * ramp * (lane % 2 == 0 ? -1f : 1f);
            var padSpacing = Columns / (float)PadCounts[lane];
            var padPhase = random.NextFloat() * Columns;
            for (var index = 0; index < PadCounts[lane]; index++)
            {
                streamEntities[lane * MaxEntitiesPerLane + index] = new LaneEntity
                {
                    X = Wrap(padPhase + index * padSpacing),
                    Length = PadLengths[lane],
                };
            }
        }
    }

    private void Step(float deltaSeconds)
    {
        HopFlash = MathF.Max(0f, HopFlash - deltaSeconds);
        BankFlash = MathF.Max(0f, BankFlash - deltaSeconds);
        BumpFlash = MathF.Max(0f, BumpFlash - deltaSeconds);
        if (clearTimer > 0f)
        {
            clearTimer -= deltaSeconds;
            if (clearTimer <= 0f)
            {
                clearTimer = 0f;
                Level++;
                BankedThisLevel = 0;
                for (var bay = 0; bay < BayCount; bay++)
                {
                    bays[bay] = false;
                }

                BuildLanes();
                Respawn();
                LevelStartedThisFrame = true;
            }

            return;
        }

        MoveLanes(deltaSeconds);
        if (deathTimer > 0f)
        {
            deathTimer -= deltaSeconds;
            if (deathTimer <= 0f)
            {
                deathTimer = 0f;
                if (Lives <= 0)
                {
                    GameOver = true;
                }
                else
                {
                    Respawn();
                }
            }

            return;
        }

        TimerRemaining -= deltaSeconds;
        if (TimerRemaining <= 0f)
        {
            TimerRemaining = 0f;
            StartDeath();
            return;
        }

        if (OnRoad)
        {
            var lane = Row - RoadFirstRow;
            for (var index = 0; index < roadCounts[lane]; index++)
            {
                var vehicle = roadEntities[lane * MaxEntitiesPerLane + index];
                if (SpansOverlap(X + CollisionMargin, 1f - CollisionMargin * 2f, vehicle.X, vehicle.Length))
                {
                    StartDeath();
                    return;
                }
            }

            DetectNearMiss(lane);
            return;
        }

        if (!OnStream)
        {
            return;
        }

        var streamLane = Row - StreamFirstRow;
        if (!IsSupported(streamLane))
        {
            StartDeath();
            return;
        }

        X += streamLaneSpeeds[streamLane] * deltaSeconds;
        KillIfOffEdge();
    }

    private void MoveLanes(float deltaSeconds)
    {
        for (var lane = 0; lane < LaneCount; lane++)
        {
            for (var index = 0; index < roadCounts[lane]; index++)
            {
                ref var vehicle = ref roadEntities[lane * MaxEntitiesPerLane + index];
                vehicle.X = Wrap(vehicle.X + roadLaneSpeeds[lane] * deltaSeconds);
            }

            for (var index = 0; index < PadCounts[lane]; index++)
            {
                ref var pad = ref streamEntities[lane * MaxEntitiesPerLane + index];
                pad.X = Wrap(pad.X + streamLaneSpeeds[lane] * deltaSeconds);
            }
        }
    }

    private void DetectNearMiss(int lane)
    {
        var direction = roadLaneSpeeds[lane] >= 0f ? 1f : -1f;
        for (var index = 0; index < roadCounts[lane]; index++)
        {
            var slot = lane * MaxEntitiesPerLane + index;
            var vehicle = roadEntities[slot];
            if (!NearMiss(X, vehicle.X, vehicle.Length, direction))
            {
                continue;
            }

            if (slot == nearMissVehicle)
            {
                return;
            }

            nearMissVehicle = slot;
            NearMissThisFrame = true;
            NearMissX = Wrap(vehicle.X + vehicle.Length * 0.5f);
            NearMissDirection = direction;
            return;
        }

        nearMissVehicle = -1;
    }

    private bool IsSupported(int lane)
    {
        for (var index = 0; index < PadCounts[lane]; index++)
        {
            var pad = streamEntities[lane * MaxEntitiesPerLane + index];
            if (PadSupports(X, pad.X, pad.Length))
            {
                return true;
            }
        }

        return false;
    }

    private void KillIfOffEdge()
    {
        if (X + 0.5f < 0f || X + 0.5f > Columns)
        {
            StartDeath();
        }
    }

    private void HopUp()
    {
        if (Row == StreamLastRow)
        {
            TryBank();
            return;
        }

        Row++;
        SnapOffStream();
        if (Row <= maxRowReached)
        {
            return;
        }

        Score += HopPoints * (Row - maxRowReached);
        maxRowReached = Row;
    }

    private void TryBank()
    {
        var bay = BayAt(X);
        if (bay < 0)
        {
            Bump((int)MathF.Round(X));
            return;
        }

        if (bays[bay])
        {
            Bump(BayColumns[bay]);
            return;
        }

        bays[bay] = true;
        BankedTotal++;
        BankedThisLevel++;
        LastBankedBay = bay;
        BankFlash = BankFlashSeconds;
        BankedBayThisFrame = bay;
        LastBankPoints = BankPoints(TimerRemaining);
        Score += LastBankPoints;
        if (BankedThisLevel >= BayCount)
        {
            Score += LevelClearBonus;
            LevelClearedThisFrame = true;
            clearTimer = LevelClearPauseSeconds;
            return;
        }

        Respawn();
    }

    private void Bump(int column)
    {
        BumpFlash = BumpFlashSeconds;
        BumpColumn = Math.Clamp(column, 0, Columns - 1);
        BumpedThisFrame = true;
    }

    private void SnapOffStream()
    {
        if (OnStream)
        {
            return;
        }

        X = Math.Clamp(MathF.Floor(X + 0.5f), 0f, Columns - 1);
    }

    private void StartDeath()
    {
        Lives--;
        deathTimer = DeathPauseSeconds;
        HopFlash = 0f;
        DiedThisFrame = true;
    }

    private void Respawn()
    {
        Row = StartRow;
        X = StartColumn;
        TimerRemaining = LifeTimerSeconds;
        maxRowReached = StartRow;
        HopFlash = 0f;
        nearMissVehicle = -1;
    }
}
