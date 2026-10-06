using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Squadron;

internal enum ShipKind : byte
{
    Drone,
    Raptor,
    Warden,
}

internal enum ShipState : byte
{
    Waiting,
    FlyIn,
    Parked,
    Diving,
    Beam,
    Returning,
    Gone,
}

internal struct Ship
{
    public ShipKind Kind;
    public ShipState State;
    public Vector2 Position;
    public Vector2 Slot;
    public Vector2 P0;
    public Vector2 P1;
    public Vector2 P2;
    public Vector2 P3;
    public bool TargetsSlot;
    public float FlyAt;
    public float PathTime;
    public float PathDuration;
    public float BeamTime;
    public bool BeamRun;
    public bool HoldsCaptive;
    public float FireAt1;
    public float FireAt2;
}

internal struct EnemyShot
{
    public Vector2 Position;
    public Vector2 Velocity;
}

internal sealed class SquadronBoard
{
    public const float Width = 100f;
    public const float Height = 140f;
    public const float ShipWidth = 8f;
    public const float ShipHeight = 6f;
    public const float PlayerWidth = 9f;
    public const float PlayerHeight = 4.5f;
    public const float PlayerRowY = 126f;
    public const float BeamTopHalfWidth = 2f;
    public const float BeamBottomHalfWidth = 10f;
    public const int DroneParkedPoints = 50;
    public const int DroneDivingPoints = 100;
    public const int RaptorParkedPoints = 80;
    public const int RaptorDivingPoints = 160;
    public const int WardenParkedPoints = 150;
    public const int WardenDivingPoints = 300;
    public const int RescueBonus = 1000;
    public const int ChallengeHitPoints = 100;
    public const int PerfectBonus = 1000;
    public const int StageClearBonus = 200;
    public const int ChallengeShipCount = 20;
    public const int StartLives = 3;
    public const int ChallengeEveryNth = 3;
    public const int MaxShips = 26;
    public const int MaxBullets = 3;
    public const int MaxEnemyShots = 4;
    public const float CaptureSeconds = 1.5f;
    public const float RescueSeconds = 1.6f;
    public const float RespawnSeconds = 1.2f;
    public const float ResultBannerSeconds = 2.2f;
    public const float StageBannerSeconds = 1.8f;
    public const float FrameFlipSeconds = 0.28f;
    public const float BeamExtendSeconds = 0.5f;
    public const float BeamHoldSeconds = 1.6f;
    public const float BeamRetractSeconds = 0.5f;
    private const int BaseDronesPerRow = 5;
    private const int RaptorsPerRow = 4;
    private const int WardenCount = 2;
    private const int ExtraDronesPerStage = 2;
    private const int MaxExtraDrones = 6;
    private const float CenterX = 50f;
    private const float ColumnPitch = 12f;
    private const float WardenRowY = 15f;
    private const float RaptorRowAY = 25f;
    private const float RaptorRowBY = 35f;
    private const float DroneRowAY = 45f;
    private const float DroneRowBY = 55f;
    private const float DroneRowCY = 65f;
    private const float BreatheAmplitude = 0.08f;
    private const float BreatheRate = 0.9f;
    private static readonly Vector2 BreatheCenter = new(50f, 38f);
    private const float PlayerSpeed = 62f;
    private const float BulletSpeed = 120f;
    private const float ShotSpeed = 45f;
    private const int SingleShotCap = 2;
    private const int DualShotCap = 3;
    private const float DualSpread = 4.5f;
    private const int MaxAirborneDivers = 3;
    private const float FlyInSeconds = 1.9f;
    private const float FirstWaveDelay = 0.8f;
    private const float WaveInterval = 1.5f;
    private const float ShipStagger = 0.16f;
    private const float ReturnSeconds = 1.6f;
    private const float ChallengeFlySeconds = 3.2f;
    private const float ChallengeWaveInterval = 2.4f;
    private const float ChallengeStagger = 0.24f;
    private const float BeamHoverY = 82f;
    private const float WardenDiveChance = 0.22f;
    private const float BeamChance = 0.65f;
    private const float FirstDiveDelay = 3.5f;
    private const float DiveRetrySeconds = 0.4f;
    private const float MinimumShotAimY = 10f;
    private const float BulletHitHalfWidth = ShipWidth * 0.5f;
    private const float BulletHitHalfHeight = ShipHeight * 0.5f;
    private const float PlayerMinX = PlayerWidth * 0.5f;
    private const float PlayerMaxX = Width - PlayerWidth * 0.5f;
    private readonly Ship[] ships = new Ship[MaxShips];
    private readonly Vector2[] bullets = new Vector2[MaxBullets];
    private readonly EnemyShot[] shots = new EnemyShot[MaxEnemyShots];
    private readonly int[] parkedWardens = new int[MaxShips];
    private readonly int[] parkedEscorts = new int[MaxShips];
    private readonly Vector2[] killPositions = new Vector2[MaxShips];
    private readonly ShipKind[] killKinds = new ShipKind[MaxShips];
    private readonly int[] killPoints = new int[MaxShips];
    private GameRandom random;
    private int shipCount;
    private int bulletCount;
    private int shotCount;
    private float breathePhase;
    private float diveTimer;
    private float respawnTimer;
    private float resultTimer;
    private float captureTimer;
    private float rescueTimer;
    private float frameTimer;
    private Vector2 captureFrom;
    private Vector2 rescueFrom;
    private int captureWarden = -1;
    public int Score { get; private set; }
    public int Stage { get; private set; }
    public int Lives { get; private set; }
    public bool GameOver { get; private set; }
    public bool Dual { get; private set; }
    public bool DualAchieved { get; private set; }
    public float PlayerX { get; private set; } = CenterX;
    public bool AnimFrame { get; private set; }
    public bool IsChallenge { get; private set; }
    public float StageTime { get; private set; }
    public int ChallengeHits { get; private set; }
    public int LastChallengeHits { get; private set; }
    public bool LastChallengeWasPerfect { get; private set; }
    public bool CaptureActive { get; private set; }
    public bool RescueActive { get; private set; }
    public int ShotsFired { get; private set; }
    public int ShotsHit { get; private set; }
    public int Rescues { get; private set; }
    public bool Respawning => respawnTimer > 0f;
    public float RespawnRemaining => respawnTimer;
    public bool ShowingResult => resultTimer > 0f;
    public int ShipCount => shipCount;
    public int BulletCount => bulletCount;
    public int ShotCount => shotCount;
    public int KillCount { get; private set; }
    public bool ShotFiredThisFrame { get; private set; }
    public bool PlayerHitThisFrame { get; private set; }
    public bool DualLostThisFrame { get; private set; }
    public bool CaptureStartedThisFrame { get; private set; }
    public bool RescueStartedThisFrame { get; private set; }
    public bool RescueCompletedThisFrame { get; private set; }
    public bool StageStartedThisFrame { get; private set; }
    public bool StageClearedThisFrame { get; private set; }
    public bool ChallengeEndedThisFrame { get; private set; }
    public Ship GetShip(int index) => ships[index];
    public Vector2 GetBullet(int index) => bullets[index];
    public EnemyShot GetShot(int index) => shots[index];
    public Vector2 KillPosition(int index) => killPositions[index];
    public ShipKind KillKind(int index) => killKinds[index];
    public int KillPoints(int index) => killPoints[index];
    public Vector2 PlayerCenter => new(PlayerX, PlayerRowY - PlayerHeight * 0.5f);
    public float PlayerHalfWidth => Dual ? PlayerWidth : PlayerWidth * 0.5f;

