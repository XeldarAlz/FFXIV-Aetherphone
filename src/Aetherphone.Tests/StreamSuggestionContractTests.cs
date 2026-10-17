using Aetherphone.Core.Notifications;
using Aetherphone.Core.Video;
using Xunit;

namespace Aetherphone.Tests;

public sealed class StreamSuggestionContractTests
{
    [Fact]
    public void ASuggestionAlertGroupsUnderTheStreamAppSettings()
    {
        var notification = new PhoneNotification(StreamSuggestionNotifier.AppId, "Queue suggestion",
            "Nym suggested a video for the queue.", System.DateTime.Now, default,
            StreamSuggestionNotifier.GroupKey);
        Assert.Equal("aetherstream:suggestions", notification.GroupKey);
        Assert.Equal(PhoneNotification.StackKeyOf("aetherstream", "aetherstream:suggestions"), notification.StackKey);
        Assert.Equal("aetherstream", notification.SettingsKey);
    }

    [Fact]
    public void ASuggestionAlertStaysQuietWhileTheQueueIsBeingWatched()
    {
        Assert.True(StreamSuggestionNotifier.Watching(1_000, 1_200));
        Assert.False(StreamSuggestionNotifier.Watching(1_000, 9_000));
        Assert.False(StreamSuggestionNotifier.Watching(0, 9_000));
    }

    [Fact]
    public void AnUpNextLaunchFiresOnceAndOnlyOnce()
    {
        var launcher = new AetherStreamLauncher();
        Assert.False(launcher.TryConsumeUpNext());

        launcher.RequestUpNext();
        Assert.True(launcher.TryConsumeUpNext());
        Assert.False(launcher.TryConsumeUpNext());
    }
}
