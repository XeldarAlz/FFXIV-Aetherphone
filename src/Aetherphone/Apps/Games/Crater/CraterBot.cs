using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Crater;

internal readonly struct CraterPlan
{
    public static readonly CraterPlan Worst = new(default, float.MinValue, Vector2.Zero, false);

    public readonly CraterShot Shot;
    public readonly float Score;
    public readonly Vector2 Impact;
    public readonly bool Hits;

    public CraterPlan(CraterShot shot, float score, Vector2 impact, bool hits)
    {
        Shot = shot;
        Score = score;
        Impact = impact;
        Hits = hits;
    }
}

internal sealed class CraterBot
{
    public const int ElevationSteps = 19;
    public const int PowerSteps = 15;
    public const int CandidatesPerFacing = ElevationSteps * PowerSteps;
    public const int CandidateCount = CandidatesPerFacing * 2;
    private const float FirstElevation = -10f * MathF.PI / 180f;
    private const float ElevationStep = 5f * MathF.PI / 180f;
    private const float FirstPower = 0.3f;
    private const float PowerStep = 0.05f;
    private const int CandidatesPerTick = 8;
    private const float KillBonus = 30f;
    private const float FriendlyWeight = 1.6f;
    private const float ShieldPop = 2f;
    private const float MissWeight = 0.5f;
    private const float AimSpeed = 2.5f;
    private const float MinThinkSeconds = 0.5f;
    private const float MaxThinkSeconds = 0.9f;
    private const float EasyWalkChance = 0.3f;
    private const float HardWalkChance = 0.4f;
    private const float MinWalkSeconds = 0.6f;
    private const float MaxWalkSeconds = 1.5f;
    private const float StuckSeconds = 0.15f;
    private const float StuckDistance = 0.0005f;
    private const int MaxHops = 2;
    private const float RetreatDistance = 3f;
    private const float LedgeProbe = 0.8f;
    private const float WaterMargin = 0.5f;
    private const float MaxDrop = 1.5f;
    private const float WrongWeaponChance = 0.25f;
    private const float EasyAimError = 0.12f;
    private const float EasyPowerError = 0.1f;
    private const float HardAimError = 0.025f;
    private const float HardPowerError = 0.02f;
    private const float ClusterChance = 0.3f;
    private const float ClusterScore = 20f;
    private const float GrenadeFallbackScore = 20f;
    private const int ShieldHealth = 30;
    private const float ShieldScore = 15f;
    private const float MinPower = 0.05f;

    private static readonly CraterWeapon[] Arsenal =
    {
        CraterWeapon.Shell, CraterWeapon.Grenade, CraterWeapon.Cluster, CraterWeapon.Drill,
    };

    private BotStep step = BotStep.Done;
    private CraterLevel level;
    private CraterWeapon planWeapon;
    private CraterPlan best = CraterPlan.Worst;
    private CraterShot chosen;
    private float waitSeconds;
    private float walkSeconds;
    private float stuckSeconds;
    private float lastX;
    private int walkDirection;
    private int hops;
    private int candidate;
    private bool walked;
    private bool grenadePlanned;

    private enum BotStep : byte
    {
        Think,
        Walk,
        Plan,
        Aim,
        Charge,
        Done,
    }

    public static CraterShot Candidate(int index, CraterWeapon weapon, int fuse)
    {
        var facing = index < CandidatesPerFacing ? -1 : 1;
        var local = index % CandidatesPerFacing;
        var elevation = FirstElevation + local / PowerSteps * ElevationStep;
        var power = FirstPower + local % PowerSteps * PowerStep;
        return new CraterShot(weapon, facing, elevation, power, fuse);
    }

    public static CraterPlan PlanBest(CraterBoard board, int mover, CraterWeapon weapon)
    {
        var plan = CraterPlan.Worst;
        for (var index = 0; index < CandidateCount; index++)
        {
            Consider(board, mover, Candidate(index, weapon, CraterRules.DefaultFuse), ref plan);
        }

        return plan;
    }