    public Vector2 CapturePosition
    {
        get
        {
            if (!CaptureActive || captureWarden < 0)
            {
                return captureFrom;
            }

            return Vector2.Lerp(captureFrom, ships[captureWarden].Position, Smooth(captureTimer / CaptureSeconds));
        }
    }

    public Vector2 RescuePosition => Vector2.Lerp(rescueFrom, PlayerCenter, Smooth(rescueTimer / RescueSeconds));

    public static bool StageIsChallenge(int stage) => stage % ChallengeEveryNth == 0;

    public static float DiveInterval(int stage) => MathF.Max(0.85f, 2.6f - 0.2f * (stage - 1));

    public static float PairChance(int stage) => MathF.Min(0.65f, 0.1f + 0.09f * (stage - 1));

    public static float DiveSeconds(int stage) => MathF.Max(1.7f, 2.5f - 0.07f * (stage - 1));

    public static int ThirdDroneRow(int stage) => Math.Min(MaxExtraDrones, (stage - 1) * ExtraDronesPerStage);

    public static int FormationSize(int stage) => BaseDronesPerRow * 2 + ThirdDroneRow(stage) + RaptorsPerRow * 2 + WardenCount;

    public static int PointsFor(ShipKind kind, bool parked)
    {
        switch (kind)
        {
            case ShipKind.Drone:
                return parked ? DroneParkedPoints : DroneDivingPoints;
            case ShipKind.Raptor:
                return parked ? RaptorParkedPoints : RaptorDivingPoints;
            default:
                return parked ? WardenParkedPoints : WardenDivingPoints;
        }
    }

