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

    public static bool IsWager(string action)
    {
        return string.Equals(action, Double, StringComparison.Ordinal)
            || string.Equals(action, Split, StringComparison.Ordinal);
    }

    public static string VerbFor(int action)
    {
        return action switch
        {
            BlackjackRules.ActionHit => Hit,
            BlackjackRules.ActionStand => Stand,
            BlackjackRules.ActionDouble => Double,
            BlackjackRules.ActionSplit => Split,
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

    public const long MaxBet = 100_000;

    public static readonly long[] HouseTierMinBets = { 2_500, 10_000, 25_000 };

    public static readonly long[] HouseTierMaxBets = { 25_000, 50_000, 100_000 };

    public static long HouseFloor => HouseTierMinBets[0];

    public static long HouseTop => HouseTierMaxBets[^1];

    public const long BetStep = 10;

    public const int ActionHit = 1;

    public const int ActionStand = 2;

    public const int ActionDouble = 4;

    public const int ActionSplit = 8;

    private const long RackHands = 20;

    public static long RackFor(long tableMaxBet, long minBuyIn, long maxBuyIn, long bankroll)
    {
        if (bankroll < minBuyIn)
        {
            return 0;
        }

        var suggested = Math.Clamp(tableMaxBet * RackHands, minBuyIn, maxBuyIn);
        return Math.Min(suggested, bankroll);
    }

    public static bool Allows(int actionsMask, int action)
    {
        return action != 0 && (actionsMask & action) == action;
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
