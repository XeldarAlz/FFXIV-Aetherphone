using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Drift;

internal enum RockSize : byte
{
    Small,
    Medium,
    Large,
}

internal enum ShipState : byte
{
    Flying,
    Warping,
    Wrecked,
}

internal readonly struct DriftControls
{
    public readonly float Turn;
    public readonly bool Thrust;
    public readonly bool Fire;
    public readonly bool Warp;

    public DriftControls(float turn, bool thrust, bool fire, bool warp)
    {
        Turn = Math.Clamp(turn, -1f, 1f);
        Thrust = thrust;
        Fire = fire;
        Warp = warp;
    }
}

internal struct Rock
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Angle;
    public float Spin;
    public RockSize Size;
    public byte Shape;
}

internal struct Shot
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Life;
}

internal readonly struct RockBreak
{
    public readonly Vector2 Position;
    public readonly Vector2 Velocity;
    public readonly RockSize Size;
    public readonly int Points;

    public RockBreak(Vector2 position, Vector2 velocity, RockSize size, int points)
    {
        Position = position;
        Velocity = velocity;
        Size = size;
        Points = points;
    }
}

internal sealed class DriftBoard
{
    public const float Width = 100f;
    public const float Height = 136f;
    public const float ShipRadius = 2.2f;
    public const float ShipSize = 2.8f;
    public const float RotateSpeed = 4.4f;
    public const float ThrustAcceleration = 62f;
    public const float Drag = 0.42f;
    public const float MaxSpeed = 56f;
    public const float ShotSpeed = 86f;
    public const float ShotLife = 0.85f;
    public const int MaxShots = 6;
    public const float FireCooldown = 0.17f;
    public const float WarpSeconds = 0.45f;
    public const float WarpCooldown = 2.5f;
    public const float WarpClearance = 12f;
    public const float WarpShieldSeconds = 0.35f;
    public const float WreckSeconds = 1.6f;
    public const float SpawnClearance = 16f;
    public const float MaxSpawnWait = 3f;
    public const float InvulnerableSeconds = 2f;
    public const int StartLives = 3;
    public const int MaxLives = 6;
    public const int ExtraLifeScore = 10000;
    public const int MaxRocks = 64;
    public const int RockShapeCount = 8;
    public const int MaxRocksPerWave = 8;
    public const float WaveDelay = 1.8f;
    public const float SpawnDistance = 34f;
    public const int SaucerFirstWave = 3;
    public const float BigSaucerRadius = 4f;
    public const float SmallSaucerRadius = 2.6f;
    public const int BigSaucerPoints = 200;
    public const int SmallSaucerPoints = 1000;
    public const float SaucerShotSpeed = 46f;
    public const float SaucerShotLife = 1.5f;
    public const int MaxSaucerShots = 4;
    public const float MinSaucerGap = 10f;
    public const float MaxSaucerGap = 16f;
    private const float StepSeconds = 1f / 120f;
    private const float SaucerTurnSeconds = 1.2f;
    private const float BigSaucerSpeed = 15f;
    private const float SmallSaucerSpeed = 21f;
    private const float SaucerClimb = 0.6f;
    private const float BigSaucerFireSeconds = 1.3f;
    private const float SmallSaucerFireSeconds = 0.95f;
    private const float SaucerMargin = 14f;
    private const float RockSpin = 1.4f;
    private const float RockHitScale = 0.92f;
    private const int MaxBreaks = 24;
    private const int SpawnAttempts = 12;
    private static readonly float[] Radii = { 2.4f, 4.6f, 8.4f };
    private static readonly int[] RockPointTable = { 100, 50, 20 };
    private static readonly float[] MinSpeeds = { 15f, 10f, 6f };
    private static readonly float[] MaxSpeeds = { 25f, 17f, 11f };

    private readonly Rock[] rocks = new Rock[MaxRocks];
    private readonly Shot[] shots = new Shot[MaxShots];
    private readonly Shot[] saucerShots = new Shot[MaxSaucerShots];
    private readonly RockBreak[] breaks = new RockBreak[MaxBreaks];
    private GameRandom random;
    private ComboMeter combo = ComboMeter.Create();
    private int rockCount;
    private int shotCount;
    private int saucerShotCount;
    private float stateTimer;
    private float spawnWait;
    private float fireTimer;
    private float warpCooldownTimer;
    private float invulnerableTimer;
    private float waveTimer;
    private bool waveClearing;
    private bool warpRequested;
    private int nextExtraLife;
    private float saucerTimer;
    private float saucerTurnTimer;
    private float saucerFireTimer;
    private float saucerTravel;
    private float saucerDirection = 1f;
    private float saucerRise;
    private Vector2 warpTarget;

