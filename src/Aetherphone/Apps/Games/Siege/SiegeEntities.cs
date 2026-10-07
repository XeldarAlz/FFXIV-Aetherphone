namespace Aetherphone.Apps.Games.Siege;

internal enum EnemyState : byte
{
    Walking,
    Eating,
    Burrowing,
    Surfacing,
}

internal enum SiegePhase : byte
{
    Prelude,
    Spawning,
    Clearing,
    Break,
    Won,
    Lost,
}

internal enum PlantResult : byte
{
    Planted,
    Closed,
    Locked,
    OutOfBounds,
    Occupied,
    CoolingDown,
    Unaffordable,
}

internal enum SiegeEventKind : byte
{
    WaveStarted,
    WaveCleared,
    EnemySpawned,
    EnemyHit,
    EnemyKilled,
    PotLost,
    Surfaced,
    Summoned,
    SeedFired,
    DefenderPlanted,
    DefenderBitten,
    DefenderLost,
    DefenderRemoved,
    BombExploded,
    SunProduced,
    SkyMote,
    MoteCollected,
    MoteExpired,
    GardenHit,
    LevelWon,
    LevelLost,
}

internal struct SiegeDefender
{
    public DefenderKind Kind;
    public float Health;
    public float MaxHealth;
    public float Timer;
    public float Age;
    public float Recoil;
    public float Flash;

    public readonly bool Occupied => Kind != DefenderKind.None;

    public readonly float HealthFraction => MaxHealth <= 0f ? 0f : Math.Clamp(Health / MaxHealth, 0f, 1f);
}

internal struct SiegeEnemy
{
    public EnemyKind Kind;
    public EnemyState State;
    public bool Alive;
    public bool HasPot;
    public byte Column;
    public sbyte BiteRow;
    public float Y;
    public float Health;
    public float MaxHealth;
    public float SlowSeconds;
    public float BiteTimer;
    public float StateTimer;
    public float SummonTimer;
    public float Squash;
    public float Age;
    public float Stride;
    public int Id;

    public readonly bool Slowed => SlowSeconds > 0f;

    public readonly bool Targetable => Alive && State != EnemyState.Burrowing;

    public readonly float HealthFraction => MaxHealth <= 0f ? 0f : Math.Clamp(Health / MaxHealth, 0f, 1f);
}

internal struct SiegeSeed
{
    public byte Column;
    public bool Frost;
    public bool Alive;
    public float Y;
    public int Id;
}

internal struct SiegeMote
{
    public Vector2 From;
    public Vector2 To;
    public float Age;
    public float Travel;
    public int Value;
    public bool Alive;
    public bool Sky;
    public int Id;

    public readonly bool Landed => Age >= Travel;

    public readonly float Remaining => Travel + SiegeRules.MoteLingerSeconds - Age;
}

internal readonly struct SiegeEvent
{
    public readonly SiegeEventKind Kind;
    public readonly Vector2 Position;
    public readonly byte Column;
    public readonly sbyte Row;
    public readonly byte Detail;
    public readonly int Value;

    public SiegeEvent(SiegeEventKind kind, Vector2 position, int column, int row, byte detail, int value)
    {
        Kind = kind;
        Position = position;
        Column = (byte)Math.Clamp(column, 0, byte.MaxValue);
        Row = (sbyte)Math.Clamp(row, sbyte.MinValue, sbyte.MaxValue);
        Detail = detail;
        Value = value;
    }
}
