using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Shell;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SwitcherGeometryTests
{
    private const float Tolerance = 1e-3f;
    private const float Scale = 1.5f;
    private static readonly Rect Screen = new(new Vector2(100f, 50f), new Vector2(460f, 830f));

    [Fact]
    public void LayoutKeepsTheScreenAspectAtHalfHeight()
    {
        var layout = SwitcherGeometry.Layout(Screen, Scale, 4);
        Assert.Equal(Screen.Height * SwitcherGeometry.CardHeightFraction, layout.CardHeight, Tolerance);
        Assert.Equal(layout.CardHeight * (Screen.Width / Screen.Height), layout.CardWidth, Tolerance);
        Assert.Equal(layout.CardWidth + SwitcherGeometry.CardGapUnits * Scale, layout.Pitch, Tolerance);
        Assert.Equal(Screen.Min.Y + Screen.Height * SwitcherGeometry.CardCenterFraction, layout.CenterY, Tolerance);
        Assert.Equal(3f, layout.MaxScroll, Tolerance);
    }

    [Fact]
    public void LayoutWithOneCardNeverScrolls()
    {
        Assert.Equal(0f, SwitcherGeometry.Layout(Screen, Scale, 1).MaxScroll, Tolerance);
        Assert.Equal(0f, SwitcherGeometry.Layout(Screen, Scale, 0).MaxScroll, Tolerance);
    }

    [Fact]
    public void FocusedSlotRestsAtTheScreenCenterAndNeighborsSitOnePitchApart()
    {
        var layout = SwitcherGeometry.Layout(Screen, Scale, 3);
        var focused = SwitcherGeometry.CardRest(in layout, 1f, 1f);
        var next = SwitcherGeometry.CardRest(in layout, 2f, 1f);
        Assert.Equal(Screen.Center.X, focused.Center.X, Tolerance);
        Assert.Equal(layout.CenterY, focused.Center.Y, Tolerance);
        Assert.Equal(layout.CardWidth, focused.Width, Tolerance);
        Assert.Equal(layout.CardHeight, focused.Height, Tolerance);
        Assert.Equal(layout.Pitch, next.Center.X - focused.Center.X, Tolerance);
        Assert.Equal(focused.Center.Y, next.Center.Y, Tolerance);
    }

    [Fact]
    public void ScalingKeepsTheCenterFixed()
    {
        var rect = new Rect(new Vector2(10f, 20f), new Vector2(110f, 220f));
        var scaled = SwitcherGeometry.Scaled(rect, 0.98f);
        Assert.Equal(rect.Center.X, scaled.Center.X, Tolerance);
        Assert.Equal(rect.Center.Y, scaled.Center.Y, Tolerance);
        Assert.Equal(rect.Width * 0.98f, scaled.Width, Tolerance);
        Assert.Equal(rect.Height * 0.98f, scaled.Height, Tolerance);
    }

    [Theory]
    [InlineData(-0.4f, 3, 0)]
    [InlineData(0.49f, 3, 0)]
    [InlineData(0.51f, 3, 1)]
    [InlineData(1.6f, 3, 2)]
    [InlineData(7f, 3, 2)]
    [InlineData(2f, 0, 0)]
    public void SnapRoundsToTheNearestSlotInsideTheDeck(float projected, int cardCount, int expected)
    {
        Assert.Equal(expected, SwitcherGeometry.SnapSlot(projected, cardCount));
    }

    [Fact]
    public void FlingProjectionCarriesTheScrollPastTheTravel()
    {
        const float pitch = 200f;
        var dragOnly = SwitcherGeometry.ProjectedScroll(1f, -120f, 0f, pitch);
        var flung = SwitcherGeometry.ProjectedScroll(1f, -120f, -1500f, pitch);
        Assert.Equal(1.6f, dragOnly, Tolerance);
        Assert.True(flung > dragOnly);
        Assert.Equal(1f, SwitcherGeometry.ProjectedScroll(1f, -120f, -1500f, 0f), Tolerance);
    }

    [Fact]
    public void RubberBandResistsOnlyOutsideTheDeck()
    {
        Assert.Equal(1.25f, SwitcherGeometry.RubberBand(1.25f, 3f), Tolerance);
        Assert.Equal(-0.5f * SwitcherGeometry.OverscrollResistance, SwitcherGeometry.RubberBand(-0.5f, 3f),
            Tolerance);
        Assert.Equal(3f + 0.5f * SwitcherGeometry.OverscrollResistance, SwitcherGeometry.RubberBand(3.5f, 3f),
            Tolerance);
    }

    [Fact]
    public void CardRoundingStaysConcentricWithTheScreen()
    {
        var layout = SwitcherGeometry.Layout(Screen, Scale, 2);
        var rounding = SwitcherGeometry.CardRounding(layout.CardWidth, Screen.Width, 40f);
        Assert.Equal(40f * SwitcherGeometry.CardHeightFraction, rounding, Tolerance);
    }

    [Fact]
    public void ParallaxQuadCoversTheScreenAcrossTheWholeScrollRange()
    {
        var layout = SwitcherGeometry.Layout(Screen, Scale, 4);
        var reach = SwitcherGeometry.ParallaxReachSlots(4);
        for (var step = -8; step <= 40; step++)
        {
            var scroll = step * 0.125f;
            var quad = SwitcherGeometry.ParallaxQuad(Screen, scroll, layout.Pitch, reach);
            Assert.True(quad.Min.X <= Screen.Min.X + Tolerance, $"left edge uncovered at scroll {scroll}");
            Assert.True(quad.Max.X >= Screen.Max.X - Tolerance, $"right edge uncovered at scroll {scroll}");
            Assert.True(quad.Min.Y <= Screen.Min.Y + Tolerance, $"top edge uncovered at scroll {scroll}");
            Assert.True(quad.Max.Y >= Screen.Max.Y - Tolerance, $"bottom edge uncovered at scroll {scroll}");
        }
    }

    [Fact]
    public void ParallaxQuadMovesAFractionOfTheCardMotionAndKeepsTheAspect()
    {
        var layout = SwitcherGeometry.Layout(Screen, Scale, 4);
        var reach = SwitcherGeometry.ParallaxReachSlots(4);
        var first = SwitcherGeometry.ParallaxQuad(Screen, 0f, layout.Pitch, reach);
        var second = SwitcherGeometry.ParallaxQuad(Screen, 1f, layout.Pitch, reach);
        Assert.Equal(-layout.Pitch * SwitcherGeometry.ParallaxFactor, second.Min.X - first.Min.X, Tolerance);
        Assert.Equal(first.Width, second.Width, Tolerance);
        Assert.Equal(Screen.Width / Screen.Height, first.Width / first.Height, Tolerance);
        Assert.Equal(Screen.Center.Y, first.Center.Y, Tolerance);
    }

    [Fact]
    public void ParallaxQuadStopsFollowingBeyondTheMargin()
    {
        var layout = SwitcherGeometry.Layout(Screen, Scale, 2);
        var reach = SwitcherGeometry.ParallaxReachSlots(2);
        var atMargin = SwitcherGeometry.ParallaxQuad(Screen, -SwitcherGeometry.ParallaxMarginSlots, layout.Pitch,
            reach);
        var farBeyond = SwitcherGeometry.ParallaxQuad(Screen, -10f, layout.Pitch, reach);
        Assert.Equal(atMargin.Min.X, farBeyond.Min.X, Tolerance);
        Assert.Equal(Screen.Min.X, atMargin.Min.X, Tolerance);
    }

    [Theory]
    [InlineData(0.31f, 0f, true)]
    [InlineData(0.29f, 0f, false)]
    [InlineData(0.05f, -1000f, true)]
    [InlineData(0.05f, 1000f, false)]
    public void ReleaseClosesPastThirtyPercentOrOnAnUpwardFling(float liftFraction, float velocityY, bool expected)
    {
        const float cardHeight = 400f;
        const float fling = 900f;
        Assert.Equal(expected,
            SwitcherGeometry.ClosesOnRelease(cardHeight * liftFraction, cardHeight, velocityY, fling));
    }
}
