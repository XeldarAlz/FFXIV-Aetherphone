using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public class ScrollThumbTests
{
    private const float ViewTop = 100f;
    private const float ViewHeight = 600f;
    private const float Inset = ScrollThumb.InsetUnits;

    private static ScrollThumb Thumb(float scrollY, float maxY) =>
        ScrollThumb.Measure(ViewTop, ViewHeight, scrollY, maxY, 1f);

    [Fact]
    public void Height_ShrinksAsContentGrows()
    {
        var shorter = Thumb(0f, 300f);
        var longer = Thumb(0f, 1200f);
        Assert.True(longer.Height < shorter.Height);
        Assert.Equal(200.0, longer.Height, 3);
    }

    [Fact]
    public void Height_RespectsTheMinimum()
    {
        var thumb = Thumb(0f, 1000000f);
        Assert.Equal(ScrollThumb.MinHeightUnits, thumb.Height, 3);
    }

    [Fact]
    public void Height_MinimumFollowsScale()
    {
        var thumb = ScrollThumb.Measure(ViewTop, ViewHeight, 0f, 1000000f, 2f);
        Assert.Equal(ScrollThumb.MinHeightUnits * 2f, thumb.Height, 3);
    }

    [Fact]
    public void Top_SitsAtTheInsetWhenScrolledToTop()
    {
        var thumb = Thumb(0f, 1200f);
        Assert.Equal(ViewTop + Inset, thumb.Top, 3);
    }

    [Fact]
    public void Top_SitsAtTheEndOfTravelWhenScrolledToBottom()
    {
        var thumb = Thumb(1200f, 1200f);
        Assert.Equal(ViewTop + Inset + thumb.Travel, thumb.Top, 3);
        Assert.Equal(ViewTop + ViewHeight - Inset, thumb.Top + thumb.Height, 3);
    }

    [Fact]
    public void ScrollDelta_FollowsThePointerDirection()
    {
        var thumb = Thumb(0f, 1200f);
        Assert.True(thumb.ScrollDelta(10f) > 0f);
        Assert.True(thumb.ScrollDelta(-10f) < 0f);
    }

    [Fact]
    public void ScrollDelta_FullTravelCoversTheWholeRange()
    {
        var thumb = Thumb(0f, 1200f);
        Assert.Equal(1200.0, thumb.ScrollDelta(thumb.Travel), 3);
    }

    [Fact]
    public void ScrollDelta_OutpacesThePointerOnLongContent()
    {
        var thumb = Thumb(0f, 1200f);
        Assert.True(thumb.ScrollDelta(1f) > 1f);
    }

    [Fact]
    public void ScrollDelta_ZeroTravelReturnsZero()
    {
        var thumb = ScrollThumb.Measure(ViewTop, 20f, 0f, 1200f, 1f);
        Assert.Equal(0.0, thumb.Travel, 3);
        Assert.Equal(0.0, thumb.ScrollDelta(50f), 3);
    }
}
