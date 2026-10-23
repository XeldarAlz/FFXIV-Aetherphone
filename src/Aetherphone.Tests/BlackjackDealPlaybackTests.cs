using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class BlackjackDealPlaybackTests
{
    private const string Hand = "hand-1";
    private const string NextHand = "hand-2";
    private const int SeatSlot = 0;

    [Fact]
    public void TheOpeningDealFliesOneCardAtATimeInDealRounds()
    {
        var playback = Primed();
        playback.Update(Board(Hand, BlackjackPhases.Dealing, new[] { 4, -1 }, new[] { 8, 21 }), 0f);
        Assert.Equal(0f, playback.TravelOf(SeatSlot, 0));
        Assert.Equal(0f, playback.TravelOf(BlackjackDealPlayback.DealerSlot, 0));

        playback.Update(Board(Hand, BlackjackPhases.Dealing, new[] { 4, -1 }, new[] { 8, 21 }),
            BlackjackDealChoreography.StaggerSeconds * 0.5f);
        Assert.True(playback.TravelOf(SeatSlot, 0) > 0f);
        Assert.Equal(0f, playback.TravelOf(BlackjackDealPlayback.DealerSlot, 0));
        Assert.Equal(0f, playback.TravelOf(SeatSlot, 1));

        playback.Update(Board(Hand, BlackjackPhases.Dealing, new[] { 4, -1 }, new[] { 8, 21 }),
            BlackjackDealChoreography.StaggerSeconds);
        Assert.True(playback.TravelOf(BlackjackDealPlayback.DealerSlot, 0) > 0f);
        Assert.Equal(0f, playback.TravelOf(SeatSlot, 1));
        Assert.Equal(0f, playback.TravelOf(BlackjackDealPlayback.DealerSlot, 1));

        playback.Update(Board(Hand, BlackjackPhases.Dealing, new[] { 4, -1 }, new[] { 8, 21 }),
            BlackjackDealChoreography.StaggerSeconds);
        Assert.True(playback.TravelOf(SeatSlot, 1) > 0f);
        Assert.Equal(0f, playback.TravelOf(BlackjackDealPlayback.DealerSlot, 1));

        playback.Update(Board(Hand, BlackjackPhases.Dealing, new[] { 4, -1 }, new[] { 8, 21 }),
            BlackjackDealChoreography.TravelSeconds + BlackjackDealChoreography.StaggerSeconds * 4f);
        Assert.Equal(1f, playback.TravelOf(SeatSlot, 0));
        Assert.Equal(1f, playback.TravelOf(SeatSlot, 1));
        Assert.Equal(1f, playback.TravelOf(BlackjackDealPlayback.DealerSlot, 0));
        Assert.Equal(1f, playback.TravelOf(BlackjackDealPlayback.DealerSlot, 1));
    }

    [Fact]
    public void AHitMidHandStillFliesFromTheShoe()
    {
        var playback = Primed();
        playback.Update(Board(Hand, BlackjackPhases.Dealing, new[] { 4, -1 }, new[] { 8, 21 }), 0f);
        playback.Update(Board(Hand, BlackjackPhases.PlayerTurns, new[] { 4, -1 }, new[] { 8, 21 }), 5f);
        Assert.Equal(1f, playback.TravelOf(SeatSlot, 1));

        playback.Update(Board(Hand, BlackjackPhases.PlayerTurns, new[] { 4, -1 }, new[] { 8, 21, 30 }), 0f);
        Assert.Equal(0f, playback.TravelOf(SeatSlot, 2));
        Assert.Equal(1f, playback.TravelOf(SeatSlot, 1));

        playback.Update(Board(Hand, BlackjackPhases.PlayerTurns, new[] { 4, -1 }, new[] { 8, 21, 30 }),
            BlackjackDealChoreography.TravelSeconds * 0.5f);
        var travel = playback.TravelOf(SeatSlot, 2);
        Assert.True(travel > 0f);
        Assert.True(travel < 1f);
    }

    [Fact]
    public void JoiningAHandInProgressShowsTheTableAlreadyDealt()
    {
        var playback = new BlackjackDealPlayback();
        playback.Update(Board(Hand, BlackjackPhases.PlayerTurns, new[] { 4, -1 }, new[] { 8, 21 }), 0f);
        Assert.Equal(1f, playback.TravelOf(SeatSlot, 0));
        Assert.Equal(1f, playback.TravelOf(SeatSlot, 1));
        Assert.Equal(1f, playback.TravelOf(BlackjackDealPlayback.DealerSlot, 0));
    }

    [Fact]
    public void TheNextHandAfterAMidHandJoinStillAnimates()
    {
        var playback = new BlackjackDealPlayback();
        playback.Update(Board(Hand, BlackjackPhases.PlayerTurns, new[] { 4, -1 }, new[] { 8, 21 }), 0f);
        playback.Update(Board(NextHand, BlackjackPhases.Dealing, new[] { 9, -1 }, new[] { 10, 11 }), 1f);
        Assert.Equal(0f, playback.TravelOf(SeatSlot, 0));
    }

    [Fact]
    public void TheHoleCardFlipsExactlyOnceWhenItTurnsOver()
    {
        var playback = Primed();
        playback.Update(Board(Hand, BlackjackPhases.Dealing, new[] { 4, -1 }, new[] { 8, 21 }), 0f);
        playback.Update(Board(Hand, BlackjackPhases.PlayerTurns, new[] { 4, -1 }, new[] { 8, 21 }), 5f);
        Assert.False(playback.HoleRevealing());

        playback.Update(Board(Hand, BlackjackPhases.DealerPlay, new[] { 4, 20 }, new[] { 8, 21 }), 0.1f);
        Assert.True(playback.HoleRevealing());
        Assert.Equal(0f, playback.HoleReveal());

        playback.Update(Board(Hand, BlackjackPhases.DealerPlay, new[] { 4, 20 }, new[] { 8, 21 }),
            BlackjackDealChoreography.RevealSeconds);
        Assert.Equal(1f, playback.HoleReveal());
        Assert.False(playback.HoleRevealing());

        playback.Update(Board(Hand, BlackjackPhases.DealerPlay, new[] { 4, 20 }, new[] { 8, 21 }), 0.1f);
        Assert.False(playback.HoleRevealing());
    }

    [Fact]
    public void ASpectatorJoiningAfterTheRevealNeverSeesAFlip()
    {
        var playback = new BlackjackDealPlayback();
        playback.Update(Board(Hand, BlackjackPhases.DealerPlay, new[] { 4, 20 }, new[] { 8, 21 }), 0f);
        Assert.False(playback.HoleRevealing());
        Assert.Equal(1f, playback.HoleReveal());
    }

    [Fact]
    public void AnotherSeatsHandFlipsOnceWhenItTurnsOver()
    {
        var playback = Primed();
        playback.Update(Board(Hand, BlackjackPhases.Dealing, new[] { 4, -1 }, new[] { -1, -1 }), 0f);
        playback.Update(Board(Hand, BlackjackPhases.PlayerTurns, new[] { 4, -1 }, new[] { -1, -1 }), 5f);
        Assert.False(playback.HandRevealing(SeatSlot));
        Assert.Equal(1f, playback.HandReveal(SeatSlot));

        playback.Update(Board(Hand, BlackjackPhases.PlayerTurns, new[] { 4, -1 }, new[] { 8, 21 }), 0.1f);
        Assert.True(playback.HandRevealing(SeatSlot));
        Assert.Equal(0f, playback.HandReveal(SeatSlot));

        playback.Update(Board(Hand, BlackjackPhases.PlayerTurns, new[] { 4, -1 }, new[] { 8, 21 }),
            BlackjackDealChoreography.RevealSeconds);
        Assert.Equal(1f, playback.HandReveal(SeatSlot));
        Assert.False(playback.HandRevealing(SeatSlot));

        playback.Update(Board(Hand, BlackjackPhases.PlayerTurns, new[] { 4, -1 }, new[] { 8, 21 }), 0.1f);
        Assert.False(playback.HandRevealing(SeatSlot));
    }

    [Fact]
    public void YourOwnSeatNeverFlipsWhenItsHandTurnsOver()
    {
        var playback = Primed();
        playback.Update(Board(Hand, BlackjackPhases.Dealing, new[] { 4, -1 }, new[] { -1, -1 }), 0f, 0);
        playback.Update(Board(Hand, BlackjackPhases.PlayerTurns, new[] { 4, -1 }, new[] { -1, -1 }), 5f, 0);
        playback.Update(Board(Hand, BlackjackPhases.PlayerTurns, new[] { 4, -1 }, new[] { 8, 21 }), 0.1f, 0);

        Assert.False(playback.HandRevealing(SeatSlot));
        Assert.Equal(1f, playback.HandReveal(SeatSlot));
    }

    [Fact]
    public void AHandDealtFaceUpNeverFlips()
    {
        var playback = Primed();
        playback.Update(Board(Hand, BlackjackPhases.Dealing, new[] { 4, -1 }, new[] { 8, 21 }), 0f);
        playback.Update(Board(Hand, BlackjackPhases.PlayerTurns, new[] { 4, -1 }, new[] { 8, 21 }), 5f);

        Assert.False(playback.HandRevealing(SeatSlot));
    }

    [Fact]
    public void ASpectatorJoiningAfterAHandTurnedOverNeverSeesItFlip()
    {
        var playback = new BlackjackDealPlayback();
        playback.Update(Board(Hand, BlackjackPhases.PlayerTurns, new[] { 4, -1 }, new[] { 8, 21 }), 0f);

        Assert.False(playback.HandRevealing(SeatSlot));
        Assert.Equal(1f, playback.HandReveal(SeatSlot));
    }

    [Fact]
    public void ASplitFlipsOnlyTheHandThatTurnedOver()
    {
        var closed = new[] { -1, -1 };
        var playback = Primed();
        playback.Update(Board(Hand, BlackjackPhases.Dealing, new[] { 4, -1 }, new[] { closed, closed }), 0f);
        playback.Update(Board(Hand, BlackjackPhases.PlayerTurns, new[] { 4, -1 }, new[] { closed, closed }), 5f);
        playback.Update(Board(Hand, BlackjackPhases.PlayerTurns, new[] { 4, -1 }, new[] { new[] { 8, 21 }, closed }),
            0.1f);

        Assert.True(playback.HandRevealing(BlackjackDealPlayback.SlotOf(0, 0)));
        Assert.False(playback.HandRevealing(BlackjackDealPlayback.SlotOf(0, 1)));
    }

    [Fact]
    public void TheNextHandForgetsWhichHandsTurnedOver()
    {
        var playback = Primed();
        playback.Update(Board(Hand, BlackjackPhases.Dealing, new[] { 4, -1 }, new[] { -1, -1 }), 0f);
        playback.Update(Board(Hand, BlackjackPhases.PlayerTurns, new[] { 4, -1 }, new[] { 8, 21 }), 5f);
        playback.Update(Board(NextHand, BlackjackPhases.Dealing, new[] { 9, -1 }, new[] { 10, 11 }), 1f);

        Assert.False(playback.HandRevealing(SeatSlot));
        Assert.Equal(1f, playback.HandReveal(SeatSlot));
    }

    private static BlackjackDealPlayback Primed()
    {
        var playback = new BlackjackDealPlayback();
        playback.Update(Board(Hand, BlackjackPhases.Betting, null, (int[]?)null), 0f);
        return playback;
    }

    private static CasinoBlackjackRoomStateDto Board(string handId, int phase, int[]? dealerCards, int[]? seatCards)
    {
        return Board(handId, phase, dealerCards, seatCards is null ? null : new[] { seatCards });
    }

    private static CasinoBlackjackRoomStateDto Board(string handId, int phase, int[]? dealerCards,
        int[][]? seatHands)
    {
        CasinoBlackjackSeatDto[]? seats = null;
        if (seatHands is not null)
        {
            var hands = new CasinoBlackjackHandDto[seatHands.Length];
            for (var handIndex = 0; handIndex < seatHands.Length; handIndex++)
            {
                hands[handIndex] = new CasinoBlackjackHandDto(Cards: seatHands[handIndex]);
            }

            seats = new[]
            {
                new CasinoBlackjackSeatDto(SeatIndex: 0, UserId: "user-0", State: BlackjackSeatStates.Seated,
                    Hands: hands),
            };
        }

        return new CasinoBlackjackRoomStateDto(HandId: handId, Phase: phase, DealerCards: dealerCards, Seats: seats);
    }
}