    public int Score { get; private set; }

    public int Lives { get; private set; }

    public int Wave { get; private set; }

    public bool GameOver { get; private set; }

    public float PlaySeconds { get; private set; }

    public ShipState State { get; private set; }

    public Vector2 ShipPosition { get; private set; }

    public Vector2 ShipVelocity { get; private set; }

    public float ShipAngle { get; private set; }

    public bool Thrusting { get; private set; }

    public bool SaucerActive { get; private set; }

    public bool SaucerSmall { get; private set; }

    public Vector2 SaucerPosition { get; private set; }

    public int ShotsFired { get; private set; }

    public int ShotsHit { get; private set; }

    public int RocksBroken { get; private set; }

    public int SaucersDowned { get; private set; }

    public int WavesCleared { get; private set; }

    public int BreakCount { get; private set; }

    public int FiredCount { get; private set; }

    public bool ShipLostThisFrame { get; private set; }

    public bool SpawnedThisFrame { get; private set; }

    public bool WarpStartedThisFrame { get; private set; }

    public bool WarpEndedThisFrame { get; private set; }

    public Vector2 WarpFrom { get; private set; }

    public bool SaucerArrivedThisFrame { get; private set; }

    public bool SaucerFiredThisFrame { get; private set; }

    public bool SaucerDownedThisFrame { get; private set; }

    public Vector2 SaucerDownPosition { get; private set; }

    public int SaucerDownPoints { get; private set; }

    public bool ExtraLifeThisFrame { get; private set; }

    public bool WaveClearedThisFrame { get; private set; }

    public bool WaveStartedThisFrame { get; private set; }

    public bool ComboRaisedThisFrame { get; private set; }

    public int RockCount => rockCount;

    public int ShotCount => shotCount;

    public int SaucerShotCount => saucerShotCount;

    public ComboMeter Combo => combo;

    public bool Invulnerable => invulnerableTimer > 0f;

    public bool ShipVisible => State == ShipState.Flying;

    public Vector2 WarpTarget => warpTarget;

    public float WarpProgress => State == ShipState.Warping ? 1f - stateTimer / WarpSeconds : 0f;

    public float SaucerRadius => SaucerSmall ? SmallSaucerRadius : BigSaucerRadius;

    public Vector2 Heading => HeadingFor(ShipAngle);

    public ref readonly Rock RockAt(int index) => ref rocks[index];

    public ref readonly Shot ShotAt(int index) => ref shots[index];

    public ref readonly Shot SaucerShotAt(int index) => ref saucerShots[index];

    public RockBreak BreakAt(int index) => breaks[index];

    public static float RockRadius(RockSize size) => Radii[(int)size];

    public static int RockPoints(RockSize size) => RockPointTable[(int)size];

    public static bool SaucerAllowed(int wave) => wave >= SaucerFirstWave;

    public static float FirstSaucerDelay(int wave) => MathF.Max(6f, 15f - wave);

    public static float SmallSaucerChance(int wave) => Math.Clamp((wave - 4) * 0.2f, 0f, 0.8f);

    public static int RocksForWave(int wave) => Math.Min(2 + Math.Max(1, wave), MaxRocksPerWave);

    public static float SpeedFactor(int wave) => 1f + Math.Min(Math.Max(0, wave - 1), 8) * 0.06f;

    public static Vector2 HeadingFor(float angle) => new(MathF.Sin(angle), -MathF.Cos(angle));

    public static float Wrap(float value, float size) => value - MathF.Floor(value / size) * size;

    public static Vector2 WrapPosition(Vector2 position) => new(Wrap(position.X, Width), Wrap(position.Y, Height));

    public static Vector2 WrapDelta(Vector2 from, Vector2 to)
    {
        var delta = to - from;
        return new Vector2(Centered(delta.X, Width), Centered(delta.Y, Height));
    }

