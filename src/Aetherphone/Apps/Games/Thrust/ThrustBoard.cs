using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Thrust;

internal enum ThrustState : byte
{
    Running,
    Dying,
    Over,
}

internal enum ThrustDeath : byte
{
    None,
    Zapped,
    Blasted,
}

internal enum MissilePhase : byte
{
    Warning,
    Locked,
    Flying,
}

internal struct ThrustMissile
{
    public float X;
    public float Y;
    public float Timer;
    public MissilePhase Phase;
    public bool Passed;
}

internal sealed class ThrustBoard
{
    public const float Height = ThrustGenerator.Height;
    public const float PlayerRadius = ThrustGenerator.PlayerRadius;
    public const float MountedRadius = 0.58f;
    public const float StartX = 0f;
    public const float BaseSpeed = 4.6f;
    public const float MaxSpeed = 8.5f;
    public const float SpeedRamp = 1400f;
    public const float Gravity = 30f;
    public const float ThrustAccel = 66f;
    public const float MaxRise = 11f;
    public const float MaxFall = 12f;
    public const float MountedGravity = 34f;
    public const float JumpVelocity = 13f;
    public const float AirJumpVelocity = 11.5f;
    public const float WarningSeconds = 1.2f;
    public const float LockSeconds = 0.5f;
    public const float MissileSpeed = 14f;
    public const float MissileRadius = 0.32f;
    public const float LaunchAhead = 8.5f;
    public const float MissileStart = 200f;
    public const float InvulnerableSeconds = 1.5f;
    public const float FirstVehicle = 260f;
    public const float NearMissMargin = 0.55f;
    public const int CoinPoints = 10;
    public const int NearMissPoints = 15;
    public const int MilestoneMetres = 250;
    public const float ChainWindowSeconds = 1f;
    public const float RunwayLength = 8f;
    private const float GenerateAhead = 30f;
    private const float RecycleBehind = 8f;
    private const float MaxStepSeconds = 1f / 120f;
    private const float TrackRate = 5f;
    private const float DyingSeconds = 1.4f;
    private const float DyingFriction = 1.6f;
    private const float DismountHop = -7f;
    private const float PickupRadius = 0.55f;
    private const float CoinReach = PlayerRadius + ThrustGenerator.CoinRadius;
    private const int ZapperCapacity = 48;
    private const int CoinCapacity = 256;
    private const int MissileCapacity = 3;
    private const int EventCapacity = 16;
    private const ulong MissileSalt = 0x5C0FF1E5UL;
    private const float OpenAirGap = 4f;
    private const float OpenAirBefore = 3f;
    private const float OpenAirAfter = 3f;
    private const float OpenAirSeconds = 0.6f;
    private const float OpenAirStep = 0.5f;
    private const float OpenAirRetrySeconds = 0.25f;

    private readonly ThrustZapper[] zappers = new ThrustZapper[ZapperCapacity];
    private readonly Vector2[] coins = new Vector2[CoinCapacity];
    private readonly ThrustMissile[] missiles = new ThrustMissile[MissileCapacity];
    private readonly ThrustZapper[] chunkZappers = new ThrustZapper[ThrustGenerator.MaxZappers];
    private readonly Vector2[] chunkCoins = new Vector2[ThrustGenerator.MaxCoins];
    private readonly Vector2[] coinEvents = new Vector2[EventCapacity];
    private GameRandom chunkRandom;
    private GameRandom missileRandom;
    private ComboMeter chain = new(ChainWindowSeconds);
    private int zapperCount;
    private int coinCount;
    private int missileCount;
    private int coinEventCount;
    private int chunkOrdinal;
    private float nextChunkX;
    private float pathY;
    private float missileClock;
    private float nextVehicle;
    private float invulnerable;
    private float dyingSeconds;
    private float dyingSpeed;
    private int airJumps;
    private int coinScore;
    private int bonusScore;
    private int milestonesReached;
    private bool tapQueued;

    public ThrustState State { get; private set; }

    public ThrustDeath Death { get; private set; }

    public float X { get; private set; }

    public float Y { get; private set; }

    public float VelocityY { get; private set; }

    public float Time { get; private set; }

