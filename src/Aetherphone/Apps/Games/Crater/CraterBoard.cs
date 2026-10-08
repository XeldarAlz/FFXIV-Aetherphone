using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Framework.World;

namespace Aetherphone.Apps.Games.Crater;

internal sealed partial class CraterBoard
{
    public const int NoTeam = -1;
    public const int MaxProjectiles = 16;
    public const int MaxCraters = 384;
    private const int EventCapacity = 96;
    private const int StyleCount = 3;
    private const float NeverBlasted = 1000f;

    private readonly TerrainMask terrain = new(CraterRules.Columns, CraterRules.Rows, CraterRules.MetresPerCell);
    private readonly CraterMoogle[] moogles = new CraterMoogle[CraterRules.MaxMoogles];
    private readonly float[] aims = new float[CraterRules.MaxMoogles];
    private readonly int[] lastAttackers = new int[CraterRules.MaxMoogles];
    private readonly CraterProjectile[] projectiles = new CraterProjectile[MaxProjectiles];
    private readonly CraterMark[] craters = new CraterMark[MaxCraters];
    private readonly int[] ammo = new int[CraterRules.MaxTeams * CraterRules.WeaponCount];
    private readonly bool[] botTeams = new bool[CraterRules.MaxTeams];
    private readonly int[] nextMembers = new int[CraterRules.MaxTeams];
    private readonly int[] damageDealt = new int[CraterRules.MaxTeams];
    private readonly int[] knockouts = new int[CraterRules.MaxTeams];
    private readonly CraterWeapon[] teamWeapons = new CraterWeapon[CraterRules.MaxTeams];
    private readonly float[] teamPowers = new float[CraterRules.MaxTeams];
    private readonly CraterEvent[] events = new CraterEvent[EventCapacity];
    private readonly CraterBot bot = new();
    private FixedStepClock clock = new(CraterRules.TickSeconds, CraterRules.MaxCatchUpSeconds);
    private GameRandom random;
    private int eventHead;
    private int eventCount;
    private int craterCount;
    private int playedMask;
    private int nextProjectileId;
    private int walkIntent;
    private int walkLeft;
    private float holdSeconds;

    public ulong Seed { get; private set; }

    public TerrainStyle Style { get; private set; }

    public CraterLevel Level { get; private set; }

    public CraterPhase Phase { get; private set; } = CraterPhase.Over;

    public float PhaseSeconds { get; private set; }

    public int TeamCount { get; private set; }

    public int MoogleCount { get; private set; }

    public int ActiveTeam { get; private set; }

    public int ActiveMoogle { get; private set; }

    public int Round { get; private set; }

    public int WindLevel { get; private set; }

    public float WaterLevel { get; private set; }

    public float WaterTarget { get; private set; }

    public float TurnLeft { get; private set; }

    public float Charge { get; private set; }

    public bool Charging { get; private set; }

    public CraterWeapon Weapon { get; private set; }

    public int Fuse { get; private set; } = CraterRules.DefaultFuse;

    public int Winner { get; private set; } = NoTeam;

    public bool SuddenDeath { get; private set; }

    public long TickCount { get; private set; }

    public Vector2 LastBlast { get; private set; }

    public float SinceBlast { get; private set; } = NeverBlasted;

    public TerrainMask Terrain => terrain;

    public float Wind => CraterRules.Wind(WindLevel);

    public bool Started => TeamCount > 0;

    public bool Over => Phase == CraterPhase.Over;

    public bool CanAct => Phase == CraterPhase.Aiming && moogles[ActiveMoogle].Alive;

    public int WalkLeft => walkLeft;

    public bool HumanTurn => CanAct && !botTeams[ActiveTeam];

    public bool BotTurn => Started && !Over && botTeams[ActiveTeam];

    public float ActiveAim => aims[ActiveMoogle];

    public int CraterCount => craterCount;

    public ReadOnlySpan<CraterMoogle> Moogles => new(moogles, 0, MoogleCount);

    public ReadOnlySpan<CraterProjectile> Projectiles => projectiles;

    public ReadOnlySpan<CraterMark> Craters => new(craters, 0, craterCount);

    public ref readonly CraterMoogle Moogle(int index) => ref moogles[index];