    public void Reset(GameRandom seededRandom, int startWave = 1)
    {
        random = seededRandom;
        combo.Reset();
        Score = 0;
        Lives = StartLives;
        GameOver = false;
        PlaySeconds = 0f;
        ShotsFired = 0;
        ShotsHit = 0;
        RocksBroken = 0;
        SaucersDowned = 0;
        WavesCleared = 0;
        nextExtraLife = ExtraLifeScore;
        rockCount = 0;
        shotCount = 0;
        saucerShotCount = 0;
        fireTimer = 0f;
        warpCooldownTimer = 0f;
        warpRequested = false;
        waveClearing = false;
        waveTimer = 0f;
        SaucerActive = false;
        ClearFrameEvents();
        SpawnShip();
        StartWave(Math.Max(1, startWave));
    }

    public void ClearRocks()
    {
        rockCount = 0;
    }

    public bool SpawnRock(Vector2 position, Vector2 velocity, RockSize size)
    {
        if (rockCount >= MaxRocks)
        {
            return false;
        }

        ref var rock = ref rocks[rockCount++];
        rock.Position = WrapPosition(position);
        rock.Velocity = velocity;
        rock.Angle = random.NextFloat() * MathF.Tau;
        rock.Spin = random.Range(-RockSpin, RockSpin);
        rock.Size = size;
        rock.Shape = (byte)random.Next(RockShapeCount);
        return true;
    }

    public void Step(float deltaSeconds, in DriftControls controls)
    {
        ClearFrameEvents();
        if (GameOver || deltaSeconds <= 0f)
        {
            return;
        }

        warpRequested |= controls.Warp;
        var substeps = new Substeps(deltaSeconds, StepSeconds);
        for (var step = 0; step < substeps.Count; step++)
        {
            StepOnce(substeps.Step, controls);
            if (GameOver)
            {
                return;
            }
        }

        warpRequested = false;
    }

    private void StepOnce(float step, in DriftControls controls)
    {
        PlaySeconds += step;
        var multiplierBefore = combo.Multiplier;
        combo.Update(step);
        fireTimer = MathF.Max(0f, fireTimer - step);
        warpCooldownTimer = MathF.Max(0f, warpCooldownTimer - step);
        invulnerableTimer = MathF.Max(0f, invulnerableTimer - step);
        AdvanceShip(step, controls);
        MoveRocks(step);
        MoveShots(shots, ref shotCount, step);
        AdvanceSaucer(step);
        MoveShots(saucerShots, ref saucerShotCount, step);
        CollideShots();
        CollideSaucerShots();
        CollideShip();
        CollideSaucer();
        AdvanceWave(step);
        if (combo.Multiplier > multiplierBefore)
        {
            ComboRaisedThisFrame = true;
        }
    }

    private void ClearFrameEvents()
    {
        BreakCount = 0;
        FiredCount = 0;
        ShipLostThisFrame = false;
        SpawnedThisFrame = false;
        WarpStartedThisFrame = false;
        WarpEndedThisFrame = false;
        SaucerArrivedThisFrame = false;
        SaucerFiredThisFrame = false;
        SaucerDownedThisFrame = false;
        SaucerDownPoints = 0;
        ExtraLifeThisFrame = false;
        WaveClearedThisFrame = false;
        WaveStartedThisFrame = false;
        ComboRaisedThisFrame = false;
    }

    private void SpawnShip()
    {
        State = ShipState.Flying;
        ShipPosition = new Vector2(Width * 0.5f, Height * 0.5f);
        ShipVelocity = Vector2.Zero;
        ShipAngle = 0f;
        Thrusting = false;
        stateTimer = 0f;
        spawnWait = 0f;
        invulnerableTimer = InvulnerableSeconds;
    }

    private void StartWave(int wave)
    {
        Wave = wave;
        waveClearing = false;
        waveTimer = 0f;
        WaveStartedThisFrame = true;
        saucerTimer = FirstSaucerDelay(wave);
        var count = RocksForWave(wave);
        var factor = SpeedFactor(wave);
        for (var index = 0; index < count; index++)
        {
            var position = EdgePoint();
            for (var attempt = 0; attempt < SpawnAttempts; attempt++)
            {
                if (WrapDelta(ShipPosition, position).Length() >= SpawnDistance)
                {
                    break;
                }

                position = EdgePoint();
            }

            var angle = random.NextFloat() * MathF.Tau;
            var speed = random.Range(MinSpeeds[(int)RockSize.Large], MaxSpeeds[(int)RockSize.Large]) * factor;
            SpawnRock(position, new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed, RockSize.Large);
        }
    }

