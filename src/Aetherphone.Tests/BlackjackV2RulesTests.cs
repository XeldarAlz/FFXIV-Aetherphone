using Aetherphone.Core.Casino;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class BlackjackV2RulesTests
{
    [Fact]
    public void TheActionsMaskAndOutcomesMirrorTheBackend()
    {
        Assert.Equal(1, BlackjackRules.ActionHit);
        Assert.Equal(2, BlackjackRules.ActionStand);
        Assert.Equal(4, BlackjackRules.ActionDouble);
        Assert.Equal(8, BlackjackRules.ActionSplit);
        Assert.Equal(16, BlackjackRules.ActionInsurance);
        Assert.Equal(32, BlackjackRules.ActionSurrender);
        Assert.Equal(6, BlackjackOutcomes.Surrender);
        Assert.Equal(8, BlackjackRules.InsuranceSeconds);
        Assert.Equal(100, BlackjackSideBets.SideBetMin);
        Assert.Equal(4, BlackjackRules.HouseTierCount);
    }

    [Fact]
    public void EveryActionHasItsWireVerb()
    {
        Assert.Equal("insurance", BlackjackActions.VerbFor(BlackjackRules.ActionInsurance));
        Assert.Equal("no_insurance", BlackjackActions.VerbFor(BlackjackRules.ActionDeclineInsurance));
        Assert.Equal("surrender", BlackjackActions.VerbFor(BlackjackRules.ActionSurrender));
        Assert.True(BlackjackActions.IsWager(BlackjackActions.Insurance));
        Assert.False(BlackjackActions.IsWager(BlackjackActions.NoInsurance));
        Assert.False(BlackjackActions.IsWager(BlackjackActions.Surrender));
    }

    [Fact]
    public void DecliningInsuranceRidesOnTheInsuranceBit()
    {
        Assert.True(BlackjackRules.Allows(BlackjackRules.ActionInsurance, BlackjackRules.ActionDeclineInsurance));
        Assert.False(BlackjackRules.Allows(BlackjackRules.ActionHit, BlackjackRules.ActionDeclineInsurance));
        Assert.True(BlackjackRules.Allows(BlackjackRules.ActionSurrender | BlackjackRules.ActionHit,
            BlackjackRules.ActionSurrender));
    }

    [Theory]
    [InlineData(2_500, 1_250)]
    [InlineData(2_501, 1_250)]
    [InlineData(0, 0)]
    public void InsuranceAndSurrenderAreHalfTheBetRoundedDown(long bet, long half)
    {
        Assert.Equal(half, BlackjackRules.InsuranceFor(bet));
        Assert.Equal(half, BlackjackRules.SurrenderReturn(bet));
    }

    [Fact]
    public void InsurancePaysTwoToOneOnlyAgainstANatural()
    {
        Assert.Equal(3_750, BlackjackSideBets.InsuranceReturn(1_250, true));
        Assert.Equal(0, BlackjackSideBets.InsuranceReturn(1_250, false));
    }

    [Theory]
    [InlineData(4, 4 + 13, BlackjackSideBets.PairMixed)]
    [InlineData(4 + 13, 4 + 26, BlackjackSideBets.PairColoured)]
    [InlineData(4, 4 + 39, BlackjackSideBets.PairColoured)]
    [InlineData(4, 4, BlackjackSideBets.PairPerfect)]
    [InlineData(10, 11, BlackjackSideBets.PairNone)]
    [InlineData(0, 52, BlackjackSideBets.PairPerfect)]
    public void PerfectPairsReadsRankSuitAndColour(int first, int second, int kind)
    {
        Assert.Equal(kind, BlackjackSideBets.PerfectPairsKind(first, second));
    }

    [Fact]
    public void TheAceTurnsNoCornerInTwentyOnePlusThree()
    {
        Assert.Equal(BlackjackSideBets.ThreeStraight, BlackjackSideBets.TwentyOnePlusThreeKind(
            PlayingCards.Encode(0, PlayingCards.Spades), PlayingCards.Encode(1, PlayingCards.Hearts),
            PlayingCards.Encode(2, PlayingCards.Clubs)));
        Assert.Equal(BlackjackSideBets.ThreeStraight, BlackjackSideBets.TwentyOnePlusThreeKind(
            PlayingCards.Encode(11, PlayingCards.Spades), PlayingCards.Encode(12, PlayingCards.Hearts),
            PlayingCards.Encode(0, PlayingCards.Clubs)));
        Assert.Equal(BlackjackSideBets.ThreeNone, BlackjackSideBets.TwentyOnePlusThreeKind(
            PlayingCards.Encode(12, PlayingCards.Spades), PlayingCards.Encode(0, PlayingCards.Hearts),
            PlayingCards.Encode(1, PlayingCards.Clubs)));
        Assert.Equal(BlackjackSideBets.ThreeSuitedTrips, BlackjackSideBets.TwentyOnePlusThreeKind(
            PlayingCards.Encode(6, PlayingCards.Hearts), PlayingCards.Encode(6, PlayingCards.Hearts),
            PlayingCards.Encode(6, PlayingCards.Hearts)));
    }

    [Fact]
    public void ReturnsHandTheStakeBackWithTheWin()
    {
        Assert.Equal(1_300, BlackjackSideBets.ReturnFor(BlackjackSideBet.PerfectPairs, 100,
            BlackjackSideBets.PairColoured));
        Assert.Equal(10_100, BlackjackSideBets.ReturnFor(BlackjackSideBet.TwentyOnePlusThree, 100,
            BlackjackSideBets.ThreeSuitedTrips));
        Assert.Equal(0, BlackjackSideBets.ReturnFor(BlackjackSideBet.TwentyOnePlusThree, 100,
            BlackjackSideBets.ThreeNone));
    }

    [Theory]
    [InlineData(0, 2_500, true)]
    [InlineData(100, 2_500, true)]
    [InlineData(2_500, 2_500, true)]
    [InlineData(99, 2_500, false)]
    [InlineData(5_000, 2_500, false)]
    public void ASideBetIsNoneOrBetweenTheMinimumAndTheMainBet(long amount, long mainBet, bool valid)
    {
        Assert.Equal(valid, BlackjackSideBets.IsValidSideBet(amount, mainBet));
    }

    [Theory]
    [InlineData(9_999, 0)]
    [InlineData(10_000, 1)]
    [InlineData(250_000, 2)]
    [InlineData(999_999, 2)]
    [InlineData(1_000_000, 3)]
    public void QuickSeatPicksTheHighestTierTheCeilingCovers(long ceiling, int tier)
    {
        Assert.Equal(tier, BlackjackRules.QuickTierFor(ceiling));
    }

    [Fact]
    public void TheVaultIsAHouseRoom()
    {
        Assert.True(CasinoRoomIds.IsBlackjackHouse(CasinoRoomIds.BlackjackVault));
        Assert.False(CasinoRoomIds.IsBlackjackHouse("table-a1b2"));
    }
}
