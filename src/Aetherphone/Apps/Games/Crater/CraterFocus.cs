using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Crater;

internal static class CraterFocus
{
    private const float EdgeSlack = 1f;
    private const float SkyCeiling = -3f;
    private const float FlightCeiling = -30f;
    private const float BelowWater = 2.4f;
    private const float ProjectileLeadSeconds = 0.22f;
    private const float MaxLead = 3f;
    private const float AimLead = 1.4f;
    private const float FollowSmoothSeconds = 0.35f;
    private const float ProjectileSmoothSeconds = 0.16f;
    private const float ImpactSmoothSeconds = 0.25f;
    private const float MovingSpeed = 0.6f;

    public static Vector2 Goal(ReadOnlySpan<CraterProjectile> projectiles, ReadOnlySpan<CraterMoogle> moogles,
        float sinceBlast, Vector2 lastBlast, int activeMoogle, bool aiming, float activeAim, out float smoothSeconds,
        out bool flying)
    {
        flying = false;
        var lead = -1;
        var lowestId = int.MaxValue;
        for (var index = 0; index < projectiles.Length; index++)
        {
            if (projectiles[index].Alive && projectiles[index].Id < lowestId)
            {
                lowestId = projectiles[index].Id;
                lead = index;
            }
        }

        if (lead >= 0)
        {
            flying = true;
            smoothSeconds = ProjectileSmoothSeconds;
            ref readonly var projectile = ref projectiles[lead];
            var ahead = projectile.Velocity * ProjectileLeadSeconds;
            if (ahead.LengthSquared() > MaxLead * MaxLead)
            {
                ahead = Vector2.Normalize(ahead) * MaxLead;
            }

            return projectile.Position + ahead;
        }

        smoothSeconds = ImpactSmoothSeconds;
        if (sinceBlast < CraterRules.ImpactHoldSeconds)
        {
            return lastBlast;
        }

        var mover = FastestMover(moogles);
        if (mover >= 0)
        {
            return moogles[mover].Position;
        }

        smoothSeconds = FollowSmoothSeconds;
        if (activeMoogle < 0 || activeMoogle >= moogles.Length)
        {
            return new Vector2(CraterRules.WorldWidth * 0.5f, CraterRules.WorldHeight * 0.5f);
        }

        ref readonly var active = ref moogles[activeMoogle];
        if (!aiming || !active.Alive)
        {
            return active.Position;
        }

        return active.Position + CraterRules.AimDirection(activeAim, active.Facing) * AimLead;
    }

    public static Vector2 KeepInView(in Camera2D camera, Vector2 goal, bool flying, float waterLevel)
    {
        var zoomPixels = MathF.Max(0.0001f, camera.Zoom);
        var halfWidth = camera.View.Width / zoomPixels * 0.5f;
        var halfHeight = camera.View.Height / zoomPixels * 0.5f;
        var minX = halfWidth - EdgeSlack;
        var maxX = CraterRules.WorldWidth - halfWidth + EdgeSlack;
        var x = minX > maxX ? CraterRules.WorldWidth * 0.5f : Math.Clamp(goal.X, minX, maxX);
        var minY = (flying ? FlightCeiling : SkyCeiling) + halfHeight;
        var maxY = waterLevel + BelowWater - halfHeight;
        var y = minY > maxY ? maxY : Math.Clamp(goal.Y, minY, maxY);
        return new Vector2(x, y);
    }

    private static int FastestMover(ReadOnlySpan<CraterMoogle> moogles)
    {
        var fastest = -1;
        var best = MovingSpeed * MovingSpeed;
        for (var index = 0; index < moogles.Length; index++)
        {
            ref readonly var moogle = ref moogles[index];
            if (moogle.Sunk || moogle.Grounded)
            {
                continue;
            }

            var speed = moogle.Velocity.LengthSquared();
            if (speed <= best)
            {
                continue;
            }

            best = speed;
            fastest = index;
        }

        return fastest;
    }
}
