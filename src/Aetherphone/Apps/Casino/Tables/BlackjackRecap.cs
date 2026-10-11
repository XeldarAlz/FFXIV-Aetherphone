using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Tables;

internal readonly struct BlackjackSeatRecap
{
    public readonly long Staked;
    public readonly long Returned;
    public readonly long PairsStake;
    public readonly int PairsKind;
    public readonly long PairsWin;
    public readonly long ThreeStake;
    public readonly int ThreeKind;
    public readonly long ThreeWin;
    public readonly long Insurance;
    public readonly long InsuranceWin;
    public readonly bool Surrendered;
    public readonly bool Settled;

    public BlackjackSeatRecap(long staked, long returned, long pairsStake, int pairsKind, long pairsWin,
        long threeStake, int threeKind, long threeWin, long insurance, long insuranceWin, bool surrendered,
        bool settled)
    {
        Staked = staked;
        Returned = returned;
        PairsStake = pairsStake;
        PairsKind = pairsKind;
        PairsWin = pairsWin;
        ThreeStake = threeStake;
        ThreeKind = threeKind;
        ThreeWin = threeWin;
        Insurance = insurance;
        InsuranceWin = insuranceWin;
        Surrendered = surrendered;
        Settled = settled;
    }

    public long Net => Returned - Staked;

    public int Sign => Net > 0 ? 1 : Net < 0 ? -1 : 0;

    public bool PairsPaid => PairsKind > 0 && PairsWin > 0;

    public bool ThreePaid => ThreeKind > 0 && ThreeWin > 0;

    public bool InsurancePaid => InsuranceWin > 0;
}

internal static class BlackjackRecap
{
    public static BlackjackSeatRecap Of(CasinoBlackjackSeatDto? seat, CasinoBlackjackHandDto[] hands)
    {
        if (seat is null || hands.Length == 0)
        {
            return default;
        }

        var staked = 0L;
        var returned = 0L;
        var settled = true;
        var surrendered = false;
        for (var index = 0; index < hands.Length; index++)
        {
            var hand = hands[index];
            staked += hand.Bet;
            returned += Math.Max(0, hand.Bet + hand.Delta);
            settled &= hand.Outcome != BlackjackOutcomes.Pending;
            surrendered |= hand.Surrendered || hand.Outcome == BlackjackOutcomes.Surrender;
        }

        var sides = seat.SideBets;
        var pairsStake = sides?.PerfectPairs ?? 0;
        var threeStake = sides?.TwentyOnePlusThree ?? 0;
        var pairsWin = sides?.PerfectPairsWin ?? 0;
        var threeWin = sides?.TwentyOnePlusThreeWin ?? 0;
        staked += pairsStake + threeStake + seat.Insurance;
        returned += pairsWin + threeWin + seat.InsuranceWin;
        return new BlackjackSeatRecap(staked, returned, pairsStake, sides?.PerfectPairsKind ?? 0, pairsWin,
            threeStake, sides?.TwentyOnePlusThreeKind ?? 0, threeWin, seat.Insurance, seat.InsuranceWin,
            surrendered, settled);
    }

    public static bool Settled(CasinoBlackjackHandDto[] hands)
    {
        if (hands.Length == 0)
        {
            return false;
        }

        for (var index = 0; index < hands.Length; index++)
        {
            if (hands[index].Outcome == BlackjackOutcomes.Pending)
            {
                return false;
            }
        }

        return true;
    }

    public static int PlayedSeats(CasinoBlackjackRoomStateDto board)
    {
        var seats = board.Seats;
        if (seats is null)
        {
            return 0;
        }

        var played = 0;
        for (var index = 0; index < seats.Length; index++)
        {
            if (seats[index].Hands is { Length: > 0 })
            {
                played++;
            }
        }

        return played;
    }

    public static bool InsuranceOffered(CasinoBlackjackRoomStateDto board, int actionsMask)
    {
        return board.InsuranceOpen && BlackjackRules.Allows(actionsMask, BlackjackRules.ActionInsurance);
    }

    public static bool SideBetsOffered(CasinoBlackjackRoomStateDto board)
    {
        return board.SideBetMin > 0 && CasinoCurrencies.Of(board) != CasinoCurrencies.Gil;
    }
}
