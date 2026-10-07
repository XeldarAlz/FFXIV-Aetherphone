namespace Aetherphone.Apps.Games.Crater;

internal sealed partial class CraterBoard
{
    private const float FloorNormal = -0.55f;
    private const float CeilingNormal = 0.2f;
    private const float WallNormal = -0.5f;
    private const float BodyBounce = 0.25f;
    private const float BodyDrag = 0.96f;
    private const float StillSpeed = 0.1f;
    private const float StillLimitSeconds = 0.4f;
    private const float GroundEpsilon = 0.002f;
    private const float LandingSlack = 0.05f;
    private const float ClearanceLift = 0.04f;
    private const float ClearanceShrink = 0.92f;
    private const float KnockLift = 0.6f;
    private const float KnockClearance = 0.02f;
    private const float HardLanding = 1f;

    public bool BodiesSettled()
    {
        for (var index = 0; index < MoogleCount; index++)
        {
            ref readonly var moogle = ref moogles[index];
            if (!moogle.Sunk && !moogle.Grounded)
            {
                return false;
            }
        }

        return true;
    }

    public float GroundTop(float x, float fromY)
    {
        var spread = CraterRules.MoogleRadius * CraterRules.FootSpread;
        var top = terrain.SurfaceY(x, fromY);
        top = MathF.Min(top, terrain.SurfaceY(x - spread, fromY));
        return MathF.Min(top, terrain.SurfaceY(x + spread, fromY));
    }

    private void StepBodies(float deltaSeconds)
    {
        for (var index = 0; index < MoogleCount; index++)
        {
            StepMoogle(index, deltaSeconds);
        }
    }

    private void StepMoogle(int index, float deltaSeconds)
    {
        ref var moogle = ref moogles[index];
        if (moogle.Sunk)
        {
            return;
        }

        if (moogle.Grounded)
        {
            if (moogle.Alive && index == ActiveMoogle && Phase == CraterPhase.Aiming)
            {
                if (jumpQueued)
                {
                    Leap(index, ref moogle);
                }
                else if (walkIntent != 0 && !Charging)
                {
                    Walk(ref moogle, walkIntent, deltaSeconds);
                }
            }

            if (moogle.Grounded)
            {
                KeepSupported(ref moogle);
            }
        }
        else
        {
            Fly(index, ref moogle, deltaSeconds);
        }

        if (moogle.Position.Y > WaterLevel)
        {
            Drown(index, ref moogle);
        }
    }

    private void Walk(ref CraterMoogle moogle, int direction, float deltaSeconds)
    {
        moogle.Facing = direction;
        var nextX = moogle.Position.X + direction * CraterRules.WalkSpeed * deltaSeconds;
        var bottom = moogle.Position.Y + CraterRules.MoogleRadius;
        var limit = bottom - CraterRules.StepUp;
        if (FootBlocked(nextX, limit))
        {
            return;
        }

        var top = GroundTop(nextX, limit + GroundEpsilon);
        if (top > bottom + CraterRules.SnapDown)
        {
            moogle.Position = new Vector2(nextX, moogle.Position.Y);
            Unground(ref moogle, new Vector2(direction * CraterRules.WalkSpeed, 0f));
            return;
        }

        var center = new Vector2(nextX, top - CraterRules.MoogleRadius);
        if (Obstructed(center, direction))
        {
            return;
        }

        moogle.Position = center;
    }

    private bool FootBlocked(float x, float limit)
    {
        var spread = CraterRules.MoogleRadius * CraterRules.FootSpread;
        var probeY = limit - CraterRules.MetresPerCell * 0.5f;
        return terrain.IsSolid(new Vector2(x, probeY)) || terrain.IsSolid(new Vector2(x - spread, probeY)) ||
               terrain.IsSolid(new Vector2(x + spread, probeY));
    }

    private bool Obstructed(Vector2 center, int direction)
    {
        var lifted = center - new Vector2(0f, ClearanceLift);
        if (!terrain.CollideCircle(lifted, CraterRules.MoogleRadius * ClearanceShrink, out var normal, out _))
        {
            return false;
        }

        return normal.Y > CeilingNormal || normal.X * direction < WallNormal;
    }

    private void KeepSupported(ref CraterMoogle moogle)
    {
        var bottom = moogle.Position.Y + CraterRules.MoogleRadius;
        var top = GroundTop(moogle.Position.X, bottom - CraterRules.StepUp + GroundEpsilon);
        if (top > bottom + CraterRules.SnapDown)
        {
            Unground(ref moogle, Vector2.Zero);
            return;
        }

        if (top > bottom + GroundEpsilon)
        {
            moogle.Position = new Vector2(moogle.Position.X, top - CraterRules.MoogleRadius);
        }
    }

    private void Leap(int index, ref CraterMoogle moogle)
    {
        Unground(ref moogle, new Vector2(moogle.Facing * CraterRules.JumpSpeedX, -CraterRules.JumpSpeedY));
        Push(CraterEventKind.Jumped, moogle.Position, Vector2.Zero, 0f, 0, index, moogle.Team, ProjectileKind.Shell);
    }

