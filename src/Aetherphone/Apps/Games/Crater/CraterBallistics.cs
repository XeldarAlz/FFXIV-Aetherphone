using Aetherphone.Apps.Games.Framework.World;

namespace Aetherphone.Apps.Games.Crater;

internal static class CraterBallistics
{
    private const float Epsilon = 1e-6f;
    private const float BounceNudge = 0.01f;
    private const float RestNormal = -0.6f;
    private const float SupportProbe = 0.03f;
    private static readonly Vector2 Up = new(0f, -1f);

    public static FlightStep Advance(ref CraterProjectile projectile, TerrainMask terrain, float wind, float waterLevel,
        ReadOnlySpan<CraterMoogle> moogles, float deltaSeconds)
    {
        projectile.Age += deltaSeconds;
        var radius = CraterRules.ProjectileRadius(projectile.Kind);
        var grenade = projectile.Kind == ProjectileKind.Grenade;
        if (grenade)
        {
            projectile.Fuse -= deltaSeconds;
            if (projectile.Fuse <= 0f)
            {
                return new FlightStep(FlightOutcome.Fused, projectile.Position, Up, CraterEvent.None, 0f);
            }

            if (projectile.Resting)
            {
                if (terrain.IsSolid(projectile.Position + new Vector2(0f, radius + SupportProbe)))
                {
                    return new FlightStep(FlightOutcome.Flying, projectile.Position, Up, CraterEvent.None, 0f);
                }

                projectile.Resting = false;
            }
        }

        var pull = new Vector2(wind * CraterRules.WindFactor(projectile.Kind), CraterRules.Gravity);
        projectile.Velocity += pull * deltaSeconds;
        var start = projectile.Position;
        var travel = projectile.Velocity * deltaSeconds;
        var length = travel.Length();
        var fraction = 2f;
        var normal = Up;
        var hitMoogle = CraterEvent.None;
        if (length > Epsilon)
        {
            if (terrain.Raycast(start, travel, length, out var terrainHit))
            {
                fraction = terrainHit.Distance / length;
                normal = terrainHit.Normal;
            }

            for (var index = 0; index < moogles.Length; index++)
            {
                ref readonly var moogle = ref moogles[index];
                if (!moogle.Alive || (index == projectile.Owner && projectile.Age < CraterRules.OwnerGraceSeconds))
                {
                    continue;
                }

                if (!SegmentCircle(start, travel, moogle.Position, CraterRules.MoogleRadius + radius, out var along) ||
                    along >= fraction)
                {
                    continue;
                }

                fraction = along;
                hitMoogle = index;
                var contact = start + travel * along - moogle.Position;
                normal = contact.LengthSquared() > Epsilon ? Vector2.Normalize(contact) : Up;
            }
        }

        if (fraction > 1f)
        {
            projectile.Position = start + travel;
            return Boundary(projectile.Position, waterLevel);
        }

        var point = start + travel * fraction;
        if (!grenade)
        {
            projectile.Position = point;
            return new FlightStep(FlightOutcome.Impact, point, normal, hitMoogle, 0f);
        }

        return Bounce(ref projectile, point, normal, radius, hitMoogle);
    }

    public static bool SegmentCircle(Vector2 start, Vector2 travel, Vector2 center, float radius, out float along)
    {
        along = 0f;
        var offset = start - center;
        var outside = offset.LengthSquared() - radius * radius;
        if (outside <= 0f)
        {
            return true;
        }

        var lengthSquared = travel.LengthSquared();
        var approach = Vector2.Dot(offset, travel);
        if (approach >= 0f || lengthSquared <= Epsilon)
        {
            return false;
        }

        var discriminant = approach * approach - lengthSquared * outside;
        if (discriminant < 0f)
        {
            return false;
        }

        along = (-approach - MathF.Sqrt(discriminant)) / lengthSquared;
        return along <= 1f;
    }

    private static FlightStep Bounce(ref CraterProjectile projectile, Vector2 point, Vector2 normal, float radius,
        int hitMoogle)
    {
        var into = Vector2.Dot(projectile.Velocity, normal);
        var bounceSpeed = MathF.Max(0f, -into);
        if (into < 0f)
        {
            var reflected = projectile.Velocity - (1f + CraterRules.GrenadeRestitution) * into * normal;
            var normalPart = Vector2.Dot(reflected, normal) * normal;
            projectile.Velocity = normalPart + (reflected - normalPart) * CraterRules.GrenadeFriction;
        }

        projectile.Position = point + normal * (radius + BounceNudge);
        var restSpeed = CraterRules.GrenadeRestSpeed;
        if (normal.Y < RestNormal && projectile.Velocity.LengthSquared() < restSpeed * restSpeed)
        {
            projectile.Velocity = Vector2.Zero;
            projectile.Resting = true;
        }

        return new FlightStep(FlightOutcome.Flying, projectile.Position, normal, hitMoogle, bounceSpeed);
    }

    private static FlightStep Boundary(Vector2 position, float waterLevel)
    {
        if (position.Y >= waterLevel)
        {
            return new FlightStep(FlightOutcome.Splashed, new Vector2(position.X, waterLevel), Up, CraterEvent.None, 0f);
        }

        if (position.X < -CraterRules.LostMargin || position.X > CraterRules.WorldWidth + CraterRules.LostMargin ||
            position.Y < CraterRules.SkyLimit)
        {
            return new FlightStep(FlightOutcome.Lost, position, Up, CraterEvent.None, 0f);
        }

        return new FlightStep(FlightOutcome.Flying, position, Up, CraterEvent.None, 0f);
    }
}
