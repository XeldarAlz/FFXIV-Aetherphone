using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class TradeSyncTests
{
    private const string Host = "u-host";
    private const string Player = "u-mira";
    private const string Table = "table-a1";

    private static readonly string[] MiraWindow = { "Trade", "Mira Rhan", "Confirm" };
    private static readonly string[] HostWindow = { "Rhan Host" };

    private static TradeWindowSample Open(string[] names, long mine, long theirs, long wallet) =>
        new(true, names, mine, theirs, wallet);

    private static TradeWindowSample Closed(long wallet) => new(false, Array.Empty<string>(), 0, 0, wallet);

    private static TradeTableContext HostContext(params CasinoLedgerEntryDto[] entries) =>
        new(Table, Host, Host, "Rhan Host",
            new[] { new TradeSeat(Player, "Mira Rhan"), new TradeSeat("u-other", "Other Person") }, entries);

    private static TradeTableContext PlayerContext(params CasinoLedgerEntryDto[] entries) =>
        new(Table, Player, Host, "Rhan Host", new[] { new TradeSeat(Player, "Mira Rhan") }, entries);

    [Fact]
    public void ACompletedTradeIsReadFromTheWalletAndTheOffers()
    {
        var tracker = new TradeSyncTracker();
        Assert.Null(tracker.Observe(Open(MiraWindow, 0, 500_000, 1_000_000), 0));
        Assert.Null(tracker.Observe(Closed(1_000_000), 100));
        var done = tracker.Observe(Closed(1_500_000), 200);
        Assert.NotNull(done);
        Assert.Equal(500_000, done!.Value.Received);
        Assert.Equal(0, done.Value.Sent);
        Assert.Equal(TradeTrackerStage.Idle, tracker.Stage);
    }

    [Fact]
    public void SwappedOfferSidesStillMatchTheWallet()
    {
        var tracker = new TradeSyncTracker();
        tracker.Observe(Open(MiraWindow, 300_000, 0, 1_000_000), 0);
        var done = tracker.Observe(Closed(1_300_000), 50);
        Assert.Equal(300_000, done!.Value.Received);
        Assert.Equal(0, done.Value.Sent);
    }

    [Fact]
    public void ACancelledTradeLeavesNothingBehind()
    {
        var tracker = new TradeSyncTracker();
        tracker.Observe(Open(MiraWindow, 0, 500_000, 1_000_000), 0);
        Assert.Null(tracker.Observe(Closed(1_000_000), 10));
        Assert.Null(tracker.Observe(Closed(1_000_000), TradeSyncTracker.SettleWindowMilliseconds + 20));
        Assert.Equal(TradeTrackerStage.Idle, tracker.Stage);
        Assert.Null(tracker.Observe(Closed(1_200_000), TradeSyncTracker.SettleWindowMilliseconds + 40));
    }

    [Fact]
    public void UnreadOffersFallBackToTheWalletDelta()
    {
        var tracker = new TradeSyncTracker();
        tracker.Observe(Open(MiraWindow, 0, 0, 2_000_000), 0);
        var done = tracker.Observe(Closed(1_250_000), 10);
        Assert.Equal(0, done!.Value.Received);
        Assert.Equal(750_000, done.Value.Sent);
    }

    [Fact]
    public void TheHostReceivingGilProposesABuyIn()
    {
        var proposal = TradeLedgerMatcher.Match(new TradeCompletion(MiraWindow, 500_000, 0), HostContext());
        Assert.Equal(TradeProposalKind.Propose, proposal.Kind);
        Assert.Equal(CasinoLedgerKinds.BuyIn, proposal.LedgerKind);
        Assert.Equal(Player, proposal.CounterpartyUserId);
        Assert.Equal(500_000, proposal.Amount);
        Assert.True(proposal.HostSide);
        Assert.True(proposal.Incoming);
    }

    [Fact]
    public void TheHostConfirmsTheBuyInThePlayerAlreadyClaimed()
    {
        var claimed = new CasinoLedgerEntryDto("e1", Table, CasinoLedgerKinds.BuyIn, 500_000, Player, "Mira Rhan", Host,
            "Rhan Host", PayerConfirmed: true);
        var proposal = TradeLedgerMatcher.Match(new TradeCompletion(MiraWindow, 500_000, 0), HostContext(claimed));
        Assert.Equal(TradeProposalKind.Confirm, proposal.Kind);
        Assert.Equal("e1", proposal.EntryId);
    }

    [Fact]
    public void TheHostSendingGilSettlesTheCashOutOrProposesAPayout()
    {
        var cashOut = new CasinoLedgerEntryDto("e2", Table, CasinoLedgerKinds.CashOut, 80_000, Host, "Rhan Host",
            Player, "Mira Rhan");
        var settled = TradeLedgerMatcher.Match(new TradeCompletion(MiraWindow, 0, 80_000), HostContext(cashOut));
        Assert.Equal(TradeProposalKind.Confirm, settled.Kind);
        Assert.Equal("e2", settled.EntryId);
        var fresh = TradeLedgerMatcher.Match(new TradeCompletion(MiraWindow, 0, 90_000), HostContext(cashOut));
        Assert.Equal(TradeProposalKind.Propose, fresh.Kind);
        Assert.Equal(CasinoLedgerKinds.Payout, fresh.LedgerKind);
        Assert.False(fresh.Incoming);
    }

    [Fact]
    public void ATradeWithSomeoneNotSeatedIsIgnored()
    {
        var proposal = TradeLedgerMatcher.Match(new TradeCompletion(new[] { "Stranger Danger" }, 500_000, 0),
            HostContext());
        Assert.Equal(TradeProposalKind.None, proposal.Kind);
    }

    [Fact]
    public void ThePlayerSendingGilToTheHostProposesTheirBuyIn()
    {
        var proposal = TradeLedgerMatcher.Match(new TradeCompletion(HostWindow, 0, 250_000), PlayerContext());
        Assert.Equal(TradeProposalKind.Propose, proposal.Kind);
        Assert.Equal(CasinoLedgerKinds.BuyIn, proposal.LedgerKind);
        Assert.Equal(Host, proposal.CounterpartyUserId);
        Assert.False(proposal.HostSide);
    }

    [Fact]
    public void ThePlayerReceivingAPayoutConfirmsIt()
    {
        var payout = new CasinoLedgerEntryDto("e3", Table, CasinoLedgerKinds.Payout, 40_000, Host, "Rhan Host",
            Player, "Mira Rhan", PayerConfirmed: true);
        var proposal = TradeLedgerMatcher.Match(new TradeCompletion(HostWindow, 40_000, 0), PlayerContext(payout));
        Assert.Equal(TradeProposalKind.Confirm, proposal.Kind);
        Assert.Equal("e3", proposal.EntryId);
        Assert.True(proposal.Incoming);
    }

    [Fact]
    public void NamesCompareWithoutWorldsIconsOrCase()
    {
        Assert.Equal("mira rhan", TradeNames.Normalize("Mira Rhan@Gilgamesh"));
        Assert.Equal("mira rhan", TradeNames.Normalize("  MIRA   Rhan "));
        Assert.Equal("mira rhan", TradeNames.Normalize("Mira Rhan\uE05DGilgamesh"));
        Assert.Equal("j'ayna o'ren", TradeNames.Normalize("J'ayna O'ren"));
    }

    [Fact]
    public void AmountsParseWithGroupSeparatorsOnly()
    {
        Assert.True(TradeNames.TryAmount("1,500,000", out var grouped));
        Assert.Equal(1_500_000, grouped);
        Assert.True(TradeNames.TryAmount("2 000", out var spaced));
        Assert.Equal(2_000, spaced);
        Assert.False(TradeNames.TryAmount("Mira", out _));
        Assert.False(TradeNames.TryAmount("1234567890123", out _));
    }
}
