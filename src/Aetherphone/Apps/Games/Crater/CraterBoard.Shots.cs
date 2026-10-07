using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Crater;

internal sealed partial class CraterBoard
{
    public const int NoOwner = -1;
    private const float BombletLift = 0.08f;
    private const float BounceEventSpeed = 1.5f;
    private const float GrenadeNudgeReach = 1.5f;
    private const float GrenadeNudgeShare = 0.5f;
    private const float GrenadeNudgeLift = 0.5f;
    private static readonly Vector2 Up = new(0f, -1f);

    public bool Predict(int moogle, in CraterShot shot, Span<Vector2> path, int stride, out int pathCount,
        out FlightStep end, out float seconds) =>
        CraterBallistics.Predict(MakeProjectile(moogle, shot), terrain, Wind, WaterLevel, Moogles, path, stride,
            out pathCount, out end, out seconds);

    private void StepProjectiles(float deltaSeconds)
    {
        for (var index = 0; index < MaxProjectiles; index++)
        {
            ref var projectile = ref projectiles[index];
            if (!projectile.Alive)
            {
                continue;
            }

            if (projectile.Drilling)
            {
                StepDrill(ref projectile, deltaSeconds);
                continue;
            }

            var step = CraterBallistics.Advance(ref projectile, terrain, Wind, WaterLevel, Moogles, deltaSeconds);
            switch (step.Outcome)
            {
                case FlightOutcome.Flying:
                    if (step.BounceSpeed >= BounceEventSpeed)
                    {
                        Push(CraterEventKind.Bounced, step.Point, step.Normal, 0f, (int)step.BounceSpeed, step.Moogle,
                            projectile.Team, projectile.Kind);
                    }

                    break;
                case FlightOutcome.Fused:
                    Detonate(ref projectile, projectile.Position);
                    break;
                case FlightOutcome.Impact:
                    Strike(ref projectile, step);
                    break;
                case FlightOutcome.Splashed:
                    projectile.Alive = false;
                    Push(CraterEventKind.Splashed, step.Point, Vector2.Zero, 0f, 0, CraterEvent.None, projectile.Team,
                        projectile.Kind);
                    break;
                default:
                    projectile.Alive = false;
                    break;
            }
        }
    }

    private void Strike(ref CraterProjectile projectile, in FlightStep step)
    {
        var kind = projectile.Kind;
        var team = projectile.Team;
        if (kind == ProjectileKind.Drill && step.Moogle == CraterEvent.None)
        {
            StartDrill(ref projectile, step.Point);
            return;
        }

        projectile.Alive = false;
        Explode(step.Point, CraterRules.Blast(kind), team, kind);
        if (kind == ProjectileKind.Cluster)
        {
            SplitCluster(step.Point, step.Normal, team);
        }
    }

    private void SplitCluster(Vector2 point, Vector2 normal, int team)
    {
        var up = normal.Y < 0f ? normal : Up;
        Push(CraterEventKind.ClusterSplit, point, up, 0f, CraterRules.BombletCount, CraterEvent.None, team,
            ProjectileKind.Cluster);
        for (var piece = 0; piece < CraterRules.BombletCount; piece++)
        {
            var angle = (piece - (CraterRules.BombletCount - 1) * 0.5f) * CraterRules.BombletSpread;
            var direction = Rotate(up, angle);
            Spawn(new CraterProjectile
            {
                Position = point + up * BombletLift,
                Velocity = direction * CraterRules.BombletSpeed,
                Kind = ProjectileKind.Bomblet,
                Alive = true,
                Owner = NoOwner,
                Team = team,
            });
        }
    }

    private void StartDrill(ref CraterProjectile projectile, Vector2 point)
    {
        var heading = projectile.Velocity.LengthSquared() > 0.0001f ? Vector2.Normalize(projectile.Velocity) : -Up;
        projectile.Position = point;
        projectile.Velocity = heading * CraterRules.DrillSpeed;
        projectile.Drilling = true;
        projectile.DrillLeft = CraterRules.DrillSeconds;
        projectile.DrillCarve = CraterRules.DrillCarveSeconds;
        projectile.DrillFrom = point;
        terrain.Carve(point, CraterRules.TunnelRadius);
        Push(CraterEventKind.DrillStarted, point, heading, CraterRules.TunnelRadius, 0, CraterEvent.None,
            projectile.Team, ProjectileKind.Drill);
    }

