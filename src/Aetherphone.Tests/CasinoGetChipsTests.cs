using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoGetChipsTests
{
    private const long Rate = 1_000;

    [Fact]
    public void TheSheetOffersTenFiftyAndAHundredBetsInWholeCoins()
    {
        var options = new ChipsOption[ChipsAmounts.OptionCount];

        var count = ChipsAmounts.ForNeed(5_000, 0, 1_016_251, Rate, ChipsNeedKind.Bet, options);

        Assert.Equal(3, count);
        Assert.Equal(new ChipsOption(50, 50_000, 10), options[0]);
        Assert.Equal(new ChipsOption(250, 250_000, 50), options[1]);
        Assert.Equal(new ChipsOption(500, 500_000, 100), options[2]);
    }

    [Fact]
    public void TheStackAlreadyHeldCountsTowardEveryOption()
    {
        var options = new ChipsOption[ChipsAmounts.OptionCount];

        var count = ChipsAmounts.ForNeed(5_000, 37_400, 1_000_000, Rate, ChipsNeedKind.Bet, options);

        Assert.Equal(3, count);
        Assert.Equal(13, options[0].Coins);
        Assert.Equal(13_000, options[0].Chips);
        Assert.Equal(10, options[0].Covers);
        Assert.Equal(213, options[1].Coins);
        Assert.Equal(463, options[2].Coins);
    }

    [Fact]
    public void AmountsRoundUpToWholeCoinsAndNeverBelowOne()
    {
        var options = new ChipsOption[ChipsAmounts.OptionCount];

        ChipsAmounts.ForNeed(250, 0, 1_000, Rate, ChipsNeedKind.Bet, options);

        Assert.Equal(3, options[0].Coins);
        Assert.Equal(25, options[2].Coins);
        Assert.Equal(1, ChipsAmounts.CoinsToReach(500, 400, Rate));
        Assert.Equal(1, ChipsAmounts.CoinsToReach(100, 5_000, Rate));
        Assert.Equal(2, ChipsAmounts.CoinsFor(1_001, Rate));
        Assert.Equal(1, ChipsAmounts.CoinsFor(1_000, Rate));
        Assert.Equal(0, ChipsAmounts.CoinsFor(0, Rate));
    }

    [Fact]
    public void AShortWalletClampsTheOptionsAndDropsTheDuplicates()
    {
        var options = new ChipsOption[ChipsAmounts.OptionCount];

        var count = ChipsAmounts.ForNeed(100_000, 0, 1_500, Rate, ChipsNeedKind.Bet, options);

        Assert.Equal(2, count);
        Assert.Equal(1_000, options[0].Coins);
        Assert.Equal(10, options[0].Covers);
        Assert.Equal(1_500, options[1].Coins);
        Assert.Equal(15, options[1].Covers);
    }

    [Fact]
    public void AnEmptyWalletOrAMissingNeedOffersNothing()
    {
        var options = new ChipsOption[ChipsAmounts.OptionCount];

        Assert.Equal(0, ChipsAmounts.ForNeed(5_000, 0, 0, Rate, ChipsNeedKind.Bet, options));
        Assert.Equal(0, ChipsAmounts.ForNeed(0, 0, 500, Rate, ChipsNeedKind.Bet, options));
        Assert.Equal(0, ChipsAmounts.ForNeed(5_000, 0, 500, 0, ChipsNeedKind.Bet, options));
    }

    [Fact]
    public void ATableBuyInOffersOneTwoAndFiveRacks()
    {
        var options = new ChipsOption[ChipsAmounts.OptionCount];

        var count = ChipsAmounts.ForNeed(200_000, 50_000, 10_000, Rate, ChipsNeedKind.BuyIn, options);

        Assert.Equal(3, count);
        Assert.Equal(150, options[0].Coins);
        Assert.Equal(1, options[0].Covers);
        Assert.Equal(350, options[1].Coins);
        Assert.Equal(950, options[2].Coins);
        Assert.Equal(new[] { 1, 2, 5 }, ChipsAmounts.MultiplesFor(ChipsNeedKind.BuyIn));
        Assert.Equal(new[] { 10, 50, 100 }, ChipsAmounts.MultiplesFor(ChipsNeedKind.Bet));
    }

    [Fact]
    public void TheCashierQuickAmountsScaleWithTheWallet()
    {
        var quick = new long[ChipsAmounts.QuickCount];

        Assert.Equal(3, ChipsAmounts.Quick(1_016_251, quick));
        Assert.Equal(new long[] { 10_000, 50_000, 200_000 }, quick);

        Assert.Equal(3, ChipsAmounts.Quick(150, quick));
        Assert.Equal(new long[] { 1, 5, 20 }, quick);

        Assert.Equal(1, ChipsAmounts.Quick(3, quick));
        Assert.Equal(1, quick[0]);
        Assert.Equal(0, ChipsAmounts.Quick(0, quick));
    }

    [Fact]
    public void NiceFloorStepsOneTwoFive()
    {
        Assert.Equal(0, ChipsAmounts.NiceFloor(0));
        Assert.Equal(1, ChipsAmounts.NiceFloor(1));
        Assert.Equal(2, ChipsAmounts.NiceFloor(4));
        Assert.Equal(5, ChipsAmounts.NiceFloor(9));
        Assert.Equal(10, ChipsAmounts.NiceFloor(19));
        Assert.Equal(200_000, ChipsAmounts.NiceFloor(254_062));
        Assert.Equal(50_000, ChipsAmounts.NiceFloor(50_812));
    }

    [Fact]
    public void AutoTopUpBuysTenBetsWhenTheWalletCoversTheBet()
    {
        Assert.Equal(50, AutoTopUpRule.CoinsFor(true, 5_000, 0, 1_000, Rate));
        Assert.Equal(48, AutoTopUpRule.CoinsFor(true, 5_000, 2_500, 1_000, Rate));
        Assert.Equal(3, AutoTopUpRule.CoinsFor(true, 5_000, 2_500, 3, Rate));
    }

    [Fact]
    public void AutoTopUpStaysOutOfTheWayWhenItCannotHelp()
    {
        Assert.Equal(0, AutoTopUpRule.CoinsFor(false, 5_000, 0, 1_000, Rate));
        Assert.Equal(0, AutoTopUpRule.CoinsFor(true, 5_000, 5_000, 1_000, Rate));
        Assert.Equal(0, AutoTopUpRule.CoinsFor(true, 5_000, 0, 4, Rate));
        Assert.Equal(0, AutoTopUpRule.CoinsFor(true, 5_000, 0, 0, Rate));
        Assert.Equal(0, AutoTopUpRule.CoinsFor(true, 5_000, 0, 1_000, 0));
    }

    [Fact]
    public void AutoTopUpRestsAfterARefusal()
    {
        Assert.False(AutoTopUpRule.Resting(0, 1_000_000));
        Assert.True(AutoTopUpRule.Resting(1_000_000, 1_000_000 + AutoTopUpRule.RetryAfterRefusalMilliseconds - 1));
        Assert.False(AutoTopUpRule.Resting(1_000_000, 1_000_000 + AutoTopUpRule.RetryAfterRefusalMilliseconds));
    }

    [Fact]
    public void ChipBalancesReadTheirCoinValueInWholeCoins()
    {
        var text = new ChipValueText();

        Assert.Equal(5_111, ChipValue.Coins(5_111_825, Rate));
        Assert.Equal(-12, ChipValue.Coins(-12_500, Rate));
        Assert.Equal(0, ChipValue.Coins(999, Rate));
        Assert.Equal(0, ChipValue.Coins(5_000, 0));
        Assert.Equal(Loc.T(L.Strip.CoinsAmount, NumberText.Group(5_111)), text.Full(5_111_825, Rate));
        Assert.Equal(Loc.T(L.Strip.CoinsAmount, NumberText.Compact(5_111)), text.Compact(5_111_825, Rate));
        Assert.Same(text.Full(5_111_825, Rate), text.Full(5_111_825, Rate));
    }

    [Fact]
    public void TheMaxWinLinePrintsChipsAndCoins()
    {
        var text = new MaxWinText();

        Assert.Equal(Loc.T(L.Chips.MaxWinLine, NumberText.Compact(50_000_000), NumberText.Group(50_000)),
            text.For(50_000_000, Rate));
        Assert.Same(text.For(50_000_000, Rate), text.For(50_000_000, Rate));
        Assert.Equal(string.Empty, text.For(0, Rate));
    }

    [Fact]
    public void ABoughtStackLandsInTheStateBeforeTheNextRefresh()
    {
        var state = new CasinoStateDto(Balance: 1_000, Sitting: new CasinoSittingDto(Id: "s1", Stack: 2_500));
        var bought = new CasinoBuyChipsDto(true, string.Empty, 50, 50_000, 52_500, 950,
            new CasinoSittingDto(Id: "s1", State: 1, Stack: 52_500),
            new CasinoCeilingDto(MaxBet: 10_000, Reason: CasinoLadder.ReasonLevelKey));

        var next = CasinoStore.BuyAbsorbedInto(state, bought);

        Assert.Equal(52_500, next.Sitting!.Stack);
        Assert.Equal(950, next.Balance);
        Assert.Equal(10_000, next.Ceiling!.MaxBet);
    }

    [Fact]
    public void AFirstBuyOpensTheBankrollAndARefusalChangesNothing()
    {
        var state = new CasinoStateDto(Balance: 1_000);
        var bought = new CasinoBuyChipsDto(true, string.Empty, 20, 20_000, 20_000, 980,
            new CasinoSittingDto(Id: "s9", State: 1, Stack: 20_000));

        Assert.Equal("s9", CasinoStore.BuyAbsorbedInto(state, bought).Sitting!.Id);
        Assert.Same(state, CasinoStore.BuyAbsorbedInto(state, bought with { Granted = false,
            Reason = CasinoReasons.Insufficient }));
    }

    [Fact]
    public void AStackOnlyAnswerUpdatesTheLiveBankroll()
    {
        var state = new CasinoStateDto(Sitting: new CasinoSittingDto(Id: "s1", Stack: 1_000));
        var bought = new CasinoBuyChipsDto(true, string.Empty, 5, 5_000, 6_000, 10);

        Assert.Equal(6_000, CasinoStore.BuyAbsorbedInto(state, bought).Sitting!.Stack);
    }
}
