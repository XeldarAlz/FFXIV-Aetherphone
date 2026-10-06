using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Skyfall;

internal struct Meteor
{
    public Vector2 Start;
    public Vector2 Position;
    public Vector2 Direction;
    public float Speed;
    public bool CanSplit;
    public float SplitY;
}

internal struct Interceptor
{
    public Vector2 Position;
    public Vector2 Target;
    public Vector2 Direction;
}

internal struct Blast
{
    public Vector2 Center;
    public float Radius;
    public bool Growing;
    public float Hold;
    public bool FromShot;
    public bool Scored;
}

internal sealed class SkyfallBoard
{
    public const float Width = 100f;
    public const float Height = 140f;
    public const float GroundY = 130f;
    public const float BatteryX = Width * 0.5f;
    public const float BarrelY = GroundY - 3f;
    public const int CityCount = 6;
    public const float CityHalfWidth = 5f;
    public const int AmmoPerWave = 28;
    public const int MeteorPoints = 25;
    public const int CityBonus = 100;
    public const int AmmoBonus = 5;
    public const float BlastMaxRadius = 8.5f;
    public const float WaveBreakSeconds = 2.2f;
    public const int MaxMeteors = 64;
    public const int MaxInterceptors = 32;
    public const int MaxBlasts = 96;
    public const int ShieldWaveInterval = 2;
    public const float ShieldFallSpeed = 6f;
    public const float ShieldSpawnDelay = 2f;
    public const float ShieldRadius = 3f;
    public const float MeteorSpawnY = -4f;
    public static readonly float[] CityX = { 9f, 22f, 35f, 65f, 78f, 91f };
    private const float ShotSpeed = 110f;
    private const float BlastGrowth = 30f;
    private const float BlastHold = 0.22f;
    private const float BlastShrink = BlastGrowth * 0.75f;
    private const float BlastStartRadius = 0.5f;
    private const int MaxMeteorsPerWave = 26;
    private const float MaxMeteorSpeed = 22f;
    private const int SplitFromWave = 3;
    private const float SplitChance = 0.25f;
    private const float BatteryTargetChance = 0.15f;
    private const float FirstSpawnDelay = 0.6f;
    private const float ShieldSpawnMinX = 15f;
    private const float ShieldSpawnMaxX = 85f;
    private const float MinimumShotDistanceSquared = 1f;
    private readonly bool[] cities = new bool[CityCount];
    private readonly Meteor[] meteors = new Meteor[MaxMeteors];
    private readonly Interceptor[] interceptors = new Interceptor[MaxInterceptors];
    private readonly Blast[] blasts = new Blast[MaxBlasts];
    private readonly Vector2[] destroyedPositions = new Vector2[MaxMeteors];
    private readonly Vector2[] blastSpawnPositions = new Vector2[MaxBlasts];
    private GameRandom random;
    private int pendingSpawns;
    private float spawnTimer;
    private float waveBreak;
    private float shieldSpawnTimer = -1f;
    public int Score { get; private set; }
    public int Wave { get; private set; }
    public int Ammo { get; private set; }
    public int CitiesLeft { get; private set; }
    public bool GameOver { get; private set; }
    public int LastWaveBonus { get; private set; }
    public int MeteorCount { get; private set; }
    public int InterceptorCount { get; private set; }
    public int BlastCount { get; private set; }
    public int DestroyedCount { get; private set; }
    public int BlastSpawnCount { get; private set; }
    public int ShotsFired { get; private set; }
    public int ShotsHit { get; private set; }
    public int MeteorsDestroyed { get; private set; }
    public int ShieldCharges { get; private set; }
    public bool ShieldFalling { get; private set; }
    public Vector2 ShieldPosition { get; private set; }
    public int CityLostThisFrame { get; private set; } = -1;
    public int ShieldAbsorbedCityThisFrame { get; private set; } = -1;
    public bool ShieldCollectedThisFrame { get; private set; }
    public bool WaveStartedThisFrame { get; private set; }
    public bool WaveClearedThisFrame { get; private set; }
    public bool LastMeteorDestroyedThisFrame { get; private set; }
    public bool ShotFiredThisFrame { get; private set; }
    public bool DryFireThisFrame { get; private set; }
    public bool InWaveBreak => waveBreak > 0f;
    public bool CityAlive(int index) => cities[index];
    public Meteor GetMeteor(int index) => meteors[index];
    public Interceptor GetInterceptor(int index) => interceptors[index];
    public Blast GetBlast(int index) => blasts[index];
    public Vector2 DestroyedPosition(int index) => destroyedPositions[index];
    public Vector2 BlastSpawnPosition(int index) => blastSpawnPositions[index];