    private Vector2 EdgePoint()
    {
        return random.Next(2) == 0
            ? new Vector2(random.Range(0f, Width), 0f)
            : new Vector2(0f, random.Range(0f, Height));
    }

    private void AdvanceShip(float step, in DriftControls controls)
    {
        switch (State)
        {
            case ShipState.Flying:
                Fly(step, controls);
                return;
            case ShipState.Warping:
                stateTimer -= step;
                if (stateTimer > 0f)
                {
                    return;
                }

                State = ShipState.Flying;
                ShipPosition = warpTarget;
                invulnerableTimer = MathF.Max(invulnerableTimer, WarpShieldSeconds);
                WarpEndedThisFrame = true;
                return;
            default:
                Recover(step);
                return;
        }
    }

    private void Fly(float step, in DriftControls controls)
    {
        ShipAngle = Wrap(ShipAngle + controls.Turn * RotateSpeed * step, MathF.Tau);
        Thrusting = controls.Thrust;
        var velocity = ShipVelocity;
        if (Thrusting)
        {
            velocity += HeadingFor(ShipAngle) * ThrustAcceleration * step;
        }

        velocity *= MathF.Max(0f, 1f - Drag * step);
        var speed = velocity.Length();
        if (speed > MaxSpeed)
        {
            velocity *= MaxSpeed / speed;
        }

        ShipVelocity = velocity;
        ShipPosition = WrapPosition(ShipPosition + velocity * step);
        if (controls.Fire && fireTimer <= 0f)
        {
            FireShot();
        }

        if (!warpRequested)
        {
            return;
        }

        warpRequested = false;
        if (warpCooldownTimer <= 0f)
        {
            BeginWarp();
        }
    }

    private void FireShot()
    {
        if (shotCount >= MaxShots)
        {
            return;
        }

        var heading = HeadingFor(ShipAngle);
        ref var shot = ref shots[shotCount++];
        shot.Position = WrapPosition(ShipPosition + heading * ShipSize * 1.15f);
        shot.Velocity = heading * ShotSpeed + ShipVelocity;
        shot.Life = ShotLife;
        fireTimer = FireCooldown;
        ShotsFired++;
        FiredCount++;
    }

    private void BeginWarp()
    {
        WarpFrom = ShipPosition;
        var best = WrapPosition(new Vector2(random.Range(0f, Width), random.Range(0f, Height)));
        var bestClearance = Clearance(best);
        for (var attempt = 1; attempt < SpawnAttempts && bestClearance < WarpClearance; attempt++)
        {
            var candidate = new Vector2(random.Range(0f, Width), random.Range(0f, Height));
            var clearance = Clearance(candidate);
            if (clearance <= bestClearance)
            {
                continue;
            }

            best = candidate;
            bestClearance = clearance;
        }

        warpTarget = best;
        State = ShipState.Warping;
        stateTimer = WarpSeconds;
        warpCooldownTimer = WarpCooldown;
        ShipVelocity = Vector2.Zero;
        Thrusting = false;
        WarpStartedThisFrame = true;
    }

    private float Clearance(Vector2 point)
    {
        var clearance = float.MaxValue;
        for (var index = 0; index < rockCount; index++)
        {
            ref readonly var rock = ref rocks[index];
            var gap = WrapDelta(point, rock.Position).Length() - RockRadius(rock.Size);
            clearance = MathF.Min(clearance, gap);
        }

        if (SaucerActive)
        {
            clearance = MathF.Min(clearance, WrapDelta(point, SaucerPosition).Length() - SaucerRadius);
        }

        return clearance;
    }

    private void Recover(float step)
    {
        if (stateTimer > 0f)
        {
            stateTimer -= step;
            return;
        }

        if (Lives <= 0)
        {
            GameOver = true;
            return;
        }

        spawnWait += step;
        var center = new Vector2(Width * 0.5f, Height * 0.5f);
        if (Clearance(center) < SpawnClearance && spawnWait < MaxSpawnWait)
        {
            return;
        }

        SpawnShip();
        SpawnedThisFrame = true;
    }

    private void MoveRocks(float step)
    {
        for (var index = 0; index < rockCount; index++)
        {
            ref var rock = ref rocks[index];
            rock.Position = WrapPosition(rock.Position + rock.Velocity * step);
            rock.Angle += rock.Spin * step;
        }
    }

