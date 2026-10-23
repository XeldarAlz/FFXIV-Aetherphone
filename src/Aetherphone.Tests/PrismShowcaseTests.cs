using Aetherphone.Apps.Prism;
using Aetherphone.Core.Apps;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PrismShowcaseTests
{
    private const long ArrivedAt = 1_800_000_000;
    private const long EndsAt = ArrivedAt + PrismShowcase.Seconds;

    [Fact]
    public void AnUnopenedTileSparklesThroughTheWindow()
    {
        Assert.Equal(HomeTileEffect.Spectrum, PrismShowcase.Effect(EndsAt, false, ArrivedAt));
        Assert.Equal(HomeTileEffect.Spectrum, PrismShowcase.Effect(EndsAt, false, EndsAt - 1));
    }

    [Fact]
    public void OpeningPrismSettlesTheTileToAGlow()
    {
        Assert.Equal(HomeTileEffect.Glow, PrismShowcase.Effect(EndsAt, true, ArrivedAt));
    }

    [Fact]
    public void TheShowcaseEndsAfterThreeDays()
    {
        Assert.Equal(HomeTileEffect.None, PrismShowcase.Effect(EndsAt, false, EndsAt));
        Assert.Equal(HomeTileEffect.None, PrismShowcase.Effect(EndsAt, true, EndsAt + 60));
    }
}
