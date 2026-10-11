using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class BlackjackPlaybackTests
{
    [Fact]
    public void EverySeatSettlesAgainstTheDealerOnItsOwn()
    {
        var board = Board(BlackjackPhases.Settlement,
            Seat(0, Hand(2_000, BlackjackOutcomes.Win, 2_000)),
            Seat(1, Hand(2_000, BlackjackOutcomes.Lose, -2_000)),
            Seat(2, Hand(2_000, BlackjackOutcomes.Push, 0)));

        var winner = BlackjackRecap.Of(board.Seats![0], board.Seats[0].Hands!);
        var loser = BlackjackRecap.Of(board.Seats[1], board.Seats[1].Hands!);
        var pushed = BlackjackRecap.Of(board.Seats[2], board.Seats[2].Hands!);

        Assert.Equal(1, winner.Sign);
        Assert.Equal(-1, loser.Sign);
        Assert.Equal(0, pushed.Sign);
        Assert.Equal(3, BlackjackRecap.PlayedSeats(board));
    }

    [Fact]
    public void ASideBetWinCountsTowardTheNetOverTheWholeStake()
    {
        var seat = Seat(0, Hand(1_000, BlackjackOutcomes.Lose, -1_000)) with
        {
            SideBets = new CasinoBlackjackSideBetsDto(100, 100, BlackjackSideBets.PairColoured, 0, 1_300, 0),
        };

        var recap = BlackjackRecap.Of(seat, seat.Hands!);

        Assert.Equal(1_200, recap.Staked);
        Assert.Equal(1_300, recap.Returned);
        Assert.Equal(100, recap.Net);
        Assert.True(recap.PairsPaid);
        Assert.False(recap.ThreePaid);
    }

    [Fact]
    public void InsurancePaidAgainstANaturalOffsetsTheLostHand()
    {
        var seat = Seat(0, Hand(2_000, BlackjackOutcomes.Lose, -2_000)) with { Insurance = 1_000, InsuranceWin = 3_000 };

        var recap = BlackjackRecap.Of(seat, seat.Hands!);

        Assert.Equal(3_000, recap.Staked);
        Assert.Equal(3_000, recap.Returned);
        Assert.Equal(0, recap.Net);
        Assert.True(recap.InsurancePaid);
    }

    [Fact]
    public void ASurrenderHandsHalfTheBetBack()
    {
        var seat = Seat(0, Hand(2_000, BlackjackOutcomes.Surrender, -1_000) with { Surrendered = true });

        var recap = BlackjackRecap.Of(seat, seat.Hands!);

        Assert.True(recap.Surrendered);
        Assert.Equal(1_000, recap.Returned);
        Assert.Equal(-1, recap.Sign);
    }

    [Fact]
    public void AMidHandJoinSeesNothingSettledUntilEveryHandIsDecided()
    {
        var seat = Seat(0, Hand(2_000, BlackjackOutcomes.Pending, 0), Hand(2_000, BlackjackOutcomes.Win, 2_000));

        Assert.False(BlackjackRecap.Of(seat, seat.Hands!).Settled);
        Assert.False(BlackjackRecap.Settled(Array.Empty<CasinoBlackjackHandDto>()));
        Assert.Equal(0, BlackjackRecap.Of(null, seat.Hands!).Staked);
    }

    [Fact]
    public void TheInsuranceWindowOnlyOffersWhenTheLaneCarriesTheBit()
    {
        var board = Board(BlackjackPhases.Dealing) with { InsuranceOpen = true };

        Assert.True(BlackjackRecap.InsuranceOffered(board, BlackjackRules.ActionInsurance));
        Assert.False(BlackjackRecap.InsuranceOffered(board, 0));
        Assert.False(BlackjackRecap.InsuranceOffered(board with { InsuranceOpen = false },
            BlackjackRules.ActionInsurance));
    }

    [Fact]
    public void GilTablesNeverOfferSideBets()
    {
        var board = Board(BlackjackPhases.Betting) with { SideBetMin = 100 };

        Assert.True(BlackjackRecap.SideBetsOffered(board));
        Assert.False(BlackjackRecap.SideBetsOffered(board with { Currency = CasinoCurrencies.Gil }));
        Assert.False(BlackjackRecap.SideBetsOffered(board with { SideBetMin = 0 }));
    }

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = new BlackjackIdleScript(42);
        var second = new BlackjackIdleScript(42);
        for (var step = 0; step < 40; step++)
        {
            first.Advance(0.37f);
            second.Advance(0.37f);
            for (var index = 0; index < BlackjackIdleScript.CardsPerRound; index++)
            {
                Assert.Equal(first.CardAt(index), second.CardAt(index));
                Assert.Equal(first.TravelOf(index), second.TravelOf(index));
            }
        }

        Assert.True(first.Round > 0);
    }

    [Fact]
    public void TheIdleDealerDealsEverySpotBeforeTheDealerAndSweeps()
    {
        Assert.Equal(BlackjackIdleScript.Spots, BlackjackIdleScript.TargetOf(BlackjackIdleScript.Spots));
        Assert.True(BlackjackIdleScript.IsDealerCard(BlackjackIdleScript.Spots));
        var script = new BlackjackIdleScript();
        script.Advance(BlackjackIdleScript.RoundSeconds - 0.01f);
        Assert.True(script.Sweep > 0.9f);
        script.Advance(0.02f);
        Assert.Equal(1, script.Round);
        Assert.Equal(0f, script.Sweep);
    }

    private static CasinoBlackjackRoomStateDto Board(int phase, params CasinoBlackjackSeatDto[] seats)
    {
        return new CasinoBlackjackRoomStateDto(HandId: "hand-1", Phase: phase, Seats: seats);
    }

    private static CasinoBlackjackSeatDto Seat(int seatIndex, params CasinoBlackjackHandDto[] hands)
    {
        return new CasinoBlackjackSeatDto(seatIndex, "user-" + seatIndex, "Seat", 50_000,
            BlackjackSeatStates.Seated, true, Hands: hands);
    }

    private static CasinoBlackjackHandDto Hand(long bet, int outcome, long delta)
    {
        return new CasinoBlackjackHandDto(new[] { 9, 5 }, bet, 15, Outcome: outcome, Delta: delta);
    }
}
