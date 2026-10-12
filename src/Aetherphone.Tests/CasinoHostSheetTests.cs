using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
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
    public void RaisingTheCoinMaxBetPastTheGilBankIndexNeverThrows()
    {
        var draft = new HostDraft();
        var span = draft.BetSpan;
        for (var index = span.First; index <= span.Last; index++)
        {
            draft.MaxBet = index;
            draft.Normalize(false);
            Assert.Equal(index, draft.MaxBet);
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
    public void AHostDealtDraftWaitsForTheDealersTapUntilAutoDealIsTurnedOn()
    {
        var draft = new HostDraft();
        draft.SelectCurrency(CasinoCurrencies.Practice, false);
        draft.DealerMode = CasinoDealerModes.Host;
        draft.Normalize(false);

        Assert.False(draft.AutoDeal);
        Assert.False(draft.Build(null).AutoDeal);

        draft.AutoDeal = true;
        Assert.True(draft.Build(null).AutoDeal);

        draft.AutoDeal = false;
        draft.DealerMode = CasinoDealerModes.House;
        Assert.True(draft.Build(null).AutoDeal);

        draft.AutoDeal = true;
        draft.Reset(HostGame.Blackjack);
        Assert.False(draft.AutoDeal);
    }

    [Fact]
    public void TheDealerSeatTravelsToGilButAChipTableAlwaysDealsOnTheClock()
    {
        var draft = new HostDraft();
        draft.SelectCurrency(CasinoCurrencies.Practice, true);
        draft.DealerMode = CasinoDealerModes.Host;
        draft.AutoDeal = false;

        draft.SelectCurrency(CasinoCurrencies.Gil, true);
        draft.Normalize(true);
        var gil = draft.Build(null);
        Assert.Equal(CasinoDealerModes.Host, gil.DealerMode);
        Assert.False(gil.AutoDeal);
        Assert.Equal(string.Empty, CasinoHostingRules.Check(gil));

        draft.SelectCurrency(CasinoCurrencies.Chips, true);
        draft.Normalize(true);
        var chips = draft.Build(null);
        Assert.Equal(CasinoDealerModes.House, chips.DealerMode);
        Assert.True(chips.AutoDeal);
        Assert.Equal(string.Empty, CasinoHostingRules.Check(chips));
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
        Assert.Equal(6, HostGames.All.Length);
        for (var index = 0; index < HostGames.All.Length; index++)
        {
            Assert.Equal((HostGame)index, HostGames.All[index].Game);
            Assert.False(string.IsNullOrEmpty(HostGames.All[index].Line.Key));
        }

        Assert.Equal(5, HostGames.Count(false));
        Assert.Equal(6, HostGames.Count(true));
        Assert.Equal(HostGame.Book, HostGames.All[HostGames.Count(true) - 1].Game);
    }

    [Fact]
    public void ABookIsAGilLedgerWithABankAndNoSeats()
    {
        var draft = new HostDraft();
        draft.SelectGame(HostGame.Book, true);
        draft.Normalize(true);

        var config = draft.Build(Ward);

        Assert.Equal(CasinoWire.BookKind, config.GameKind);
        Assert.Equal(CasinoCurrencies.Gil, config.Currency);
        Assert.Equal(0, config.Seats);
        Assert.Equal(10_000_000, config.Bank);
        Assert.Equal(config.Bank, config.MaxPayout);
        Assert.False(config.Spectators);
        Assert.False(config.AutoDeal);
        Assert.Equal(CasinoDealerModes.House, config.DealerMode);
        Assert.Null(config.HouseRules);
        Assert.Null(config.CoDealers);
        Assert.Same(Ward, config.Location);
        Assert.Equal(string.Empty, CasinoHostingRules.Check(config));

        Assert.Equal(HostGame.Book, HostGames.FromKind(CasinoWire.BookKind));
        Assert.False(HostGames.IsVenue(HostGame.Book));
        Assert.True(HostGames.IsBook(HostGame.Book));
        Assert.False(HostGames.HasSeats(HostGame.Book));
        Assert.False(HostGames.Accepts(HostGame.Book, CasinoCurrencies.Chips, true));
        Assert.False(HostGames.Accepts(HostGame.Book, CasinoCurrencies.Practice, true));
        Assert.True(HostGames.Accepts(HostGame.Book, CasinoCurrencies.Gil, true));
        Assert.False(HostGames.Accepts(HostGame.Book, CasinoCurrencies.Gil, false));
    }

    [Fact]
    public void ABookNeverLeavesGilEvenWhenChipsAreAskedFor()
    {
        var draft = new HostDraft();
        draft.SelectGame(HostGame.Book, true);
        draft.SelectCurrency(CasinoCurrencies.Chips, true);
        Assert.Equal(CasinoCurrencies.Gil, draft.Currency);

        draft.SelectCurrency(CasinoCurrencies.Practice, true);
        draft.Normalize(true);
        Assert.Equal(CasinoCurrencies.Gil, draft.Currency);
        Assert.Equal(CasinoCurrencies.Gil, HostGames.FallbackCurrency(HostGame.Book, CasinoCurrencies.Chips, false));
    }

    [Fact]
    public void TheHostSheetSkipsThePlayForStepForABook()
    {
        var book = HostFlowLayout.Steps(HostGame.Book);
        Assert.Equal(HostStep.None, book & HostStep.PlayFor);
        Assert.Equal(1, HostFlowLayout.StepOrdinal(book, HostStep.Game));
        Assert.Equal(0, HostFlowLayout.StepOrdinal(book, HostStep.PlayFor));
        Assert.Equal(2, HostFlowLayout.StepOrdinal(book, HostStep.Table));

        for (var game = HostGame.Blackjack; game < HostGame.Book; game++)
        {
            var steps = HostFlowLayout.Steps(game);
            Assert.Equal(HostFlowLayout.EveryStep, steps);
            Assert.Equal(1, HostFlowLayout.StepOrdinal(steps, HostStep.Game));
            Assert.Equal(2, HostFlowLayout.StepOrdinal(steps, HostStep.PlayFor));
            Assert.Equal(3, HostFlowLayout.StepOrdinal(steps, HostStep.Table));
        }
    }

    [Fact]
    public void ABookPayoutCapRunsFromTheBottomOfTheLadderToTheBank()
    {
        var draft = new HostDraft();
        draft.SelectGame(HostGame.Book, true);
        draft.Normalize(true);

        Assert.Equal(0, draft.PayoutSpan.First);
        Assert.Equal(draft.Bank, draft.PayoutSpan.Last);

        draft.MaxPayout = 0;
        draft.PayoutCapped = true;
        draft.Normalize(true);
        var config = draft.Build(null);
        Assert.Equal(HostLadders.Gil[0], config.MaxPayout);
        Assert.Equal(string.Empty, CasinoHostingRules.Check(config));

        draft.MaxPayout = HostLadders.Gil.Length - 1;
        draft.Normalize(true);
        Assert.Equal(draft.Bank, draft.MaxPayout);
    }

    [Fact]
    public void ABookDraftRoundTripsThroughTheRememberedDraft()
    {
        var draft = new HostDraft();
        draft.SelectGame(HostGame.Book, true);
        draft.Name = "Mist Lounge";
        draft.Listing = CasinoListings.Open;
        draft.Bank = HostLadders.FloorIndex(HostLadders.Gil, 5_000_000);
        draft.Normalize(true);

        var memory = draft.Snapshot(true);
        var restored = new HostDraft();
        restored.Restore(memory);
        restored.Normalize(true);

        Assert.Equal(HostGame.Book, HostDraft.GameOf(memory));
        Assert.Equal(draft.Build(null), restored.Build(null));
        Assert.Equal("Book, Gil, bank " + NumberText.Compact(5_000_000), HostDraftSummary.Of(restored));
    }

    [Fact]
    public void ARememberedBookDraftStaysPutWhileTheServerDoesNotListTheBook()
    {
        var book = new HostDraft();
        book.SelectGame(HostGame.Book, true);
        var blackjack = new HostDraft();

        Assert.True(HostDraft.Reopens(book.Snapshot(true), true));
        Assert.False(HostDraft.Reopens(book.Snapshot(true), false));
        Assert.True(HostDraft.Reopens(blackjack.Snapshot(true), false));
    }
}