    public bool Holding { get; private set; }

    public bool Grounded { get; private set; }

    public bool Mounted { get; private set; }

    public bool VehicleActive { get; private set; }

    public Vector2 VehiclePosition { get; private set; }

    public int Coins { get; private set; }

    public int NearMisses { get; private set; }

    public int BestChain { get; private set; }

    public int Hits { get; private set; }

    public bool ThrustStartedThisStep { get; private set; }

    public bool BonkedThisStep { get; private set; }

    public bool LandedThisStep { get; private set; }

    public float LandingImpact { get; private set; }

    public bool JumpedThisStep { get; private set; }

    public bool ChainTierUpThisStep { get; private set; }

    public bool NearMissThisStep { get; private set; }

    public Vector2 NearMissPosition { get; private set; }

    public bool MountedThisStep { get; private set; }

    public bool DismountedThisStep { get; private set; }

    public bool MissileWarnedThisStep { get; private set; }

    public bool MissileLockedThisStep { get; private set; }

    public bool MissileLaunchedThisStep { get; private set; }

    public bool DiedThisStep { get; private set; }

    public int MilestoneThisStep { get; private set; }

    public int CoinsThisStep => coinEventCount;

    public float Distance => X - StartX;

    public int Metres => (int)Distance;

    public int Score => Metres + coinScore + bonusScore;

    public float Speed => State == ThrustState.Running ? RunSpeed(Distance) : dyingSpeed;

    public float Radius => Mounted ? MountedRadius : PlayerRadius;

    public bool Invulnerable => invulnerable > 0f;

    public float DyingProgress => Math.Clamp(dyingSeconds / DyingSeconds, 0f, 1f);

    public ComboMeter Chain => chain;

    public int ZapperCount => zapperCount;

    public int CoinCount => coinCount;

    public int MissileCount => missileCount;

    public ReadOnlySpan<ThrustZapper> Zappers => zappers.AsSpan(0, zapperCount);

    public ref readonly ThrustZapper ZapperAt(int index) => ref zappers[index];

    public Vector2 CoinAt(int index) => coins[index];

    public ref readonly ThrustMissile MissileAt(int index) => ref missiles[index];

    public Vector2 CoinEventAt(int index) => coinEvents[index];

    public static float RunSpeed(float distance) =>
        BaseSpeed + (MaxSpeed - BaseSpeed) * (1f - MathF.Exp(-MathF.Max(0f, distance) / SpeedRamp));

    public void Reset(GameRandom seededRandom)
    {
        chunkRandom = seededRandom;
        var fork = seededRandom;
        missileRandom = GameRandom.FromSeed((((ulong)fork.NextUInt() << 32) | fork.NextUInt()) ^ MissileSalt);
        chain = new ComboMeter(ChainWindowSeconds);
        State = ThrustState.Running;
        Death = ThrustDeath.None;
        X = StartX;
        Y = Height - PlayerRadius;
        VelocityY = 0f;
        Time = 0f;
        Holding = false;
        Grounded = true;
        Mounted = false;
        VehicleActive = false;
        VehiclePosition = Vector2.Zero;
        Coins = 0;
        NearMisses = 0;
        BestChain = 0;
        Hits = 0;
        zapperCount = 0;
        coinCount = 0;
        missileCount = 0;
        chunkOrdinal = 0;
        nextChunkX = StartX + RunwayLength;
        pathY = Height * 0.5f;
        missileClock = missileRandom.Range(3f, 6f);
        nextVehicle = FirstVehicle;
        invulnerable = 0f;
        dyingSeconds = 0f;
        dyingSpeed = 0f;
        airJumps = 0;
        coinScore = 0;
        bonusScore = 0;
        milestonesReached = 0;
        tapQueued = false;
        ClearEvents();
        Generate();
    }

    public void Tap()
    {
        if (State == ThrustState.Running)
        {
            tapQueued = true;
        }
    }

