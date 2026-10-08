using Aetherphone.Windows.Components;

namespace Aetherphone.Core.Casino;

internal static class BlackjackSeatStates
{
    public const int Empty = 0;

    public const int Seated = 1;

    public const int SittingOut = 2;
}

internal static class BlackjackPhases
{
    public const int Betting = 0;

    public const int Dealing = 1;

    public const int PlayerTurns = 2;

    public const int DealerPlay = 3;

    public const int Settlement = 4;

    public const int Intermission = 5;

    public const int Settled = 6;

    public const int Voided = 7;

    public static bool Live(int phase)
    {
        return phase < Settled;
    }

    public static bool Over(int phase)
    {
        return phase >= Settlement;
    }
}

internal static class BlackjackActions
{
    public const string Hit = "hit";

    public const string Stand = "stand";

    public const string Double = "double";

    public const string Split = "split";

    public const string Insurance = "insurance";

    public const string NoInsurance = "no_insurance";

    public const string Surrender = "surrender";

    public static bool IsWager(string action)
    {
        return string.Equals(action, Double, StringComparison.Ordinal)
            || string.Equals(action, Split, StringComparison.Ordinal)
            || string.Equals(action, Insurance, StringComparison.Ordinal);
    }

    public static string VerbFor(int action)
    {
        return action switch
        {
            BlackjackRules.ActionHit => Hit,
            BlackjackRules.ActionStand => Stand,
            BlackjackRules.ActionDouble => Double,
            BlackjackRules.ActionSplit => Split,
            BlackjackRules.ActionInsurance => Insurance,
            BlackjackRules.ActionDeclineInsurance => NoInsurance,
            BlackjackRules.ActionSurrender => Surrender,
            _ => string.Empty,
        };
    }
}

internal static class BlackjackOutcomes
{
    public const int Pending = 0;

    public const int Win = 1;

    public const int Lose = 2;

    public const int Push = 3;

    public const int Blackjack = 4;

    public const int Bust = 5;

    public const int Surrender = 6;
}

internal static class BlackjackRules
{
    public const int SeatCount = 6;

    public const int Decks = 6;

    public const int ShoeCards = Decks * PlayingCards.DeckSize;

    public const int MaxHandsPerSeat = 4;

    public const int DealerStandsOn = 17;

    public const int TargetTotal = 21;

    public const long MinBet = 500;

    public const long MaxBet = 10_000_000;

    public const int HouseTierCount = 4;

    public const int InsuranceSeconds = 8;

    public static readonly long[] HouseTierMinBets = { 500, 5_000, 50_000, 500_000 };

    public static readonly long[] HouseTierMaxBets = { 10_000, 100_000, 1_000_000, 10_000_000 };

    public static long HouseFloor => HouseTierMinBets[0];

    public static long HouseTop => HouseTierMaxBets[^1];

    public const long BetStep = 10;

    public const int ActionHit = 1;

    public const int ActionStand = 2;

    public const int ActionDouble = 4;

    public const int ActionSplit = 8;

    public const int ActionInsurance = 16;

    public const int ActionSurrender = 32;

    public const int ActionDeclineInsurance = 64;

    public const int ReturnTenths = 995;

    private const long RackHands = 20;

    private const long QuickTierHeadroom = 2;

    public static long RackFor(long playerMaxBet, long tableMinBet, long tableMaxBet, long minBuyIn, long maxBuyIn,
        long bankroll)
    {
        if (bankroll < minBuyIn)
        {
            return 0;
        }

        var bandTop = tableMaxBet > 0 ? tableMaxBet : playerMaxBet;
        var bandBottom = Math.Min(Math.Max(0, tableMinBet), bandTop);
        var chosen = playerMaxBet > 0 ? Math.Clamp(playerMaxBet, bandBottom, bandTop) : bandTop;
        var suggested = chosen * RackHands;
        if (playerMaxBet > 0)
        {
            suggested = Math.Min(suggested, playerMaxBet * RackHands);
        }

        suggested = Math.Clamp(suggested, minBuyIn, Math.Max(minBuyIn, maxBuyIn));
        return Math.Min(suggested, bankroll);
    }

    public static bool Allows(int actionsMask, int action)
    {
        if (action == ActionDeclineInsurance)
        {
            return (actionsMask & ActionInsurance) == ActionInsurance;
        }

        return action != 0 && (actionsMask & action) == action;
    }

    public static int QuickTierFor(long ceiling)
    {
        var tier = 0;
        for (var index = 1; index < HouseTierCount; index++)
        {
            if (HouseTierMinBets[index] * QuickTierHeadroom <= ceiling)
            {
                tier = index;
            }
        }

        return tier;
    }

    public static long InsuranceFor(long bet)
    {
        return bet <= 0 ? 0 : bet / 2;
    }

    public static long SurrenderReturn(long bet)
    {
        return bet <= 0 ? 0 : bet / 2;
    }

    public static bool IsSeat(int seatIndex)
    {
        return seatIndex >= 0 && seatIndex < SeatCount;
    }

    public static int CardValue(int card)
    {
        if (!PlayingCards.IsCard(card))
        {
            return 0;
        }

        var rank = PlayingCards.Rank(card);
        if (rank == 0)
        {
            return 1;
        }

        return rank >= 9 ? 10 : rank + 1;
    }

    public static int Total(ReadOnlySpan<int> cards, out bool soft)
    {
        var sum = 0;
        var aces = 0;
        for (var index = 0; index < cards.Length; index++)
        {
            var value = CardValue(cards[index]);
            sum += value;
            if (value == 1)
            {
                aces++;
            }
        }

        soft = false;
        if (aces > 0 && sum + 10 <= TargetTotal)
        {
            soft = true;
            return sum + 10;
        }

        return sum;
    }

    public static bool IsBust(int total)
    {
        return total > TargetTotal;
    }

    public static bool IsNatural(ReadOnlySpan<int> cards, int splitIndex, bool seatSplit)
    {
        if (cards.Length != 2 || splitIndex != 0 || seatSplit)
        {
            return false;
        }

        return Total(cards, out _) == TargetTotal;
    }

    public static long BlackjackPayout(long bet)
    {
        return bet <= 0 ? 0 : bet * 3 / 2;
    }

    public static SeatPhase PhaseOf(int seatState, bool connected, bool waiting, bool committed)
    {
        if (seatState == BlackjackSeatStates.Empty)
        {
            return SeatPhase.Empty;
        }

        if (seatState == BlackjackSeatStates.SittingOut)
        {
            return SeatPhase.Out;
        }

        if (seatState != BlackjackSeatStates.Seated)
        {
            return SeatPhase.Empty;
        }

        if (!connected)
        {
            return SeatPhase.Away;
        }

        if (waiting)
        {
            return SeatPhase.Waiting;
        }

        return committed ? SeatPhase.Betting : SeatPhase.Sitting;
    }
}