    public float BeamExtent(in Ship ship)
    {
        if (ship.State != ShipState.Beam)
        {
            return 0f;
        }

        if (ship.BeamTime < BeamExtendSeconds)
        {
            return ship.BeamTime / BeamExtendSeconds;
        }

        var holdEnd = BeamExtendSeconds + BeamHoldSeconds;
        if (ship.BeamTime < holdEnd)
        {
            return 1f;
        }

        return Math.Clamp(1f - (ship.BeamTime - holdEnd) / BeamRetractSeconds, 0f, 1f);
    }

    public Vector2 SlotPosition(in Ship ship) =>
        BreatheCenter + (ship.Slot - BreatheCenter) * (1f + BreatheAmplitude * MathF.Sin(breathePhase));

    public void StartGame(GameRandom seededRandom)
    {
        random = seededRandom;
        Score = 0;
        Lives = StartLives;
        GameOver = false;
        Dual = false;
        DualAchieved = false;
        CaptureActive = false;
        RescueActive = false;
        captureWarden = -1;
        respawnTimer = 0f;
        resultTimer = 0f;
        frameTimer = 0f;
        ShotsFired = 0;
        ShotsHit = 0;
        Rescues = 0;
        PlayerX = CenterX;
        ClearFrameEvents();
        StartStage(1);
    }

    public void Move(float direction, float deltaSeconds)
    {
        if (GameOver || CaptureActive || direction == 0f)
        {
            return;
        }

        PlayerX = Math.Clamp(PlayerX + direction * PlayerSpeed * deltaSeconds, PlayerMinX, PlayerMaxX);
    }

    public bool Fire()
    {
        if (GameOver || CaptureActive || respawnTimer > 0f)
        {
            return false;
        }

        var cap = Dual ? DualShotCap : SingleShotCap;
        if (bulletCount >= cap)
        {
            return false;
        }

        var muzzleY = PlayerRowY - PlayerHeight;
        if (!Dual)
        {
            bullets[bulletCount++] = new Vector2(PlayerX, muzzleY);
            ShotsFired++;
            ShotFiredThisFrame = true;
            return true;
        }

        bullets[bulletCount++] = new Vector2(PlayerX - DualSpread, muzzleY);
        ShotsFired++;
        if (bulletCount < cap)
        {
            bullets[bulletCount++] = new Vector2(PlayerX + DualSpread, muzzleY);
            ShotsFired++;
        }

        ShotFiredThisFrame = true;
        return true;
    }

