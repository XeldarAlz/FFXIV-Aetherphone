namespace Aetherphone.Apps.Games.Thrust;

internal static class ThrustAutopilot
{
    private const float Horizon = 1.1f;
    private const float SimulationStep = 1f / 30f;
    private const float CandidateStep = 0.35f;
    private const float EdgeKeep = 0.6f;
    private const float Lead = 0.16f;
    private const float Deadband = 0.08f;
    private const float SafetyMargin = 0.14f;
    private const float SurvivalWeight = 100f;
    private const float MoveWeight = 0.6f;
    private const float CenterWeight = 0.15f;
    private const float CoinWeight = 1.2f;
    private const float AlertCenterWeight = 1.2f;
    private const float MountedJumpRise = 1.4f;
    private const float MountedFallSpeed = 1.5f;

    public static bool Hold(ThrustBoard board, out bool tap)
    {
        tap = false;
        if (board.State != ThrustState.Running)
        {
            return false;
        }

        var target = Plan(board);
        if (board.Mounted)
        {
            tap = target < board.Y - MountedJumpRise && (board.Grounded || board.VelocityY > MountedFallSpeed);
            return false;
        }

        return Holds(board.Y, board.VelocityY, target);
    }

    private static bool Holds(float y, float velocity, float target) => y + velocity * Lead > target + Deadband;

    private static float Plan(ThrustBoard board)
    {
        var best = board.Y;
        var bestScore = float.MinValue;
        var alert = board.MissileCount > 0;
        var coinWeight = alert ? 0f : CoinWeight;
        var centerWeight = alert ? AlertCenterWeight : CenterWeight;
        for (var candidate = EdgeKeep; candidate <= ThrustBoard.Height - EdgeKeep + 0.001f; candidate += CandidateStep)
        {
            var survived = Survive(board, candidate, out var coins);
            var score = survived * SurvivalWeight + coins * coinWeight - MathF.Abs(candidate - board.Y) * MoveWeight -
                        MathF.Abs(candidate - ThrustBoard.Height * 0.5f) * centerWeight;
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            best = candidate;
        }

        return best;
    }

    private static float Survive(ThrustBoard board, float target, out int coins)
    {
        coins = 0;
        var y = board.Y;
        var velocity = board.VelocityY;
        var x = board.X;
        var speed = board.Speed;
        var radius = board.Radius;
        var coinReach = radius + ThrustGenerator.CoinRadius;
        var nextCoin = 0;
        for (var elapsed = SimulationStep; elapsed <= Horizon; elapsed += SimulationStep)
        {
            var acceleration = Holds(y, velocity, target) ? ThrustBoard.Gravity - ThrustBoard.ThrustAccel : ThrustBoard.Gravity;
            velocity = Math.Clamp(velocity + acceleration * SimulationStep, -ThrustBoard.MaxRise, ThrustBoard.MaxFall);
            y += velocity * SimulationStep;
            if (y < radius)
            {
                y = radius;
                velocity = MathF.Max(0f, velocity);
            }

            if (y > ThrustBoard.Height - radius)
            {
                y = ThrustBoard.Height - radius;
                velocity = MathF.Min(0f, velocity);
            }

            x += speed * SimulationStep;
            if (Collides(board, new Vector2(x, y), elapsed, radius))
            {
                return elapsed;
            }

            while (nextCoin < board.CoinCount)
            {
                var coin = board.CoinAt(nextCoin);
                if (coin.X > x + coinReach)
                {
                    break;
                }

                if (coin.X < x - coinReach)
                {
                    nextCoin++;
                    continue;
                }

                if (Vector2.DistanceSquared(coin, new Vector2(x, y)) > coinReach * coinReach)
                {
                    break;
                }

                coins++;
                nextCoin++;
            }
        }

        return Horizon;
    }

    private static bool Collides(ThrustBoard board, Vector2 point, float elapsed, float radius)
    {
        var zapperReach = ThrustGenerator.ZapperRadius + radius + SafetyMargin;
        var time = board.Time + elapsed;
        for (var index = 0; index < board.ZapperCount; index++)
        {
            ref readonly var zapper = ref board.ZapperAt(index);
            if (zapper.Right < point.X - zapperReach || zapper.Left > point.X + zapperReach)
            {
                continue;
            }

            if (ThrustGenerator.SegmentDistance(point, zapper, time) <= zapperReach)
            {
                return true;
            }
        }

        var missileReach = ThrustBoard.MissileRadius + radius + SafetyMargin;
        for (var index = 0; index < board.MissileCount; index++)
        {
            ref readonly var missile = ref board.MissileAt(index);
            if (missile.Phase == MissilePhase.Warning)
            {
                continue;
            }

            var flying = missile.Phase == MissilePhase.Flying ? elapsed : elapsed - (ThrustBoard.LockSeconds - missile.Timer);
            if (flying < 0f)
            {
                continue;
            }

            var launchX = missile.Phase == MissilePhase.Flying
                ? missile.X
                : board.X + board.Speed * (ThrustBoard.LockSeconds - missile.Timer) + ThrustBoard.LaunchAhead;
            var missileX = launchX - ThrustBoard.MissileSpeed * flying;
            if (Vector2.DistanceSquared(new Vector2(missileX, missile.Y), point) <= missileReach * missileReach)
            {
                return true;
            }
        }

        return false;
    }
}
