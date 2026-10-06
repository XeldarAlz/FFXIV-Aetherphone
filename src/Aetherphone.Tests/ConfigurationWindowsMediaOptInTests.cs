using Newtonsoft.Json;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ConfigurationWindowsMediaOptInTests
{
    [Fact]
    public void WindowsMediaIsOffAndTheWelcomeIsPendingForAFreshConfiguration()
    {
        var configuration = JsonConvert.DeserializeObject<Configuration>("{}")!;

        Assert.False(configuration.ShowWindowsMedia);
        Assert.False(configuration.MusicWelcomeShown);
    }

    [Fact]
    public void OptInMigrationTurnsOffWindowsMediaSavedOnByTheOldDefault()
    {
        const string json =
            """{"ShowWindowsMedia":true,"PublishToWindowsMedia":true,"WindowsMediaSource":"Spotify.exe"}""";
        var configuration = JsonConvert.DeserializeObject<Configuration>(json)!;

        var migrated = configuration.ApplyWindowsMediaOptIn();

        Assert.True(migrated);
        Assert.False(configuration.ShowWindowsMedia);
        Assert.True(configuration.WindowsMediaOptInApplied);
        Assert.True(configuration.PublishToWindowsMedia);
        Assert.Equal("Spotify.exe", configuration.WindowsMediaSource);
    }

    [Fact]
    public void OptInMigrationKeepsAChoiceMadeAfterItRan()
    {
        const string json = """{"ShowWindowsMedia":true,"WindowsMediaOptInApplied":true}""";
        var configuration = JsonConvert.DeserializeObject<Configuration>(json)!;

        var migrated = configuration.ApplyWindowsMediaOptIn();

        Assert.False(migrated);
        Assert.True(configuration.ShowWindowsMedia);
    }
}
