namespace Aetherphone.Apps.Games.Crater;

internal sealed partial class CraterBoard
{
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

    public float GroundTop(float x, float fromY) => CraterFooting.GroundTop(terrain, x, fromY);

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
                    CraterMotion.Stride(terrain, ref moogle, walkIntent, deltaSeconds);
                }
            }

            if (moogle.Grounded)
            {
                CraterMotion.KeepSupported(terrain, ref moogle);
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

    private void Leap(int index, ref CraterMoogle moogle)
    {
        var leap = new Vector2(moogle.Facing * CraterRules.JumpSpeedX, -CraterRules.JumpSpeedY);
        CraterMotion.Unground(ref moogle, leap);
        Push(CraterEventKind.Jumped, moogle.Position, Vector2.Zero, 0f, 0, index, moogle.Team, ProjectileKind.Shell);
    }

    private void Fly(int index, ref CraterMoogle moogle, float deltaSeconds)
    {
        if (!CraterMotion.Fly(terrain, ref moogle, deltaSeconds) || !moogle.Alive)
        {
            return;
        }

        var fall = moogle.Position.Y - moogle.ApexY;
        var damage = CraterRules.FallDamage(fall);
        if (damage > 0)
        {
            Push(CraterEventKind.FallHurt, moogle.Position, Vector2.Zero, fall, damage, index, moogle.Team,
                ProjectileKind.Shell);
            ApplyDamage(index, damage, lastAttackers[index]);
            return;
        }

        if (fall >= HardLanding)
        {
            Push(CraterEventKind.Landed, moogle.Position, Vector2.Zero, fall, 0, index, moogle.Team,
                ProjectileKind.Shell);
        }
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
        CraterMotion.Unground(ref moogle, moogle.Velocity + push);
    }
}