    public void Tick(float deltaSeconds)
    {
        ClearFrameEvents();
        if (GameOver || deltaSeconds <= 0f)
        {
            return;
        }

        frameTimer += deltaSeconds;
        if (frameTimer >= FrameFlipSeconds)
        {
            frameTimer -= FrameFlipSeconds;
            AnimFrame = !AnimFrame;
        }

        if (CaptureActive)
        {
            captureTimer += deltaSeconds;
            if (captureTimer >= CaptureSeconds)
            {
                CompleteCapture();
            }

            return;
        }

        if (respawnTimer > 0f)
        {
            respawnTimer -= deltaSeconds;
            return;
        }

        if (resultTimer > 0f)
        {
            resultTimer -= deltaSeconds;
            if (resultTimer <= 0f)
            {
                resultTimer = 0f;
                StartStage(Stage + 1);
            }

            return;
        }

        StageTime += deltaSeconds;
        breathePhase += deltaSeconds * BreatheRate;
        if (RescueActive)
        {
            rescueTimer += deltaSeconds;
            if (rescueTimer >= RescueSeconds)
            {
                CompleteRescue();
            }
        }

        if (!IsChallenge)
        {
            TickDives(deltaSeconds);
        }

        var substeps = new Substeps(deltaSeconds, BulletHitHalfHeight / BulletSpeed);
        for (var step = 0; step < substeps.Count; step++)
        {
            TickShips(substeps.Step);
            if (GameOver || CaptureActive || respawnTimer > 0f)
            {
                break;
            }

            MoveBullets(substeps.Step);
            MoveShots(substeps.Step);
            if (GameOver || CaptureActive || respawnTimer > 0f)
            {
                break;
            }
        }

        CheckStageEnd();
    }

    private void ClearFrameEvents()
    {
        KillCount = 0;
        ShotFiredThisFrame = false;
        PlayerHitThisFrame = false;
        DualLostThisFrame = false;
        CaptureStartedThisFrame = false;
        RescueStartedThisFrame = false;
        RescueCompletedThisFrame = false;
        StageStartedThisFrame = false;
        StageClearedThisFrame = false;
        ChallengeEndedThisFrame = false;
    }

    private void StartStage(int stage)
    {
        Stage = stage;
        StageTime = 0f;
        ChallengeHits = 0;
        breathePhase = 0f;
        diveTimer = FirstDiveDelay;
        shipCount = 0;
        bulletCount = 0;
        shotCount = 0;
        RescueActive = false;
        IsChallenge = StageIsChallenge(stage);
        if (IsChallenge)
        {
            BuildChallenge();
        }
        else
        {
            BuildFormation();
            AssignWaves();
        }

        StageStartedThisFrame = true;
    }

    private void AddShip(ShipKind kind, Vector2 slot)
    {
        if (shipCount >= MaxShips)
        {
            return;
        }

        ships[shipCount++] = new Ship
        {
            Kind = kind,
            State = ShipState.Waiting,
            Position = new Vector2(-20f, -20f),
            Slot = slot,
            FireAt1 = -1f,
            FireAt2 = -1f,
        };
    }

    private void AddRow(ShipKind kind, int count, float y)
    {
        var left = CenterX - (count - 1) * ColumnPitch * 0.5f;
        for (var index = 0; index < count; index++)
        {
            AddShip(kind, new Vector2(left + index * ColumnPitch, y));
        }
    }

    private void BuildFormation()
    {
        AddRow(ShipKind.Drone, BaseDronesPerRow, DroneRowAY);
        AddRow(ShipKind.Drone, BaseDronesPerRow, DroneRowBY);
        var third = ThirdDroneRow(Stage);
        if (third > 0)
        {
            AddRow(ShipKind.Drone, third, DroneRowCY);
        }

        AddRow(ShipKind.Raptor, RaptorsPerRow, RaptorRowAY);
        AddRow(ShipKind.Raptor, RaptorsPerRow, RaptorRowBY);
        AddRow(ShipKind.Warden, WardenCount, WardenRowY);
    }

