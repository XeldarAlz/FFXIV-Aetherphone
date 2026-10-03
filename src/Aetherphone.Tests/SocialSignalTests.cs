using Aetherphone.Core;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SocialSignalTests
{
    private const string Velvet = "velvet";
    private const string Chirper = "chirper";

    [Fact]
    public void ABarePingRefreshesEveryListener()
    {
        var signal = new SocialSignal(null, null);

        Assert.True(signal.CoversNotifications);
        Assert.True(signal.CoversNotices);
        Assert.True(signal.CoversApp(Velvet));
    }

    [Fact]
    public void ANoticePingLeavesTheNotificationListsAlone()
    {
        var signal = new SocialSignal(null, SocialSignalKinds.Notice);

        Assert.True(signal.CoversNotices);
        Assert.False(signal.CoversNotifications);
        Assert.False(signal.CoversApp(Velvet));
    }

    [Fact]
    public void ANotificationPingForOneAppSkipsTheOtherApps()
    {
        var signal = new SocialSignal(Chirper, SocialSignalKinds.Notification);

        Assert.True(signal.CoversNotifications);
        Assert.False(signal.CoversNotices);
        Assert.True(signal.CoversApp(Chirper));
        Assert.False(signal.CoversApp(Velvet));
    }

    [Fact]
    public void ANotificationPingWithoutAnAppReachesEveryApp()
    {
        var signal = new SocialSignal(null, SocialSignalKinds.Notification);

        Assert.True(signal.CoversApp(Velvet));
        Assert.True(signal.CoversApp(Chirper));
        Assert.False(signal.CoversNotices);
    }
}