    public void Step(float deltaSeconds, bool holding)
    {
        ClearEvents();
        if (deltaSeconds <= 0f || State == ThrustState.Over)
        {
            return;
        }

        if (State == ThrustState.Running && holding && !Holding && !Mounted)
        {
            ThrustStartedThisStep = true;
        }

        Holding = holding && State == ThrustState.Running;
        if (tapQueued)
        {
            tapQueued = false;
            TryJump();
        }

        var steps = new Substeps(deltaSeconds, MaxStepSeconds);
        for (var step = 0; step < steps.Count && State != ThrustState.Over; step++)
        {
            if (State == ThrustState.Dying)
            {
                StepDying(steps.Step);
                continue;
            }

            StepRunning(steps.Step);
        }
    }

    public void QueueMissile()
    {
        if (missileCount >= MissileCapacity)
        {
            return;
        }

        missiles[missileCount++] = new ThrustMissile
        {
            X = X + LaunchAhead,
            Y = Y,
            Timer = 0f,
            Phase = MissilePhase.Warning,
            Passed = false,
        };
        MissileWarnedThisStep = true;
    }

    public void Mount()
    {
        Mounted = true;
        Y = Math.Clamp(Y, MountedRadius, Height - MountedRadius);
        airJumps = 0;
        VehicleActive = false;
        MountedThisStep = true;
    }

    public bool TakeHit(ThrustDeath cause)
    {
        if (State != ThrustState.Running || Invulnerable)
        {
            return false;
        }

        Hits++;
        if (Mounted)
        {
            Mounted = false;
            invulnerable = InvulnerableSeconds;
            VelocityY = DismountHop;
            Grounded = false;
            DismountedThisStep = true;
            return true;
        }

        Death = cause;
        State = ThrustState.Dying;
        DiedThisStep = true;
        dyingSeconds = 0f;
        dyingSpeed = RunSpeed(Distance);
        Holding = false;
        return true;
    }

    private void ClearEvents()
    {
        ThrustStartedThisStep = false;
        BonkedThisStep = false;
        LandedThisStep = false;
        LandingImpact = 0f;
        JumpedThisStep = false;
        ChainTierUpThisStep = false;
        NearMissThisStep = false;
        MountedThisStep = false;
        DismountedThisStep = false;
        MissileWarnedThisStep = false;
        MissileLockedThisStep = false;
        MissileLaunchedThisStep = false;
        DiedThisStep = false;
        MilestoneThisStep = 0;
        coinEventCount = 0;
    }

    private void TryJump()
    {
        if (!Mounted)
        {
            return;
        }

        if (Grounded)
        {
            VelocityY = -JumpVelocity;
            Grounded = false;
            JumpedThisStep = true;
            return;
        }

        if (airJumps > 0)
        {
            return;
        }

        airJumps++;
        VelocityY = -AirJumpVelocity;
        JumpedThisStep = true;
    }

    private void StepRunning(float deltaSeconds)
    {
        Time += deltaSeconds;
        invulnerable = MathF.Max(0f, invulnerable - deltaSeconds);
        chain.Update(deltaSeconds);
        X += RunSpeed(Distance) * deltaSeconds;
        Fly(deltaSeconds);
        CheckMilestone();
        Generate();
        CollectCoins();
        CollectVehicle();
        UpdateMissiles(deltaSeconds);
        if (State != ThrustState.Running)
        {
            return;
        }

        CheckZappers();
        Recycle();
    }

    private void Fly(float deltaSeconds)
    {
        var radius = Radius;
        if (Mounted)
        {
            VelocityY = MathF.Min(MaxFall, VelocityY + MountedGravity * deltaSeconds);
        }
        else
        {
            var acceleration = Holding ? Gravity - ThrustAccel : Gravity;
            VelocityY = Math.Clamp(VelocityY + acceleration * deltaSeconds, -MaxRise, MaxFall);
        }

        Y += VelocityY * deltaSeconds;
        if (Y - radius < 0f)
        {
            Y = radius;
            if (VelocityY < -2f)
            {
                BonkedThisStep = true;
            }

            VelocityY = MathF.Max(0f, VelocityY);
        }

        if (Y + radius < Height)
        {
            Grounded = false;
            return;
        }

        if (!Grounded)
        {
            LandedThisStep = true;
            LandingImpact = MathF.Max(LandingImpact, VelocityY);
        }

        Y = Height - radius;
        VelocityY = MathF.Min(0f, VelocityY);
        Grounded = true;
        airJumps = 0;
    }

