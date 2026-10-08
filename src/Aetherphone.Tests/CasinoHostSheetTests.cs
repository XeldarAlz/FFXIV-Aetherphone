using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoHostSheetTests
{
    [Fact]
    public void TheDefaultDraftIsAPrivateChipTableTheServerAccepts()
    {
        var draft = new HostDraft();
        var config = draft.Build(1000);

        Assert.Equal(CasinoWire.BlackjackKind, config.GameKind);
        Assert.Equal(CasinoCurrencies.Chips, config.Currency);
        Assert.Equal(CasinoListings.Private, config.Listing);
        Assert.Equal(6, config.Seats);
        Assert.Equal(500, config.MinBet);
        Assert.Equal(10_000, config.MaxBet);
        Assert.Equal(20_000, config.MinBuyIn);
        Assert.Equal(5_000_000, config.MaxBuyIn);
        Assert.Null(config.HouseRules);
        Assert.True(config.AutoDeal);
        Assert.Equal(string.Empty, CasinoHostingRules.Check(config));
    }

    [Fact]
    public void APracticeDraftCarriesItsStackRulesAndDealer()
    {
        var draft = new HostDraft
        {
            Currency = CasinoCurrencies.Practice,
            PracticeStack = "250000",
            FaceUp = true,
            DealerMode = CasinoDealerModes.Host,
            AutoDeal = false,
            Pays = CasinoRuleSheet.PaysTwoToOne,
            Decks = 2,
            HitsSoft17 = true,
        };

        var config = draft.Build(1000);

        Assert.True(config.Practice);
        Assert.Equal(250_000, config.PracticeStack);
        Assert.True(config.FaceUp);
        Assert.Equal(CasinoDealerModes.Host, config.DealerMode);
        Assert.False(config.AutoDeal);
        Assert.Equal(0, config.MinBuyIn);
        Assert.Equal(CasinoRuleSheet.PaysTwoToOne, config.HouseRules!.BlackjackPays);
        Assert.Equal(2, config.HouseRules.Decks);
        Assert.True(config.HouseRules.DealerHitsSoft17);
        Assert.Equal(string.Empty, CasinoHostingRules.Check(config));
    }

    [Fact]
    public void AGilDraftNeedsABankThatCoversTheBet()
    {
        var draft = new HostDraft { Currency = CasinoCurrencies.Gil, Bank = "20000000", GilMaxBet = "500000" };
        var config = draft.Build(1000);

        Assert.Equal(20_000_000, config.Bank);
        Assert.Equal(500_000, config.MaxBet);
        Assert.False(config.FaceUp);
        Assert.False(config.PracticeRebuy);
        Assert.Equal(string.Empty, CasinoHostingRules.Check(config));

        var unbanked = new HostDraft { Currency = CasinoCurrencies.Gil, GilMaxBet = "500000" }.Build(1000);
        Assert.Equal(CasinoReasons.ConfigInvalid, CasinoHostingRules.Check(unbanked));
    }

    [Fact]
    public void AmountFieldsReadOnlyWholePositiveNumbers()
    {
        Assert.Equal(1234, HostDraft.Parse("1234"));
        Assert.Equal(0, HostDraft.Parse(string.Empty));
        Assert.Equal(0, HostDraft.Parse("-5"));
        Assert.Equal(0, HostDraft.Parse("1.5"));
    }
}
