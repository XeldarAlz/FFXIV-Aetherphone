using Aetherphone.Apps.Skywatcher;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SkywatcherCardFadeTests
{
    private const float Edge = 100f;
    private const float Height = 18f;
    private const float Ramp = 8f;

    [Fact]
    public void CardsAreInvisibleAtTheEdgeOnceTheyHaveSlidUnderIt()
    {
        var fade = CardFade.Under(Edge, Edge - 40f, Height, Ramp);
        Assert.True(fade.Active);
        Assert.Equal(0f, fade.Visibility(Edge), 4);
        Assert.Equal(0f, fade.Visibility(Edge - 5f), 4);
    }

    [Fact]
    public void CardsAreFullyVisibleFromTheBottomOfTheBand()
    {
        var fade = CardFade.Under(Edge, Edge - 40f, Height, Ramp);
        Assert.Equal(1f, fade.Visibility(Edge + Height), 4);
        Assert.Equal(1f, fade.Visibility(Edge + Height + 50f), 4);
    }

    [Fact]
    public void VisibilityRisesSmoothlyWithNoStepAcrossTheBand()
    {
        var fade = CardFade.Under(Edge, Edge - 40f, Height, Ramp);
        var previous = fade.Visibility(Edge - 1f);
        for (var step = -10; step <= 300; step++)
        {
            var current = fade.Visibility(Edge + step * 0.1f);
            Assert.True(current >= previous - 0.0001f);
            Assert.True(current - previous <= 0.01f);
            previous = current;
        }
    }

    [Fact]
    public void TheBandEaseOutHasNoKinkAtEitherEnd()
    {
        var fade = CardFade.Under(Edge, Edge - 40f, Height, Ramp);
        Assert.True(fade.Visibility(Edge + 0.2f) < 0.001f);
        Assert.True(fade.Visibility(Edge + Height - 0.2f) > 0.999f);
    }

    [Fact]
    public void NothingFadesWhileTheCardsStillSitBelowTheEdge()
    {
        var fade = CardFade.Under(Edge, Edge + 4f, Height, Ramp);
        Assert.False(fade.Active);
        Assert.Equal(1f, fade.Visibility(Edge), 4);
        Assert.Equal(1f, fade.Visibility(Edge + 6f), 4);
    }

    [Fact]
    public void TheFadeStrengthensAsTheFirstCardSlidesUnder()
    {
        var half = CardFade.Under(Edge, Edge - Ramp * 0.5f, Height, Ramp);
        Assert.Equal(0.5f, half.Strength, 4);
        Assert.Equal(0.5f, half.Visibility(Edge), 4);
        Assert.Equal(1f, CardFade.Under(Edge, Edge - Ramp, Height, Ramp).Strength, 4);
    }

    [Fact]
    public void PanelSlicesShareWholePixelBoundariesWithNoGaps()
    {
        const float top = 100f;
        const float bottom = 121f;
        const int slices = 14;
        Assert.Equal(top, CardFade.SliceBoundary(top, bottom, 0, slices));
        Assert.Equal(bottom, CardFade.SliceBoundary(top, bottom, slices, slices));
        var previous = top;
        for (var boundaryIndex = 1; boundaryIndex <= slices; boundaryIndex++)
        {
            var boundary = CardFade.SliceBoundary(top, bottom, boundaryIndex, slices);
            Assert.Equal(MathF.Round(boundary), boundary);
            Assert.True(boundary >= previous);
            previous = boundary;
        }
    }
}
