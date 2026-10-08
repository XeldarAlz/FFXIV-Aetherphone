namespace Aetherphone.Core.Casino;

internal static class WheelRules
{
    public const int SegmentCount = 50;

    public const int SpotCount = 5;

    public const long MinStakePerSpot = 500;

    public const long MaxStakePerSpot = 50_000;

    public const long MaxStakePerRound = 200_000;

    public static readonly int[] Multipliers = { 1, 3, 5, 11, 23 };

    public static readonly int[] SegmentCounts = { 24, 12, 8, 4, 2 };

    public static readonly int[] Segments =
    {
        0, 1, 0, 2, 0, 1, 0, 3,
        0, 1, 0, 2, 0, 1, 0, 3,
        0, 1, 0, 2, 0, 1, 0, 3,
        0, 1, 0, 2, 0, 1, 0, 3,
        0, 1, 0, 2, 0, 4,
        0, 1, 0, 2, 0, 4,
        0, 1, 2, 0, 1, 2,
    };

    public static bool IsSpot(int spot)
    {
        return spot >= 0 && spot < SpotCount;
    }

    public static bool IsSegment(int segment)
    {
        return segment >= 0 && segment < SegmentCount;
    }

    public static int SpotAt(int segment)
    {
        return IsSegment(segment) ? Segments[segment] : -1;
    }

    public static int MultiplierOf(int spot)
    {
        return IsSpot(spot) ? Multipliers[spot] : 0;
    }

    public static int SegmentsOn(int spot)
    {
        return IsSpot(spot) ? SegmentCounts[spot] : 0;
    }

    public static int ReturnBasisPointsFor(int spot)
    {
        return IsSpot(spot) ? SegmentCounts[spot] * (Multipliers[spot] + 1) * 10_000 / SegmentCount : 0;
    }

    public static bool Wins(int segment, int spot)
    {
        return IsSpot(spot) && SpotAt(segment) == spot;
    }

    public static long Returned(int segment, int spot, long stake)
    {
        return Wins(segment, spot) ? stake * (MultiplierOf(spot) + 1) : 0;
    }

    public static bool IsStakeInRange(long amount)
    {
        return amount >= MinStakePerSpot && amount <= MaxStakePerSpot;
    }

    public static long Headroom(long stakedThisRound)
    {
        var left = MaxStakePerRound - stakedThisRound;
        if (left <= 0)
        {
            return 0;
        }

        return left < MaxStakePerSpot ? left : MaxStakePerSpot;
    }

    public static long HeadroomOn(long stakedThisRound, long stakedOnSpot)
    {
        var round = Headroom(stakedThisRound);
        var spot = MaxStakePerSpot - stakedOnSpot;
        if (spot <= 0)
        {
            return 0;
        }

        return spot < round ? spot : round;
    }

    public static long ClampOn(long amount, long stakedThisRound, long stakedOnSpot, long stack)
    {
        var ceiling = HeadroomOn(stakedThisRound, stakedOnSpot);
        if (stack < ceiling)
        {
            ceiling = stack;
        }

        if (ceiling < MinStakePerSpot || amount < MinStakePerSpot)
        {
            return 0;
        }

        return amount > ceiling ? ceiling : amount;
    }

    public static long Clamp(long amount, long stakedThisRound, long stack)
    {
        var ceiling = Headroom(stakedThisRound);
        if (stack < ceiling)
        {
            ceiling = stack;
        }

        if (ceiling < MinStakePerSpot)
        {
            return 0;
        }

        if (amount < MinStakePerSpot)
        {
            return 0;
        }

        return amount > ceiling ? ceiling : amount;
    }
}
