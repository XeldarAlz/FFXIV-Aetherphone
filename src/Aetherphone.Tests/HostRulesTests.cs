using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HostRulesTests
{
    private const HostRuleSection SeatGame = HostRuleSection.Turns | HostRuleSection.Room;

    public static TheoryData<int, int, int> SectionCases => new()
    {
        { (int)HostGame.Blackjack, CasinoCurrencies.Chips, (int)(HostRuleSection.Dealing | SeatGame) },
        {
            (int)HostGame.Blackjack, CasinoCurrencies.Practice,
            (int)(HostRuleSection.Dealing | HostRuleSection.Blackjack | HostRuleSection.FreePlay | SeatGame)
        },
        {
            (int)HostGame.Blackjack, CasinoCurrencies.Gil,
            (int)(HostRuleSection.Dealing | HostRuleSection.Blackjack | HostRuleSection.Gil | SeatGame)
        },
        { (int)HostGame.Holdem, CasinoCurrencies.Chips, (int)(HostRuleSection.Holdem | SeatGame) },
        {
            (int)HostGame.Holdem, CasinoCurrencies.Practice,
            (int)(HostRuleSection.Holdem | HostRuleSection.FreePlay | SeatGame)
        },
        {
            (int)HostGame.DiceTable, CasinoCurrencies.Practice,
            (int)(HostRuleSection.Dice | HostRuleSection.FreePlay | HostRuleSection.Room)
        },
        { (int)HostGame.DiceTable, CasinoCurrencies.Gil, (int)(HostRuleSection.Dice | HostRuleSection.Room) },
        {
            (int)HostGame.Deathroll, CasinoCurrencies.Practice,
            (int)(HostRuleSection.FreePlay | HostRuleSection.Room)
        },
        { (int)HostGame.Deathroll, CasinoCurrencies.Gil, (int)HostRuleSection.Room },
        { (int)HostGame.Raffle, CasinoCurrencies.Practice, (int)(HostRuleSection.FreePlay | HostRuleSection.Room) },
        { (int)HostGame.Raffle, CasinoCurrencies.Gil, (int)HostRuleSection.Room },
        { (int)HostGame.Book, CasinoCurrencies.Gil, (int)(HostRuleSection.Gil | HostRuleSection.Room) },
    };

    [Theory]
    [MemberData(nameof(SectionCases))]
    public void TheRulesPageShowsOnlyTheSectionsThatApply(int game, int currency, int expected)
    {
        Assert.Equal((HostRuleSection)expected, HostRules.Sections((HostGame)game, currency));
    }

    [Fact]
    public void EverySectionShowsForSomeGameAndTheRoomShowsForAll()
    {
        var seen = HostRuleSection.None;
        for (var game = HostGame.Blackjack; game <= HostGame.Book; game++)
        {
            for (var index = 0; index < CasinoCurrencies.All.Length; index++)
            {
                var currency = CasinoCurrencies.All[index];
                if (!HostGames.Accepts(game, currency, true))
                {
                    continue;
                }

                var sections = HostRules.Sections(game, currency);
                Assert.Equal(HostRuleSection.Room, sections & HostRuleSection.Room);
                seen |= sections;
            }
        }

        Assert.Equal((HostRuleSection)255, seen);
    }

    [Fact]
    public void TheDiceRoundStakeAppearsOnlyForGilHighestRollTables()
    {
        var draft = new HostDraft();
        draft.SelectGame(HostGame.DiceTable, true);
        Assert.False(HostRules.StakesEachRound(draft));

        draft.SelectCurrency(CasinoCurrencies.Gil, true);
        Assert.True(HostRules.StakesEachRound(draft));

        draft.HighestWins = false;
        Assert.False(HostRules.StakesEachRound(draft));
    }

    [Fact]
    public void TheDefaultBlackjackSummaryNamesTheClockTheTurnsAndThePin()
    {
        var draft = new HostDraft();

        Assert.Equal("The clock deals, 20 s turns, pinned to this venue", HostRules.Summary(draft, true));
        Assert.Equal("The clock deals, 20 s turns, not pinned", HostRules.Summary(draft, false));
    }

    [Fact]
    public void AFreePlayBlackjackSummaryCarriesTheDealerRulesAndStack()
    {
        var draft = new HostDraft();
        draft.SelectCurrency(CasinoCurrencies.Practice, true);
        draft.DealerMode = CasinoDealerModes.Host;
        draft.TurnSeconds = 30;
        draft.Pays = CasinoRuleSheet.PaysTwoToOne;
        draft.Decks = 2;
        draft.Stack = HostLadders.FloorIndex(HostLadders.PracticeStacks, 250_000);

        Assert.Equal("You deal, 30 s turns, 2:1, 2 decks, stack " + NumberText.Compact(250_000) + ", not pinned",
            HostRules.Summary(draft, false));
    }

    [Fact]
    public void AChipTableSummaryAlwaysSaysTheClockDeals()
    {
        var draft = new HostDraft();
        draft.DealerMode = CasinoDealerModes.Host;

        Assert.False(draft.HostDeals);
        Assert.StartsWith("The clock deals", HostRules.Summary(draft, true));
    }

    [Fact]
    public void AHoldemSummaryNamesTheAnteShare()
    {
        var draft = new HostDraft();
        draft.SelectGame(HostGame.Holdem, true);

        Assert.Equal("20 s turns, no ante, pinned to this venue", HostRules.Summary(draft, true));

        draft.Ante = Array.IndexOf(HostLadders.AnteTenths, 2);
        Assert.Equal("20 s turns, ante 20%, pinned to this venue", HostRules.Summary(draft, true));
    }

    [Fact]
    public void ADiceSummaryNamesTheWinnerTheRoundAndAnyStake()
    {
        var draft = new HostDraft();
        draft.SelectGame(HostGame.DiceTable, true);
        draft.RoundSeconds = Array.IndexOf(HostLadders.RoundSeconds, 120);
        draft.HighestWins = false;

        Assert.Equal("Lowest roll wins, 2 min rounds, stack " + NumberText.Compact(draft.StackValue) + ", not pinned",
            HostRules.Summary(draft, false));

        draft.SelectCurrency(CasinoCurrencies.Gil, true);
        draft.HighestWins = true;
        draft.DiceStake = Array.IndexOf(HostLadders.DiceStakes, 50_000L);
        draft.Normalize(true);
        Assert.Equal("Highest roll wins, 2 min rounds, stake " + NumberText.Compact(50_000) + " gil a round, not pinned",
            HostRules.Summary(draft, false));
    }

    [Fact]
    public void AGilSummaryMentionsThePayoutCapOnlyOnceItIsSet()
    {
        var draft = new HostDraft();
        draft.SelectGame(HostGame.Book, true);
        draft.Normalize(true);
        Assert.Equal("Pinned to this venue", HostRules.Summary(draft, true));

        draft.MaxPayout = HostLadders.FloorIndex(HostLadders.Gil, 1_000_000);
        draft.PayoutCapped = true;
        draft.Normalize(true);
        Assert.Equal("Payouts up to " + NumberText.Compact(1_000_000) + " gil, pinned to this venue",
            HostRules.Summary(draft, true));
    }

    [Fact]
    public void TheSummaryKeyChangesWithEveryRuleItShows()
    {
        var draft = new HostDraft();
        var key = HostRulesKey.Of(draft, true);
        Assert.Equal(key, HostRulesKey.Of(draft, true));
        Assert.NotEqual(key, HostRulesKey.Of(draft, false));

        draft.TurnSeconds = 45;
        Assert.NotEqual(key, HostRulesKey.Of(draft, true));
        draft.TurnSeconds = CasinoHostingRules.DefaultTurnSeconds;

        draft.SelectCurrency(CasinoCurrencies.Practice, true);
        Assert.NotEqual(key, HostRulesKey.Of(draft, true));
    }

    [Fact]
    public void ARefusedCurrencySaysWhyTheGameTurnsItDown()
    {
        Assert.Equal(L.HostSetup.BookGilOnly.Key, HostRules.Refusal(HostGame.Book, CasinoCurrencies.Chips, true).Key);
        Assert.Equal(L.HostSetup.BookGilOnly.Key,
            HostRules.Refusal(HostGame.Book, CasinoCurrencies.Practice, true).Key);
        Assert.Equal(L.Tables.PlayRoomsNoCoins.Key,
            HostRules.Refusal(HostGame.Raffle, CasinoCurrencies.Chips, true).Key);
        Assert.Equal(L.Tables.PlayHoldemNoGil.Key, HostRules.Refusal(HostGame.Holdem, CasinoCurrencies.Gil, true).Key);
        Assert.Equal(L.Tables.PlayHoldemNoGil.Key,
            HostRules.Refusal(HostGame.Holdem, CasinoCurrencies.Gil, false).Key);
        Assert.Equal(L.Tables.PlayGilClosed.Key,
            HostRules.Refusal(HostGame.Blackjack, CasinoCurrencies.Gil, false).Key);
        Assert.Equal(L.Tables.PlayGilClosed.Key, HostRules.Refusal(HostGame.Book, CasinoCurrencies.Gil, false).Key);
    }

    [Fact]
    public void TheHintsFollowTheChosenCurrencyAndListing()
    {
        Assert.Equal(L.HostSetup.ChipsHint.Key, HostRules.CurrencyHint(CasinoCurrencies.Chips).Key);
        Assert.Equal(L.Tables.PlayPracticeLine.Key, HostRules.CurrencyHint(CasinoCurrencies.Practice).Key);
        Assert.Equal(L.Tables.PlayGilLine.Key, HostRules.CurrencyHint(CasinoCurrencies.Gil).Key);
        Assert.Equal(L.Tables.JoinOpenLine.Key, HostRules.ListingHint(CasinoListings.Open).Key);
        Assert.Equal(L.Tables.JoinKnockLine.Key, HostRules.ListingHint(CasinoListings.Knock).Key);
        Assert.Equal(L.Tables.JoinInviteLine.Key, HostRules.ListingHint(CasinoListings.Private).Key);
        Assert.Equal("knock to join", Loc.T(HostRules.ListingSummary(CasinoListings.Knock)));
        Assert.Equal("anyone can join", Loc.T(HostRules.ListingSummary(CasinoListings.Open)));
        Assert.Equal("invite only", Loc.T(HostRules.ListingSummary(CasinoListings.Private)));
    }

    [Fact]
    public void AFreePlayHoldemStackShowsWhatTheTableDeals()
    {
        var draft = new HostDraft();
        draft.SelectGame(HostGame.Holdem, true);
        draft.SelectCurrency(CasinoCurrencies.Practice, true);
        draft.BigBlind = HostLadders.FloorIndex(HostLadders.Bets, 25_000);
        draft.Normalize(true);

        var config = draft.Build(null);

        Assert.Equal(25_000, draft.BigBlindValue);
        Assert.Equal(250_000, config.PracticeStack);
        Assert.Equal(config.PracticeStack, draft.StackValue);
        Assert.Equal(config.PracticeStack, draft.TableStackValue);
        Assert.Equal(250_000, HostLadders.PracticeStacks[draft.StackSpan.First]);
        Assert.Equal("20 s turns, no ante, stack " + NumberText.Compact(250_000) + ", not pinned",
            HostRules.Summary(draft, false));
        Assert.Equal(string.Empty, CasinoHostingRules.Check(config));
    }

    [Fact]
    public void EveryFreePlayHoldemBlindOpensWithTheStackItShows()
    {
        var draft = new HostDraft();
        draft.SelectGame(HostGame.Holdem, true);
        draft.SelectCurrency(CasinoCurrencies.Practice, true);
        var blinds = HostLadders.BigBlinds;
        for (var index = blinds.First; index <= blinds.Last; index++)
        {
            draft.Stack = 0;
            draft.BigBlind = index;
            draft.Normalize(true);
            var config = draft.Build(null);
            Assert.Equal(draft.StackValue, config.PracticeStack);
            Assert.True(config.PracticeStack >= draft.BigBlindValue * HoldemRules.HostedMinBuyInBigBlinds);
            Assert.Equal(string.Empty, CasinoHostingRules.Check(config));
        }
    }

    [Fact]
    public void OnlyHoldemRaisesTheStackFloor()
    {
        var blackjack = new HostDraft();
        blackjack.SelectCurrency(CasinoCurrencies.Practice, true);
        blackjack.Stack = 0;
        blackjack.Normalize(true);

        Assert.Equal(0, blackjack.StackFloor);
        Assert.Equal(HostLadders.All(HostLadders.PracticeStacks), blackjack.StackSpan);
        Assert.Equal(HostLadders.PracticeStacks[0], blackjack.TableStackValue);

        var dice = new HostDraft();
        dice.SelectGame(HostGame.DiceTable, true);
        dice.Stack = 0;
        dice.Normalize(true);
        Assert.Equal(HostLadders.PracticeStacks[0], dice.Build(null).PracticeStack);
    }

    [Fact]
    public void RoundLengthsReadAsSecondsThenMinutes()
    {
        Assert.Equal("30 s", HostRules.RoundText(30));
        Assert.Equal("1 min", HostRules.RoundText(60));
        Assert.Equal("10 min", HostRules.RoundText(600));
        Assert.Equal("100%", HostRules.AnteText(HostLadders.AnteTenths.Length - 1));
    }
}
