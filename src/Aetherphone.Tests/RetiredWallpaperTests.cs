using Aetherphone.Core.Home;
using Aetherphone.Core.Wallpapers;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RetiredWallpaperTests
{
    [Fact]
    public void RetiredIdsMoveToTheirReplacementsEverywhere()
    {
        var configuration = new Configuration
        {
            LightWallpaperId = "DuskLight",
            DarkWallpaperId = "ShadowDark",
            Looks = new List<HomeLook>
            {
                new() { Id = Guid.NewGuid(), LightWallpaperId = "HaloLight", DarkWallpaperId = "SkyDark" },
            },
        };

        Assert.True(configuration.ReplaceRetiredWallpapers());
        Assert.Equal("BloomLight", configuration.LightWallpaperId);
        Assert.Equal("FrostDark", configuration.DarkWallpaperId);
        Assert.Equal("CrystalLight", configuration.Looks[0].LightWallpaperId);
        Assert.Equal("CurrentDark", configuration.Looks[0].DarkWallpaperId);
    }

    [Fact]
    public void CurrentAndCustomIdsAreLeftAlone()
    {
        var configuration = new Configuration
        {
            LightWallpaperId = "EmberLight",
            DarkWallpaperId = "custom-0123456789abcdef",
            Looks = new List<HomeLook>
            {
                new() { Id = Guid.NewGuid(), LightWallpaperId = "PrismLight", DarkWallpaperId = "PrismDark" },
            },
        };

        Assert.False(configuration.ReplaceRetiredWallpapers());
        Assert.Equal("EmberLight", configuration.LightWallpaperId);
        Assert.Equal("custom-0123456789abcdef", configuration.DarkWallpaperId);
        Assert.Equal("PrismLight", configuration.Looks[0].LightWallpaperId);
    }

    [Fact]
    public void DefaultsPointAtBundledFiles()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Wallpapers");

        Assert.True(File.Exists(Path.Combine(directory, BuiltInWallpapers.DefaultLightId + ".jpg")));
        Assert.True(File.Exists(Path.Combine(directory, BuiltInWallpapers.DefaultDarkId + ".jpg")));
    }
}
