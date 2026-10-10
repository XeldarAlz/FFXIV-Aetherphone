using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SeasonalThemeTests
{
    private static readonly DateTime HalloweenNight = new(2026, 10, 31, 21, 0, 0);
    private static readonly DateTime Midsummer = new(2026, 7, 1, 12, 0, 0);

    [Theory]
    [InlineData(2026, 10, 17)]
    [InlineData(2026, 10, 31)]
    [InlineData(2026, 11, 1)]
    [InlineData(2027, 10, 20)]
    public void DatesInsideTheWindowAreHalloween(int year, int month, int day)
    {
        Assert.True(SeasonalTheme.IsHalloweenDate(new DateTime(year, month, day, 23, 59, 0)));
    }

    [Theory]
    [InlineData(2026, 10, 16)]
    [InlineData(2026, 11, 2)]
    [InlineData(2026, 1, 1)]
    [InlineData(2026, 12, 25)]
    public void DatesOutsideTheWindowAreNotHalloween(int year, int month, int day)
    {
        Assert.False(SeasonalTheme.IsHalloweenDate(new DateTime(year, month, day, 0, 0, 0)));
    }

    [Fact]
    public void HalloweenShowsInsideTheWindowWhenDecorationsAreOn()
    {
        SeasonalTheme.Update(new Configuration(), HalloweenNight);

        Assert.True(SeasonalTheme.Halloween);
    }

    [Fact]
    public void TurningDecorationsOffHidesHalloween()
    {
        SeasonalTheme.Update(new Configuration { SeasonalDecorations = false }, HalloweenNight);

        Assert.False(SeasonalTheme.Halloween);
    }

    [Fact]
    public void HalloweenStaysHiddenOutsideTheWindow()
    {
        SeasonalTheme.Update(new Configuration(), Midsummer);

        Assert.False(SeasonalTheme.Halloween);
    }

    [Fact]
    public void PreviewOnlyForcesHalloweenOnPrereleaseBuilds()
    {
        SeasonalTheme.Update(new Configuration { PreviewHalloween = true }, Midsummer);

        Assert.Equal(AepConstants.IsPrerelease, SeasonalTheme.Halloween);
    }

    [Fact]
    public void OnlyTheThirtyFirstIsHalloweenNight()
    {
        SeasonalTheme.Update(new Configuration(), HalloweenNight);

        Assert.True(SeasonalTheme.IsHalloweenNight(new DateTime(2026, 10, 31)));
        Assert.False(SeasonalTheme.IsHalloweenNight(new DateTime(2026, 10, 30)));
        Assert.False(SeasonalTheme.IsHalloweenNight(new DateTime(2026, 11, 1)));
    }

    [Fact]
    public void PreviewRespectsTheDecorationsToggle()
    {
        SeasonalTheme.Update(new Configuration { PreviewHalloween = true, SeasonalDecorations = false }, Midsummer);

        Assert.False(SeasonalTheme.Halloween);
    }
}