    private void CheckMilestone()
    {
        var reached = Metres / MilestoneMetres;
        if (reached <= milestonesReached)
        {
            return;
        }

        milestonesReached = reached;
        MilestoneThisStep = reached * MilestoneMetres;
    }

    private void Generate()
    {
        while (nextChunkX < X + GenerateAhead)
        {
            var count = ThrustGenerator.Generate(ref chunkRandom, chunkOrdinal, nextChunkX, ref pathY, chunkZappers,
                chunkCoins, out var placedCoins, out _);
            for (var index = 0; index < count && zapperCount < ZapperCapacity; index++)
            {
                zappers[zapperCount++] = chunkZappers[index];
            }

            for (var index = 0; index < placedCoins && coinCount < CoinCapacity; index++)
            {
                coins[coinCount++] = chunkCoins[index];
            }

            if (Distance >= nextVehicle && !Mounted && !VehicleActive && count == 0)
            {
                VehicleActive = true;
                VehiclePosition = new Vector2(nextChunkX + ThrustGenerator.ChunkWidth * 0.5f, pathY);
                nextVehicle = Distance + chunkRandom.Range(650f, 950f);
            }

            nextChunkX += ThrustGenerator.ChunkWidth;
            chunkOrdinal++;
        }
    }

    private void CollectCoins()
    {
        var reach = CoinReach + (Mounted ? MountedRadius - PlayerRadius : 0f);
        for (var index = 0; index < coinCount;)
        {
            var coin = coins[index];
            if (coin.X > X + reach)
            {
                return;
            }

            if (Vector2.DistanceSquared(coin, new Vector2(X, Y)) > reach * reach)
            {
                index++;
                continue;
            }

            var tierBefore = chain.Multiplier;
            var multiplier = chain.Hit();
            Coins++;
            coinScore += CoinPoints * multiplier;
            BestChain = Math.Max(BestChain, chain.Count);
            ChainTierUpThisStep |= multiplier > tierBefore;
            if (coinEventCount < EventCapacity)
            {
                coinEvents[coinEventCount++] = coin;
            }

            Array.Copy(coins, index + 1, coins, index, coinCount - index - 1);
            coinCount--;
        }
    }

    private void CollectVehicle()
    {
        if (!VehicleActive)
        {
            return;
        }

        if (VehiclePosition.X < X - RecycleBehind)
        {
            VehicleActive = false;
            return;
        }

        var reach = PickupRadius + Radius;
        if (Vector2.DistanceSquared(VehiclePosition, new Vector2(X, Y)) <= reach * reach && !Mounted)
        {
            Mount();
        }
    }

    private void UpdateMissiles(float deltaSeconds)
    {
        if (Distance >= MissileStart)
        {
            missileClock -= deltaSeconds;
            if (missileClock <= 0f && !OpenAirAhead())
            {
                missileClock = OpenAirRetrySeconds;
            }
            else if (missileClock <= 0f)
            {
                QueueMissile();
                var tier = ThrustGenerator.Tier(X);
                missileClock = missileRandom.Range(5.5f - tier * 1.2f, 9f - tier * 2f);
            }
        }

        var track = 1f - MathF.Exp(-TrackRate * deltaSeconds);
        for (var index = 0; index < missileCount;)
        {
            ref var missile = ref missiles[index];
            if (!AdvanceMissile(ref missile, deltaSeconds, track))
            {
                missiles[index] = missiles[missileCount - 1];
                missileCount--;
                continue;
            }

            if (State != ThrustState.Running)
            {
                return;
            }

            index++;
        }
    }

    public bool OpenAirAhead()
    {
        var speed = RunSpeed(Distance);
        var crossing = X + speed * (WarningSeconds + LockSeconds);
        var span = Zappers;
        for (var x = crossing - OpenAirBefore; x <= crossing + speed * OpenAirSeconds + OpenAirAfter; x += OpenAirStep)
        {
            if (ThrustGenerator.LargestGap(span, x, Time, true) < OpenAirGap)
            {
                return false;
            }
        }

        return true;
    }