    public float Aim(int moogle) => aims[moogle];

    public bool IsBot(int team) => team >= 0 && team < TeamCount && botTeams[team];

    public int Ammo(int team, CraterWeapon weapon) => ammo[team * CraterRules.WeaponCount + (int)weapon];

    public int DamageDealt(int team) => damageDealt[team];

    public int Knockouts(int team) => knockouts[team];

    public float TeamPower(int team) => teamPowers[team];

    public void Start(ulong seed, in CraterSetup setup)
    {
        Seed = seed;
        random = GameRandom.FromSeed(seed);
        TeamCount = Math.Clamp(setup.Teams, CraterRules.MinTeams, CraterRules.MaxTeams);
        MoogleCount = TeamCount * CraterRules.MooglesPerTeam;
        Level = setup.Level;
        for (var team = 0; team < CraterRules.MaxTeams; team++)
        {
            botTeams[team] = team < TeamCount && (setup.BotMask & (1 << team)) != 0;
            nextMembers[team] = 0;
            damageDealt[team] = 0;
            knockouts[team] = 0;
            teamWeapons[team] = CraterWeapon.Shell;
            teamPowers[team] = CraterRules.DefaultPower;
            for (var weapon = 0; weapon < CraterRules.WeaponCount; weapon++)
            {
                ammo[team * CraterRules.WeaponCount + weapon] = CraterRules.StartingAmmo((CraterWeapon)weapon);
            }
        }

        Style = (TerrainStyle)random.Next(StyleCount);
        terrain.Generate(ref random, Style);
        WaterLevel = CraterRules.WorldHeight - CraterRules.WaterDepth;
        WaterTarget = WaterLevel;
        Array.Clear(projectiles);
        craterCount = 0;
        eventHead = 0;
        eventCount = 0;
        playedMask = 0;
        nextProjectileId = 0;
        Round = 1;
        Winner = NoTeam;
        SuddenDeath = false;
        TickCount = 0;
        SinceBlast = NeverBlasted;
        LastBlast = Vector2.Zero;
        Fuse = CraterRules.DefaultFuse;
        clock.Reset();
        PlaceTeams();
        BeginTurn(random.Next(TeamCount));
    }

    public void PlaceMoogle(int index, Vector2 center)
    {
        ref var moogle = ref moogles[index];
        moogle.Position = center;
        moogle.Velocity = Vector2.Zero;
        moogle.Grounded = false;
        moogle.ApexY = center.Y;
        moogle.StillSeconds = 0f;
        var bottom = center.Y + CraterRules.MoogleRadius;
        var top = GroundTop(center.X, bottom - CraterRules.StepUp);
        if (top <= bottom + CraterRules.SnapDown)
        {
            moogle.Position = new Vector2(center.X, top - CraterRules.MoogleRadius);
            moogle.Grounded = true;
        }
    }

    public void SetWind(int level)
    {
        WindLevel = Math.Clamp(level, -CraterRules.MaxWindLevel, CraterRules.MaxWindLevel);
    }

    public void Step(float deltaSeconds)
    {
        if (!Started)
        {
            return;
        }

        var ticks = clock.Advance(deltaSeconds);
        for (var tick = 0; tick < ticks; tick++)
        {
            Tick();
        }
    }

    public void Tick()
    {
        if (!Started)
        {
            return;
        }

        var deltaSeconds = CraterRules.TickSeconds;
        TickCount++;
        SinceBlast = MathF.Min(NeverBlasted, SinceBlast + deltaSeconds);
        PhaseSeconds += deltaSeconds;
        AdvanceWater(deltaSeconds);
        switch (Phase)
        {
            case CraterPhase.TurnIntro:
                if (PhaseSeconds >= CraterRules.TurnIntroSeconds)
                {
                    EnterPhase(CraterPhase.Aiming);
                }

                break;
            case CraterPhase.Aiming:
                StepAiming(deltaSeconds);
                break;
            case CraterPhase.Flight:
                StepProjectiles(deltaSeconds);
                if (!AnyProjectile())
                {
                    Hold(CraterRules.ImpactHoldSeconds);
                }

                break;
            case CraterPhase.Settling:
                if ((PhaseSeconds >= holdSeconds && BodiesSettled()) || PhaseSeconds >= CraterRules.MaxSettleSeconds)
                {
                    EndTurn();
                }

                break;
            default:
                break;
        }

        StepBodies(deltaSeconds);
    }