    public static float Score(CraterBoard board, int mover, Vector2 point, ProjectileKind kind, bool landed,
        out bool hits)
    {
        var team = board.Moogle(mover).Team;
        var blast = CraterRules.Blast(kind);
        var moogles = board.Moogles;
        var score = 0f;
        var nearest = float.MaxValue;
        hits = false;
        for (var index = 0; index < moogles.Length; index++)
        {
            ref readonly var moogle = ref moogles[index];
            if (!moogle.Alive)
            {
                continue;
            }

            var distance = Vector2.Distance(moogle.Position, point);
            var enemy = moogle.Team != team;
            if (enemy)
            {
                nearest = MathF.Min(nearest, distance);
            }

            var damage = landed ? CraterRules.BlastDamage(blast, distance) : 0;
            if (damage == 0)
            {
                continue;
            }

            if (moogle.Shielded)
            {
                score += enemy ? ShieldPop : -ShieldPop;
                continue;
            }

            var dealt = Math.Min(damage, moogle.Health);
            var value = dealt + (dealt >= moogle.Health ? KillBonus : 0f);
            if (enemy)
            {
                hits = true;
                score += value;
            }
            else
            {
                score -= value * FriendlyWeight;
            }
        }

        if (!hits && score == 0f && nearest < float.MaxValue)
        {
            score = -nearest * MissWeight;
        }

        return score;
    }

    public void Begin(CraterLevel botLevel, ref GameRandom random)
    {
        level = botLevel;
        step = BotStep.Think;
        waitSeconds = random.Range(MinThinkSeconds, MaxThinkSeconds);
        walked = false;
        grenadePlanned = false;
        hops = 0;
        candidate = 0;
        best = CraterPlan.Worst;
    }

    public void Tick(CraterBoard board, ref GameRandom random, float deltaSeconds)
    {
        switch (step)
        {
            case BotStep.Think:
                waitSeconds -= deltaSeconds;
                if (waitSeconds <= 0f)
                {
                    var chance = level == CraterLevel.Hard ? HardWalkChance : EasyWalkChance;
                    WalkOrPlan(board, ref random, random.Chance(chance));
                }

                return;
            case BotStep.Walk:
                StepWalk(board, deltaSeconds);
                return;
            case BotStep.Plan:
                StepPlan(board, ref random);
                return;
            case BotStep.Aim:
                StepAim(board, deltaSeconds);
                return;
            case BotStep.Charge:
                if (board.Charge >= chosen.Power)
                {
                    board.Fire(chosen.Power);
                    step = BotStep.Done;
                }

                return;
            default:
                return;
        }
    }

    private static void Consider(CraterBoard board, int mover, in CraterShot shot, ref CraterPlan plan)
    {
        var landed = board.Predict(mover, shot, Span<Vector2>.Empty, 1, out _, out var end, out _);
        var score = Score(board, mover, end.Point, CraterRules.KindOf(shot.Weapon), landed, out var hits);
        if (score > plan.Score)
        {
            plan = new CraterPlan(shot, score, end.Point, hits);
        }
    }

    private void WalkOrPlan(CraterBoard board, ref GameRandom random, bool wantsWalk)
    {
        if (!TryWalk(board, ref random, wantsWalk))
        {
            StartPlanning(CraterWeapon.Shell);
        }
    }

    private bool TryWalk(CraterBoard board, ref GameRandom random, bool wantsWalk)
    {
        if (!wantsWalk || walked)
        {
            return false;
        }

        walked = true;
        var direction = WalkDirection(board);
        if (direction == 0)
        {
            return false;
        }

        step = BotStep.Walk;
        walkDirection = direction;
        walkSeconds = random.Range(MinWalkSeconds, MaxWalkSeconds);
        stuckSeconds = 0f;
        lastX = board.Moogle(board.ActiveMoogle).Position.X;
        return true;
    }

    private void StartPlanning(CraterWeapon weapon)
    {
        step = BotStep.Plan;
        planWeapon = weapon;
        candidate = 0;
        if (weapon == CraterWeapon.Shell)
        {
            best = CraterPlan.Worst;
        }
    }

    private static int WalkDirection(CraterBoard board)
    {
        var mover = board.ActiveMoogle;
        ref readonly var me = ref board.Moogle(mover);
        var nearest = float.MaxValue;
        var toward = 0;
        var moogles = board.Moogles;
        for (var index = 0; index < moogles.Length; index++)
        {
            ref readonly var other = ref moogles[index];
            if (!other.Alive || other.Team == me.Team)
            {
                continue;
            }

            var distance = MathF.Abs(other.Position.X - me.Position.X);
            if (distance >= nearest)
            {
                continue;
            }

            nearest = distance;
            toward = other.Position.X >= me.Position.X ? 1 : -1;
        }

        if (toward == 0)
        {
            return 0;
        }

        var direction = nearest < RetreatDistance ? -toward : toward;
        if (SafeAhead(board, direction))
        {
            return direction;
        }

        return SafeAhead(board, -direction) ? -direction : 0;
    }

