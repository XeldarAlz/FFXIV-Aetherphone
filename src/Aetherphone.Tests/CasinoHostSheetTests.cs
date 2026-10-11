using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoHostSheetTests
{
    private static readonly CasinoTableLocationDto Ward = new(73, 339, 12, 30);

    [Fact]
    public void TheDefaultDraftIsAPrivateCoinTableTheServerAccepts()
    {
        var config = new HostDraft().Build(null);

        Assert.Equal(CasinoWire.BlackjackKind, config.GameKind);
        Assert.Equal(CasinoCurrencies.Chips, config.Currency);
        Assert.Equal(CasinoListings.Private, config.Listing);
        Assert.Equal(CasinoHostingRules.DefaultSeats, config.Seats);
        Assert.Equal(BlackjackRules.MinBet, config.MinBet);
        Assert.Equal(CasinoHostingRules.DefaultChipMaxBet, config.MaxBet);
        Assert.Equal(0, config.MinBuyIn);
        Assert.Equal(0, config.MaxBuyIn);
        Assert.Null(config.HouseRules);
        Assert.True(config.AutoDeal);
        Assert.Equal(string.Empty, CasinoHostingRules.Check(config));
    }

    [Fact]
    public void EveryStakeOnTheCoinLadderIsARungTheServerAccepts()
    {
        var draft = new HostDraft();
        var span = draft.BetSpan;
        for (var index = span.First; index <= span.Last; index++)
        {
            draft.MinBet = span.First;
            draft.MaxBet = index;
            Assert.Equal(string.Empty, CasinoHostingRules.Check(draft.Build(null)));
        }
    }

    [Fact]
    public void APracticeDraftCarriesItsStackRulesAndDealer()
    {
        var draft = new HostDraft();
        draft.SelectCurrency(CasinoCurrencies.Practice, false);
        draft.Stack = HostLadders.FloorIndex(HostLadders.PracticeStacks, 250_000);
        draft.FaceUp = true;
        draft.DealerMode = CasinoDealerModes.Host;
        draft.AutoDeal = false;
        draft.Pays = CasinoRuleSheet.PaysTwoToOne;
        draft.Decks = 2;
        draft.HitsSoft17 = true;
        draft.Normalize(false);

        var config = draft.Build(Ward);

        Assert.True(config.Practice);
        Assert.Equal(250_000, config.PracticeStack);
        Assert.True(config.FaceUp);
        Assert.Equal(CasinoDealerModes.Host, config.DealerMode);
        Assert.False(config.AutoDeal);
        Assert.Equal(CasinoRuleSheet.PaysTwoToOne, config.HouseRules!.BlackjackPays);
        Assert.Equal(2, config.HouseRules.Decks);
        Assert.True(config.HouseRules.DealerHitsSoft17);
        Assert.Same(Ward, config.Location);
        Assert.Equal(string.Empty, CasinoHostingRules.Check(config));
    }

    [Fact]
    public void PracticeBetsNeverOutgrowTheStartingStack()
    {
        var draft = new HostDraft();
        draft.SelectCurrency(CasinoCurrencies.Practice, false);
        draft.MaxBet = HostLadders.Bets.Length - 1;
        draft.Stack = 0;
        draft.Normalize(false);

        var config = draft.Build(null);

        Assert.True(config.MaxBet <= config.PracticeStack);
        Assert.True(config.MinBet <= config.MaxBet);
        Assert.Equal(string.Empty, CasinoHostingRules.Check(config));
    }

    [Fact]
    public void AGilDraftKeepsTheBetAndPayoutInsideTheBank()
    {
        var draft = new HostDraft();
        draft.SelectCurrency(CasinoCurrencies.Gil, true);
        draft.Bank = HostLadders.FloorIndex(HostLadders.Gil, 1_000_000);
        draft.MaxBet = HostLadders.Gil.Length - 1;
        draft.Normalize(true);

        var config = draft.Build(null);

        Assert.Equal(1_000_000, config.Bank);
        Assert.True(config.MaxBet <= config.Bank);
        Assert.Equal(config.Bank, config.MaxPayout);
        Assert.False(config.FaceUp);
        Assert.False(config.PracticeRebuy);
        Assert.Equal(string.Empty, CasinoHostingRules.Check(config));
    }

    [Fact]
    public void GilFallsBackWhenTheFloorHasNoGilTables()
    {
        var draft = new HostDraft();
        draft.SelectCurrency(CasinoCurrencies.Gil, false);
        Assert.Equal(CasinoCurrencies.Chips, draft.Currency);

        draft.SelectCurrency(CasinoCurrencies.Gil, true);
        draft.Normalize(false);
        Assert.Equal(CasinoCurrencies.Chips, draft.Currency);
    }

    [Fact]
    public void AHoldemDraftAsksForBlindsAndRefusesGil()
    {
        var draft = new HostDraft();
        draft.SelectGame(HostGame.Holdem, true);
        draft.Seats = 9;
        draft.SelectCurrency(CasinoCurrencies.Gil, true);
        draft.Normalize(true);

        var config = draft.Build(null);

        Assert.Equal(HoldemRules.Kind, config.GameKind);
        Assert.Equal(CasinoCurrencies.Chips, config.Currency);
        Assert.Equal(9, config.Seats);
        Assert.Equal(CasinoHostingRules.DefaultHoldemBigBlind / 2, config.Poker!.SmallBlind);
        Assert.Equal(string.Empty, CasinoHostingRules.Check(config));
    }

    [Fact]
    public void SwitchingBackToBlackjackClampsTheSeatsToSix()
    {
        var draft = new HostDraft();
        draft.SelectGame(HostGame.Holdem, false);
        draft.Seats = 9;
        draft.SelectGame(HostGame.Blackjack, false);
        draft.Normalize(false);

        Assert.Equal(CasinoHostingRules.MaxSeats, draft.Build(null).Seats);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void RoomsStartAsPracticeAndPassTheVenueCheck(int game)
    {
        var draft = new HostDraft();
        draft.SelectGame((HostGame)game, true);
        draft.Normalize(true);

        var config = draft.Build(null);

        Assert.Equal(CasinoCurrencies.Practice, config.Currency);
        Assert.Equal(0, config.Seats);
        Assert.Equal(string.Empty, VenueRules.Check(config));
    }

    [Fact]
    public void RoomsNeverPlayForCoins()
    {
        var draft = new HostDraft();
        draft.SelectGame(HostGame.DiceTable, true);
        draft.SelectCurrency(CasinoCurrencies.Chips, true);

        Assert.Equal(CasinoCurrencies.Practice, draft.Currency);
        Assert.False(HostGames.Accepts(HostGame.Raffle, CasinoCurrencies.Chips, true));
    }

    [Fact]
    public void AGilDeathrollStakesOnTheGilLadder()
    {
        var draft = new HostDraft();
        draft.SelectGame(HostGame.Deathroll, true);
        draft.SelectCurrency(CasinoCurrencies.Gil, true);
        draft.Normalize(true);

        var config = draft.Build(null);

        Assert.Equal(CasinoCurrencies.Gil, config.Currency);
        Assert.Equal(VenueRules.DefaultStake, config.Deathroll!.Stake);
        Assert.Equal(string.Empty, VenueRules.Check(config));
    }

    [Fact]
    public void ADiceTableCarriesItsSidesAndRound()
    {
        var draft = new HostDraft();
        draft.SelectGame(HostGame.DiceTable, false);
        draft.Sides = HostLadders.FloorIndex(HostLadders.DiceSides, 20);
        draft.RoundSeconds = Array.IndexOf(HostLadders.RoundSeconds, 120);

        var dice = draft.Build(null).Dice!;

        Assert.Equal(20, dice.Sides);
        Assert.Equal(120, dice.RoundSeconds);
    }

    [Fact]
    public void LadderIndexesSnapToTheNearestRungBelowOrAbove()
    {
        long[] rungs = { 10, 100, 1_000 };
        Assert.Equal(0, HostLadders.FloorIndex(rungs, 5));
        Assert.Equal(1, HostLadders.FloorIndex(rungs, 999));
        Assert.Equal(2, HostLadders.CeilIndex(rungs, 101));
        Assert.Equal(2, HostLadders.CeilIndex(rungs, 5_000));
        Assert.Equal(new LadderSpan(1, 2), HostLadders.Span(rungs, 50, 2_000));
    }

    [Fact]
    public void EveryHostableGameHasAPosterLine()
    {
        Assert.Equal(5, HostGames.All.Length);
        for (var index = 0; index < HostGames.All.Length; index++)
        {
            Assert.Equal((HostGame)index, HostGames.All[index].Game);
            Assert.False(string.IsNullOrEmpty(HostGames.All[index].Line.Key));
        }
    }
}
