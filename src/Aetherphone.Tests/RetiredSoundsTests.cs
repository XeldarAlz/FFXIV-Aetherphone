using Aetherphone.Core.Notifications;
using Newtonsoft.Json;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RetiredSoundsTests
{
    [Fact]
    public void RetiredRingtoneMapsToItsReplacement()
    {
        var changed = false;

        var upgraded = RetiredSounds.Replace("file:Ringtone_3.mp3", SoundKind.Ringtone, ref changed);

        Assert.True(changed);
        Assert.Equal("file:Horizon.mp3", upgraded);
    }

    [Fact]
    public void RetiredNotificationMapsToItsReplacement()
    {
        var changed = false;

        var upgraded = RetiredSounds.Replace("file:Notification_1.mp3", SoundKind.Notification, ref changed);

        Assert.True(changed);
        Assert.Equal(SoundLibrary.BundledNotificationToken, upgraded);
    }

    [Fact]
    public void UserFilesSilentAndNullPassThrough()
    {
        var changed = false;

        Assert.Equal("file:my_song.mp3", RetiredSounds.Replace("file:my_song.mp3", SoundKind.Ringtone, ref changed));
        Assert.Equal(SoundTokens.Silent, RetiredSounds.Replace(SoundTokens.Silent, SoundKind.Notification, ref changed));
        Assert.Null(RetiredSounds.Replace(null, SoundKind.Notification, ref changed));
        Assert.False(changed);
    }

    [Fact]
    public void ConfigurationUpgradesGlobalAndPerAppChoices()
    {
        const string json = """
            {"RingtoneSound":"file:Ringtone_6.mp3","NotificationSound":"file:Notification_7.mp3",
             "NotificationSettings":{"message":{"Sound":"file:Notification_2.mp3"},"clock":{"Sound":null}}}
            """;
        var configuration = JsonConvert.DeserializeObject<Configuration>(json)!;

        Assert.True(configuration.ReplaceRetiredSounds());
        Assert.Equal("file:Prism.mp3", configuration.RingtoneSound);
        Assert.Equal("file:Spark.mp3", configuration.NotificationSound);
        Assert.Equal("file:Bloom.mp3", configuration.NotificationSettings["message"].Sound);
        Assert.Null(configuration.NotificationSettings["clock"].Sound);
        Assert.False(configuration.ReplaceRetiredSounds());
    }

    [Theory]
    [InlineData("Ringtones", SoundLibrary.BundledRingtoneToken)]
    [InlineData("Notifications", SoundLibrary.BundledNotificationToken)]
    public void DefaultTokensShipWithThePlugin(string folder, string token)
    {
        Assert.True(SoundTokens.TryFile(token, out var fileName));
        var path = Path.Combine(FindProjectRoot(), "src", "Aetherphone", "Sounds", folder, fileName);
        Assert.True(File.Exists(path), $"missing bundled default {path}");
    }

    private static string FindProjectRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Aetherphone.sln")))
        {
            current = current.Parent;
        }

        Assert.NotNull(current);
        return current.FullName;
    }
}
