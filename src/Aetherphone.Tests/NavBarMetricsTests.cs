using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class NavBarMetricsTests
{
    private const float Tolerance = 1e-4f;

    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void CollapseFollowsScrollOverSixtyUnits(float scale)
    {
        Assert.Equal(0f, NavBarMetrics.Progress(0f, scale), Tolerance);
        Assert.Equal(0.5f, NavBarMetrics.Progress(30f * scale, scale), Tolerance);
        Assert.Equal(1f, NavBarMetrics.Progress(60f * scale, scale), Tolerance);
        Assert.Equal(1f, NavBarMetrics.Progress(400f * scale, scale), Tolerance);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(2f)]
    public void GlassFadesInOverTheFirstTwentyUnits(float scale)
    {
        Assert.Equal(0f, NavBarMetrics.GlassOpacity(0f, scale), Tolerance);
        Assert.Equal(0.5f, NavBarMetrics.GlassOpacity(10f * scale, scale), Tolerance);
        Assert.Equal(1f, NavBarMetrics.GlassOpacity(20f * scale, scale), Tolerance);
        Assert.Equal(1f, NavBarMetrics.GlassOpacity(60f * scale, scale), Tolerance);
    }

    [Fact]
    public void OverscrollNeverCollapsesOrFrosts()
    {
        Assert.Equal(0f, NavBarMetrics.Progress(-40f, 1f), Tolerance);
        Assert.Equal(0f, NavBarMetrics.GlassOpacity(-40f, 1f), Tolerance);
    }

    [Fact]
    public void ProgressIsMonotonic()
    {
        var previous = 0f;
        for (var scrollY = 0f; scrollY <= 80f; scrollY += 1f)
        {
            var progress = NavBarMetrics.Progress(scrollY, 1f);
            Assert.True(progress >= previous, $"Progress fell from {previous} to {progress} at scroll {scrollY}.");
            previous = progress;
        }
    }

    [Fact]
    public void TitlesCrossFadeAndStayComplementary()
    {
        for (var step = 0; step <= 10; step++)
        {
            var progress = step / 10f;
            var large = NavBarMetrics.LargeTitleAlpha(progress);
            var inline = NavBarMetrics.InlineTitleAlpha(progress);
            Assert.InRange(large, 0f, 1f);
            Assert.InRange(inline, 0f, 1f);
            Assert.Equal(1f, large + inline, Tolerance);
        }

        Assert.Equal(1f, NavBarMetrics.LargeTitleAlpha(0f), Tolerance);
        Assert.Equal(0f, NavBarMetrics.LargeTitleAlpha(1f), Tolerance);
        Assert.Equal(1f, NavBarMetrics.InlineTitleAlpha(1f), Tolerance);
    }

    [Fact]
    public void ExpandedBarIsTheInlineBarPlusTheTitleBand()
    {
        Assert.Equal(82f, NavBarMetrics.ExpandedHeight, Tolerance);
        Assert.Equal(44f, NavBarMetrics.InlineHeight, Tolerance);
        Assert.Equal(NavBarMetrics.ExpandedHeight, NavBarMetrics.InlineHeight + NavBarMetrics.BandHeight, Tolerance);
    }

    [Fact]
    public void ButtonsAreSpacedByDiameterPlusGapAndStayInsideTheGlassInset()
    {
        const float contentRight = 360f;
        var first = NavBarMetrics.ButtonCenterX(contentRight, 0, 2, 1f);
        var last = NavBarMetrics.ButtonCenterX(contentRight, 1, 2, 1f);
        Assert.Equal(Metrics.Size.GlassButton + NavBarMetrics.ButtonGap, last - first, Tolerance);
        Assert.True(last + Metrics.Size.GlassButton * 0.5f <= contentRight - Metrics.Space.GlassInset,
            "The right-most button must not cross the glass inset.");
        Assert.Equal(last, NavBarMetrics.ButtonCenterX(contentRight, 0, 1, 1f), Tolerance);
    }

    [Fact]
    public void ButtonsWidthCoversEveryButtonAndTheGapsBetweenThem()
    {
        Assert.Equal(0f, NavBarMetrics.ButtonsWidth(0, 1f), Tolerance);
        Assert.Equal(Metrics.Size.GlassButton, NavBarMetrics.ButtonsWidth(1, 1f), Tolerance);
        Assert.Equal(Metrics.Size.GlassButton * 2f + NavBarMetrics.ButtonGap, NavBarMetrics.ButtonsWidth(2, 1f),
            Tolerance);
        Assert.Equal(NavBarMetrics.ButtonsWidth(2, 1f) * 2f, NavBarMetrics.ButtonsWidth(2, 2f), Tolerance);
    }
}
