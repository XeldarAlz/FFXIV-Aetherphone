using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HostDraftSummaryTests
{
    [Fact]
    public void AGilBlackjackDraftNamesItsBank()
    {
        var draft = new HostDraft();
        draft.SelectCurrency(CasinoCurrencies.Gil, true);
        draft.Bank = HostLadders.FloorIndex(HostLadders.Gil, 25_000_000);
        draft.Normalize(true);

        var text = HostDraftSummary.Of(draft);

        Assert.Equal("Blackjack, Gil, bank " + NumberText.Compact(25_000_000), text);
        Assert.Equal(text, HostDraftSummary.Of(draft.Snapshot(true), true));
    }

    [Fact]
    public void AChipBlackjackDraftNamesItsBetRange()
    {
        var draft = new HostDraft();
        draft.Normalize(true);

        var text = HostDraftSummary.Of(draft);

        Assert.Equal(
            "Blackjack, Chips, " + NumberText.Compact(draft.MinBetValue) + " to " + NumberText.Compact(draft.MaxBetValue),
            text);
    }

    [Fact]
    public void AGilDraftFallsBackToChipsWhenGilIsClosed()
    {
        var draft = new HostDraft();
        draft.SelectCurrency(CasinoCurrencies.Gil, true);
        draft.Normalize(true);

        var text = HostDraftSummary.Of(draft.Snapshot(true), false);

        Assert.StartsWith("Blackjack, Chips, ", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ADeathrollDraftNamesItsStakeAndADiceTableOnlyItsCurrency()
    {
        var deathroll = new HostDraft();
        deathroll.SelectGame(HostGame.Deathroll, true);
        deathroll.SelectCurrency(CasinoCurrencies.Gil, true);
        deathroll.Normalize(true);
        Assert.Equal("Deathroll, Gil, stake " + NumberText.Compact(deathroll.StakeValue), HostDraftSummary.Of(deathroll));

        var dice = new HostDraft();
        dice.SelectGame(HostGame.DiceTable, false);
        dice.Normalize(false);
        Assert.Equal("Dice table, Practice", HostDraftSummary.Of(dice));
    }

    [Fact]
    public void ABookDraftNamesItsBank()
    {
        var draft = new HostDraft();
        draft.SelectGame(HostGame.Book, true);
        draft.Bank = HostLadders.FloorIndex(HostLadders.Gil, 5_000_000);
        draft.Normalize(true);

        Assert.Equal("Book, Gil, bank " + NumberText.Compact(5_000_000), HostDraftSummary.Of(draft));
        Assert.Equal(HostDraftSummary.Of(draft), HostDraftSummary.Of(draft.Snapshot(true), true));
    }

    [Fact]
    public void AHoldemDraftNamesItsBlinds()
    {
        var draft = new HostDraft();
        draft.SelectGame(HostGame.Holdem, true);
        draft.Normalize(true);

        var text = HostDraftSummary.Of(draft);

        Assert.Equal(
            "Hold'em, Chips, " + NumberText.Compact(draft.BigBlindValue / 2) + " to "
            + NumberText.Compact(draft.BigBlindValue), text);
    }
}
