using Aetherphone.Apps.Games.Framework.World;

namespace Aetherphone.Apps.Games.Crater;

internal static class CraterFooting
{
    public const float GroundEpsilon = 0.002f;
    private const float CeilingNormal = 0.2f;
    private const float WallNormal = -0.5f;
    private const float ClearanceLift = 0.04f;
    private const float ClearanceShrink = 0.92f;
    private const float TunnelStepShare = 0.5f;

    public static float GroundTop(TerrainMask terrain, float x, float fromY)
    {
        var spread = CraterRules.MoogleRadius * CraterRules.FootSpread;
        var top = terrain.SurfaceY(x, fromY);
        top = MathF.Min(top, terrain.SurfaceY(x - spread, fromY));
        return MathF.Min(top, terrain.SurfaceY(x + spread, fromY));
    }

    public static bool FootBlocked(TerrainMask terrain, float x, float limit)
    {
        var spread = CraterRules.MoogleRadius * CraterRules.FootSpread;
        var probeY = limit - CraterRules.MetresPerCell * 0.5f;
        return terrain.IsSolid(new Vector2(x, probeY)) || terrain.IsSolid(new Vector2(x - spread, probeY)) ||
               terrain.IsSolid(new Vector2(x + spread, probeY));
    }

    public static bool Obstructed(TerrainMask terrain, Vector2 center, int direction)
    {
        var lifted = center - new Vector2(0f, ClearanceLift);
        if (!terrain.CollideCircle(lifted, CraterRules.MoogleRadius * ClearanceShrink, out var normal, out _))
        {
            return false;
        }

        return normal.Y > CeilingNormal || normal.X * direction < WallNormal;
    }

    public static bool TryWalkStep(TerrainMask terrain, Vector2 center, int direction, float waterLevel,
        out Vector2 next)
    {
        next = center;
        var radius = CraterRules.MoogleRadius;
        var nextX = center.X + direction * CraterRules.WalkSpeed * CraterRules.TickSeconds;
        if (nextX < radius || nextX > CraterRules.WorldWidth - radius)
        {
            return false;
        }

        var bottom = center.Y + radius;
        var limit = bottom - CraterRules.StepUp;
        if (FootBlocked(terrain, nextX, limit))
        {
            return false;
        }

        var top = GroundTop(terrain, nextX, limit + GroundEpsilon);
        if (top > bottom + CraterRules.SnapDown)
        {
            return false;
        }

        var candidate = new Vector2(nextX, top - radius);
        if (Obstructed(terrain, candidate, direction) || candidate.Y + radius >= waterLevel)
        {
            return false;
        }

        next = candidate;
        return true;
    }

    public static bool TeleportSpot(TerrainMask terrain, float waterLevel, Vector2 target, out Vector2 destination)
    {
        destination = target;
        var radius = CraterRules.MoogleRadius;
        if (target.X < radius || target.X > CraterRules.WorldWidth - radius || target.Y < 0f ||
            target.Y >= waterLevel || terrain.IsSolid(target))
        {
            return false;
        }

        var top = GroundTop(terrain, target.X, target.Y);
        if (top >= waterLevel - radius)
        {
            return false;
        }

        destination = new Vector2(target.X, top - radius);
        return !terrain.CollideCircle(destination - new Vector2(0f, radius * 0.2f), radius * 0.8f, out _, out _);
    }

    public static void CarveTunnel(TerrainMask terrain, Vector2 from, Vector2 to)
    {
        var offset = to - from;
        var steps = Math.Max(1, (int)MathF.Ceiling(offset.Length() / (CraterRules.TunnelRadius * TunnelStepShare)));
        for (var step = 0; step <= steps; step++)
        {
            terrain.Carve(from + offset * (step / (float)steps), CraterRules.TunnelRadius);
        }
    }
}