    private static bool SafeAhead(CraterBoard board, int direction)
    {
        ref readonly var me = ref board.Moogle(board.ActiveMoogle);
        var x = me.Position.X + direction * LedgeProbe;
        if (x < CraterRules.MoogleRadius || x > CraterRules.WorldWidth - CraterRules.MoogleRadius)
        {
            return false;
        }

        var bottom = me.Position.Y + CraterRules.MoogleRadius;
        var top = board.GroundTop(x, bottom - CraterRules.StepUp);
        return top < board.WaterLevel - WaterMargin && top - bottom < MaxDrop;
    }

    private void StepWalk(CraterBoard board, float deltaSeconds)
    {
        ref readonly var me = ref board.Moogle(board.ActiveMoogle);
        walkSeconds -= deltaSeconds;
        if (walkSeconds <= 0f || !SafeAhead(board, walkDirection))
        {
            board.SetWalk(0);
            if (me.Grounded)
            {
                StartPlanning(CraterWeapon.Shell);
            }

            return;
        }

        board.SetWalk(walkDirection);
        if (me.Grounded && MathF.Abs(me.Position.X - lastX) < StuckDistance)
        {
            stuckSeconds += deltaSeconds;
            if (stuckSeconds >= StuckSeconds)
            {
                stuckSeconds = 0f;
                if (hops < MaxHops)
                {
                    board.Jump();
                    hops++;
                }
                else
                {
                    walkSeconds = 0f;
                }
            }
        }
        else
        {
            stuckSeconds = 0f;
        }

        lastX = me.Position.X;
    }

    private void StepPlan(CraterBoard board, ref GameRandom random)
    {
        var mover = board.ActiveMoogle;
        for (var count = 0; count < CandidatesPerTick && candidate < CandidateCount; count++)
        {
            Consider(board, mover, Candidate(candidate, planWeapon, CraterRules.DefaultFuse), ref best);
            candidate++;
        }

        if (candidate < CandidateCount)
        {
            return;
        }

        if (level == CraterLevel.Hard && !grenadePlanned && best.Score < GrenadeFallbackScore)
        {
            grenadePlanned = true;
            StartPlanning(CraterWeapon.Grenade);
            return;
        }

        if (!best.Hits && TryWalk(board, ref random, true))
        {
            return;
        }

        Choose(board, ref random);
    }

    private void Choose(CraterBoard board, ref GameRandom random)
    {
        var team = board.ActiveTeam;
        ref readonly var me = ref board.Moogle(board.ActiveMoogle);
        var shot = best.Shot;
        var weapon = shot.Weapon;
        var fuse = shot.Fuse;
        if (level == CraterLevel.Hard)
        {
            if (me.Health <= ShieldHealth && !me.Shielded && best.Score < ShieldScore &&
                board.Ammo(team, CraterWeapon.Shield) != 0)
            {
                board.SelectWeapon(CraterWeapon.Shield);
                board.BeginCharge();
                step = BotStep.Done;
                return;
            }

            if (weapon == CraterWeapon.Shell && best.Score >= ClusterScore &&
                board.Ammo(team, CraterWeapon.Cluster) != 0 && random.Chance(ClusterChance))
            {
                weapon = CraterWeapon.Cluster;
            }
        }
        else if (random.Chance(WrongWeaponChance))
        {
            weapon = Arsenal[random.Next(Arsenal.Length)];
            fuse = random.Next(CraterRules.MinFuse, CraterRules.MaxFuse + 1);
            if (board.Ammo(team, weapon) == 0)
            {
                weapon = CraterWeapon.Shell;
            }
        }

        var aimError = level == CraterLevel.Hard ? HardAimError : EasyAimError;
        var powerError = level == CraterLevel.Hard ? HardPowerError : EasyPowerError;
        var elevation = Math.Clamp(shot.Elevation + random.Range(-aimError, aimError), CraterRules.MinElevation,
            CraterRules.MaxElevation);
        var power = Math.Clamp(shot.Power + random.Range(-powerError, powerError), MinPower, 1f);
        chosen = new CraterShot(weapon, shot.Facing, elevation, power, fuse);
        board.SelectWeapon(weapon);
        board.SetFuse(fuse);
        board.SetFacing(shot.Facing);
        step = BotStep.Aim;
    }

    private void StepAim(CraterBoard board, float deltaSeconds)
    {
        var current = board.ActiveAim;
        var delta = chosen.Elevation - current;
        var stride = AimSpeed * deltaSeconds;
        if (MathF.Abs(delta) > stride)
        {
            board.SetAim(current + MathF.Sign(delta) * stride);
            return;
        }

        board.SetAim(chosen.Elevation);
        board.BeginCharge();
        step = BotStep.Charge;
    }
}