    private void AssignWaves()
    {
        var total = shipCount;
        var waves = Math.Clamp((int)MathF.Ceiling(total / 6f), 2, 5);
        var shipIndex = 0;
        for (var wave = 0; wave < waves; wave++)
        {
            var size = total / waves + (wave < total % waves ? 1 : 0);
            var fromLeft = wave % 2 == 0;
            for (var index = 0; index < size && shipIndex < total; index++, shipIndex++)
            {
                ref var ship = ref ships[shipIndex];
                ship.FlyAt = FirstWaveDelay + wave * WaveInterval + index * ShipStagger;
                ship.PathDuration = FlyInSeconds;
                ship.TargetsSlot = true;
                ship.P0 = fromLeft ? new Vector2(-10f, 30f) : new Vector2(110f, 30f);
                ship.P1 = fromLeft ? new Vector2(35f, -18f) : new Vector2(65f, -18f);
                ship.P2 = fromLeft ? new Vector2(88f, 58f) : new Vector2(12f, 58f);
            }
        }
    }

    private void BuildChallenge()
    {
        const int waves = 4;
        const int perWave = ChallengeShipCount / waves;
        for (var wave = 0; wave < waves; wave++)
        {
            var fromLeft = wave % 2 == 0;
            var entryY = 16f + wave * 7f;
            for (var index = 0; index < perWave; index++)
            {
                var kind = wave == waves - 1 && index < 2 ? ShipKind.Warden : index % 2 == 1 ? ShipKind.Raptor : ShipKind.Drone;
                AddShip(kind, Vector2.Zero);
                ref var ship = ref ships[shipCount - 1];
                ship.FlyAt = FirstWaveDelay + wave * ChallengeWaveInterval + index * ChallengeStagger;
                ship.PathDuration = ChallengeFlySeconds;
                ship.TargetsSlot = false;
                ship.P0 = fromLeft ? new Vector2(-10f, entryY) : new Vector2(110f, entryY);
                ship.P1 = fromLeft ? new Vector2(30f, 126f) : new Vector2(70f, 126f);
                ship.P2 = fromLeft ? new Vector2(74f, -12f) : new Vector2(26f, -12f);
                ship.P3 = fromLeft ? new Vector2(112f, 88f) : new Vector2(-12f, 88f);
            }
        }
    }

    private Vector2 PathPosition(in Ship ship)
    {
        var t = Math.Clamp(ship.PathTime / ship.PathDuration, 0f, 1f);
        var target = ship.TargetsSlot ? SlotPosition(in ship) : ship.P3;
        var inverse = 1f - t;
        return inverse * inverse * inverse * ship.P0 + 3f * inverse * inverse * t * ship.P1 + 3f * inverse * t * t * ship.P2 +
            t * t * t * target;
    }

    private void TickShips(float deltaSeconds)
    {
        for (var index = 0; index < shipCount; index++)
        {
            ref var ship = ref ships[index];
            switch (ship.State)
            {
                case ShipState.Waiting:
                    if (StageTime >= ship.FlyAt)
                    {
                        ship.State = ShipState.FlyIn;
                        ship.PathTime = 0f;
                        ship.Position = ship.P0;
                    }

                    break;
                case ShipState.FlyIn:
                    ship.PathTime += deltaSeconds;
                    if (ship.PathTime >= ship.PathDuration)
                    {
                        if (ship.TargetsSlot)
                        {
                            ship.State = ShipState.Parked;
                            ship.Position = SlotPosition(in ship);
                        }
                        else
                        {
                            ship.State = ShipState.Gone;
                        }
                    }
                    else
                    {
                        ship.Position = PathPosition(in ship);
                    }

                    break;
                case ShipState.Parked:
                    ship.Position = SlotPosition(in ship);
                    break;
                case ShipState.Diving:
                    ship.PathTime += deltaSeconds;
                    TickDiveFire(ref ship);
                    if (ship.PathTime >= ship.PathDuration)
                    {
                        if (ship.BeamRun)
                        {
                            ship.State = ShipState.Beam;
                            ship.BeamTime = 0f;
                            ship.Position = ship.P3;
                        }
                        else
                        {
                            BeginReturn(ref ship);
                        }
                    }
                    else
                    {
                        ship.Position = PathPosition(in ship);
                        CheckPlayerCollision(ref ship);
                    }

                    break;
                case ShipState.Beam:
                    ship.BeamTime += deltaSeconds;
                    TryBeamCapture(ref ship, index);
                    if (ship.BeamTime >= BeamExtendSeconds + BeamHoldSeconds + BeamRetractSeconds)
                    {
                        BeginReturn(ref ship);
                    }

                    break;
                case ShipState.Returning:
                    ship.PathTime += deltaSeconds;
                    if (ship.PathTime >= ship.PathDuration)
                    {
                        ship.State = ShipState.Parked;
                        ship.Position = SlotPosition(in ship);
                    }
                    else
                    {
                        ship.Position = PathPosition(in ship);
                    }

                    break;
            }

            if (CaptureActive || respawnTimer > 0f || GameOver)
            {
                return;
            }
        }
    }

