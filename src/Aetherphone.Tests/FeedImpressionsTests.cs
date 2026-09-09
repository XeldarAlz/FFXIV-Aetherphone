using Aetherphone.Core.Social;
using Xunit;

namespace Aetherphone.Tests;

public sealed class FeedImpressionsTests
{
    private const float ViewportTop = 0f;

    private const float ViewportBottom = 1000f;

    private const float CappedFrame = 0.25f;

    [Fact]
    public void FourCappedFramesInViewAreOneSecond()
    {
        var impressions = new FeedImpressions();

        Assert.False(Advance(impressions, 3, 0f, 100f));
        Assert.True(Advance(impressions, 1, 0f, 100f));
    }

    [Fact]
    public void APostThatCountedDoesNotCountAgain()
    {
        var impressions = new FeedImpressions();

        Assert.True(Advance(impressions, 4, 0f, 100f));
        Assert.False(Advance(impressions, 8, 0f, 100f));
    }

    [Fact]
    public void ACardShowingLessThanHalfNeverCounts()
    {
        var impressions = new FeedImpressions();

        Assert.False(Advance(impressions, 20, 900f, 1300f));
    }

    [Fact]
    public void DwellDoesNotSurviveTheCardLeavingTheViewport()
    {
        var impressions = new FeedImpressions();

        Assert.False(Advance(impressions, 3, 0f, 100f));
        Assert.False(Advance(impressions, 1, 990f, 1090f));
        Assert.False(Advance(impressions, 3, 0f, 100f));
        Assert.True(Advance(impressions, 1, 0f, 100f));
    }

    [Fact]
    public void ACardTallerThanTheViewportCountsWhenItFillsIt()
    {
        var impressions = new FeedImpressions();

        Assert.True(Advance(impressions, 4, -500f, 2500f));
    }

    [Fact]
    public void AStalledFrameBanksNoMoreThanTheCap()
    {
        var impressions = new FeedImpressions();

        Assert.False(Advance(impressions, 3, 0f, 100f, 30f));
        Assert.True(Advance(impressions, 1, 0f, 100f, 30f));
    }

    [Fact]
    public void ResetLetsAPostCountAgain()
    {
        var impressions = new FeedImpressions();

        Assert.True(Advance(impressions, 4, 0f, 100f));
        impressions.Reset();
        Assert.True(Advance(impressions, 4, 0f, 100f));
    }

    private static bool Advance(FeedImpressions impressions, int frames, float rowTop, float rowBottom,
        float frameSeconds = CappedFrame)
    {
        var counted = false;
        for (var frame = 0; frame < frames; frame++)
        {
            impressions.BeginFrame(ViewportTop, ViewportBottom, frameSeconds);
            counted |= impressions.Observe("post", rowTop, rowBottom);
        }

        return counted;
    }
}
