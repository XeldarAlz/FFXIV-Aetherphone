using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoHostDraftMemoryTests
{
    private const ulong Main = 11UL;
    private const ulong Alt = 22UL;

    private static CasinoPreferences PreferencesFor(Configuration configuration, ulong contentId, Action? save = null) =>
        new(configuration, () => contentId, save ?? (() => { }));

    private static HostDraft GilBlackjack()
    {
        var draft = new HostDraft();
        draft.SelectCurrency(CasinoCurrencies.Gil, true);
        draft.Seats = 4;
        draft.Listing = CasinoListings.Open;
        draft.Name = "Night table";
        draft.Bank = HostLadders.FloorIndex(HostLadders.Gil, 25_000_000);
        draft.MaxBet = HostLadders.FloorIndex(HostLadders.Gil, 1_000_000);
        draft.MinBet = HostLadders.FloorIndex(HostLadders.Gil, 5_000);
        draft.DealerMode = CasinoDealerModes.Host;
        draft.AutoDeal = true;
        draft.Pays = CasinoRuleSheet.PaysTwoToOne;
        draft.HitsSoft17 = true;
        draft.Normalize(true);
        return draft;
    }

    [Fact]
    public void ARememberedDraftComesBackAsTheSameTable()
    {
        var configuration = new Configuration();
        var saves = 0;
        var preferences = PreferencesFor(configuration, Main, () => saves++);
        var original = GilBlackjack();

        preferences.RememberHostDraft(original.Snapshot(false));

        Assert.Equal(1, saves);
        Assert.True(preferences.HasHostDraft);
        Assert.True(preferences.TryLoadHostDraft(out var memory));
        Assert.False(memory.PinLocation);
        var restored = new HostDraft();
        restored.Restore(memory);
        restored.Normalize(true);
        Assert.Equal(HostGame.Blackjack, HostDraft.GameOf(memory));
        Assert.Equal(original.Build(null), restored.Build(null));
        Assert.Equal("Night table", restored.Name);
    }

    [Fact]
    public void ARoomDraftRoundTripsItsOwnOptions()
    {
        var configuration = new Configuration();
        var preferences = PreferencesFor(configuration, Main);
        var original = new HostDraft();
        original.SelectGame(HostGame.Deathroll, true);
        original.SelectCurrency(CasinoCurrencies.Gil, true);
        original.StartAt = HostLadders.FloorIndex(HostLadders.StartNumbers, 10_000);
        original.Stake = HostLadders.FloorIndex(HostLadders.Gil, 50_000);
        original.Listing = CasinoListings.Knock;
        original.Normalize(true);

        preferences.RememberHostDraft(original.Snapshot(true));

        Assert.True(preferences.TryLoadHostDraft(out var memory));
        var restored = new HostDraft();
        restored.Restore(memory);
        restored.Normalize(true);
        Assert.Equal(HostGame.Deathroll, restored.Game);
        Assert.Equal(original.Build(null), restored.Build(null));
    }

    [Fact]
    public void ADiceStakeRoundTripsThroughTheRememberedDraft()
    {
        var configuration = new Configuration();
        var preferences = PreferencesFor(configuration, Main);
        var original = new HostDraft();
        original.SelectGame(HostGame.DiceTable, true);
        original.SelectCurrency(CasinoCurrencies.Gil, true);
        original.DiceStake = Array.IndexOf(HostLadders.DiceStakes, 25_000L);
        original.Normalize(true);

        preferences.RememberHostDraft(original.Snapshot(true));

        Assert.True(preferences.TryLoadHostDraft(out var memory));
        var restored = new HostDraft();
        restored.Restore(memory);
        restored.Normalize(true);
        Assert.Equal(25_000, restored.Build(null).Dice!.Stake);
        Assert.Equal(original.Build(null), restored.Build(null));
    }

    [Fact]
    public void ADraftFromAnotherVersionIsDiscarded()
    {
        var configuration = new Configuration();
        var preferences = PreferencesFor(configuration, Main);
        preferences.RememberHostDraft(GilBlackjack().Snapshot(true));
        configuration.CasinoHostDrafts[Main].Version = CasinoHostDraftMemory.CurrentVersion + 1;

        Assert.False(preferences.HasHostDraft);
        Assert.False(preferences.TryLoadHostDraft(out var memory));
        Assert.Null(memory);
    }

    [Fact]
    public void DraftsAreKeptPerCharacter()
    {
        var configuration = new Configuration();
        var main = PreferencesFor(configuration, Main);
        var alt = PreferencesFor(configuration, Alt);
        main.RememberHostDraft(GilBlackjack().Snapshot(true));

        Assert.True(main.HasHostDraft);
        Assert.False(alt.HasHostDraft);

        var raffle = new HostDraft();
        raffle.SelectGame(HostGame.Raffle, true);
        alt.RememberHostDraft(raffle.Snapshot(true));

        Assert.True(main.TryLoadHostDraft(out var mainMemory));
        Assert.True(alt.TryLoadHostDraft(out var altMemory));
        Assert.Equal(HostGame.Blackjack, HostDraft.GameOf(mainMemory));
        Assert.Equal(HostGame.Raffle, HostDraft.GameOf(altMemory));
    }

    [Fact]
    public void WithoutACharacterNothingIsRememberedOrLoaded()
    {
        var configuration = new Configuration();
        var saves = 0;
        var preferences = PreferencesFor(configuration, 0, () => saves++);

        preferences.RememberHostDraft(GilBlackjack().Snapshot(true));

        Assert.Equal(0, saves);
        Assert.Empty(configuration.CasinoHostDrafts);
        Assert.False(preferences.HasHostDraft);
    }

    [Fact]
    public void StaleLadderIndexesSnapBackOntoTheLadder()
    {
        var memory = GilBlackjack().Snapshot(true);
        memory.Bank = 999;
        memory.MaxBet = -4;
        memory.Game = 42;
        memory.Listing = 9;
        memory.Pays = 7;

        var restored = new HostDraft();
        restored.Restore(memory);
        restored.Normalize(true);
        var config = restored.Build(null);

        Assert.Equal(HostGame.Book, restored.Game);
        Assert.Equal(CasinoListings.Open, restored.Listing);
        Assert.Equal(CasinoRuleSheet.PaysEven, restored.Pays);
        Assert.Equal(HostLadders.Gil[^1], HostLadders.Gil[restored.Bank]);
        Assert.Equal(string.Empty, CasinoHostingRules.Check(config));
    }
}