    private void TickDiveFire(ref Ship ship)
    {
        if (ship.FireAt1 >= 0f && ship.PathTime >= ship.FireAt1)
        {
            ship.FireAt1 = -1f;
            FireShot(in ship);
        }

        if (ship.FireAt2 >= 0f && ship.PathTime >= ship.FireAt2)
        {
            ship.FireAt2 = -1f;
            FireShot(in ship);
        }
    }

    private void FireShot(in Ship ship)
    {
        if (shotCount >= MaxEnemyShots)
        {
            return;
        }

        var aim = PlayerCenter - ship.Position;
        aim.Y = MathF.Max(aim.Y, MinimumShotAimY);
        shots[shotCount++] = new EnemyShot
        {
            Position = ship.Position,
            Velocity = Vector2.Normalize(aim) * ShotSpeed,
        };
    }

    private int AirborneCount()
    {
        var count = 0;
        for (var index = 0; index < shipCount; index++)
        {
            if (ships[index].State is ShipState.Diving or ShipState.Beam or ShipState.Returning)
            {
                count++;
            }
        }

        return count;
    }

    private bool AnyCaptive()
    {
        for (var index = 0; index < shipCount; index++)
        {
            if (ships[index].State != ShipState.Gone && ships[index].HoldsCaptive)
            {
                return true;
            }
        }

        return false;
    }

    private void TickDives(float deltaSeconds)
    {
        diveTimer -= deltaSeconds;
        if (diveTimer > 0f)
        {
            return;
        }

        diveTimer = DiveRetrySeconds;
        if (AirborneCount() >= MaxAirborneDivers)
        {
            return;
        }

        var wardenCount = 0;
        var escortCount = 0;
        for (var index = 0; index < shipCount; index++)
        {
            if (ships[index].State != ShipState.Parked)
            {
                continue;
            }

            if (ships[index].Kind == ShipKind.Warden)
            {
                parkedWardens[wardenCount++] = index;
            }
            else
            {
                parkedEscorts[escortCount++] = index;
            }
        }

        if (wardenCount == 0 && escortCount == 0)
        {
            return;
        }

        if (wardenCount > 0 && (escortCount == 0 || Chance() < WardenDiveChance))
        {
            var warden = parkedWardens[random.Next(wardenCount)];
            var beamEligible = !Dual && !RescueActive && !AnyCaptive();
            StartDive(ref ships[warden], beamEligible && Chance() < BeamChance);
        }
        else
        {
            var pick = random.Next(escortCount);
            StartDive(ref ships[parkedEscorts[pick]], false);
            parkedEscorts[pick] = parkedEscorts[--escortCount];
            if (escortCount > 0 && Chance() < PairChance(Stage))
            {
                StartDive(ref ships[parkedEscorts[random.Next(escortCount)]], false);
            }
        }

        diveTimer = DiveInterval(Stage) * (0.8f + Chance() * 0.5f);
    }