    public void SetWalk(int direction)
    {
        walkIntent = CanAct ? Math.Sign(direction) : 0;
    }

    public void SetAim(float elevation)
    {
        if (!CanAct)
        {
            return;
        }

        aims[ActiveMoogle] = Math.Clamp(elevation, CraterRules.MinElevation, CraterRules.MaxElevation);
    }

    public void SetFacing(int facing)
    {
        if (!CanAct || facing == 0)
        {
            return;
        }

        moogles[ActiveMoogle].Facing = Math.Sign(facing);
    }

    public bool SelectWeapon(CraterWeapon weapon)
    {
        if (!CanAct || Charging || Ammo(ActiveTeam, weapon) == 0)
        {
            return false;
        }

        Weapon = weapon;
        teamWeapons[ActiveTeam] = weapon;
        return true;
    }

    public void SetFuse(int seconds)
    {
        if (CanAct)
        {
            Fuse = Math.Clamp(seconds, CraterRules.MinFuse, CraterRules.MaxFuse);
        }
    }

    public void BeginCharge()
    {
        if (!CanAct || Charging)
        {
            return;
        }

        if (Weapon == CraterWeapon.Shield)
        {
            RaiseShield();
            return;
        }

        if (!CraterRules.Fires(Weapon) || Ammo(ActiveTeam, Weapon) == 0)
        {
            return;
        }

        Charging = true;
        Charge = 0f;
        walkIntent = 0;
    }

    public void ReleaseCharge()
    {
        if (!Charging)
        {
            return;
        }

        Fire(Charge);
    }

    public bool Fire(float power)
    {
        if (!CanAct)
        {
            return false;
        }

        if (Weapon == CraterWeapon.Shield)
        {
            return RaiseShield();
        }

        if (!CraterRules.Fires(Weapon) || !Spend(Weapon))
        {
            return false;
        }

        var clamped = Math.Clamp(power, 0f, 1f);
        ref readonly var shooter = ref moogles[ActiveMoogle];
        var shot = new CraterShot(Weapon, shooter.Facing, aims[ActiveMoogle], clamped, Fuse);
        var projectile = MakeProjectile(ActiveMoogle, shot);
        Spawn(projectile);
        teamPowers[ActiveTeam] = clamped;
        Charging = false;
        Charge = 0f;
        walkIntent = 0;
        EnterPhase(CraterPhase.Flight);
        Push(CraterEventKind.Launched, projectile.Position, projectile.Velocity, 0f, (int)Weapon, ActiveMoogle,
            ActiveTeam, projectile.Kind);
        return true;
    }

    public bool Teleport(Vector2 target)
    {
        if (!CanAct || Weapon != CraterWeapon.Teleport || Ammo(ActiveTeam, CraterWeapon.Teleport) == 0)
        {
            return false;
        }

        if (!TryTeleportSpot(target, out var destination))
        {
            return false;
        }

        Spend(CraterWeapon.Teleport);
        ref var moogle = ref moogles[ActiveMoogle];
        var origin = moogle.Position;
        moogle.Position = destination;
        moogle.Velocity = Vector2.Zero;
        moogle.Grounded = true;
        moogle.ApexY = destination.Y;
        moogle.StillSeconds = 0f;
        Push(CraterEventKind.Teleported, origin, destination, 0f, 0, ActiveMoogle, ActiveTeam, ProjectileKind.Shell);
        Hold(CraterRules.UtilityHoldSeconds);
        return true;
    }

    public bool TryTeleportSpot(Vector2 target, out Vector2 destination) =>
        CraterFooting.TeleportSpot(terrain, WaterLevel, target, out destination);

    public bool TryTakeEvent(out CraterEvent entry)
    {
        if (eventCount == 0)
        {
            entry = default;
            return false;
        }

        entry = events[eventHead];
        eventHead = (eventHead + 1) % EventCapacity;
        eventCount--;
        return true;
    }