    private void StepDrill(ref CraterProjectile projectile, float deltaSeconds)
    {
        projectile.Age += deltaSeconds;
        projectile.DrillLeft -= deltaSeconds;
        projectile.DrillCarve -= deltaSeconds;
        var start = projectile.Position;
        var travel = projectile.Velocity * deltaSeconds;
        var reach = CraterRules.MoogleRadius + CraterRules.ProjectileRadius(ProjectileKind.Drill);
        for (var index = 0; index < MoogleCount; index++)
        {
            ref readonly var moogle = ref moogles[index];
            if (!moogle.Alive || !Geometry2D.SweepCircle(start, travel, moogle.Position, reach, out var along))
            {
                continue;
            }

            var hit = start + travel * along;
            CarveTunnel(projectile.DrillFrom, hit);
            Detonate(ref projectile, hit);
            return;
        }

        projectile.Position = start + travel;
        if (projectile.Position.Y >= WaterLevel)
        {
            CarveTunnel(projectile.DrillFrom, projectile.Position);
            projectile.Alive = false;
            Push(CraterEventKind.Splashed, new Vector2(projectile.Position.X, WaterLevel), Vector2.Zero, 0f, 0,
                CraterEvent.None, projectile.Team, ProjectileKind.Drill);
            return;
        }

        if (projectile.DrillLeft <= 0f)
        {
            CarveTunnel(projectile.DrillFrom, projectile.Position);
            Detonate(ref projectile, projectile.Position);
            return;
        }

        if (projectile.DrillCarve > 0f)
        {
            return;
        }

        CarveTunnel(projectile.DrillFrom, projectile.Position);
        projectile.DrillFrom = projectile.Position;
        projectile.DrillCarve += CraterRules.DrillCarveSeconds;
    }

    private void CarveTunnel(Vector2 from, Vector2 to) => CraterFooting.CarveTunnel(terrain, from, to);

    private void Detonate(ref CraterProjectile projectile, Vector2 point)
    {
        var kind = projectile.Kind;
        var team = projectile.Team;
        projectile.Alive = false;
        Explode(point, CraterRules.Blast(kind), team, kind);
    }

    private void Explode(Vector2 center, in BlastSpec blast, int team, ProjectileKind kind)
    {
        terrain.Carve(center, blast.Radius);
        if (craterCount < MaxCraters)
        {
            craters[craterCount++] = new CraterMark(center, blast.Radius);
        }

        LastBlast = center;
        SinceBlast = 0f;
        Push(CraterEventKind.Exploded, center, Vector2.Zero, blast.Radius, blast.Damage, CraterEvent.None, team, kind);
        for (var index = 0; index < MoogleCount; index++)
        {
            ref var moogle = ref moogles[index];
            if (moogle.Sunk)
            {
                continue;
            }

            var offset = moogle.Position - center;
            var distance = offset.Length();
            var factor = CraterRules.BlastFactor(blast, distance);
            if (factor <= 0f)
            {
                continue;
            }

            if (!moogle.Alive)
            {
                Knock(ref moogle, offset, distance, blast.Knockback * factor);
                continue;
            }

            if (moogle.Shielded)
            {
                moogle.Shielded = false;
                Push(CraterEventKind.Shielded, moogle.Position, Vector2.Zero, 0f, 0, index, moogle.Team, kind);
                continue;
            }

            lastAttackers[index] = team;
            Knock(ref moogle, offset, distance, blast.Knockback * factor);
            ApplyDamage(index, CraterRules.BlastDamage(blast, distance), team);
        }

        NudgeGrenades(center, blast);
    }

    private void NudgeGrenades(Vector2 center, in BlastSpec blast)
    {
        var reach = blast.Radius * GrenadeNudgeReach;
        for (var index = 0; index < MaxProjectiles; index++)
        {
            ref var projectile = ref projectiles[index];
            if (!projectile.Alive || projectile.Kind != ProjectileKind.Grenade)
            {
                continue;
            }

            var offset = projectile.Position - center;
            var distance = offset.Length();
            if (distance >= reach)
            {
                continue;
            }

            var away = distance > 0.01f ? offset / distance : Up;
            var strength = blast.Knockback * GrenadeNudgeShare * (1f - distance / reach);
            projectile.Resting = false;
            projectile.Velocity += Vector2.Normalize(away + Up * GrenadeNudgeLift) * strength;
        }
    }

    private void Spawn(in CraterProjectile projectile)
    {
        for (var index = 0; index < MaxProjectiles; index++)
        {
            if (projectiles[index].Alive)
            {
                continue;
            }

            projectiles[index] = projectile;
            projectiles[index].Id = nextProjectileId++;
            projectiles[index].DrillFrom = projectile.Position;
            return;
        }
    }

    private static Vector2 Rotate(Vector2 vector, float angle)
    {
        var cosine = MathF.Cos(angle);
        var sine = MathF.Sin(angle);
        return new Vector2(vector.X * cosine - vector.Y * sine, vector.X * sine + vector.Y * cosine);
    }
}