    private void StartDive(ref Ship ship, bool beamRun)
    {
        var side = ship.Position.X < CenterX ? -1f : 1f;
        ship.State = ShipState.Diving;
        ship.PathTime = 0f;
        ship.TargetsSlot = false;
        ship.BeamRun = beamRun;
        ship.P0 = ship.Position;
        if (beamRun)
        {
            var hoverX = Math.Clamp(PlayerX, PlayerMinX, PlayerMaxX);
            ship.PathDuration = DiveSeconds(Stage) * 0.9f;
            ship.P1 = new Vector2(ship.P0.X + side * 22f, ship.P0.Y + 26f);
            ship.P2 = new Vector2(hoverX - side * 18f, 60f);
            ship.P3 = new Vector2(hoverX, BeamHoverY);
            ship.FireAt1 = 0.3f * ship.PathDuration;
            ship.FireAt2 = -1f;
            return;
        }

        var exitX = Math.Clamp(PlayerX + (Chance() * 24f - 12f), 6f, 94f);
        ship.PathDuration = DiveSeconds(Stage);
        ship.P1 = new Vector2(ship.P0.X + side * 26f, ship.P0.Y + 30f);
        ship.P2 = new Vector2(exitX + side * 20f, 96f);
        ship.P3 = new Vector2(exitX, 150f);
        ship.FireAt1 = 0.3f * ship.PathDuration;
        ship.FireAt2 = ship.Kind == ShipKind.Drone ? -1f : 0.55f * ship.PathDuration;
    }

    private void BeginReturn(ref Ship ship)
    {
        ship.State = ShipState.Returning;
        ship.PathTime = 0f;
        ship.PathDuration = ReturnSeconds;
        ship.BeamRun = false;
        ship.FireAt1 = -1f;
        ship.FireAt2 = -1f;
        ship.TargetsSlot = true;
        var from = ship.Position.Y >= Height - 1f ? new Vector2(Math.Clamp(ship.Position.X, 5f, 95f), -10f) : ship.Position;
        ship.P0 = from;
        ship.P1 = new Vector2(from.X, (from.Y + ship.Slot.Y) * 0.5f);
        ship.P2 = new Vector2(ship.Slot.X, ship.Slot.Y - 18f);
        ship.Position = from;
    }

    private void TryBeamCapture(ref Ship ship, int shipIndex)
    {
        if (Dual || CaptureActive || RescueActive || respawnTimer > 0f)
        {
            return;
        }

        if (ship.BeamTime < BeamExtendSeconds || ship.BeamTime > BeamExtendSeconds + BeamHoldSeconds)
        {
            return;
        }

        if (MathF.Abs(PlayerX - ship.Position.X) > BeamBottomHalfWidth)
        {
            return;
        }

        CaptureActive = true;
        captureWarden = shipIndex;
        captureTimer = 0f;
        captureFrom = PlayerCenter;
        bulletCount = 0;
        shotCount = 0;
        CaptureStartedThisFrame = true;
    }

    private void CompleteCapture()
    {
        CaptureActive = false;
        if (captureWarden >= 0)
        {
            ref var warden = ref ships[captureWarden];
            warden.HoldsCaptive = true;
            BeginReturn(ref warden);
        }

        captureWarden = -1;
        LoseLife();
    }

    private void StartRescue(Vector2 from)
    {
        RescueActive = true;
        rescueTimer = 0f;
        rescueFrom = from;
        RescueStartedThisFrame = true;
    }

    private void CompleteRescue()
    {
        RescueActive = false;
        Dual = true;
        DualAchieved = true;
        Rescues++;
        Score += RescueBonus;
        RescueCompletedThisFrame = true;
    }

    private void MoveBullets(float deltaSeconds)
    {
        for (var index = bulletCount - 1; index >= 0; index--)
        {
            bullets[index].Y -= BulletSpeed * deltaSeconds;
            var bullet = bullets[index];
            if (bullet.Y < -4f)
            {
                bullets[index] = bullets[--bulletCount];
                continue;
            }

            var hit = FindShipHit(bullet);
            if (hit < 0)
            {
                continue;
            }

            ShotsHit++;
            KillShip(hit);
            bullets[index] = bullets[--bulletCount];
        }
    }

