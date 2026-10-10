using Aetherphone.Core.Notifications;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SeasonalSoundTests
{
    private const string SeasonalPrefix = "Halloween";

    [Fact]
    public void EveryHalloweenSoundIsSeasonal()
    {
        foreach (var sound in Enum.GetValues<UiSound>())
        {
            var named = sound.ToString().StartsWith(SeasonalPrefix, StringComparison.Ordinal);
            Assert.Equal(named, UiSoundCatalog.IsSeasonal(sound));
        }
    }

    [Fact]
    public void PlainSoundsAreNeverSeasonal()
    {
        foreach (var sound in Enum.GetValues<UiSound>())
        {
            if (UiSoundCatalog.PlainFor(sound) is { } plain)
            {
                Assert.True(UiSoundCatalog.IsSeasonal(sound));
                Assert.False(UiSoundCatalog.IsSeasonal(plain));
            }
        }
    }

    [Theory]
    [InlineData(nameof(UiSound.HalloweenHoot), nameof(UiSound.Refresh))]
    [InlineData(nameof(UiSound.HalloweenOrgan), nameof(UiSound.Refresh))]
    [InlineData(nameof(UiSound.HalloweenIgnite), nameof(UiSound.Refresh))]
    [InlineData(nameof(UiSound.HalloweenWhisper), nameof(UiSound.MessageSent))]
    public void RefreshAndSendSoundsFallBackToTheirUsualSound(string seasonal, string plain)
    {
        Assert.Equal(Enum.Parse<UiSound>(plain), UiSoundCatalog.PlainFor(Enum.Parse<UiSound>(seasonal)));
    }

    [Fact]
    public void SeasonalOnlySoundsFallBackToSilence()
    {
        Assert.Null(UiSoundCatalog.PlainFor(UiSound.HalloweenSparkle));
        Assert.Null(UiSoundCatalog.PlainFor(UiSound.HalloweenRise));
    }
}