    public int TeamHealth(int team)
    {
        var total = 0;
        for (var index = 0; index < MoogleCount; index++)
        {
            if (moogles[index].Team == team && moogles[index].Alive)
            {
                total += moogles[index].Health;
            }
        }

        return total;
    }

    public bool TeamAlive(int team) => (AliveTeamMask() & (1 << team)) != 0;

    public int AliveTeamCount() => BitOperations.PopCount((uint)AliveTeamMask());

    public bool AnyProjectile()
    {
        for (var index = 0; index < MaxProjectiles; index++)
        {
            if (projectiles[index].Alive)
            {
                return true;
            }
        }

        return false;
    }

    public CraterProjectile MakeProjectile(int moogle, in CraterShot shot) =>
        CraterBallistics.Launch(moogles[moogle].Position, moogle, moogles[moogle].Team, shot);

    private void PlaceTeams()
    {
        var spawns = terrain.SpawnPoints(MoogleCount);
        var mirrored = random.Chance(0.5f);
        for (var slot = 0; slot < MoogleCount; slot++)
        {
            var team = slot % TeamCount;
            if (mirrored)
            {
                team = TeamCount - 1 - team;
            }

            var index = team * CraterRules.MooglesPerTeam + slot / TeamCount;
            var x = slot < spawns.Length
                ? spawns[slot].X
                : CraterRules.WorldWidth * (slot + 0.5f) / MoogleCount;
            var top = slot < spawns.Length ? spawns[slot].Y : GroundTop(x, 0f);
            ref var moogle = ref moogles[index];
            moogle = default;
            moogle.Team = team;
            moogle.Health = CraterRules.MaxHealth;
            moogle.Alive = true;
            moogle.Facing = x < CraterRules.WorldWidth * 0.5f ? 1 : -1;
            aims[index] = CraterRules.DefaultElevation;
            lastAttackers[index] = NoTeam;
            PlaceMoogle(index, new Vector2(x, top - CraterRules.MoogleRadius));
        }
    }

    private void BeginTurn(int team)
    {
        ActiveTeam = team;
        ActiveMoogle = NextMember(team);
        WindLevel = random.Next(-CraterRules.MaxWindLevel, CraterRules.MaxWindLevel + 1);
        var remembered = teamWeapons[team];
        Weapon = Ammo(team, remembered) == 0 ? CraterWeapon.Shell : remembered;
        TurnLeft = CraterRules.TurnSeconds;
        Charging = false;
        Charge = 0f;
        walkIntent = 0;
        walkLeft = CraterRules.WalkTicks;
        Array.Fill(lastAttackers, NoTeam);
        EnterPhase(CraterPhase.TurnIntro);
        Push(CraterEventKind.TurnStarted, moogles[ActiveMoogle].Position, Vector2.Zero, 0f, Round, ActiveMoogle, team,
            ProjectileKind.Shell);
        if (botTeams[team])
        {
            bot.Begin(Level, ref random);
        }
    }

    private int NextMember(int team)
    {
        var first = team * CraterRules.MooglesPerTeam;
        for (var offset = 0; offset < CraterRules.MooglesPerTeam; offset++)
        {
            var member = (nextMembers[team] + offset) % CraterRules.MooglesPerTeam;
            if (!moogles[first + member].Alive)
            {
                continue;
            }

            nextMembers[team] = (member + 1) % CraterRules.MooglesPerTeam;
            return first + member;
        }

        return first;
    }

    private void StepAiming(float deltaSeconds)
    {
        if (!moogles[ActiveMoogle].Alive)
        {
            Hold(CraterRules.SettleSeconds);
            return;
        }

        if (botTeams[ActiveTeam])
        {
            bot.Tick(this, ref random, deltaSeconds);
            if (Phase != CraterPhase.Aiming)
            {
                return;
            }
        }

        if (Charging)
        {
            Charge = MathF.Min(1f, Charge + deltaSeconds / CraterRules.ChargeSeconds);
        }

        TurnLeft -= deltaSeconds;
        if (TurnLeft > 0f)
        {
            return;
        }

        TurnLeft = 0f;
        Push(CraterEventKind.TimeUp, moogles[ActiveMoogle].Position, Vector2.Zero, 0f, 0, ActiveMoogle, ActiveTeam,
            ProjectileKind.Shell);
        if (Charging && Fire(Charge))
        {
            return;
        }

        Hold(CraterRules.SettleSeconds);
    }

