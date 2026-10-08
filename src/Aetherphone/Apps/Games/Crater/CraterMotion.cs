using Aetherphone.Apps.Games.Framework.World;

namespace Aetherphone.Apps.Games.Crater;

internal static class CraterMotion
{
    private const float FloorNormal = -0.55f;
    private const float BodyBounce = 0.25f;
    private const float BodyDrag = 0.96f;
    private const float StillSpeed = 0.1f;
    private const float StillLimitSeconds = 0.4f;
    private const float LandingSlack = 0.05f;
    private const float RestProbe = 0.03f;
    private const float RestNormal = -0.3f;

    public static bool Stride(TerrainMask terrain, ref CraterMoogle moogle, int direction, float deltaSeconds)
    {
        moogle.Facing = direction;
        var radius = CraterRules.MoogleRadius;
        var nextX = moogle.Position.X + direction * CraterRules.WalkSpeed * deltaSeconds;
        if (nextX < radius || nextX > CraterRules.WorldWidth - radius)
        {
            return false;
        }

        var bottom = moogle.Position.Y + radius;
        var limit = bottom - CraterRules.ClimbStep;
        if (CraterFooting.FootBlocked(terrain, nextX, limit))
        {
            return false;
        }

        var top = CraterFooting.GroundTop(terrain, nextX, limit + CraterFooting.GroundEpsilon);
        if (top > bottom + CraterRules.SnapDown)
        {
            moogle.Position = new Vector2(nextX, moogle.Position.Y);
            Unground(ref moogle, new Vector2(direction * CraterRules.WalkSpeed, 0f));
            return true;
        }

        var center = new Vector2(nextX, top - radius);
        if (CraterFooting.HeadObstructed(terrain, center, direction))
        {
            return false;
        }

        moogle.Position = center;
        return true;
    }

    public static void KeepSupported(TerrainMask terrain, ref CraterMoogle moogle)
    {
        var bottom = moogle.Position.Y + CraterRules.MoogleRadius;
        var top = CraterFooting.GroundTop(terrain, moogle.Position.X,
            bottom - CraterRules.StepUp + CraterFooting.GroundEpsilon);
        if (top > bottom + CraterRules.SnapDown)
        {
            if (!Resting(terrain, moogle.Position))
            {
                Unground(ref moogle, Vector2.Zero);
            }

            return;
        }

        if (top > bottom + CraterFooting.GroundEpsilon)
        {
            moogle.Position = new Vector2(moogle.Position.X, top - CraterRules.MoogleRadius);
        }
    }

    public static bool Fly(TerrainMask terrain, ref CraterMoogle moogle, float deltaSeconds)
    {
        moogle.Velocity += new Vector2(0f, CraterRules.Gravity * deltaSeconds);
        moogle.Position += moogle.Velocity * deltaSeconds;
        moogle.ApexY = MathF.Min(moogle.ApexY, moogle.Position.Y);
        KeepInside(ref moogle);
        if (terrain.CollideCircle(moogle.Position, CraterRules.MoogleRadius, out var normal, out var depth))
        {
            moogle.Position += normal * depth;
            if (normal.Y < FloorNormal && TryLand(terrain, ref moogle))
            {
                return true;
            }

            var into = Vector2.Dot(moogle.Velocity, normal);
            if (into < 0f)
            {
                moogle.Velocity -= normal * into * (1f + BodyBounce);
            }

            moogle.Velocity *= BodyDrag;
            KeepInside(ref moogle);
        }

        if (moogle.Velocity.LengthSquared() >= StillSpeed * StillSpeed)
        {
            moogle.StillSeconds = 0f;
            return false;
        }

        moogle.StillSeconds += deltaSeconds;
        if (moogle.StillSeconds >= StillLimitSeconds)
        {
            moogle.Grounded = true;
            moogle.Velocity = Vector2.Zero;
            moogle.StillSeconds = 0f;
        }

        return false;
    }

    public static void Unground(ref CraterMoogle moogle, Vector2 velocity)
    {
        moogle.Grounded = false;
        moogle.Velocity = velocity;
        moogle.ApexY = moogle.Position.Y;
        moogle.StillSeconds = 0f;
    }

    private static bool Resting(TerrainMask terrain, Vector2 position)
    {
        var probe = position + new Vector2(0f, RestProbe);
        return terrain.CollideCircle(probe, CraterRules.MoogleRadius, out var normal, out _) && normal.Y < RestNormal;
    }

    private static void KeepInside(ref CraterMoogle moogle)
    {
        var radius = CraterRules.MoogleRadius;
        var x = Math.Clamp(moogle.Position.X, radius, CraterRules.WorldWidth - radius);
        if (x == moogle.Position.X)
        {
            return;
        }

        moogle.Position = new Vector2(x, moogle.Position.Y);
        moogle.Velocity = new Vector2(0f, moogle.Velocity.Y);
    }

    private static bool TryLand(TerrainMask terrain, ref CraterMoogle moogle)
    {
        var bottom = moogle.Position.Y + CraterRules.MoogleRadius;
        var top = CraterFooting.GroundTop(terrain, moogle.Position.X, bottom - CraterRules.StepUp);
        if (top > bottom + CraterRules.SnapDown + LandingSlack)
        {
            return false;
        }

        moogle.Position = new Vector2(moogle.Position.X, top - CraterRules.MoogleRadius);
        moogle.Grounded = true;
        moogle.Velocity = Vector2.Zero;
        moogle.StillSeconds = 0f;
        return true;
    }
}
