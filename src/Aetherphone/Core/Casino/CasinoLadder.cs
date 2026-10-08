using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Casino;

internal enum CeilingReason : byte
{
    Level,
    Balance,
}

internal readonly record struct CasinoCeiling(
    long MaxBet,
    long LevelCap,
    long BalanceCap,
    int Level,
    CeilingReason Reason,
    bool FromServer);

internal static class CasinoLadder
{
    public const long BalanceDivisor = 20;

    public const string ReasonLevelKey = "level";

    public const string ReasonBalanceKey = "balance";

    public static readonly long[] Rungs =
    {
        100, 250, 500, 1_000, 2_500, 5_000, 10_000, 25_000, 50_000, 100_000, 250_000, 500_000,
        1_000_000, 2_500_000, 5_000_000, 10_000_000, 25_000_000, 50_000_000, 100_000_000, 250_000_000,
        500_000_000, 1_000_000_000, 2_500_000_000, 5_000_000_000, 10_000_000_000, 25_000_000_000,
        50_000_000_000,
    };

    public static readonly int[] AnchorLevels = { 1, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };

    public static readonly long[] DefaultAnchors =
    {
        10_000, 50_000, 250_000, 1_000_000, 5_000_000, 25_000_000, 100_000_000, 500_000_000,
        2_000_000_000, 10_000_000_000, 50_000_000_000,
    };

    public static long Lowest => Rungs[0];

    public static long Highest => Rungs[^1];

    public static bool IsRung(long amount)
    {
        for (var index = 0; index < Rungs.Length; index++)
        {
            if (Rungs[index] == amount)
            {
                return true;
            }
        }

        return false;
    }

    public static long FloorToRung(long amount)
    {
        var floored = Rungs[0];
        for (var index = 0; index < Rungs.Length; index++)
        {
            if (Rungs[index] > amount)
            {
                break;
            }

            floored = Rungs[index];
        }

        return floored;
    }

    public static long CeilToRung(long amount)
    {
        for (var index = 0; index < Rungs.Length; index++)
        {
            if (Rungs[index] >= amount)
            {
                return Rungs[index];
            }
        }

        return Highest;
    }

    public static long StepUp(long amount)
    {
        for (var index = 0; index < Rungs.Length; index++)
        {
            if (Rungs[index] > amount)
            {
                return Rungs[index];
            }
        }

        return Highest;
    }

    public static long StepDown(long amount)
    {
        for (var index = Rungs.Length - 1; index >= 0; index--)
        {
            if (Rungs[index] < amount)
            {
                return Rungs[index];
            }
        }

        return Lowest;
    }

    public static long LevelCap(int level) => LevelCap(level, DefaultAnchors);

    public static long LevelCap(int level, long[] anchors)
    {
        if (anchors.Length != AnchorLevels.Length)
        {
            anchors = DefaultAnchors;
        }

        if (level <= AnchorLevels[0])
        {
            return anchors[0];
        }

        var last = AnchorLevels.Length - 1;
        if (level >= AnchorLevels[last])
        {
            return anchors[last];
        }

        for (var index = 0; index < last; index++)
        {
            var lowLevel = AnchorLevels[index];
            var highLevel = AnchorLevels[index + 1];
            if (level > highLevel)
            {
                continue;
            }

            var low = (double)anchors[index];
            var high = (double)anchors[index + 1];
            var fraction = (double)(level - lowLevel) / (highLevel - lowLevel);
            return (long)Math.Floor(low * Math.Pow(high / low, fraction));
        }

        return anchors[last];
    }

    public static long BalanceCap(long balance) => balance <= 0 ? 0 : balance / BalanceDivisor;

    public static long MaxBet(int level, long balance) => MaxBet(level, balance, DefaultAnchors);

    public static long MaxBet(int level, long balance, long[] anchors)
    {
        var levelCap = LevelCap(level, anchors);
        var balanceCap = BalanceCap(balance);
        return FloorToRung(levelCap > balanceCap ? levelCap : balanceCap);
    }

    public static CeilingReason ReasonFor(int level, long balance, long[] anchors) =>
        LevelCap(level, anchors) >= BalanceCap(balance) ? CeilingReason.Level : CeilingReason.Balance;

    public static CasinoCeiling CeilingFor(CasinoStateDto? state)
    {
        var balance = state?.Sitting?.Stack ?? 0;
        var server = state?.Ceiling;
        if (server is not null && server.MaxBet > 0)
        {
            var reason = string.Equals(server.Reason, ReasonBalanceKey, StringComparison.Ordinal)
                ? CeilingReason.Balance
                : CeilingReason.Level;
            return new CasinoCeiling(server.MaxBet, server.LevelCap, server.BalanceCap,
                Math.Max(1, state?.Progress?.Level ?? 1), reason, true);
        }

        var level = Math.Max(1, state?.Progress?.Level ?? 1);
        var anchors = state?.LevelCapAnchors is { Length: > 0 } stored ? stored : DefaultAnchors;
        return new CasinoCeiling(MaxBet(level, balance, anchors), LevelCap(level, anchors), BalanceCap(balance), level,
            ReasonFor(level, balance, anchors), false);
    }

    public static long Clamp(long amount, long minimumBet, long maximumBet, long stack)
    {
        var top = Top(minimumBet, maximumBet, stack);
        if (top <= 0)
        {
            return 0;
        }

        var floor = Math.Max(minimumBet, Lowest);
        if (amount <= floor)
        {
            return floor;
        }

        var snapped = FloorToRung(Math.Min(amount, top));
        return snapped < floor ? floor : snapped;
    }

    public static long Top(long minimumBet, long maximumBet, long stack)
    {
        var cap = Math.Min(maximumBet, stack);
        if (cap < minimumBet || cap < Lowest)
        {
            return 0;
        }

        var snapped = FloorToRung(cap);
        return snapped < minimumBet ? minimumBet : snapped;
    }

    public static long Half(long amount, long minimumBet, long maximumBet, long stack) =>
        Clamp(FloorToRung(amount / 2), minimumBet, maximumBet, stack);

    public static long Double(long amount, long minimumBet, long maximumBet, long stack) =>
        Clamp(CeilToRung(amount * 2), minimumBet, maximumBet, stack);
}