    private void EndTurn()
    {
        playedMask |= 1 << ActiveTeam;
        var alive = AliveTeamMask();
        if (BitOperations.PopCount((uint)alive) <= 1)
        {
            Winner = alive == 0 ? NoTeam : BitOperations.TrailingZeroCount(alive);
            EnterPhase(CraterPhase.Over);
            Push(CraterEventKind.MatchOver, Vector2.Zero, Vector2.Zero, 0f, Round, CraterEvent.None, Winner,
                ProjectileKind.Shell);
            return;
        }

        if ((playedMask & alive) == alive)
        {
            Round++;
            playedMask = 0;
            if (Round > CraterRules.SuddenDeathRound)
            {
                RaiseWater();
            }
        }

        BeginTurn(NextAliveTeam(ActiveTeam, alive));
    }

    private void RaiseWater()
    {
        WaterTarget = MathF.Max(CraterRules.MinWaterLevel, WaterTarget - CraterRules.WaterRise);
        var kind = SuddenDeath ? CraterEventKind.WaterRising : CraterEventKind.SuddenDeath;
        SuddenDeath = true;
        Push(kind, new Vector2(CraterRules.WorldWidth * 0.5f, WaterTarget), Vector2.Zero, 0f, Round, CraterEvent.None,
            NoTeam, ProjectileKind.Shell);
    }

    private void AdvanceWater(float deltaSeconds)
    {
        if (WaterLevel > WaterTarget)
        {
            WaterLevel = MathF.Max(WaterTarget, WaterLevel - CraterRules.WaterRiseSpeed * deltaSeconds);
        }
    }

    private int NextAliveTeam(int from, int alive)
    {
        for (var offset = 1; offset <= TeamCount; offset++)
        {
            var team = (from + offset) % TeamCount;
            if ((alive & (1 << team)) != 0)
            {
                return team;
            }
        }

        return from;
    }

    private int AliveTeamMask()
    {
        var mask = 0;
        for (var index = 0; index < MoogleCount; index++)
        {
            if (moogles[index].Alive)
            {
                mask |= 1 << moogles[index].Team;
            }
        }

        return mask;
    }

    private bool RaiseShield()
    {
        ref var moogle = ref moogles[ActiveMoogle];
        if (moogle.Shielded || !Spend(CraterWeapon.Shield))
        {
            return false;
        }

        moogle.Shielded = true;
        Charging = false;
        Push(CraterEventKind.ShieldRaised, moogle.Position, Vector2.Zero, 0f, 0, ActiveMoogle, ActiveTeam,
            ProjectileKind.Shell);
        Hold(CraterRules.UtilityHoldSeconds);
        return true;
    }

    private bool Spend(CraterWeapon weapon)
    {
        var slot = ActiveTeam * CraterRules.WeaponCount + (int)weapon;
        var left = ammo[slot];
        if (left == CraterRules.Unlimited)
        {
            return true;
        }

        if (left <= 0)
        {
            return false;
        }

        ammo[slot] = left - 1;
        if (left - 1 == 0 && teamWeapons[ActiveTeam] == weapon)
        {
            teamWeapons[ActiveTeam] = CraterWeapon.Shell;
        }

        return true;
    }

    private void Hold(float seconds)
    {
        holdSeconds = seconds;
        Charging = false;
        walkIntent = 0;
        EnterPhase(CraterPhase.Settling);
    }

    private void EnterPhase(CraterPhase phase)
    {
        Phase = phase;
        PhaseSeconds = 0f;
    }

    private void Push(CraterEventKind kind, Vector2 position, Vector2 target, float radius, int value, int moogle,
        int team, ProjectileKind projectile)
    {
        var entry = new CraterEvent(kind, position, target, radius, value, moogle, team, projectile);
        if (eventCount == EventCapacity)
        {
            eventHead = (eventHead + 1) % EventCapacity;
            eventCount--;
        }

        events[(eventHead + eventCount) % EventCapacity] = entry;
        eventCount++;
    }
}