    public static Vector2 CityCenter(int index) => new(CityX[index], GroundY);

    public void StartGame(GameRandom seededRandom)
    {
        random = seededRandom;
        Score = 0;
        Wave = 0;
        GameOver = false;
        CitiesLeft = CityCount;
        for (var cityIndex = 0; cityIndex < CityCount; cityIndex++)
        {
            cities[cityIndex] = true;
        }

        MeteorCount = 0;
        InterceptorCount = 0;
        BlastCount = 0;
        waveBreak = 0f;
        LastWaveBonus = 0;
        ShotsFired = 0;
        ShotsHit = 0;
        MeteorsDestroyed = 0;
        ShieldCharges = 0;
        ShieldFalling = false;
        ClearFrameEvents();
        StartWave();
    }

    public bool Fire(Vector2 target)
    {
        if (GameOver || InWaveBreak)
        {
            return false;
        }

        if (Ammo <= 0)
        {
            DryFireThisFrame = true;
            return false;
        }

        if (target.Y > BarrelY || InterceptorCount >= MaxInterceptors)
        {
            return false;
        }

        var origin = new Vector2(BatteryX, BarrelY);
        var offset = target - origin;
        if (offset.LengthSquared() < MinimumShotDistanceSquared)
        {
            return false;
        }

        Ammo--;
        ShotsFired++;
        interceptors[InterceptorCount++] = new Interceptor
        {
            Position = origin,
            Target = target,
            Direction = Vector2.Normalize(offset),
        };
        ShotFiredThisFrame = true;
        return true;
    }

    public void Update(float deltaSeconds)
    {
        ClearFrameEvents();
        if (GameOver || deltaSeconds <= 0f)
        {
            return;
        }

        if (waveBreak > 0f)
        {
            waveBreak -= deltaSeconds;
            if (waveBreak <= 0f)
            {
                waveBreak = 0f;
                StartWave();
            }

            return;
        }

        Spawn(deltaSeconds);
        TickShield(deltaSeconds);
        MoveInterceptors(deltaSeconds);
        MoveBlasts(deltaSeconds);
        MoveMeteors(deltaSeconds);
        if (!GameOver && pendingSpawns == 0 && MeteorCount == 0 && InterceptorCount == 0 && BlastCount == 0)
        {
            CompleteWave();
        }
    }

    public static int MeteorsForWave(int wave) => Math.Min(MaxMeteorsPerWave, 7 + wave * 2);

    public static int WaveBonus(int citiesLeft, int ammo) => citiesLeft * CityBonus + ammo * AmmoBonus;

    public static bool WaveCarriesShield(int wave) => wave % ShieldWaveInterval == 0;

    private void ClearFrameEvents()
    {
        DestroyedCount = 0;
        BlastSpawnCount = 0;
        CityLostThisFrame = -1;
        ShieldAbsorbedCityThisFrame = -1;
        ShieldCollectedThisFrame = false;
        WaveStartedThisFrame = false;
        WaveClearedThisFrame = false;
        LastMeteorDestroyedThisFrame = false;
        ShotFiredThisFrame = false;
        DryFireThisFrame = false;
    }

