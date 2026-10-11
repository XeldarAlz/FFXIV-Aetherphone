using Aetherphone.Apps.Casino.DealerHoldem;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class DealerHoldemPlaybackTests
{
    private static readonly int[] Hole = { 0, 13 };
    private static readonly int[] Flop = { 26, 5, 44 };
    private static readonly int[] Board = { 26, 5, 44, 7, 20 };
    private static readonly int[] Dealer = { 3, 16 };

    private static CasinoDealerHoldemDto PreFlop(string roundId = "r1", long trips = 0) => new(
        Granted: true, RoundId: roundId, Ante: 1000, Blind: 1000, Trips: trips, Stake: 2000 + trips,
        Phase: DealerHoldemRules.PhasePreFlop, Actions: new[] { "check", "bet" }, Multiples: new[] { 3, 4 },
        PlayerCards: Hole, Board: Array.Empty<int>(), DealerCards: Array.Empty<int>());

    private static CasinoDealerHoldemDto AtFlop() => PreFlop() with
    {
        Phase = DealerHoldemRules.PhaseFlop, Step = 1, Multiples = new[] { 2 }, Board = Flop,
    };

    private static CasinoDealerHoldemDto Settled(long trips = 0, long tripsPayout = 0) => PreFlop(trips: trips) with
    {
        Phase = DealerHoldemRules.PhaseSettled, Step = 1, Play = 4000, PlayMultiple = 4, Stake = 6000 + trips,
        Actions = Array.Empty<string>(), Multiples = Array.Empty<int>(), Board = Board, DealerCards = Dealer,
        PlayerHand = HoldemHands.Pack(HoldemHands.Pair, 14, 13, 12, 7, 0),
        DealerHand = HoldemHands.Pack(HoldemHands.HighCard, 13, 12, 9, 7, 4), DealerQualifies = false,
        Outcome = DealerHoldemRules.OutcomeWin, AntePayout = 1000, BlindPayout = 1000, PlayPayout = 8000,
        TripsPayout = tripsPayout, Payout = 10000 + tripsPayout,
    };

    private static List<DealerHoldemCue> Drain(DealerHoldemPlayback playback, float seconds = 30f)
    {
        var cues = new List<DealerHoldemCue>();
        for (var step = 0; step < seconds * 60f; step++)
        {
            playback.Advance(1f / 60f);
            while (playback.TryTake(out var cue))
            {
                cues.Add(cue);
            }
        }

        return cues;
    }

    [Fact]
    public void TheDealAlternatesYouAndTheDealer()
    {
        var playback = new DealerHoldemPlayback();
        Assert.True(playback.Apply(PreFlop(), false));
        Assert.False(playback.TryTake(out _));
        var cues = Drain(playback);
        Assert.Equal(new[]
        {
            DealerHoldemCueKind.Hero, DealerHoldemCueKind.Dealer, DealerHoldemCueKind.Hero, DealerHoldemCueKind.Dealer,
        }, cues.Select(cue => cue.Kind).ToArray());
        Assert.Equal(new[] { 0, 0, 1, 1 }, cues.Select(cue => cue.Index).ToArray());
        Assert.True(playback.Deciding);
        Assert.Equal(2, playback.HeroShown);
        Assert.Equal(2, playback.DealerShown);
        Assert.Equal(0, playback.BoardShown);
        Assert.False(playback.Revealed);
    }

    [Fact]
    public void TheFlopLandsThreeBoardCardsAfterAPause()
    {
        var playback = new DealerHoldemPlayback();
        playback.Apply(PreFlop(), false);
        Drain(playback);
        playback.Apply(AtFlop(), false);
        Assert.True(playback.Busy);
        Assert.False(playback.Deciding);
        var cues = Drain(playback);
        Assert.Equal(3, cues.Count);
        Assert.All(cues, cue => Assert.Equal(DealerHoldemCueKind.Board, cue.Kind));
        Assert.True(cues[0].At >= DealerHoldemPlayback.StreetPause);
        Assert.Equal(3, playback.BoardShown);
        Assert.Equal(44, playback.Card(DealerHoldemCueKind.Board, 2));
    }

    [Fact]
    public void AShowdownRevealsThenResolvesEveryStakedBetInOrderThenSettles()
    {
        var playback = new DealerHoldemPlayback();
        playback.Apply(PreFlop(trips: 1000), false);
        Drain(playback);
        playback.Apply(Settled(1000, 4000), false);
        var cues = Drain(playback);
        var kinds = cues.Select(cue => cue.Kind).ToArray();
        Assert.Equal(new[]
        {
            DealerHoldemCueKind.Board, DealerHoldemCueKind.Board, DealerHoldemCueKind.Board,
            DealerHoldemCueKind.Board, DealerHoldemCueKind.Board, DealerHoldemCueKind.Reveal,
            DealerHoldemCueKind.Verdict, DealerHoldemCueKind.Resolve, DealerHoldemCueKind.Resolve,
            DealerHoldemCueKind.Resolve, DealerHoldemCueKind.Resolve, DealerHoldemCueKind.Settle,
        }, kinds);
        var resolved = cues.Where(cue => cue.Kind == DealerHoldemCueKind.Resolve).Select(cue => cue.Index).ToArray();
        Assert.Equal(new[]
        {
            (int)DealerHoldemSpot.Play, (int)DealerHoldemSpot.Ante, (int)DealerHoldemSpot.Blind,
            (int)DealerHoldemSpot.Trips,
        }, resolved);
        Assert.True(playback.Settled);
        Assert.True(playback.Revealed);
        Assert.True(playback.VerdictShown);
        Assert.False(playback.Busy);
    }

    [Fact]
    public void ANoTripsHandResolvesOnlyTheMainBets()
    {
        var playback = new DealerHoldemPlayback();
        playback.Apply(PreFlop(), true);
        playback.Apply(Settled(), true);
        var cues = Drain(playback, 0.1f);
        Assert.Equal(3, cues.Count(cue => cue.Kind == DealerHoldemCueKind.Resolve));
        Assert.DoesNotContain(cues, cue => cue.Kind == DealerHoldemCueKind.Resolve
                                           && cue.Index == (int)DealerHoldemSpot.Trips);
    }

    [Fact]
    public void JoiningASettledHandMidEventLandsTheFinalStateAtOnce()
    {
        var playback = new DealerHoldemPlayback();
        playback.Apply(Settled(1000, 4000), true);
        var count = 0;
        while (playback.TryTake(out var cue))
        {
            Assert.True(cue.Instant);
            count++;
        }

        Assert.True(count > 0);
        Assert.True(playback.Settled);
        Assert.Equal(2, playback.HeroShown);
        Assert.Equal(5, playback.BoardShown);
        Assert.True(playback.Revealed);
        Assert.True(playback.IsResolved(DealerHoldemSpot.Trips));
        Assert.False(playback.Open);
    }

    [Fact]
    public void ResumingAnOpenFlopShowsTheCardsAndWaitsForTheDecision()
    {
        var playback = new DealerHoldemPlayback();
        playback.Apply(AtFlop(), true);
        while (playback.TryTake(out _))
        {
        }

        Assert.True(playback.Deciding);
        Assert.True(playback.Open);
        Assert.Equal(3, playback.BoardShown);
        Assert.Equal(2, playback.DealerShown);
        Assert.False(playback.Revealed);
    }

    [Fact]
    public void TheSameViewTwiceSchedulesNothingNew()
    {
        var playback = new DealerHoldemPlayback();
        playback.Apply(AtFlop(), false);
        var first = Drain(playback).Count;
        playback.Apply(AtFlop(), false);
        Assert.Empty(Drain(playback));
        Assert.Equal(7, first);
    }

    [Fact]
    public void SnapMakesEveryQueuedCueDue()
    {
        var playback = new DealerHoldemPlayback();
        playback.Apply(PreFlop(), false);
        playback.Snap();
        var count = 0;
        while (playback.TryTake(out _))
        {
            count++;
        }

        Assert.Equal(4, count);
        Assert.True(playback.Deciding);
    }

    [Fact]
    public void ANewRoundIdStartsAFreshTable()
    {
        var playback = new DealerHoldemPlayback();
        playback.Apply(Settled(), true);
        Drain(playback, 0.1f);
        playback.Apply(PreFlop("r2"), false);
        Assert.Equal("r2", playback.RoundId);
        Assert.Equal(0, playback.HeroShown);
        Assert.Equal(0, playback.BoardShown);
        Assert.False(playback.Settled);
        Assert.False(playback.IsResolved(DealerHoldemSpot.Ante));
    }

    [Fact]
    public void EachBetReadsItsResultFromTheServerPayout()
    {
        var round = Settled(1000, 0);
        Assert.Equal(DealerHoldemResult.Win, DealerHoldemPlayback.ResultOf(round, DealerHoldemSpot.Play));
        Assert.Equal(DealerHoldemResult.Push, DealerHoldemPlayback.ResultOf(round, DealerHoldemSpot.Ante));
        Assert.Equal(DealerHoldemResult.Push, DealerHoldemPlayback.ResultOf(round, DealerHoldemSpot.Blind));
        Assert.Equal(DealerHoldemResult.Lose, DealerHoldemPlayback.ResultOf(round, DealerHoldemSpot.Trips));
        Assert.Equal(DealerHoldemResult.None, DealerHoldemPlayback.ResultOf(PreFlop(), DealerHoldemSpot.Play));
    }

    [Fact]
    public void AFoldFinishesWithoutAPlayBet()
    {
        var playback = new DealerHoldemPlayback();
        var folded = Settled() with
        {
            Play = 0, PlayMultiple = 0, Stake = 2000, Folded = true, Outcome = DealerHoldemRules.OutcomeFolded,
            AntePayout = 0, BlindPayout = 0, PlayPayout = 0, Payout = 0,
        };
        playback.Apply(folded, true);
        var cues = Drain(playback, 0.1f);
        Assert.DoesNotContain(cues, cue => cue.Kind == DealerHoldemCueKind.Resolve
                                           && cue.Index == (int)DealerHoldemSpot.Play);
        Assert.Equal(DealerHoldemResult.Lose, DealerHoldemPlayback.ResultOf(folded, DealerHoldemSpot.Ante));
        Assert.True(playback.Settled);
    }

    [Fact]
    public void AFinishingViewOffersNoDecision()
    {
        var playback = new DealerHoldemPlayback();
        playback.Apply(PreFlop() with { Actions = Array.Empty<string>(), Multiples = Array.Empty<int>() }, true);
        while (playback.TryTake(out _))
        {
        }

        Assert.True(playback.Finishing);
        Assert.False(playback.Deciding);
        Assert.True(playback.Open);
    }

    [Fact]
    public void ARoundWithoutAnIdIsIgnored()
    {
        var playback = new DealerHoldemPlayback();
        Assert.False(playback.Apply(new CasinoDealerHoldemDto(), false));
        Assert.False(playback.HasRound);
    }
}
