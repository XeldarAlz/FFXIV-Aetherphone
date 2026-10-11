using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoHostingRulesTests
{
    [Fact]
    public void ConstantsMatchTheBackendHostingRules()
    {
        Assert.Equal(2, CasinoHostingRules.MinSeats);
        Assert.Equal(6, CasinoHostingRules.MaxSeats);
        Assert.Equal(24, CasinoHostingRules.NameMaxLength);
        Assert.Equal(1_000, CasinoHostingRules.MinPracticeStack);
        Assert.Equal(1_000_000_000, CasinoHostingRules.MaxPracticeStack);
        Assert.Equal(100_000, CasinoHostingRules.DefaultPracticeStack);
        Assert.Equal(99_999_999_999, CasinoHostingRules.MaxGil);
        Assert.Equal(new[] { 15, 20, 30, 45 }, CasinoHostingRules.TurnSeconds);
        Assert.Equal(3, CasinoHostingRules.TimeBankUses);
        Assert.Equal(10, CasinoHostingRules.TimeBankSecondsPerUse);
        Assert.Equal(3, CasinoHostingRules.MaxCoDealers);
        Assert.Equal(10, CasinoHostingRules.TournamentMinHands);
        Assert.Equal(50, CasinoHostingRules.TournamentMaxHands);
        Assert.Equal(10_000, CasinoHostingRules.DefaultChipMaxBet);
        Assert.Equal(new[] { 1, 2, 4, 6, 8 }, CasinoRuleSheet.Decks);
        Assert.Equal(0, CasinoCurrencies.Chips);
        Assert.Equal(1, CasinoCurrencies.Practice);
        Assert.Equal(2, CasinoCurrencies.Gil);
        Assert.Equal(0, CasinoListings.Private);
        Assert.Equal(1, CasinoListings.Knock);
        Assert.Equal(2, CasinoListings.Open);
    }

    [Fact]
    public void TheStandardSheetIsTheHouseGame()
    {
        var standard = CasinoRuleSheet.Standard;

        Assert.Equal(CasinoRuleSheet.PaysThreeToTwo, standard.BlackjackPays);
        Assert.False(standard.DealerHitsSoft17);
        Assert.Equal(6, standard.Decks);
        Assert.Equal(CasinoRuleSheet.SplitsToFour, standard.Splits);
        Assert.Equal(CasinoRuleSheet.DoublesAny, standard.Doubles);
        Assert.False(standard.FiveCardCharlie);
        Assert.True(standard.DealerPeek);
        Assert.True(CasinoRuleSheet.IsStandard(new CasinoBlackjackRuleSheetDto()));
        Assert.False(CasinoRuleSheet.IsStandard(new CasinoBlackjackRuleSheetDto(Decks: 1)));
    }

    [Fact]
    public void NaturalsPayWhatTheSheetSays()
    {
        Assert.Equal(1500, CasinoRuleSheet.NaturalPayout(CasinoRuleSheet.PaysThreeToTwo, 1000));
        Assert.Equal(8, CasinoRuleSheet.NaturalPayout(CasinoRuleSheet.PaysThreeToTwo, 5));
        Assert.Equal(2000, CasinoRuleSheet.NaturalPayout(CasinoRuleSheet.PaysTwoToOne, 1000));
        Assert.Equal(1000, CasinoRuleSheet.NaturalPayout(CasinoRuleSheet.PaysEven, 1000));
        Assert.Equal(0, CasinoRuleSheet.NaturalPayout(CasinoRuleSheet.PaysEven, 0));
    }

    [Fact]
    public void ChipTablesRefuseEveryVenueOnlyOption()
    {
        var chips = new CasinoTableConfigDto();
        Assert.Equal(string.Empty, CasinoHostingRules.Check(chips));
        Assert.Equal(CasinoReasons.PracticeOnly, CasinoHostingRules.Check(chips with { FaceUp = true }));
        Assert.Equal(CasinoReasons.PracticeOnly,
            CasinoHostingRules.Check(chips with { DealerMode = CasinoDealerModes.Host }));
        Assert.Equal(CasinoReasons.PracticeOnly, CasinoHostingRules.Check(chips with { AutoDeal = false }));
        Assert.Equal(CasinoReasons.PracticeOnly,
            CasinoHostingRules.Check(chips with { HouseRules = new CasinoBlackjackRuleSheetDto(Decks: 2) }));
        Assert.Equal(string.Empty,
            CasinoHostingRules.Check(chips with { HouseRules = new CasinoBlackjackRuleSheetDto() }));
        Assert.Equal(CasinoReasons.ConfigInvalid, CasinoHostingRules.Check(chips with { MinBet = 750 }));
        Assert.Equal(CasinoReasons.ConfigInvalid,
            CasinoHostingRules.Check(chips with { MinBet = 25_000, MaxBet = 10_000 }));
    }

    [Fact]
    public void PracticeTablesKeepEveryBetInsideTheStack()
    {
        var practice = new CasinoTableConfigDto(Currency: CasinoCurrencies.Practice, Practice: true);
        Assert.Equal(string.Empty, CasinoHostingRules.Check(practice));
        Assert.Equal(string.Empty, CasinoHostingRules.Check(practice with { FaceUp = true, AutoDeal = false }));
        Assert.Equal(CasinoReasons.ConfigInvalid, CasinoHostingRules.Check(practice with { PracticeStack = 999 }));
        Assert.Equal(CasinoReasons.ConfigInvalid,
            CasinoHostingRules.Check(practice with { MaxBet = 200_000, PracticeStack = 100_000 }));
        Assert.Equal(CasinoReasons.ConfigInvalid,
            CasinoHostingRules.Check(practice with { HouseRules = new CasinoBlackjackRuleSheetDto(Decks: 3) }));
    }

    [Fact]
    public void GilTablesDeclareABankThatCoversEveryPromise()
    {
        var gil = new CasinoTableConfigDto(Currency: CasinoCurrencies.Gil, Bank: 20_000_000, MaxBet: 500_000);
        Assert.Equal(string.Empty, CasinoHostingRules.Check(gil));
        Assert.Equal(20_000_000, CasinoHostingRules.PayoutCeiling(gil));
        Assert.Equal(CasinoReasons.PracticeOnly, CasinoHostingRules.Check(gil with { FaceUp = true }));
        Assert.Equal(CasinoReasons.ConfigInvalid, CasinoHostingRules.Check(gil with { Bank = 0 }));
        Assert.Equal(CasinoReasons.ConfigInvalid, CasinoHostingRules.Check(gil with { MaxBet = 30_000_000 }));
        Assert.Equal(CasinoReasons.ConfigInvalid, CasinoHostingRules.Check(gil with { MaxPayout = 100_000 }));
        Assert.Equal(CasinoReasons.ConfigInvalid, CasinoHostingRules.Check(gil with { MaxPayout = 30_000_000 }));
        Assert.Equal(string.Empty, CasinoHostingRules.Check(gil with { MaxPayout = 5_000_000 }));
    }

    [Fact]
    public void TheSheetRefusesSeatsClocksAndNamesOutsideTheBands()
    {
        var config = new CasinoTableConfigDto(Currency: CasinoCurrencies.Practice);
        Assert.Equal(CasinoReasons.ConfigInvalid, CasinoHostingRules.Check(config with { Seats = 1 }));
        Assert.Equal(CasinoReasons.ConfigInvalid, CasinoHostingRules.Check(config with { Seats = 7 }));
        Assert.Equal(CasinoReasons.ConfigInvalid, CasinoHostingRules.Check(config with { TurnSeconds = 25 }));
        Assert.Equal(CasinoReasons.ConfigInvalid, CasinoHostingRules.Check(config with { TimeBankUses = 2 }));
        Assert.Equal(CasinoReasons.ConfigInvalid,
            CasinoHostingRules.Check(config with { Name = "A name that runs past the cap" }));
        Assert.Equal(CasinoReasons.ConfigInvalid,
            CasinoHostingRules.Check(config with { CoDealers = new[] { "a", "b", "c", "d" } }));
    }

    [Fact]
    public void TheGilLedgerKnowsWhoseTapIsNext()
    {
        var entry = new CasinoLedgerEntryDto(EntryId: "e1", Kind: CasinoLedgerKinds.BuyIn, Amount: 500_000,
            PayerUserId: "player", PayeeUserId: "host", PayerConfirmed: true);

        Assert.Equal(LedgerSide.Payer, CasinoGilLedger.SideOf(entry, "player"));
        Assert.Equal(LedgerSide.Payee, CasinoGilLedger.SideOf(entry, "host"));
        Assert.Equal(LedgerSide.None, CasinoGilLedger.SideOf(entry, "stranger"));
        Assert.False(CasinoGilLedger.CanConfirm(entry, "player"));
        Assert.True(CasinoGilLedger.CanConfirm(entry, "host"));
        Assert.True(CasinoGilLedger.WaitsOnOtherSide(entry, "player"));
        Assert.True(CasinoGilLedger.CanDispute(entry, "host"));
        Assert.False(CasinoGilLedger.CanDispute(entry, "player"));
        Assert.True(CasinoGilLedger.Unconfirmed(entry));

        var settled = entry with { PayeeConfirmed = true, Settled = true };
        Assert.False(CasinoGilLedger.CanConfirm(settled, "host"));
        Assert.False(CasinoGilLedger.CanDispute(settled, "host"));
        Assert.False(CasinoGilLedger.Unconfirmed(settled));
        Assert.Equal(1, CasinoGilLedger.UnsettledCount(new[] { entry, settled }));

        Assert.True(CasinoLedgerKinds.Proposable(CasinoLedgerKinds.BuyIn));
        Assert.True(CasinoLedgerKinds.Proposable(CasinoLedgerKinds.Payout));
        Assert.False(CasinoLedgerKinds.Proposable(CasinoLedgerKinds.CashOut));
    }
}