    private int FindShipHit(Vector2 bullet)
    {
        for (var index = 0; index < shipCount; index++)
        {
            var ship = ships[index];
            if (ship.State is ShipState.Waiting or ShipState.Gone)
            {
                continue;
            }

            if (MathF.Abs(bullet.X - ship.Position.X) <= BulletHitHalfWidth &&
                MathF.Abs(bullet.Y - ship.Position.Y) <= BulletHitHalfHeight)
            {
                return index;
            }
        }

        return -1;
    }

    private void KillShip(int index)
    {
        ref var ship = ref ships[index];
        int points;
        if (IsChallenge)
        {
            points = ChallengeHitPoints;
            ChallengeHits++;
        }
        else
        {
            points = PointsFor(ship.Kind, ship.State == ShipState.Parked);
        }

        Score += points;
        if (ship.HoldsCaptive && ship.State != ShipState.Parked)
        {
            StartRescue(ship.Position);
        }

        killPositions[KillCount] = ship.Position;
        killKinds[KillCount] = ship.Kind;
        killPoints[KillCount] = points;
        KillCount++;
        ship.HoldsCaptive = false;
        ship.State = ShipState.Gone;
    }

    private void CheckPlayerCollision(ref Ship ship)
    {
        if (respawnTimer > 0f || CaptureActive)
        {
            return;
        }

        var center = PlayerCenter;
        if (MathF.Abs(ship.Position.Y - center.Y) > PlayerHeight * 0.5f + BulletHitHalfHeight)
        {
            return;
        }

        if (MathF.Abs(ship.Position.X - PlayerX) > PlayerHalfWidth + BulletHitHalfWidth)
        {
            return;
        }

        killPositions[KillCount] = ship.Position;
        killKinds[KillCount] = ship.Kind;
        killPoints[KillCount] = 0;
        KillCount++;
        ship.HoldsCaptive = false;
        ship.State = ShipState.Gone;
        PlayerHit();
    }

    private void MoveShots(float deltaSeconds)
    {
        for (var index = shotCount - 1; index >= 0; index--)
        {
            shots[index].Position += shots[index].Velocity * deltaSeconds;
            var position = shots[index].Position;
            if (position.Y > Height + 4f || position.X < -4f || position.X > Width + 4f)
            {
                shots[index] = shots[--shotCount];
                continue;
            }

            if (position.Y < PlayerRowY - PlayerHeight || position.Y > PlayerRowY ||
                MathF.Abs(position.X - PlayerX) > PlayerHalfWidth)
            {
                continue;
            }

            shots[index] = shots[--shotCount];
            PlayerHit();
            return;
        }
    }

    private void PlayerHit()
    {
        if (respawnTimer > 0f || CaptureActive)
        {
            return;
        }

        if (Dual)
        {
            Dual = false;
            DualLostThisFrame = true;
            return;
        }

        LoseLife();
    }

    private void LoseLife()
    {
        Lives--;
        bulletCount = 0;
        shotCount = 0;
        PlayerHitThisFrame = true;
        if (Lives <= 0)
        {
            GameOver = true;
            return;
        }

        PlayerX = CenterX;
        respawnTimer = RespawnSeconds;
    }

    private void CheckStageEnd()
    {
        if (GameOver || CaptureActive || respawnTimer > 0f || resultTimer > 0f)
        {
            return;
        }

        for (var index = 0; index < shipCount; index++)
        {
            if (ships[index].State != ShipState.Gone)
            {
                return;
            }
        }

        if (IsChallenge)
        {
            LastChallengeHits = ChallengeHits;
            LastChallengeWasPerfect = ChallengeHits >= ChallengeShipCount;
            if (LastChallengeWasPerfect)
            {
                Score += PerfectBonus;
            }

            bulletCount = 0;
            shotCount = 0;
            ChallengeEndedThisFrame = true;
            resultTimer = ResultBannerSeconds;
            return;
        }

        Score += StageClearBonus;
        StageClearedThisFrame = true;
        StartStage(Stage + 1);
    }

    private static float Smooth(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private float Chance() => random.NextFloat();
}
