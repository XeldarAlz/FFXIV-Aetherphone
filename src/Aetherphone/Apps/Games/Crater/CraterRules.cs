namespace Aetherphone.Apps.Games.Crater;

internal enum CraterWeapon : byte
{
    Shell,
    Grenade,
    Cluster,
    Drill,
    Shield,
    Teleport,
}

internal enum ProjectileKind : byte
{
    Shell,
    Grenade,
    Cluster,
    Bomblet,
    Drill,
}

internal enum CraterLevel : byte
{
    Easy,
    Hard,
}

internal enum CraterPhase : byte
{
    TurnIntro,
    Aiming,
    Flight,
    Settling,
    Over,
}

internal readonly struct BlastSpec
{
    public readonly float Radius;
    public readonly int Damage;
    public readonly float Knockback;

    public BlastSpec(float radius, int damage, float knockback)
    {
        Radius = radius;
        Damage = damage;
        Knockback = knockback;
    }
}

internal static class CraterRules
{
    public const int Columns = 640;
    public const int Rows = 360;
    public const float MetresPerCell = 0.05f;
    public const float WorldWidth = Columns * MetresPerCell;
    public const float WorldHeight = Rows * MetresPerCell;
    public const float TickSeconds = 1f / 120f;
    public const float MaxCatchUpSeconds = 0.1f;
    public const float Gravity = 9.8f;
    public const int MaxWindLevel = 10;
    public const float WindPerLevel = 0.25f;
    public const int MinTeams = 2;
    public const int MaxTeams = 4;
    public const int MooglesPerTeam = 2;
    public const int MaxMoogles = MaxTeams * MooglesPerTeam;
    public const int MaxHealth = 100;
    public const float MoogleRadius = 0.35f;
    public const float WalkSpeed = 1.5f;
    public const float JumpSpeedX = 2.2f;
    public const float JumpSpeedY = 4.8f;
    public const float StepUp = 0.1f;
    public const float SnapDown = 0.08f;
    public const float FootSpread = 0.55f;
    public const float SafeFall = 3f;
    public const int FallDamageBase = 4;
    public const float FallDamagePerMetre = 8f;
    public const int FallDamageCap = 40;
    public const float TurnSeconds = 45f;
    public const float TurnIntroSeconds = 0.9f;
    public const float ImpactHoldSeconds = 0.6f;
    public const float UtilityHoldSeconds = 0.8f;
    public const float SettleSeconds = 0.4f;
    public const float MaxSettleSeconds = 8f;
    public const float ChargeSeconds = 1.4f;
    public const float MinLaunchSpeed = 3f;
    public const float MaxLaunchSpeed = 21f;
    public const float MuzzleDistance = MoogleRadius + 0.18f;
    public const float MinElevation = -1.3f;
    public const float MaxElevation = 1.55f;
    public const float DefaultElevation = 0.6f;
    public const float DefaultPower = 0.6f;
    public const int SuddenDeathRound = 10;
    public const float WaterDepth = 1.1f;
    public const float WaterRise = 0.5f;
    public const float WaterRiseSpeed = 0.4f;
    public const float MinWaterLevel = 4f;
    public const int MinFuse = 1;
    public const int MaxFuse = 5;
    public const int DefaultFuse = 3;
    public const int WeaponCount = 6;
    public const int Unlimited = -1;
    public const int BombletCount = 5;
    public const float BombletSpeed = 4.6f;
    public const float BombletSpread = 0.42f;
    public const float DrillSpeed = 3.2f;
    public const float DrillSeconds = 1.5f;
    public const float TunnelRadius = 0.32f;
    public const float DrillCarveSeconds = 0.1f;
    public const float GrenadeRestitution = 0.45f;
    public const float GrenadeFriction = 0.78f;
    public const float GrenadeRestSpeed = 0.35f;
    public const float OwnerGraceSeconds = 0.25f;
    public const float LostMargin = 20f;
    public const float SkyLimit = -60f;

    public static readonly BlastSpec ShellBlast = new(1.2f, 45, 7.5f);
    public static readonly BlastSpec GrenadeBlast = new(1.25f, 50, 7.5f);
    public static readonly BlastSpec ClusterBlast = new(0.7f, 18, 4f);
    public static readonly BlastSpec BombletBlast = new(0.7f, 18, 4.5f);
    public static readonly BlastSpec DrillBlast = new(1f, 35, 6.5f);

    private static readonly float[] ProjectileRadii = { 0.09f, 0.11f, 0.12f, 0.07f, 0.1f };
    private static readonly float[] WindFactors = { 1f, 0f, 1f, 0.5f, 1f };
    private static readonly int[] StartingAmmoTable = { Unlimited, Unlimited, 2, 2, 2, 2 };

    public static float ProjectileRadius(ProjectileKind kind) => ProjectileRadii[(int)kind];

    public static float WindFactor(ProjectileKind kind) => WindFactors[(int)kind];

    public static int StartingAmmo(CraterWeapon weapon) => StartingAmmoTable[(int)weapon];

    public static float Wind(int level) => level * WindPerLevel;

    public static float LaunchSpeed(float power) =>
        MinLaunchSpeed + (MaxLaunchSpeed - MinLaunchSpeed) * Math.Clamp(power, 0f, 1f);

    public static Vector2 AimDirection(float elevation, int facing) =>
        new(MathF.Cos(elevation) * (facing < 0 ? -1f : 1f), -MathF.Sin(elevation));

    public static bool Fires(CraterWeapon weapon) => weapon <= CraterWeapon.Drill;

    public static ProjectileKind KindOf(CraterWeapon weapon) => weapon switch
    {
        CraterWeapon.Grenade => ProjectileKind.Grenade,
        CraterWeapon.Cluster => ProjectileKind.Cluster,
        CraterWeapon.Drill => ProjectileKind.Drill,
        _ => ProjectileKind.Shell,
    };

    public static BlastSpec Blast(ProjectileKind kind) => kind switch
    {
        ProjectileKind.Grenade => GrenadeBlast,
        ProjectileKind.Cluster => ClusterBlast,
        ProjectileKind.Bomblet => BombletBlast,
        ProjectileKind.Drill => DrillBlast,
        _ => ShellBlast,
    };

    public static float BlastFactor(in BlastSpec blast, float centreDistance)
    {
        var surfaceDistance = MathF.Max(0f, centreDistance - MoogleRadius);
        if (surfaceDistance >= blast.Radius)
        {
            return 0f;
        }

        return 1f - surfaceDistance / blast.Radius;
    }

    public static int BlastDamage(in BlastSpec blast, float centreDistance)
    {
        var factor = BlastFactor(blast, centreDistance);
        if (factor <= 0f)
        {
            return 0;
        }

        return Math.Max(1, (int)MathF.Round(blast.Damage * factor));
    }

    public static int FallDamage(float fallMetres)
    {
        if (fallMetres <= SafeFall)
        {
            return 0;
        }

        var damage = FallDamageBase + (int)MathF.Round((fallMetres - SafeFall) * FallDamagePerMetre);
        return Math.Min(FallDamageCap, damage);
    }
}