    private static void MoveShots(Shot[] pool, ref int count, float step)
    {
        for (var index = count - 1; index >= 0; index--)
        {
            ref var shot = ref pool[index];
            shot.Life -= step;
            if (shot.Life <= 0f)
            {
                pool[index] = pool[--count];
                continue;
            }

            shot.Position = WrapPosition(shot.Position + shot.Velocity * step);
        }
    }

    private void AdvanceSaucer(float step)
    {
        if (!SaucerActive)
        {
            if (!SaucerAllowed(Wave) || waveClearing)
            {
                return;
            }

            saucerTimer -= step;
            if (saucerTimer > 0f)
            {
                return;
            }

            LaunchSaucer();
            return;
        }

        var speed = SaucerSmall ? SmallSaucerSpeed : BigSaucerSpeed;
        var position = SaucerPosition;
        position.X += saucerDirection * speed * step;
        position.Y = Wrap(position.Y + saucerRise * speed * step, Height);
        SaucerPosition = position;
        saucerTravel += speed * step;
        saucerTurnTimer -= step;
        if (saucerTurnTimer <= 0f)
        {
            saucerTurnTimer = SaucerTurnSeconds;
            saucerRise = (random.Next(3) - 1) * SaucerClimb;
        }

        saucerFireTimer -= step;
        if (saucerFireTimer <= 0f)
        {
            saucerFireTimer = SaucerSmall ? SmallSaucerFireSeconds : BigSaucerFireSeconds;
            FireSaucerShot();
        }

        if (saucerTravel < Width + SaucerRadius * 2f)
        {
            return;
        }

        SaucerActive = false;
        saucerTimer = random.Range(MinSaucerGap, MaxSaucerGap);
    }

    private void LaunchSaucer()
    {
        SaucerActive = true;
        SaucerSmall = random.Chance(SmallSaucerChance(Wave));
        saucerDirection = random.Sign();
        saucerRise = 0f;
        saucerTravel = 0f;
        saucerTurnTimer = SaucerTurnSeconds;
        saucerFireTimer = (SaucerSmall ? SmallSaucerFireSeconds : BigSaucerFireSeconds) * 0.6f;
        var radius = SaucerRadius;
        var x = saucerDirection > 0f ? -radius : Width + radius;
        SaucerPosition = new Vector2(x, random.Range(SaucerMargin, Height - SaucerMargin));
        SaucerArrivedThisFrame = true;
    }

    private void FireSaucerShot()
    {
        if (saucerShotCount >= MaxSaucerShots || State != ShipState.Flying)
        {
            return;
        }

        float angle;
        if (SaucerSmall)
        {
            var toShip = WrapDelta(SaucerPosition, ShipPosition);
            var error = MathF.Max(0.06f, 0.45f - Wave * 0.03f);
            angle = MathF.Atan2(toShip.Y, toShip.X) + random.Range(-error, error);
        }
        else
        {
            angle = random.NextFloat() * MathF.Tau;
        }

        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        ref var shot = ref saucerShots[saucerShotCount++];
        shot.Position = WrapPosition(SaucerPosition + direction * SaucerRadius);
        shot.Velocity = direction * SaucerShotSpeed;
        shot.Life = SaucerShotLife;
        SaucerFiredThisFrame = true;
    }

    private void CollideShots()
    {
        for (var shotIndex = shotCount - 1; shotIndex >= 0; shotIndex--)
        {
            var position = shots[shotIndex].Position;
            if (SaucerActive && Touching(position, SaucerPosition, SaucerRadius))
            {
                RemoveShot(shotIndex);
                ShotsHit++;
                DownSaucer(true);
                continue;
            }

            var hit = RockTouching(position, 0f);
            if (hit < 0)
            {
                continue;
            }

            RemoveShot(shotIndex);
            ShotsHit++;
            BreakRock(hit, true);
        }
    }

    private void CollideSaucerShots()
    {
        for (var shotIndex = saucerShotCount - 1; shotIndex >= 0; shotIndex--)
        {
            var position = saucerShots[shotIndex].Position;
            if (State == ShipState.Flying && invulnerableTimer <= 0f && Touching(position, ShipPosition, ShipRadius))
            {
                saucerShots[shotIndex] = saucerShots[--saucerShotCount];
                LoseShip();
                continue;
            }

            var hit = RockTouching(position, 0f);
            if (hit < 0)
            {
                continue;
            }

            saucerShots[shotIndex] = saucerShots[--saucerShotCount];
            BreakRock(hit, false);
        }
    }

