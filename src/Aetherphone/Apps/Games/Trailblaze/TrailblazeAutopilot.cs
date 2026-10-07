namespace Aetherphone.Apps.Games.Trailblaze;

internal static class TrailblazeAutopilot
{
    private const float CartLookSeconds = 0.6f;
    private const float CartLookPadding = 3f;
    private const float ClearBehind = TrailblazeBoard.BodyHalfDepth + 0.2f;
    private const float Hysteresis = 1f;
    private const float JumpSlack = 0.3f;
    private const float SlideLeadSeconds = 0.14f;
    private const float SlideLeadPadding = 0.5f;
    private const float SwitchSeconds = 0.3f;
    private const float CrossSeconds = 0.25f;

    public static void Drive(TrailblazeBoard board)
    {
        if (board.State != TrailblazeState.Running || board.Flying)
        {
            return;
        }

        var lane = board.TargetLane;
        var look = board.Speed * CartLookSeconds + CartLookPadding;
        if (FirstCart(board, lane) < board.Distance + look && Dodge(board, lane))
        {
            return;
        }

        if (React(board, lane))
        {
            return;
        }

        var current = (int)MathF.Round(board.LaneX);
        if (current != lane)
        {
            React(board, current);
        }
    }

    private static bool Dodge(TrailblazeBoard board, int lane)
    {
        var bestLane = lane;
        var bestReach = FirstCart(board, lane);
        for (var offset = 1; offset < TrailblazeBoard.LaneCount; offset++)
        {
            for (var side = -1; side <= 1; side += 2)
            {
                var candidate = lane + offset * side;
                if (candidate < 0 || candidate >= TrailblazeBoard.LaneCount || !PathClear(board, lane, candidate))
                {
                    continue;
                }

                var reach = FirstCart(board, candidate);
                if (reach > bestReach + Hysteresis)
                {
                    bestLane = candidate;
                    bestReach = reach;
                }
            }
        }

        if (bestLane == lane)
        {
            return false;
        }

        board.Move(bestLane < lane ? TrailblazeMove.Left : TrailblazeMove.Right);
        return true;
    }

    private static bool PathClear(TrailblazeBoard board, int lane, int candidate)
    {
        var step = Math.Sign(candidate - lane);
        if (ObstacleBeside(board, lane + step, board.Distance + board.Speed * SwitchSeconds + 1f))
        {
            return false;
        }

        for (var through = lane + step; through != candidate; through += step)
        {
            if (FirstCart(board, through) < board.Distance + board.Speed * CrossSeconds + TrailblazeBoard.BodyHalfDepth)
            {
                return false;
            }
        }

        return true;
    }

    private static bool React(TrailblazeBoard board, int lane)
    {
        for (var index = 0; index < board.HazardCount; index++)
        {
            ref readonly var hazard = ref board.HazardAt(index);
            if (hazard.Lane != lane || hazard.Kind == TrailblazeCell.Cart ||
                hazard.Z + hazard.Length < board.Distance - TrailblazeBoard.BodyHalfDepth)
            {
                continue;
            }

            return hazard.Kind == TrailblazeCell.Gap ? ReactToGap(board, hazard) : ReactToBarrier(board, hazard);
        }

        return false;
    }

    private static bool ReactToGap(TrailblazeBoard board, in TrailblazeHazard gap)
    {
        if (board.Airborne || gap.Z + TrailblazeBoard.GapMargin < board.Distance)
        {
            return false;
        }

        var lead = (board.Speed * TrailblazeBoard.AirtimeAt(board.Speed) - gap.Length) * 0.5f + JumpSlack;
        if (gap.Z - board.Distance > lead)
        {
            return false;
        }

        board.Move(TrailblazeMove.Jump);
        return true;
    }

    private static bool ReactToBarrier(TrailblazeBoard board, in TrailblazeHazard barrier)
    {
        var clearAt = barrier.Z + barrier.Length + TrailblazeBoard.BodyHalfDepth;
        if (board.Sliding && board.Distance + board.Speed * board.SlideLeft > clearAt + SlideLeadPadding)
        {
            return false;
        }

        var lead = board.Speed * SlideLeadSeconds + SlideLeadPadding;
        if (barrier.Z - (board.Distance + TrailblazeBoard.BodyHalfDepth) > lead)
        {
            return false;
        }

        board.Move(TrailblazeMove.Slide);
        return true;
    }

    private static bool ObstacleBeside(TrailblazeBoard board, int lane, float until)
    {
        var from = board.Distance - TrailblazeBoard.BodyHalfDepth - 0.5f;
        for (var index = 0; index < board.HazardCount; index++)
        {
            ref readonly var hazard = ref board.HazardAt(index);
            if (hazard.Z > until)
            {
                return false;
            }

            if (hazard.Lane == lane && hazard.Kind != TrailblazeCell.Cart && hazard.Z + hazard.Length > from)
            {
                return true;
            }
        }

        return false;
    }

    private static float FirstCart(TrailblazeBoard board, int lane)
    {
        var from = board.Distance - ClearBehind;
        for (var index = 0; index < board.HazardCount; index++)
        {
            ref readonly var hazard = ref board.HazardAt(index);
            if (hazard.Kind != TrailblazeCell.Cart || hazard.Lane != lane || hazard.Z + hazard.Length < from)
            {
                continue;
            }

            return hazard.Z;
        }

        return float.MaxValue;
    }
}