    private void StartWave()
    {
        Wave++;
        Ammo = AmmoPerWave;
        pendingSpawns = MeteorsForWave(Wave);
        spawnTimer = FirstSpawnDelay;
        shieldSpawnTimer = WaveCarriesShield(Wave) && ShieldCharges == 0 ? ShieldSpawnDelay : -1f;
        WaveStartedThisFrame = true;
    }

    private void CompleteWave()
    {
        LastWaveBonus = WaveBonus(CitiesLeft, Ammo);
        Score += LastWaveBonus;
        ShieldFalling = false;
        shieldSpawnTimer = -1f;
        WaveClearedThisFrame = true;
        waveBreak = WaveBreakSeconds;
    }

    private void Spawn(float deltaSeconds)
    {
        if (pendingSpawns <= 0)
        {
            return;
        }

        spawnTimer -= deltaSeconds;
        if (spawnTimer > 0f || MeteorCount >= MaxMeteors)
        {
            return;
        }

        spawnTimer = MathF.Max(0.35f, 1.5f - Wave * 0.08f) * (0.6f + Chance() * 0.8f);
        pendingSpawns--;
        var start = new Vector2(Chance() * Width, MeteorSpawnY);
        var target = PickTarget();
        meteors[MeteorCount++] = new Meteor
        {
            Start = start,
            Position = start,
            Direction = Vector2.Normalize(target - start),
            Speed = MathF.Min(MaxMeteorSpeed, 7f + Wave * 1.3f) * (0.85f + Chance() * 0.3f),
            CanSplit = Wave >= SplitFromWave && Chance() < SplitChance,
            SplitY = 40f + Chance() * 30f,
        };
    }

    private void TickShield(float deltaSeconds)
    {
        if (ShieldFalling)
        {
            var position = ShieldPosition;
            position.Y += ShieldFallSpeed * deltaSeconds;
            ShieldPosition = position;
            if (position.Y >= GroundY)
            {
                ShieldFalling = false;
            }

            return;
        }

        if (shieldSpawnTimer < 0f)
        {
            return;
        }

        shieldSpawnTimer -= deltaSeconds;
        if (shieldSpawnTimer > 0f)
        {
            return;
        }

        shieldSpawnTimer = -1f;
        ShieldFalling = true;
        ShieldPosition = new Vector2(ShieldSpawnMinX + Chance() * (ShieldSpawnMaxX - ShieldSpawnMinX), MeteorSpawnY);
    }

    private Vector2 PickTarget()
    {
        if (CitiesLeft == 0 || Chance() < BatteryTargetChance)
        {
            return new Vector2(BatteryX, GroundY);
        }

        var pick = random.Next(CitiesLeft);
        for (var cityIndex = 0; cityIndex < CityCount; cityIndex++)
        {
            if (!cities[cityIndex])
            {
                continue;
            }

            if (pick == 0)
            {
                return CityCenter(cityIndex);
            }

            pick--;
        }

        return new Vector2(BatteryX, GroundY);
    }

    private void MoveInterceptors(float deltaSeconds)
    {
        var step = ShotSpeed * deltaSeconds;
        for (var index = InterceptorCount - 1; index >= 0; index--)
        {
            ref var shot = ref interceptors[index];
            if (Vector2.Distance(shot.Position, shot.Target) <= step)
            {
                SpawnBlast(shot.Target, true);
                interceptors[index] = interceptors[--InterceptorCount];
                continue;
            }

            shot.Position += shot.Direction * step;
        }
    }

    private void MoveBlasts(float deltaSeconds)
    {
        for (var index = BlastCount - 1; index >= 0; index--)
        {
            ref var blast = ref blasts[index];
            if (blast.Growing)
            {
                blast.Radius += BlastGrowth * deltaSeconds;
                if (blast.Radius >= BlastMaxRadius)
                {
                    blast.Radius = BlastMaxRadius;
                    blast.Growing = false;
                    blast.Hold = BlastHold;
                }
            }
            else if (blast.Hold > 0f)
            {
                blast.Hold -= deltaSeconds;
            }
            else
            {
                blast.Radius -= BlastShrink * deltaSeconds;
                if (blast.Radius <= 0f)
                {
                    blasts[index] = blasts[--BlastCount];
                    continue;
                }
            }

            SweepMeteors(ref blast);
            CatchShield(in blast);
        }
    }

