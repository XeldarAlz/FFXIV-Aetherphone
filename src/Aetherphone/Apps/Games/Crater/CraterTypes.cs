namespace Aetherphone.Apps.Games.Crater;

internal struct CraterMoogle
{
    public Vector2 Position;
    public Vector2 Velocity;
    public int Team;
    public int Health;
    public int Facing;
    public bool Alive;
    public bool Grounded;
    public bool Sunk;
    public bool Shielded;
    public float ApexY;
    public float StillSeconds;
}

internal struct CraterProjectile
{
    public Vector2 Position;
    public Vector2 Velocity;
    public Vector2 DrillFrom;
    public ProjectileKind Kind;
    public bool Alive;
    public bool Resting;
    public bool Drilling;
    public float Fuse;
    public float Age;
    public float DrillLeft;
    public float DrillCarve;
    public int Owner;
    public int Team;
    public int Id;
}

internal readonly struct CraterMark
{
    public readonly Vector2 Center;
    public readonly float Radius;

    public CraterMark(Vector2 center, float radius)
    {
        Center = center;
        Radius = radius;
    }
}

internal readonly struct CraterShot
{
    public readonly CraterWeapon Weapon;
    public readonly int Facing;
    public readonly float Elevation;
    public readonly float Power;
    public readonly int Fuse;

    public CraterShot(CraterWeapon weapon, int facing, float elevation, float power, int fuse)
    {
        Weapon = weapon;
        Facing = facing;
        Elevation = elevation;
        Power = power;
        Fuse = fuse;
    }
}

internal readonly struct CraterSetup
{
    public readonly int Teams;
    public readonly int BotMask;
    public readonly CraterLevel Level;

    public CraterSetup(int teams, int botMask, CraterLevel level)
    {
        Teams = teams;
        BotMask = botMask;
        Level = level;
    }
}

internal enum CraterEventKind : byte
{
    TurnStarted,
    Launched,
    Bounced,
    Exploded,
    Damaged,
    Shielded,
    Died,
    Drowned,
    Splashed,
    ClusterSplit,
    DrillStarted,
    Jumped,
    Landed,
    FallHurt,
    Teleported,
    ShieldRaised,
    TimeUp,
    SuddenDeath,
    WaterRising,
    MatchOver,
}

internal readonly struct CraterEvent
{
    public const int None = -1;

    public readonly CraterEventKind Kind;
    public readonly Vector2 Position;
    public readonly Vector2 Target;
    public readonly float Radius;
    public readonly int Value;
    public readonly int Moogle;
    public readonly int Team;
    public readonly ProjectileKind Projectile;

    public CraterEvent(CraterEventKind kind, Vector2 position, Vector2 target, float radius, int value, int moogle,
        int team, ProjectileKind projectile)
    {
        Kind = kind;
        Position = position;
        Target = target;
        Radius = radius;
        Value = value;
        Moogle = moogle;
        Team = team;
        Projectile = projectile;
    }
}

internal enum FlightOutcome : byte
{
    Flying,
    Impact,
    Fused,
    Splashed,
    Lost,
}

internal readonly struct FlightStep
{
    public readonly FlightOutcome Outcome;
    public readonly Vector2 Point;
    public readonly Vector2 Normal;
    public readonly int Moogle;
    public readonly float BounceSpeed;

    public FlightStep(FlightOutcome outcome, Vector2 point, Vector2 normal, int moogle, float bounceSpeed)
    {
        Outcome = outcome;
        Point = point;
        Normal = normal;
        Moogle = moogle;
        BounceSpeed = bounceSpeed;
    }
}
