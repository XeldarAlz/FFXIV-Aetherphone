namespace Aetherphone.Apps.Games.Siege;

internal enum DefenderKind : byte
{
    None,
    Sprout,
    Sunbloom,
    Thornwall,
    Frostbud,
    Bombcap,
}

internal enum EnemyKind : byte
{
    Walker,
    Runner,
    Armoured,
    Flyer,
    Digger,
    Boss,
}

internal static class SiegeRules
{
    public const int Columns = 5;
    public const int Rows = 8;
    public const int CellCount = Columns * Rows;
    public const int DefenderKinds = 5;
    public const int EnemyKinds = 6;
    public const int GardenHealth = 5;
    public const float StepSeconds = 1f / 60f;
    public const float MaxCatchUpSeconds = 0.1f;
    public const float SpawnY = -0.6f;
    public const float VisibleTop = -0.75f;
    public const float GardenLine = Rows;
    public const float BackReach = 0.3f;
    public const float SeedSpeed = 7f;
    public const float SeedDamage = 1f;
    public const float SeedLaunchOffset = 0.35f;
    public const float SeedVanishY = -1.2f;
    public const float TargetLead = 0.5f;
    public const float SlowFactor = 0.5f;
    public const float SlowSeconds = 3f;
    public const float BombFuseSeconds = 1f;
    public const float BombDamage = 45f;
    public const int BombReach = 1;
    public const float PotShare = 0.5f;
    public const float DiggerBurrowSpeed = 0.5f;
    public const float DiggerSurfaceOffset = 1.25f;
    public const float DiggerSurfaceSeconds = 0.8f;
    public const float DiggerLastSurface = Rows - 1.25f;
    public const float BossSummonSeconds = 6.5f;
    public const float SunValue = 25f;
    public const float SunCap = 9990f;
    public const float CampaignSunlight = 200f;
    public const float EndlessSunlight = 200f;
    public const float BaseDrip = 0.75f;
    public const float SkyMoteMinSeconds = 6f;
    public const float SkyMoteMaxSeconds = 9f;
    public const float FirstSkyMoteSeconds = 3f;
    public const float MoteFallSpeed = 0.9f;
    public const float MoteHopSeconds = 0.45f;
    public const float MoteLingerSeconds = 9f;
    public const float SproutInterval = 1.25f;
    public const float FrostInterval = 1.5f;
    public const float FirstShotSeconds = 0.6f;
    public const float SunbloomFirstSeconds = 5f;
    public const float SunbloomInterval = 12f;
    public const float PreludeSeconds = 6f;
    public const float BreakSeconds = 3.5f;
    public const float CampaignHealthGrowth = 0.01f;
    public const float EndlessHealthGrowth = 0.03f;

    private static readonly int[] Costs = { 0, 100, 50, 75, 150, 125 };
    private static readonly float[] Cooldowns = { 0f, 5f, 6f, 18f, 7f, 28f };
    private static readonly float[] DefenderHealths = { 0f, 6f, 6f, 72f, 6f, 6f };
    private static readonly float[] EnemyHealths = { 8f, 5f, 20f, 6f, 6f, 120f };
    private static readonly float[] Speeds = { 0.22f, 0.45f, 0.2f, 0.34f, 0.24f, 0.1f };
    private static readonly float[] BiteDamages = { 1f, 1f, 1f, 0f, 1f, 24f };
    private static readonly float[] BiteIntervals = { 0.5f, 0.5f, 0.5f, 1f, 0.5f, 0.8f };
    private static readonly int[] GardenDamages = { 1, 1, 1, 1, 1, GardenHealth };
    private static readonly float[] Reaches = { 0.2f, 0.2f, 0.22f, 0.2f, 0.2f, 0.45f };
    private static readonly float[] HitRadii = { 0.34f, 0.3f, 0.36f, 0.32f, 0.34f, 0.62f };

    public static int Cost(DefenderKind kind) => Costs[(int)kind];

    public static float Cooldown(DefenderKind kind) => Cooldowns[(int)kind];

    public static float Health(DefenderKind kind) => DefenderHealths[(int)kind];

    public static float Health(EnemyKind kind) => EnemyHealths[(int)kind];

    public static float Speed(EnemyKind kind) => Speeds[(int)kind];

    public static float BiteDamage(EnemyKind kind) => BiteDamages[(int)kind];

    public static float BiteInterval(EnemyKind kind) => BiteIntervals[(int)kind];

    public static int GardenDamage(EnemyKind kind) => GardenDamages[(int)kind];

    public static float Reach(EnemyKind kind) => Reaches[(int)kind];

    public static float HitRadius(EnemyKind kind) => HitRadii[(int)kind];

    public static bool Shoots(DefenderKind kind) => kind is DefenderKind.Sprout or DefenderKind.Frostbud;

    public static float FireInterval(DefenderKind kind) =>
        kind == DefenderKind.Frostbud ? FrostInterval : SproutInterval;

    public static float ContactY(EnemyKind kind, int row) => row - Reach(kind);

    public static int CellIndex(int column, int row) => row * Columns + column;

    public static int ColumnOf(int cell) => cell % Columns;

    public static int RowOf(int cell) => cell / Columns;

    public static int RowAt(float y) => (int)MathF.Floor(y);

    public static bool InBlast(int bombColumn, int bombRow, int column, float y) =>
        Math.Abs(column - bombColumn) <= BombReach && Math.Abs(RowAt(y) - bombRow) <= BombReach;

    public static float CampaignHealthScale(int level) => 1f + CampaignHealthGrowth * Math.Max(0, level - 1);

    public static float EndlessHealthScale(int wave) => 1f + EndlessHealthGrowth * Math.Max(0, wave - 1);

    public static int Stars(int health, int maxHealth)
    {
        if (health <= 0)
        {
            return 0;
        }

        if (health >= maxHealth)
        {
            return 3;
        }

        return health * 2 >= maxHealth ? 2 : 1;
    }
}
