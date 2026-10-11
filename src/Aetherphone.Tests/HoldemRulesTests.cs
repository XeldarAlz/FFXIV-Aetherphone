using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HoldemRulesTests
{
    [Fact]
    public void TheHouseRoomsMirrorTheBackendStakes()
    {
        Assert.Equal(new[] { "holdem-low", "holdem-mid", "holdem-high", "holdem-royal" }, HoldemRules.HouseRooms);
        Assert.Equal(50, HoldemRules.SmallBlindFor(HoldemRules.LowTier));
        Assert.Equal(100, HoldemRules.BigBlindFor(HoldemRules.LowTier));
        Assert.Equal(1_000, HoldemRules.BigBlindFor(HoldemRules.MidTier));
        Assert.Equal(10_000, HoldemRules.BigBlindFor(HoldemRules.HighTier));
        Assert.Equal(100_000, HoldemRules.BigBlindFor(HoldemRules.RoyalTier));
        Assert.Equal(2_000, HoldemRules.MinBuyInFor(HoldemRules.LowTier));
        Assert.Equal(10_000, HoldemRules.MaxBuyInFor(HoldemRules.LowTier));
        Assert.Equal(2_000_000, HoldemRules.MinBuyInFor(HoldemRules.RoyalTier));
        Assert.Equal(10_000_000, HoldemRules.MaxBuyInFor(HoldemRules.RoyalTier));
        Assert.Equal(6, HoldemRules.HouseSeats);
        Assert.Equal(9, HoldemRules.MaxSeats);
        Assert.Equal(1_000_000_000, HoldemRules.RoyalBalanceFloor);
        Assert.Equal(3, HoldemRules.TierOfRoom("holdem-royal"));
        Assert.Equal(-1, HoldemRules.TierOfRoom("blackjack-pit"));
    }

    [Fact]
    public void TheTimingMirrorsTheBackend()
    {
        Assert.Equal(20, HoldemRules.TurnSeconds);
        Assert.Equal(800, HoldemRules.RevealMillisecondsPerCard);
        Assert.Equal(1_000, HoldemRules.ShowdownMillisecondsPerReveal);
        Assert.Equal(10, HoldemRules.ShowWindowSeconds);
        Assert.Equal(6, HoldemRules.IntermissionSeconds);
        Assert.Equal(30, HoldemRules.RejoinPenaltyMinutes);
        Assert.Equal(3, HoldemRules.TimeBankUses);
        Assert.Equal(10, HoldemRules.TimeBankSeconds);
    }

    [Fact]
    public void ActionBitsAndVerbsMirrorTheWire()
    {
        Assert.Equal("fold", HoldemActions.VerbFor(HoldemActions.Fold));
        Assert.Equal("check", HoldemActions.VerbFor(HoldemActions.Check));
        Assert.Equal("call", HoldemActions.VerbFor(HoldemActions.Call));
        Assert.Equal("bet", HoldemActions.VerbFor(8));
        Assert.Equal("raise", HoldemActions.VerbFor(16));
        Assert.Equal("allin", HoldemActions.VerbFor(32));
        Assert.Equal("show", HoldemActions.VerbFor(64));
        Assert.Equal("muck", HoldemActions.VerbFor(128));
        Assert.True(HoldemActions.OnlyShowOrMuck(64 | 128));
        Assert.False(HoldemActions.OnlyShowOrMuck(1 | 64));
    }

    [Fact]
    public void RaisesSnapToBigBlindsInsideTheLegalBand()
    {
        Assert.Equal(400, HoldemRules.SnapRaise(380, 100, 400, 9_600));
        Assert.Equal(500, HoldemRules.SnapRaise(460, 100, 400, 9_600));
        Assert.Equal(9_600, HoldemRules.SnapRaise(9_580, 100, 400, 9_600));
        Assert.Equal(9_600, HoldemRules.SnapRaise(20_000, 100, 400, 9_600));
        Assert.Equal(250, HoldemRules.SnapRaise(100, 100, 400, 250));
    }

    [Fact]
    public void StepperMovesOneBigBlindAndLeavesAllInCleanly()
    {
        Assert.Equal(500, HoldemRules.Step(400, 1, 100, 400, 9_650));
        Assert.Equal(400, HoldemRules.Step(500, -1, 100, 400, 9_650));
        Assert.Equal(400, HoldemRules.Step(400, -1, 100, 400, 9_650));
        Assert.Equal(9_600, HoldemRules.Step(9_650, -1, 100, 400, 9_650));
    }

    [Fact]
    public void PotFractionsCallFirstThenRaiseByThePot()
    {
        Assert.Equal(600, HoldemRules.PotFraction(1, 1, 100, 100, 300, 100, 400, 10_000));
        Assert.Equal(400, HoldemRules.PotFraction(1, 2, 100, 100, 300, 100, 400, 10_000));
        Assert.Equal(1_000, HoldemRules.PotFraction(1, 1, 0, 0, 1_000, 100, 100, 10_000));
        Assert.Equal(10_000, HoldemRules.PotFraction(1, 1, 0, 0, 1_000_000, 100, 100, 10_000));
    }

    [Fact]
    public void QuickRowAmountsAreLegal()
    {
        var model = new HoldemRaiseModel(16 | 32, 100, 100, 400, 9_600, 300, 100, true);
        var minimum = HoldemRaiseComposer.Minimum(model);
        for (var index = 0; index < 5; index++)
        {
            var amount = HoldemRaiseComposer.QuickAmount(index, model, minimum);
            Assert.InRange(amount, minimum, model.MaxRaiseTo);
        }

        Assert.Equal(9_600, HoldemRaiseComposer.QuickAmount(4, model, minimum));
    }

    [Fact]
    public void SliderMapsTheBandOnBigBlindMultiples()
    {
        Assert.Equal(400, HoldemRules.AmountAt(0f, 100, 400, 9_600));
        Assert.Equal(9_600, HoldemRules.AmountAt(1f, 100, 400, 9_600));
        Assert.Equal(0, HoldemRules.AmountAt(0.5f, 100, 400, 9_600) % 100);
        Assert.Equal(1f, HoldemRules.SliderFraction(9_600, 400, 9_600));
    }

    [Fact]
    public void BuyInsRespectBandBankrollAndCeiling()
    {
        Assert.Equal(10_000, HoldemRules.BuyInCeiling(10_000, 1_000_000, 10_000, false));
        Assert.Equal(5_000, HoldemRules.BuyInCeiling(10_000, 5_000, 10_000, false));
        Assert.Equal(2_000, HoldemRules.BuyInCeiling(10_000, 1_000_000, 100, false));
        Assert.False(HoldemRules.CanBuyIn(2_000, 10_000, 1_500, 10_000, false));
        Assert.True(HoldemRules.CanBuyIn(2_000, 10_000, 0, 0, true));
        Assert.Equal(10_000, HoldemRules.DefaultBuyIn(2_000, 10_000, 50_000, 10_000, 100, false));
        Assert.Equal(3_000, HoldemRules.StepBuyIn(2_000, 1, 100, 2_000, 10_000));
        Assert.Equal(2_000, HoldemRules.StepBuyIn(2_000, -1, 100, 2_000, 10_000));
        Assert.Equal(1_200, HoldemRules.TopUpRoom(8_800, 10_000));
    }

    [Fact]
    public void BlindsAndDealOrderFollowTheButton()
    {
        HoldemRules.Blinds(new[] { 0, 2, 4 }, 2, out var small, out var big);
        Assert.Equal(4, small);
        Assert.Equal(0, big);
        HoldemRules.Blinds(new[] { 1, 3 }, 3, out small, out big);
        Assert.Equal(3, small);
        Assert.Equal(1, big);
        var order = new int[3];
        Assert.Equal(3, HoldemRules.DealOrder(new[] { 0, 2, 4 }, 2, order));
        Assert.Equal(new[] { 4, 0, 2 }, order);
    }

    [Fact]
    public void ThePreflopLabelReadsPocketPairsAndHighCards()
    {
        var pair = HoldemTable.Strength(new[] { 12, 25 });
        Assert.Equal(HoldemHands.Pair, HoldemHands.CategoryOf(pair));
        Assert.Equal(13, HoldemHands.KickerOf(pair, 0));
        var high = HoldemTable.Strength(new[] { 0, 22 });
        Assert.Equal(HoldemHands.HighCard, HoldemHands.CategoryOf(high));
        Assert.Equal(14, HoldemHands.KickerOf(high, 0));
        Assert.Equal(-1, HoldemTable.Strength(new[] { 0 }));
    }

    [Fact]
    public void TheRoyalFlushIsADisplayCaseOfTheStraightFlush()
    {
        var royal = HoldemHands.Evaluate(new[] { 0, 12, 11, 10, 9 });
        Assert.Equal(HoldemHands.StraightFlush, HoldemHands.CategoryOf(royal));
        Assert.Equal(HoldemHands.RoyalFlush, HoldemHands.DisplayCategoryOf(royal));
        var wheel = HoldemHands.Evaluate(new[] { 0, 1, 2, 3, 17 });
        Assert.Equal(HoldemHands.Straight, HoldemHands.CategoryOf(wheel));
        Assert.Equal(5, HoldemHands.KickerOf(wheel, 0));
    }

    [Fact]
    public void BalanceTitlesMapFromTheWire()
    {
        Assert.Equal(Apps.Casino.Stage.BalanceTitle.Whale, HoldemTable.TitleOf("whale"));
        Assert.Equal(Apps.Casino.Stage.BalanceTitle.HighRoller, HoldemTable.TitleOf("high_roller"));
        Assert.Equal(Apps.Casino.Stage.BalanceTitle.None, HoldemTable.TitleOf(string.Empty));
    }

    [Fact]
    public void TheRejoinPenaltyLastsThirtyMinutes()
    {
        Assert.True(HoldemStore.RejoinPenaltyApplies(1_000, 1_000 + 29 * 60_000));
        Assert.False(HoldemStore.RejoinPenaltyApplies(1_000, 1_000 + 31 * 60_000));
        Assert.False(HoldemStore.RejoinPenaltyApplies(0, 5_000));
    }

    [Fact]
    public void IdempotentIntentsReuseTheirClientIds()
    {
        Assert.True(HoldemStore.ReusesSit("holdem-low", 2, 10_000, "holdem-low", 2, 10_000));
        Assert.False(HoldemStore.ReusesSit("holdem-low", 2, 10_000, "holdem-mid", 2, 10_000));
        Assert.True(HoldemStore.ReusesAct("h", 7, 16, 300, "h", 7, 16, 300));
        Assert.False(HoldemStore.ReusesAct("h", 7, 16, 300, "h", 8, 16, 300));
    }

    [Fact]
    public void TheHandRouteIsReadWhenTheLaneMissesTheHand()
    {
        var board = new CasinoHoldemRoomStateDto(HandId: "h", ActionCount: 3, Seats: new[]
        {
            new CasinoHoldemSeatDto(0, "me", State: HoldemSeatStates.InHand),
        });
        Assert.True(HoldemStore.NeedsHand(board, null, "me", true));
        Assert.False(HoldemStore.NeedsHand(board, new CasinoHoldemYouDto("h", ActionCount: 1), "me", true));
        Assert.True(HoldemStore.NeedsHand(board, new CasinoHoldemYouDto("h", ActionCount: 1), "me", false));
        Assert.False(HoldemStore.NeedsHand(board, null, "someone", false));
    }
}