    private bool AdvanceMissile(ref ThrustMissile missile, float deltaSeconds, float track)
    {
        switch (missile.Phase)
        {
            case MissilePhase.Warning:
                missile.Timer += deltaSeconds;
                missile.Y += (Y - missile.Y) * track;
                missile.X = X + LaunchAhead;
                if (missile.Timer >= WarningSeconds)
                {
                    missile.Phase = MissilePhase.Locked;
                    missile.Timer = 0f;
                    MissileLockedThisStep = true;
                }

                return true;
            case MissilePhase.Locked:
                missile.Timer += deltaSeconds;
                missile.X = X + LaunchAhead;
                if (missile.Timer >= LockSeconds)
                {
                    missile.Phase = MissilePhase.Flying;
                    missile.Timer = 0f;
                    MissileLaunchedThisStep = true;
                }

                return true;
            default:
                missile.Timer += deltaSeconds;
                missile.X -= MissileSpeed * deltaSeconds;
                var reach = MissileRadius + Radius;
                if (Vector2.DistanceSquared(new Vector2(missile.X, missile.Y), new Vector2(X, Y)) <= reach * reach)
                {
                    return !TakeHit(ThrustDeath.Blasted);
                }

                if (!missile.Passed && missile.X < X - reach)
                {
                    missile.Passed = true;
                    if (MathF.Abs(missile.Y - Y) < reach + NearMissMargin * 2f)
                    {
                        AwardNearMiss(new Vector2(X, missile.Y));
                    }
                }

                return missile.X > X - RecycleBehind;
        }
    }

    private void CheckZappers()
    {
        var radius = Radius;
        var hitDistance = ThrustGenerator.ZapperRadius + radius;
        var player = new Vector2(X, Y);
        for (var index = 0; index < zapperCount; index++)
        {
            ref var zapper = ref zappers[index];
            if (zapper.Left > X + hitDistance + 0.1f)
            {
                continue;
            }

            if (!zapper.Passed && zapper.Right < X - hitDistance)
            {
                zapper.Passed = true;
                if (zapper.Closest < hitDistance + NearMissMargin)
                {
                    AwardNearMiss(player);
                }

                continue;
            }

            if (zapper.Passed)
            {
                continue;
            }

            var distance = ThrustGenerator.SegmentDistance(player, zapper, Time);
            zapper.Closest = MathF.Min(zapper.Closest, distance);
            if (distance > hitDistance)
            {
                continue;
            }

            if (TakeHit(ThrustDeath.Zapped) && State != ThrustState.Running)
            {
                return;
            }
        }
    }

    private void AwardNearMiss(Vector2 position)
    {
        if (Invulnerable)
        {
            return;
        }

        NearMisses++;
        bonusScore += NearMissPoints;
        NearMissThisStep = true;
        NearMissPosition = position;
    }

    private void StepDying(float deltaSeconds)
    {
        Time += deltaSeconds;
        dyingSeconds += deltaSeconds;
        dyingSpeed = MathF.Max(0f, dyingSpeed - DyingFriction * dyingSpeed * deltaSeconds);
        X += dyingSpeed * deltaSeconds;
        VelocityY = MathF.Min(MaxFall, VelocityY + Gravity * deltaSeconds);
        Y += VelocityY * deltaSeconds;
        if (Y + PlayerRadius >= Height)
        {
            Y = Height - PlayerRadius;
            VelocityY = -VelocityY * 0.3f;
        }

        if (dyingSeconds >= DyingSeconds)
        {
            State = ThrustState.Over;
        }
    }

    private void Recycle()
    {
        var limit = X - RecycleBehind;
        var gone = 0;
        while (gone < zapperCount && zappers[gone].Right < limit)
        {
            gone++;
        }

        if (gone > 0)
        {
            Array.Copy(zappers, gone, zappers, 0, zapperCount - gone);
            zapperCount -= gone;
        }

        var coinsGone = 0;
        while (coinsGone < coinCount && coins[coinsGone].X < limit)
        {
            coinsGone++;
        }

        if (coinsGone <= 0)
        {
            return;
        }

        Array.Copy(coins, coinsGone, coins, 0, coinCount - coinsGone);
        coinCount -= coinsGone;
    }
}
