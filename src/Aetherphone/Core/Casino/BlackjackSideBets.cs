using Aetherphone.Windows.Components;

namespace Aetherphone.Core.Casino;

internal enum BlackjackSideBet : byte
{
    PerfectPairs,
    TwentyOnePlusThree,
}

internal static class BlackjackSideBets
{
    public const long SideBetMin = 100;

    public const int PairNone = 0;

    public const int PairMixed = 1;

    public const int PairColoured = 2;

    public const int PairPerfect = 3;

    public const int ThreeNone = 0;

    public const int ThreeFlush = 1;

    public const int ThreeStraight = 2;

    public const int ThreeTrips = 3;

    public const int ThreeStraightFlush = 4;

    public const int ThreeSuitedTrips = 5;

    public const int PerfectPairsReturnBasisPoints = 9_389;

    public const int TwentyOnePlusThreeReturnBasisPoints = 9_537;

    public const long PerfectPairsReturnNumerator = 91_104;

    public const long PerfectPairsReturnDenominator = 97_032;

    public const long TwentyOnePlusThreeReturnNumerator = 28_689_936;

    public const long TwentyOnePlusThreeReturnDenominator = 30_079_920;

    public static readonly int[] PerfectPairsPays = { 0, 6, 12, 25 };

    public static readonly int[] TwentyOnePlusThreePays = { 0, 5, 10, 30, 40, 100 };

    private const int AceRank = 0;
    private const int QueenRank = 11;
    private const int KingRank = 12;

    public static bool IsValidSideBet(long amount, long mainBet)
    {
        return amount == 0 || (amount >= SideBetMin && amount <= mainBet);
    }

    public static long Clamp(long amount, long mainBet)
    {
        if (amount <= 0 || mainBet < SideBetMin)
        {
            return 0;
        }

        return Math.Clamp(amount, SideBetMin, mainBet);
    }

    public static int RankOf(int card)
    {
        return card % PlayingCards.DeckSize % PlayingCards.RankCount;
    }

    public static int SuitOf(int card)
    {
        return card % PlayingCards.DeckSize / PlayingCards.RankCount;
    }

    public static bool IsRed(int card)
    {
        var suit = SuitOf(card);
        return suit == PlayingCards.Hearts || suit == PlayingCards.Diamonds;
    }

    public static int PerfectPairsKind(int first, int second)
    {
        if (RankOf(first) != RankOf(second))
        {
            return PairNone;
        }

        if (SuitOf(first) == SuitOf(second))
        {
            return PairPerfect;
        }

        return IsRed(first) == IsRed(second) ? PairColoured : PairMixed;
    }

    public static int TwentyOnePlusThreeKind(int first, int second, int upCard)
    {
        var firstRank = RankOf(first);
        var secondRank = RankOf(second);
        var upRank = RankOf(upCard);
        var flush = SuitOf(first) == SuitOf(second) && SuitOf(second) == SuitOf(upCard);
        if (firstRank == secondRank && secondRank == upRank)
        {
            return flush ? ThreeSuitedTrips : ThreeTrips;
        }

        var straight = IsStraight(firstRank, secondRank, upRank);
        if (straight && flush)
        {
            return ThreeStraightFlush;
        }

        if (straight)
        {
            return ThreeStraight;
        }

        return flush ? ThreeFlush : ThreeNone;
    }

    public static int PaysFor(BlackjackSideBet bet, int kind)
    {
        var pays = bet == BlackjackSideBet.PerfectPairs ? PerfectPairsPays : TwentyOnePlusThreePays;
        return kind > 0 && kind < pays.Length ? pays[kind] : 0;
    }

    public static long ReturnFor(BlackjackSideBet bet, long amount, int kind)
    {
        var pays = PaysFor(bet, kind);
        return pays == 0 || amount <= 0 ? 0 : amount * (pays + 1);
    }

    public static long InsuranceReturn(long insurance, bool dealerNatural)
    {
        return dealerNatural && insurance > 0 ? insurance * 3 : 0;
    }

    private static bool IsStraight(int first, int second, int third)
    {
        if (first == second || second == third || first == third)
        {
            return false;
        }

        var low = Math.Min(first, Math.Min(second, third));
        var high = Math.Max(first, Math.Max(second, third));
        if (high - low == 2)
        {
            return true;
        }

        return low == AceRank && first + second + third == QueenRank + KingRank;
    }
}
