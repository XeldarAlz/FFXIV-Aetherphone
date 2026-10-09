using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class DealerHoldemRulesTests
{
    [Fact]
    public void TheBlindTableMatchesTheWireDoc()
    {
        Assert.Equal(1000 + 500_000, DealerHoldemRules.BlindReturn(1000, HoldemHands.RoyalFlush));
        Assert.Equal(1000 + 50_000, DealerHoldemRules.BlindReturn(1000, HoldemHands.StraightFlush));
        Assert.Equal(1000 + 10_000, DealerHoldemRules.BlindReturn(1000, HoldemHands.Quads));
        Assert.Equal(1000 + 3000, DealerHoldemRules.BlindReturn(1000, HoldemHands.FullHouse));
        Assert.Equal(1000 + 1500, DealerHoldemRules.BlindReturn(1000, HoldemHands.Flush));
        Assert.Equal(1000 + 1000, DealerHoldemRules.BlindReturn(1000, HoldemHands.Straight));
        Assert.Equal(1000, DealerHoldemRules.BlindReturn(1000, HoldemHands.Trips));
        Assert.Equal(1000, DealerHoldemRules.BlindReturn(1000, HoldemHands.HighCard));
    }

    [Fact]
    public void AnOddFlushBlindFloorsTheHalfUnit()
    {
        Assert.Equal(101 + 151, DealerHoldemRules.BlindReturn(101, HoldemHands.Flush));
    }

    [Fact]
    public void TheTripsTableMatchesTheWireDoc()
    {
        Assert.Equal(1000 * 51, DealerHoldemRules.TripsReturn(1000, HoldemHands.RoyalFlush));
        Assert.Equal(1000 * 41, DealerHoldemRules.TripsReturn(1000, HoldemHands.StraightFlush));
        Assert.Equal(1000 * 31, DealerHoldemRules.TripsReturn(1000, HoldemHands.Quads));
        Assert.Equal(1000 * 9, DealerHoldemRules.TripsReturn(1000, HoldemHands.FullHouse));
        Assert.Equal(1000 * 8, DealerHoldemRules.TripsReturn(1000, HoldemHands.Flush));
        Assert.Equal(1000 * 5, DealerHoldemRules.TripsReturn(1000, HoldemHands.Straight));
        Assert.Equal(1000 * 4, DealerHoldemRules.TripsReturn(1000, HoldemHands.Trips));
        Assert.Equal(0, DealerHoldemRules.TripsReturn(1000, HoldemHands.TwoPair));
        Assert.Equal(0, DealerHoldemRules.TripsReturn(1000, HoldemHands.Pair));
    }

    [Fact]
    public void ThePayTableRowsRunFromRoyalDownToTrips()
    {
        Assert.Equal(7, DealerHoldemRules.PayCategories.Length);
        Assert.Equal(HoldemHands.RoyalFlush, DealerHoldemRules.PayCategories[0]);
        Assert.Equal(HoldemHands.Trips, DealerHoldemRules.PayCategories[^1]);
        for (var index = 0; index < 6; index++)
        {
            Assert.True(DealerHoldemRules.BlindPaysOn(DealerHoldemRules.PayCategories[index]));
        }

        Assert.False(DealerHoldemRules.BlindPaysOn(HoldemHands.Trips));
    }

    [Fact]
    public void TheDealerQualifiesWithAPairOrBetter()
    {
        Assert.False(DealerHoldemRules.Qualifies(-1));
        Assert.False(DealerHoldemRules.Qualifies(HoldemHands.Pack(HoldemHands.HighCard, 14, 13, 9, 5, 3)));
        Assert.True(DealerHoldemRules.Qualifies(HoldemHands.Pack(HoldemHands.Pair, 2, 14, 13, 9, 0)));
        Assert.True(DealerHoldemRules.Qualifies(HoldemHands.Pack(HoldemHands.Flush, 14, 9, 7, 5, 2)));
    }

    [Fact]
    public void AnOpeningNeedsTheMinimumAnteAndTripsOnTheLadderUpToTheAnte()
    {
        Assert.True(DealerHoldemRules.IsOpening(DealerHoldemRules.MinAnte, 0));
        Assert.False(DealerHoldemRules.IsOpening(DealerHoldemRules.MinAnte - 1, 0));
        Assert.True(DealerHoldemRules.IsOpening(1000, 1000));
        Assert.False(DealerHoldemRules.IsOpening(1000, 2500));
        Assert.False(DealerHoldemRules.IsOpening(1000, -1));
        Assert.Equal(1000, DealerHoldemRules.TripsFor(1000));
        Assert.True(CasinoLadder.IsRung(DealerHoldemRules.TripsFor(1000)));
    }

    [Fact]
    public void TheOpeningStakeIsAnteBlindAndTrips()
    {
        Assert.Equal(2000, DealerHoldemRules.StakeAtDeal(1000, 0));
        Assert.Equal(3000, DealerHoldemRules.StakeAtDeal(1000, 1000));
    }

    [Fact]
    public void ThePhasesAndBoardSizesMatchTheWireDoc()
    {
        Assert.Equal(0, DealerHoldemRules.BoardShown(DealerHoldemRules.PhasePreFlop));
        Assert.Equal(3, DealerHoldemRules.BoardShown(DealerHoldemRules.PhaseFlop));
        Assert.Equal(5, DealerHoldemRules.BoardShown(DealerHoldemRules.PhaseRiver));
        Assert.Equal(5, DealerHoldemRules.BoardShown(DealerHoldemRules.PhaseSettled));
        Assert.True(DealerHoldemRules.IsDecision(DealerHoldemRules.PhaseRiver));
        Assert.False(DealerHoldemRules.IsDecision(DealerHoldemRules.PhaseSettled));
        Assert.True(DealerHoldemRules.IsOver(DealerHoldemRules.PhaseVoided));
        Assert.Equal(3, DealerHoldemRules.PreFlopLow);
        Assert.Equal(4, DealerHoldemRules.PreFlopHigh);
        Assert.Equal(2, DealerHoldemRules.FlopMultiple);
        Assert.Equal(1, DealerHoldemRules.RiverMultiple);
    }

    [Fact]
    public void ABetResultReadsWinPushOrLossFromTheReturn()
    {
        Assert.Equal(DealerHoldemResult.Win, DealerHoldemRules.ResultOf(1000, 2000));
        Assert.Equal(DealerHoldemResult.Push, DealerHoldemRules.ResultOf(1000, 1000));
        Assert.Equal(DealerHoldemResult.Lose, DealerHoldemRules.ResultOf(1000, 0));
        Assert.Equal(DealerHoldemResult.None, DealerHoldemRules.ResultOf(0, 0));
    }

    [Fact]
    public void TheActionsAreTheWireTokens()
    {
        Assert.True(DealerHoldemRules.IsAction("check"));
        Assert.True(DealerHoldemRules.IsAction("bet"));
        Assert.True(DealerHoldemRules.IsAction("fold"));
        Assert.False(DealerHoldemRules.IsAction("raise"));
    }
}