    private void CollideShip()
    {
        if (State != ShipState.Flying || invulnerableTimer > 0f)
        {
            return;
        }

        var hit = RockTouching(ShipPosition, ShipRadius);
        if (hit >= 0)
        {
            BreakRock(hit, true);
            LoseShip();
            return;
        }

        if (!SaucerActive || !Touching(ShipPosition, SaucerPosition, SaucerRadius + ShipRadius))
        {
            return;
        }

        DownSaucer(true);
        LoseShip();
    }

    private void CollideSaucer()
    {
        if (!SaucerActive)
        {
            return;
        }

        var hit = RockTouching(SaucerPosition, SaucerRadius);
        if (hit < 0)
        {
            return;
        }

        BreakRock(hit, false);
        DownSaucer(false);
    }

    private int RockTouching(Vector2 point, float reach)
    {
        for (var index = rockCount - 1; index >= 0; index--)
        {
            ref readonly var rock = ref rocks[index];
            if (Touching(point, rock.Position, RockRadius(rock.Size) * RockHitScale + reach))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool Touching(Vector2 first, Vector2 second, float reach) =>
        WrapDelta(first, second).LengthSquared() <= reach * reach;

    private void RemoveShot(int index)
    {
        shots[index] = shots[--shotCount];
    }

    private void BreakRock(int index, bool byPlayer)
    {
        var rock = rocks[index];
        rocks[index] = rocks[--rockCount];
        var points = 0;
        if (byPlayer)
        {
            RocksBroken++;
            points = RockPoints(rock.Size) * combo.Hit();
            AddPoints(points);
        }

        if (BreakCount < MaxBreaks)
        {
            breaks[BreakCount++] = new RockBreak(rock.Position, rock.Velocity, rock.Size, points);
        }

        if (rock.Size == RockSize.Small)
        {
            return;
        }

        var childSize = rock.Size - 1;
        var factor = SpeedFactor(Wave);
        var parentSpeed = rock.Velocity.Length();
        var baseAngle = parentSpeed > 0.01f
            ? MathF.Atan2(rock.Velocity.Y, rock.Velocity.X)
            : random.NextFloat() * MathF.Tau;
        for (var side = -1; side <= 1; side += 2)
        {
            var angle = baseAngle + side * random.Range(0.35f, 0.9f);
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var speed = random.Range(MinSpeeds[(int)childSize], MaxSpeeds[(int)childSize]) * factor;
            var offset = new Vector2(-direction.Y, direction.X) * RockRadius(childSize) * 0.5f * side;
            SpawnRock(rock.Position + offset, direction * speed, childSize);
        }
    }

    private void DownSaucer(bool byPlayer)
    {
        var points = 0;
        if (byPlayer)
        {
            SaucersDowned++;
            points = (SaucerSmall ? SmallSaucerPoints : BigSaucerPoints) * combo.Hit();
            AddPoints(points);
        }

        SaucerActive = false;
        SaucerDownedThisFrame = true;
        SaucerDownPosition = SaucerPosition;
        SaucerDownPoints = points;
        saucerTimer = random.Range(MinSaucerGap, MaxSaucerGap);
    }

    private void LoseShip()
    {
        Lives--;
        State = ShipState.Wrecked;
        stateTimer = WreckSeconds;
        spawnWait = 0f;
        Thrusting = false;
        ShipVelocity = Vector2.Zero;
        combo.Reset();
        ShipLostThisFrame = true;
    }

    private void AddPoints(int points)
    {
        Score += points;
        while (Score >= nextExtraLife)
        {
            nextExtraLife += ExtraLifeScore;
            if (Lives >= MaxLives)
            {
                continue;
            }

            Lives++;
            ExtraLifeThisFrame = true;
        }
    }

    private void AdvanceWave(float step)
    {
        if (!waveClearing)
        {
            if (rockCount > 0)
            {
                return;
            }

            waveClearing = true;
            waveTimer = WaveDelay;
            WavesCleared++;
            WaveClearedThisFrame = true;
            return;
        }

        waveTimer -= step;
        if (waveTimer > 0f)
        {
            return;
        }

        StartWave(Wave + 1);
    }

    private static float Centered(float value, float size)
    {
        var wrapped = Wrap(value + size * 0.5f, size);
        return wrapped - size * 0.5f;
    }
}
