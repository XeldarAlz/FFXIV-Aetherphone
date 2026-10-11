using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Casino;

internal enum CeilingReason : byte
{
    Level,
    Balance,
    MaxWin,
}

internal readonly record struct CasinoCeiling(
    long MaxBet,
    long LevelCap,
    long BalanceCap,
    int Level,
    CeilingReason Reason,
    bool FromServer,
    long MaxWinCap = CasinoLadder.DefaultMaxWinPerBet);

internal static class CasinoLadder
{
    public const long BalanceDivisor = 20;

    public const string ReasonLevelKey = "level";

    public const string ReasonBalanceKey = "balance";

    public const string ReasonMaxWinKey = "max_win";

    public const long DefaultMaxWinPerBet = 50_000 * CasinoChipLots.ChipPerCoin;

    public const long MaxWinBetMultiple = 10;

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

    public static long MaxBet(int level, long balance) => MaxBet(level, balance, DefaultAnchors, DefaultMaxWinPerBet);

    public static long BetBoundFor(long maxWin) => maxWin / MaxWinBetMultiple;

    public static long MaxBet(int level, long balance, long[] anchors, long maxWin)
    {
        var levelCap = LevelCap(level, anchors);
        var balanceCap = BalanceCap(balance);
        var larger = levelCap > balanceCap ? levelCap : balanceCap;
        var bound = BetBoundFor(maxWin);
        return FloorToRung(maxWin > 0 && bound < larger ? bound : larger);
    }

    public static CeilingReason ReasonFor(int level, long balance, long[] anchors, long maxWin)
    {
        var levelCap = LevelCap(level, anchors);
        var balanceCap = BalanceCap(balance);
        if (maxWin > 0 && Math.Max(levelCap, balanceCap) > BetBoundFor(maxWin))
        {
            return CeilingReason.MaxWin;
        }

        return levelCap >= balanceCap ? CeilingReason.Level : CeilingReason.Balance;
    }

    public static long MaxWinOf(CasinoStateDto? state) =>
        state is { MaxWinPerBet: > 0 } ? state.MaxWinPerBet : DefaultMaxWinPerBet;

    public static CeilingReason ReasonOf(string key)
    {
        if (string.Equals(key, ReasonMaxWinKey, StringComparison.Ordinal))
        {
            return CeilingReason.MaxWin;
        }

        return string.Equals(key, ReasonBalanceKey, StringComparison.Ordinal)
            ? CeilingReason.Balance
            : CeilingReason.Level;
    }

    public static CasinoCeiling CeilingFor(CasinoStateDto? state)
    {
        var balance = state?.Sitting?.Stack ?? 0;
        var maxWin = MaxWinOf(state);
        var server = state?.Ceiling;
        if (server is not null && server.MaxBet > 0)
        {
            return new CasinoCeiling(server.MaxBet, server.LevelCap, server.BalanceCap,
                Math.Max(1, state?.Progress?.Level ?? 1), ReasonOf(server.Reason), true,
                server.MaxWinCap > 0 ? server.MaxWinCap : maxWin);
        }

        var level = Math.Max(1, state?.Progress?.Level ?? 1);
        var anchors = state?.LevelCapAnchors is { Length: > 0 } stored ? stored : DefaultAnchors;
        return new CasinoCeiling(MaxBet(level, balance, anchors, maxWin), LevelCap(level, anchors),
            BalanceCap(balance), level, ReasonFor(level, balance, anchors, maxWin), false, maxWin);
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