    private static void Unground(ref CraterMoogle moogle, Vector2 velocity)
    {
        moogle.Grounded = false;
        moogle.Velocity = velocity;
        moogle.ApexY = moogle.Position.Y;
        moogle.StillSeconds = 0f;
    }

    private void Fly(int index, ref CraterMoogle moogle, float deltaSeconds)
    {
        moogle.Velocity += new Vector2(0f, CraterRules.Gravity * deltaSeconds);
        moogle.Position += moogle.Velocity * deltaSeconds;
        moogle.ApexY = MathF.Min(moogle.ApexY, moogle.Position.Y);
        if (terrain.CollideCircle(moogle.Position, CraterRules.MoogleRadius, out var normal, out var depth))
        {
            moogle.Position += normal * depth;
            if (normal.Y < FloorNormal && TryLand(index, ref moogle))
            {
                return;
            }

            var into = Vector2.Dot(moogle.Velocity, normal);
            if (into < 0f)
            {
                moogle.Velocity -= normal * into * (1f + BodyBounce);
            }

            moogle.Velocity *= BodyDrag;
        }

        if (moogle.Velocity.LengthSquared() >= StillSpeed * StillSpeed)
        {
            moogle.StillSeconds = 0f;
            return;
        }

        moogle.StillSeconds += deltaSeconds;
        if (moogle.StillSeconds >= StillLimitSeconds)
        {
            moogle.Grounded = true;
            moogle.Velocity = Vector2.Zero;
            moogle.StillSeconds = 0f;
        }
    }

    private bool TryLand(int index, ref CraterMoogle moogle)
    {
        var bottom = moogle.Position.Y + CraterRules.MoogleRadius;
        var top = GroundTop(moogle.Position.X, bottom - CraterRules.StepUp);
        if (top > bottom + CraterRules.SnapDown + LandingSlack)
        {
            return false;
        }

        moogle.Position = new Vector2(moogle.Position.X, top - CraterRules.MoogleRadius);
        var fall = moogle.Position.Y - moogle.ApexY;
        moogle.Grounded = true;
        moogle.Velocity = Vector2.Zero;
        moogle.StillSeconds = 0f;
        if (!moogle.Alive)
        {
            return true;
        }

        var damage = CraterRules.FallDamage(fall);
        if (damage > 0)
        {
            Push(CraterEventKind.FallHurt, moogle.Position, Vector2.Zero, fall, damage, index, moogle.Team,
                ProjectileKind.Shell);
            ApplyDamage(index, damage, lastAttackers[index]);
            return true;
        }

        if (fall >= HardLanding)
        {
            Push(CraterEventKind.Landed, moogle.Position, Vector2.Zero, fall, 0, index, moogle.Team,
                ProjectileKind.Shell);
        }

        return true;
    }

    private void Drown(int index, ref CraterMoogle moogle)
    {
        var surface = new Vector2(moogle.Position.X, WaterLevel);
        moogle.Sunk = true;
        moogle.Grounded = true;
        moogle.Velocity = Vector2.Zero;
        moogle.Shielded = false;
        if (!moogle.Alive)
        {
            Push(CraterEventKind.Splashed, surface, Vector2.Zero, 0f, 0, index, moogle.Team, ProjectileKind.Shell);
            return;
        }

        moogle.Alive = false;
        moogle.Health = 0;
        Credit(index, moogle.Team, lastAttackers[index]);
        Push(CraterEventKind.Drowned, surface, Vector2.Zero, 0f, 0, index, moogle.Team, ProjectileKind.Shell);
    }

    private void ApplyDamage(int index, int amount, int attacker)
    {
        ref var moogle = ref moogles[index];
        if (!moogle.Alive || amount <= 0)
        {
            return;
        }

        var dealt = Math.Min(amount, moogle.Health);
        moogle.Health -= dealt;
        if (attacker != NoTeam && attacker != moogle.Team)
        {
            damageDealt[attacker] += dealt;
        }

        Push(CraterEventKind.Damaged, moogle.Position, Vector2.Zero, 0f, amount, index, moogle.Team,
            ProjectileKind.Shell);
        if (moogle.Health > 0)
        {
            return;
        }

        moogle.Alive = false;
        moogle.Shielded = false;
        Credit(index, moogle.Team, attacker);
        Push(CraterEventKind.Died, moogle.Position, Vector2.Zero, 0f, 0, index, moogle.Team, ProjectileKind.Shell);
    }

    private void Credit(int index, int team, int attacker)
    {
        lastAttackers[index] = NoTeam;
        if (attacker != NoTeam && attacker != team)
        {
            knockouts[attacker]++;
        }
    }

    private static void Knock(ref CraterMoogle moogle, Vector2 offset, float distance, float strength)
    {
        var direction = distance > 0.01f ? offset / distance : new Vector2(0f, -1f);
        var lifted = direction + new Vector2(0f, -KnockLift);
        var push = lifted.LengthSquared() > 0.0001f ? Vector2.Normalize(lifted) * strength : Vector2.Zero;
        moogle.Position -= new Vector2(0f, KnockClearance);
        Unground(ref moogle, moogle.Velocity + push);
    }
}
