using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Tables;

internal readonly record struct LadderSpan(int First, int Last)
{
    public int Count => Last - First + 1;

    public int Clamp(int index) => Math.Clamp(index, First, Last);
}

internal static class HostLadders
{
    public static readonly long[] PracticeStacks =
    {
        1_000, 10_000, 25_000, 50_000, 100_000, 250_000, 500_000, 1_000_000, 10_000_000, 100_000_000,
        1_000_000_000,
    };

    public static readonly long[] Gil =
    {
        1_000, 5_000, 10_000, 25_000, 50_000, 100_000, 250_000, 500_000, 1_000_000, 2_500_000, 5_000_000,
        10_000_000, 25_000_000, 50_000_000, 100_000_000, 250_000_000, 500_000_000, 1_000_000_000,
    };

    public static readonly long[] DiceSides = { 2, 6, 10, 20, 100, 1_000, 10_000, 100_000, 1_000_000 };

    public static readonly long[] StartNumbers = { 10, 100, 1_000, 10_000, 100_000, 1_000_000 };

    public static readonly int[] RoundSeconds = { 30, 60, 120, 300, 600 };

    public static readonly int[] AnteTenths = { 0, 1, 2, 5, 10 };

    public static long[] Bets => CasinoLadder.Rungs;

    public static int FloorIndex(long[] rungs, long value)
    {
        var found = 0;
        for (var index = 0; index < rungs.Length; index++)
        {
            if (rungs[index] > value)
            {
                break;
            }

            found = index;
        }

        return found;
    }

    public static int CeilIndex(long[] rungs, long value)
    {
        for (var index = 0; index < rungs.Length; index++)
        {
            if (rungs[index] >= value)
            {
                return index;
            }
        }

        return rungs.Length - 1;
    }

    public static LadderSpan Span(long[] rungs, long lowest, long highest)
    {
        var first = CeilIndex(rungs, lowest);
        var last = Math.Max(first, FloorIndex(rungs, highest));
        return new LadderSpan(first, last);
    }

    public static LadderSpan CoinBets => Span(Bets, BlackjackRules.MinBet, BlackjackRules.MaxBet);

    public static LadderSpan PracticeBets(long stack) => Span(Bets, Bets[0], stack);

    public static LadderSpan GilBets(long bank) => Span(Gil, Gil[0], bank);

    public static LadderSpan BigBlinds => Span(Bets, CasinoHostingRules.MinHoldemBigBlind, BlackjackRules.MaxBet);

    public static LadderSpan All(long[] rungs) => new(0, rungs.Length - 1);
}