    private void SweepMeteors(ref Blast blast)
    {
        var radiusSquared = blast.Radius * blast.Radius;
        for (var index = MeteorCount - 1; index >= 0; index--)
        {
            var position = meteors[index].Position;
            if (Vector2.DistanceSquared(position, blast.Center) > radiusSquared)
            {
                continue;
            }

            Score += MeteorPoints;
            MeteorsDestroyed++;
            if (blast.FromShot && !blast.Scored)
            {
                blast.Scored = true;
                ShotsHit++;
            }

            if (DestroyedCount < destroyedPositions.Length)
            {
                destroyedPositions[DestroyedCount++] = position;
            }

            meteors[index] = meteors[--MeteorCount];
            if (MeteorCount == 0 && pendingSpawns == 0)
            {
                LastMeteorDestroyedThisFrame = true;
            }
        }
    }

    private void CatchShield(in Blast blast)
    {
        if (!ShieldFalling)
        {
            return;
        }

        var reach = blast.Radius + ShieldRadius;
        if (Vector2.DistanceSquared(ShieldPosition, blast.Center) > reach * reach)
        {
            return;
        }

        ShieldFalling = false;
        ShieldCharges = 1;
        ShieldCollectedThisFrame = true;
    }

    private void MoveMeteors(float deltaSeconds)
    {
        for (var index = MeteorCount - 1; index >= 0; index--)
        {
            ref var meteor = ref meteors[index];
            meteor.Position += meteor.Direction * meteor.Speed * deltaSeconds;
            if (meteor.CanSplit && meteor.Position.Y >= meteor.SplitY)
            {
                meteor.CanSplit = false;
                SplitMeteor(meteor.Position, meteor.Speed);
            }

            if (meteor.Position.Y < GroundY)
            {
                continue;
            }

            var impactX = meteor.Position.X;
            meteors[index] = meteors[--MeteorCount];
            SpawnBlast(new Vector2(impactX, GroundY), false);
            Impact(impactX);
        }
    }

    private void SplitMeteor(Vector2 position, float speed)
    {
        if (MeteorCount >= MaxMeteors)
        {
            return;
        }

        meteors[MeteorCount++] = new Meteor
        {
            Start = position,
            Position = position,
            Direction = Vector2.Normalize(PickTarget() - position),
            Speed = speed,
            CanSplit = false,
            SplitY = 0f,
        };
    }

    private void SpawnBlast(Vector2 center, bool fromShot)
    {
        if (BlastSpawnCount < blastSpawnPositions.Length)
        {
            blastSpawnPositions[BlastSpawnCount++] = center;
        }

        if (BlastCount >= MaxBlasts)
        {
            return;
        }

        blasts[BlastCount++] = new Blast
        {
            Center = center,
            Radius = BlastStartRadius,
            Growing = true,
            Hold = 0f,
            FromShot = fromShot,
            Scored = false,
        };
    }

    private void Impact(float x)
    {
        for (var cityIndex = 0; cityIndex < CityCount; cityIndex++)
        {
            if (!cities[cityIndex] || MathF.Abs(CityX[cityIndex] - x) > CityHalfWidth)
            {
                continue;
            }

            if (ShieldCharges > 0)
            {
                ShieldCharges--;
                ShieldAbsorbedCityThisFrame = cityIndex;
                continue;
            }

            cities[cityIndex] = false;
            CitiesLeft--;
            CityLostThisFrame = cityIndex;
        }

        if (CitiesLeft == 0)
        {
            GameOver = true;
        }
    }

    private float Chance() => random.NextFloat();
}
