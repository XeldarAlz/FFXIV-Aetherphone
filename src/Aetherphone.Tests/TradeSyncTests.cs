using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class TradeSyncTests
{
    private const string Mira = "Mira Rhan";
    private const string Ciri = "Ciri Vale";
    private const string Home = "Phoenix";

    private static TradeWindowSample Open(string partner, long mine, long theirs, long wallet, string world = "") =>
        new(true, partner, world, mine, theirs, wallet, true);

    private static TradeWindowSample Closed(long wallet) =>
        new(false, string.Empty, string.Empty, 0, 0, wallet, true);

    private static TradeWindowSample Unreadable(bool open, string partner, long mine, long theirs) =>
        new(open, partner, string.Empty, mine, theirs, 0, false);

    [Fact]
    public void ASingleReceiveCompletesAsReceived()
    {
        var tracker = new TradeSyncTracker();
        Assert.Null(tracker.Observe(Open(Mira, 0, 500_000, 1_000_000, Home), 0));
        Assert.Null(tracker.Observe(Closed(1_000_000), 100));

        var done = tracker.Observe(Closed(1_500_000), 200);

        Assert.Equal(new TradeCompletion(Mira, Home, 500_000, 0), done);
        Assert.Null(tracker.Observe(Closed(1_500_000), 300));
    }

    [Fact]
    public void ASingleSendCompletesAsSent()
    {
        var tracker = new TradeSyncTracker();
        tracker.Observe(Open(Mira, 250_000, 0, 1_000_000), 0);
        tracker.Observe(Closed(1_000_000), 100);

        var done = tracker.Observe(Closed(750_000), 150);

        Assert.Equal(new TradeCompletion(Mira, string.Empty, 0, 250_000), done);
        Assert.Equal(-250_000, done!.Value.Net);
    }

    [Fact]
    public void GilOnBothSidesCompletesWithTheNetThatReachedTheWallet()
    {
        var tracker = new TradeSyncTracker();
        tracker.Observe(Open(Mira, 20_000, 50_000, 1_000_000), 0);
        tracker.Observe(Closed(1_000_000), 50);

        var done = tracker.Observe(Closed(1_030_000), 60);

        Assert.Equal(new TradeCompletion(Mira, string.Empty, 30_000, 0), done);
    }

    [Fact]
    public void AWindowThatReshowsWithinFourSecondsIsStillOneTrade()
    {
        var tracker = new TradeSyncTracker();
        tracker.Observe(Open(Mira, 0, 300_000, 1_000_000), 0);
        Assert.Null(tracker.Observe(Closed(1_000_000), 1_000));
        Assert.Null(tracker.Observe(Open(Mira, 0, 300_000, 1_000_000), 2_500));
        Assert.Null(tracker.Observe(Closed(1_000_000), 3_000));

        var done = tracker.Observe(Closed(1_300_000), 3_100);

        Assert.Equal(new TradeCompletion(Mira, string.Empty, 300_000, 0), done);
        Assert.Null(tracker.Observe(Closed(1_600_000), 3_200));
    }

    [Fact]
    public void AReshownWindowWhosePartnerArrivesAFrameLateStillContinuesTheTrade()
    {
        var tracker = new TradeSyncTracker();
        tracker.Observe(Open(Mira, 0, 300_000, 1_000_000), 0);
        tracker.Observe(Closed(1_000_000), 1_000);
        tracker.Observe(Open(string.Empty, 0, 0, 1_000_000), 2_000);
        tracker.Observe(Open(Mira, 0, 300_000, 1_000_000), 2_016);
        tracker.Observe(Closed(1_000_000), 2_500);

        Assert.Equal(new TradeCompletion(Mira, string.Empty, 300_000, 0),
            tracker.Observe(Closed(1_300_000), 2_600));
        Assert.Null(tracker.Observe(Closed(1_600_000), 2_700));
    }

    [Fact]
    public void BackToBackTradesOfOneAmountWithTwoPartnersCompleteOnceEachInOrder()
    {
        var tracker = new TradeSyncTracker();
        tracker.Observe(Open(Mira, 0, 100_000, 1_000_000), 0);
        tracker.Observe(Closed(1_000_000), 100);
        tracker.Observe(Open(Ciri, 0, 100_000, 1_000_000), 200);
        tracker.Observe(Closed(1_000_000), 300);

        var first = tracker.Observe(Closed(1_100_000), 400);
        var second = tracker.Observe(Closed(1_200_000), 500);
        var third = tracker.Observe(Closed(1_300_000), 600);

        Assert.Equal(Mira, first!.Value.PartnerName);
        Assert.Equal(Ciri, second!.Value.PartnerName);
        Assert.Null(third);
    }

    [Fact]
    public void ThePreviousTradeCompletesWhileTheNextWindowIsOpen()
    {
        var tracker = new TradeSyncTracker();
        tracker.Observe(Open(Mira, 0, 100_000, 1_000_000), 0);
        tracker.Observe(Closed(1_000_000), 100);
        tracker.Observe(Open(Ciri, 40_000, 0, 1_000_000), 150);

        var first = tracker.Observe(Open(Ciri, 40_000, 0, 1_100_000), 200);
        tracker.Observe(Closed(1_100_000), 300);
        var second = tracker.Observe(Closed(1_060_000), 310);

        Assert.Equal(new TradeCompletion(Mira, string.Empty, 100_000, 0), first);
        Assert.Equal(new TradeCompletion(Ciri, string.Empty, 0, 40_000), second);
    }

    [Fact]
    public void AWalletChangeAFrameBeforeTheWindowClosesCompletesOnce()
    {
        var tracker = new TradeSyncTracker();
        tracker.Observe(Open(Mira, 0, 500_000, 1_000_000), 0);

        var done = tracker.Observe(Open(Mira, 0, 500_000, 1_500_000), 16);

        Assert.Equal(new TradeCompletion(Mira, string.Empty, 500_000, 0), done);
        Assert.Null(tracker.Observe(Open(Mira, 0, 500_000, 1_500_000), 32));
        Assert.Null(tracker.Observe(Closed(1_500_000), 48));
        Assert.Null(tracker.Observe(Closed(2_000_000), 64));
    }

    [Fact]
    public void AnUnrelatedWalletChangeIsIgnoredWhileATradeIsArmed()
    {
        var tracker = new TradeSyncTracker();
        tracker.Observe(Open(Mira, 0, 80_000, 1_000_000), 0);
        tracker.Observe(Closed(1_000_000), 100);

        Assert.Null(tracker.Observe(Closed(999_100), 200));
        Assert.Null(tracker.Observe(Closed(1_049_100), 300));
        var done = tracker.Observe(Closed(1_129_100), 400);

        Assert.Equal(new TradeCompletion(Mira, string.Empty, 80_000, 0), done);
    }

    [Fact]
    public void ATradeWithNoGilOfferedNeverTakesALaterWalletChange()
    {
        var tracker = new TradeSyncTracker();
        tracker.Observe(Open(Mira, 0, 0, 1_000_000), 0);
        tracker.Observe(Closed(1_000_000), 100);

        Assert.Null(tracker.Observe(Closed(999_000), 200));
        Assert.Null(tracker.Observe(Closed(1_075_000), 300));
    }

    [Fact]
    public void AnArmedTradeExpiresAfterThirtySeconds()
    {
        var tracker = new TradeSyncTracker();
        tracker.Observe(Open(Mira, 0, 50_000, 1_000_000), 0);
        tracker.Observe(Closed(1_000_000), 1_000);

        Assert.Null(tracker.Observe(Closed(1_050_000), 1_000 + TradeSyncTracker.ArmedMilliseconds + 1));
    }

    [Fact]
    public void AnArmedTradeStillCompletesOnItsLastArmedMillisecond()
    {
        var tracker = new TradeSyncTracker();
        tracker.Observe(Open(Mira, 0, 50_000, 1_000_000), 0);
        tracker.Observe(Closed(1_000_000), 1_000);

        Assert.NotNull(tracker.Observe(Closed(1_050_000), 1_000 + TradeSyncTracker.ArmedMilliseconds));
    }

    [Fact]
    public void AFifthArmedTradeDropsTheOldest()
    {
        var tracker = new TradeSyncTracker();
        string[] partners = { "Ana One", "Bea Two", "Cid Three", "Dax Four", "Eve Five" };
        for (var index = 0; index < partners.Length; index++)
        {
            tracker.Observe(Open(partners[index], 0, (index + 1) * 10_000L, 1_000_000), index * 100L);
            tracker.Observe(Closed(1_000_000), index * 100L + 50);
        }

        Assert.Null(tracker.Observe(Closed(1_010_000), 1_000));
        var second = tracker.Observe(Closed(1_030_000), 1_100);
        var fifth = tracker.Observe(Closed(1_080_000), 1_200);

        Assert.Equal("Bea Two", second!.Value.PartnerName);
        Assert.Equal("Eve Five", fifth!.Value.PartnerName);
    }

    [Fact]
    public void AnUnreadableWalletNeverCompletesAnything()
    {
        var tracker = new TradeSyncTracker();
        tracker.Observe(Open(Mira, 0, 20_000, 1_000_000), 0);
        Assert.Null(tracker.Observe(Unreadable(true, Mira, 0, 20_000), 16));
        Assert.Null(tracker.Observe(Unreadable(false, string.Empty, 0, 0), 32));
        Assert.Null(tracker.Observe(Unreadable(false, string.Empty, 0, 0), 48));

        Assert.Null(tracker.Observe(Closed(1_000_000), 64));
        Assert.Equal(new TradeCompletion(Mira, string.Empty, 20_000, 0), tracker.Observe(Closed(1_020_000), 80));
    }

    [Fact]
    public void TheFirstWalletReadOnlySetsTheReference()
    {
        var tracker = new TradeSyncTracker();
        Assert.Null(tracker.Observe(Closed(5_000_000), 0));
        Assert.Null(tracker.Observe(Closed(5_000_000), 16));
        tracker.Observe(Open(Mira, 0, 10_000, 5_000_000), 32);
        tracker.Observe(Closed(5_000_000), 48);

        Assert.NotNull(tracker.Observe(Closed(5_010_000), 64));
    }

    [Fact]
    public void ResetForgetsArmedTradesAndTheWallet()
    {
        var tracker = new TradeSyncTracker();
        tracker.Observe(Open(Mira, 0, 50_000, 1_000_000), 0);
        tracker.Observe(Closed(1_000_000), 100);

        tracker.Reset();

        Assert.Null(tracker.Observe(Closed(1_050_000), 200));
        Assert.Null(tracker.Observe(Closed(1_100_000), 300));
    }

    [Fact]
    public void AWorldReadAFrameAfterTheNameStaysOneTrade()
    {
        var tracker = new TradeSyncTracker();
        tracker.Observe(Open(Mira, 0, 70_000, 1_000_000), 0);
        tracker.Observe(Open(Mira, 0, 70_000, 1_000_000, Home), 16);
        tracker.Observe(Closed(1_000_000), 32);

        Assert.Equal(new TradeCompletion(Mira, Home, 70_000, 0), tracker.Observe(Closed(1_070_000), 48));
        Assert.Null(tracker.Observe(Closed(1_140_000), 64));
    }

    [Fact]
    public void ANewPartnerInAnOpenWindowArmsThePreviousTrade()
    {
        var tracker = new TradeSyncTracker();
        tracker.Observe(Open(Mira, 0, 60_000, 1_000_000), 0);
        tracker.Observe(Open(Ciri, 0, 90_000, 1_000_000), 16);
        tracker.Observe(Closed(1_000_000), 32);

        Assert.Equal(Mira, tracker.Observe(Closed(1_060_000), 48)!.Value.PartnerName);
        Assert.Equal(Ciri, tracker.Observe(Closed(1_150_000), 64)!.Value.PartnerName);
    }

    [Fact]
    public void TradesFollowTheTableForTwelveHoursAfterTheHostWasLastAtIt()
    {
        const long atTable = 1_000;
        Assert.False(CasinoTradeSync.HostedRecently(0, 5_000));
        Assert.True(CasinoTradeSync.HostedRecently(atTable, atTable));
        Assert.True(CasinoTradeSync.HostedRecently(atTable, atTable + CasinoTradeSync.HostedWindowMilliseconds));
        Assert.False(CasinoTradeSync.HostedRecently(atTable, atTable + CasinoTradeSync.HostedWindowMilliseconds + 1));
        Assert.Equal(12 * 60 * 60 * 1_000L, CasinoTradeSync.HostedWindowMilliseconds);
    }

    [Fact]
    public void SigningOutKeepsTheTableAndOnlyAnotherCharacterForgetsIt()
    {
        Assert.False(CasinoTradeSync.ForgetsTable("host", string.Empty));
        Assert.False(CasinoTradeSync.ForgetsTable("host", "host"));
        Assert.True(CasinoTradeSync.ForgetsTable("host", "alt"));
        Assert.True(CasinoTradeSync.ForgetsTable(string.Empty, "host"));
    }
}
