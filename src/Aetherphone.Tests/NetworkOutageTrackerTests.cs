using Aetherphone.Core.Aethernet;
using Xunit;

namespace Aetherphone.Tests;

public sealed class NetworkOutageTrackerTests
{
    private const long LostAfter = NetworkOutageTracker.LostAfterMilliseconds;
    private const long StableAfter = NetworkOutageTracker.StableAfterMilliseconds;
    private const long NoticeGap = NetworkOutageTracker.NoticeGapMilliseconds;

    [Fact]
    public void TheFirstConnectionAfterSignInSaysNothing()
    {
        var tracker = new NetworkOutageTracker();
        Assert.Equal(NetworkNotice.None, tracker.Tick(0));

        tracker.Connected(1_000);

        Assert.Equal(NetworkNotice.None, tracker.Tick(1_000 + StableAfter * 10));
    }

    [Fact]
    public void AShortDropSaysNothing()
    {
        var tracker = new NetworkOutageTracker();
        tracker.Connected(0);
        tracker.Disconnected(100_000);

        Assert.Equal(NetworkNotice.None, tracker.Tick(100_000 + LostAfter - 1));
        tracker.Connected(100_000 + LostAfter - 1);
        Assert.Equal(NetworkNotice.None, tracker.Tick(200_000));
    }

    [Fact]
    public void ALongDropSaysLostOnceThenBackOnlineOnceTheLinkHolds()
    {
        var tracker = new NetworkOutageTracker();
        tracker.Connected(0);
        tracker.Disconnected(100_000);

        Assert.Equal(NetworkNotice.Lost, tracker.Tick(100_000 + LostAfter));
        Assert.Equal(NetworkNotice.None, tracker.Tick(200_000));

        tracker.Connected(210_000);
        Assert.Equal(NetworkNotice.None, tracker.Tick(210_000 + StableAfter - 1));
        Assert.Equal(NetworkNotice.BackOnline, tracker.Tick(210_000 + StableAfter));
        Assert.Equal(NetworkNotice.None, tracker.Tick(400_000));
    }

    [Fact]
    public void ARestartNoticeComesFirstAndReplacesTheLostLine()
    {
        var tracker = new NetworkOutageTracker();
        tracker.Connected(0);

        tracker.RestartAnnounced(100_000);
        Assert.Equal(NetworkNotice.Restarting, tracker.Tick(100_000));
        Assert.Equal(NetworkNotice.None, tracker.Tick(100_000 + StableAfter * 2));

        tracker.Disconnected(100_000 + StableAfter * 2);
        Assert.Equal(NetworkNotice.None, tracker.Tick(100_000 + LostAfter * 3));

        tracker.Connected(200_000);
        Assert.Equal(NetworkNotice.BackOnline, tracker.Tick(200_000 + StableAfter));
    }

    [Fact]
    public void TheCloseFrameAfterTheRestartNoticeIsNotASecondNotice()
    {
        var tracker = new NetworkOutageTracker();
        tracker.Connected(0);
        tracker.RestartAnnounced(50_000);
        Assert.Equal(NetworkNotice.Restarting, tracker.Tick(50_000));

        tracker.RestartAnnounced(50_010);
        tracker.Disconnected(50_020);

        Assert.Equal(NetworkNotice.None, tracker.Tick(50_100));
    }

    [Fact]
    public void ARestartAnnouncedByTheCloseAloneStillGetsBackOnline()
    {
        var tracker = new NetworkOutageTracker();
        tracker.Connected(0);
        tracker.Disconnected(80_000);
        tracker.RestartAnnounced(80_000);

        Assert.Equal(NetworkNotice.Restarting, tracker.Tick(80_000));
        tracker.Connected(90_000);
        Assert.Equal(NetworkNotice.BackOnline, tracker.Tick(90_000 + StableAfter));
    }

    [Fact]
    public void AReconnectStormGivesOneLostAndOneBackOnline()
    {
        var tracker = new NetworkOutageTracker();
        tracker.Connected(0);
        tracker.Disconnected(100_000);
        var notices = new List<NetworkNotice>();
        for (var now = 100_000L; now <= 300_000; now += 250)
        {
            var flapping = now >= 125_000 && now < 250_000;
            if (flapping && now % 2_000 == 0)
            {
                tracker.Connected(now);
            }
            else if (flapping && now % 2_000 == 1_000)
            {
                tracker.Disconnected(now);
            }
            else if (now >= 250_000)
            {
                tracker.Connected(now);
            }

            var notice = tracker.Tick(now);
            if (notice != NetworkNotice.None)
            {
                notices.Add(notice);
            }
        }

        Assert.Equal(new[] { NetworkNotice.Lost, NetworkNotice.BackOnline }, notices);
    }

    [Fact]
    public void ARestartThatNeverDropsTheLinkClearsQuietly()
    {
        var tracker = new NetworkOutageTracker();
        tracker.Connected(0);
        tracker.RestartAnnounced(10_000);
        Assert.Equal(NetworkNotice.Restarting, tracker.Tick(10_000));

        Assert.Equal(NetworkNotice.None, tracker.Tick(10_000 + NetworkOutageTracker.RestartWithoutDropMilliseconds));
        Assert.Equal(NetworkNotice.None, tracker.Tick(10_000 + NetworkOutageTracker.RestartWithoutDropMilliseconds * 2));
    }

    [Fact]
    public void ANewOutageRightAfterBackOnlineWaitsForTheGap()
    {
        var tracker = new NetworkOutageTracker();
        tracker.Connected(0);
        tracker.Disconnected(100_000);
        Assert.Equal(NetworkNotice.Lost, tracker.Tick(100_000 + LostAfter));
        tracker.Connected(130_000);
        var backAt = 130_000 + StableAfter;
        Assert.Equal(NetworkNotice.BackOnline, tracker.Tick(backAt));

        tracker.Disconnected(backAt + 1_000);

        Assert.Equal(NetworkNotice.None, tracker.Tick(backAt + 1_000 + LostAfter));
        Assert.Equal(NetworkNotice.Lost, tracker.Tick(backAt + NoticeGap));
    }

    [Fact]
    public void AResetForgetsTheSession()
    {
        var tracker = new NetworkOutageTracker();
        tracker.Connected(0);
        tracker.Disconnected(1_000);
        tracker.Reset();

        Assert.False(tracker.Online);
        Assert.Equal(NetworkNotice.None, tracker.Tick(100_000));
        tracker.Connected(200_000);
        Assert.Equal(NetworkNotice.None, tracker.Tick(300_000));
    }

    [Fact]
    public void ARestartBeforeTheFirstConnectionIsIgnored()
    {
        var tracker = new NetworkOutageTracker();
        tracker.RestartAnnounced(0);

        Assert.Equal(NetworkNotice.None, tracker.Tick(0));
    }
}
