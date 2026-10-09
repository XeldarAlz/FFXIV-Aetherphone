namespace Aetherphone.Core.Casino;

internal enum DealerHoldemSpot : byte
{
    Trips,
    Ante,
    Blind,
    Play,
}

internal enum DealerHoldemResult : byte
{
    None,
    Win,
    Push,
    Lose,
}

internal static class DealerHoldemRules
{
    public const string Kind = "casino.dealerholdem";
    public const string Feature = "dealer.holdem";
    public const string Check = "check";
    public const string Bet = "bet";
    public const string Fold = "fold";
    public const long MinAnte = 100;
    public const int SpotCount = 4;
    public const int HoleCards = 2;
    public const int BoardCards = 5;
    public const int FlopCards = 3;
    public const int PhasePreFlop = 0;
    public const int PhaseFlop = 1;
    public const int PhaseRiver = 2;
    public const int PhaseSettled = 3;
    public const int PhaseVoided = 4;
    public const int OutcomePending = 0;
    public const int OutcomeWin = 1;
    public const int OutcomeTie = 2;
    public const int OutcomeDealer = 3;
    public const int OutcomeFolded = 4;
    public const int OutcomeVoided = 5;
    public const int QualifyingCategory = HoldemHands.Pair;
    public const int BlindPaysFrom = HoldemHands.Straight;
    public const int TripsPaysFrom = HoldemHands.Trips;
    public const int PreFlopHigh = 4;
    public const int PreFlopLow = 3;
    public const int FlopMultiple = 2;
    public const int RiverMultiple = 1;
    public const int MaxPlayMultiple = PreFlopHigh;
    public const int TopMultiple = 250;
    public const int ReturnTenths = 977;
    public const int TripsReturnTenths = 965;

    public static readonly int[] BlindNumerators = { 0, 0, 0, 0, 1, 3, 3, 10, 50, 500 };

    public static readonly int[] BlindDenominators = { 1, 1, 1, 1, 1, 2, 1, 1, 1, 1 };

    public static readonly int[] TripsPays = { 0, 0, 0, 3, 4, 7, 8, 30, 40, 50 };

    public static readonly int[] PayCategories =
    {
        HoldemHands.RoyalFlush,
        HoldemHands.StraightFlush,
        HoldemHands.Quads,
        HoldemHands.FullHouse,
        HoldemHands.Flush,
        HoldemHands.Straight,
        HoldemHands.Trips,
    };

    public static bool IsAction(string action) =>
        string.Equals(action, Check, StringComparison.Ordinal) || string.Equals(action, Bet, StringComparison.Ordinal)
        || string.Equals(action, Fold, StringComparison.Ordinal);

    public static bool IsOpening(long ante, long trips) =>
        ante >= MinAnte && trips >= 0 && trips <= ante && (trips == 0 || CasinoLadder.IsRung(trips));

    public static bool IsDecision(int phase) => phase is PhasePreFlop or PhaseFlop or PhaseRiver;

    public static bool IsOver(int phase) => phase is PhaseSettled or PhaseVoided;

    public static bool Qualifies(int dealerStrength) =>
        dealerStrength >= 0 && HoldemHands.CategoryOf(dealerStrength) >= QualifyingCategory;

    public static bool BlindPaysOn(int category) =>
        category >= BlindPaysFrom && category <= HoldemHands.RoyalFlush;

    public static bool TripsPaysOn(int category) =>
        category >= TripsPaysFrom && category <= HoldemHands.RoyalFlush;

    public static long BlindReturn(long blind, int category)
    {
        if (!BlindPaysOn(category))
        {
            return blind;
        }

        return blind + blind * BlindNumerators[category] / BlindDenominators[category];
    }

    public static long TripsReturn(long trips, int category) =>
        TripsPaysOn(category) ? trips * (1 + TripsPays[category]) : 0;

    public static int BoardShown(int phase) => phase switch
    {
        PhasePreFlop => 0,
        PhaseFlop => FlopCards,
        _ => BoardCards,
    };

    public static long StakeAtDeal(long ante, long trips) => ante * 2 + Math.Max(0, trips);

    public static long TripsFor(long ante) => CasinoLadder.FloorToRung(Math.Max(0, ante));

    public static DealerHoldemResult ResultOf(long staked, long returned)
    {
        if (staked <= 0)
        {
            return DealerHoldemResult.None;
        }

        if (returned > staked)
        {
            return DealerHoldemResult.Win;
        }

        return returned == staked ? DealerHoldemResult.Push : DealerHoldemResult.Lose;
    }
}
