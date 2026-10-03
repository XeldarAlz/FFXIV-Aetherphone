using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Shell;
using Xunit;

namespace Aetherphone.Tests;

public sealed class LiveBackdropPlanTests
{
    private static readonly Rect Viewport = new(Vector2.Zero, new Vector2(2560f, 1440f));

    [Fact]
    public void LadderHalvesThePhoneRegionAtEveryLevelAndRoundsUp()
    {
        var region = new Vector2(361f, 781f);
        Assert.Equal((181, 391), LiveBackdropPlan.LevelSize(region, 0));
        Assert.Equal((91, 196), LiveBackdropPlan.LevelSize(region, 1));
        Assert.Equal((46, 98), LiveBackdropPlan.LevelSize(region, 2));
        Assert.Equal((23, 49), LiveBackdropPlan.LevelSize(region, 3));
    }

    [Fact]
    public void LadderNeverProducesAnEmptyTexture()
    {
        Assert.Equal((1, 1), LiveBackdropPlan.LevelSize(new Vector2(1f, 1f), LiveBackdropPlan.Depth - 1));
        Assert.Equal((1, 1), LiveBackdropPlan.LevelSize(Vector2.Zero, 0));
        Assert.Equal((1, 1), LiveBackdropPlan.LevelSize(new Vector2(-40f, -10f), 0));
    }

    [Fact]
    public void SmoothLevelMatchesTheQuarterLevel()
    {
        var region = new Vector2(720f, 1560f);
        Assert.Equal(LiveBackdropPlan.LevelSize(region, 1), LiveBackdropPlan.SmoothSize(region));
        Assert.Equal((180, 390), LiveBackdropPlan.SmoothSize(region));
    }

    [Fact]
    public void WindowInsideTheViewportMapsToItsUvAndFillsTheWholeTarget()
    {
        var screen = new Rect(new Vector2(640f, 360f), new Vector2(1280f, 1080f));
        var window = LiveBackdropPlan.Window(screen, Viewport);
        Assert.True(window.Visible);
        Assert.Equal(new Vector2(0.25f, 0.25f), window.Uv0);
        Assert.Equal(new Vector2(0.5f, 0.75f), window.Uv1);
        Assert.Equal(Vector2.Zero, window.DestinationMin);
        Assert.Equal(Vector2.One, window.DestinationMax);
    }

    [Fact]
    public void WindowHangingOffTheLeftEdgeClampsUvAndShrinksTheDestination()
    {
        var screen = new Rect(new Vector2(-200f, 100f), new Vector2(200f, 900f));
        var window = LiveBackdropPlan.Window(screen, Viewport);
        Assert.True(window.Visible);
        Assert.Equal(0f, window.Uv0.X);
        Assert.Equal(200f / 2560f, window.Uv1.X, 5);
        Assert.Equal(0.5f, window.DestinationMin.X, 5);
        Assert.Equal(1f, window.DestinationMax.X, 5);
        Assert.Equal(0f, window.DestinationMin.Y, 5);
        Assert.Equal(1f, window.DestinationMax.Y, 5);
    }

    [Fact]
    public void WindowHangingOffTheBottomRightCornerKeepsTheVisibleFractionUnstretched()
    {
        var screen = new Rect(new Vector2(2400f, 1200f), new Vector2(2800f, 2000f));
        var window = LiveBackdropPlan.Window(screen, Viewport);
        Assert.True(window.Visible);
        Assert.Equal(Vector2.One, window.Uv1);
        Assert.Equal(Vector2.Zero, window.DestinationMin);
        Assert.Equal(0.4f, window.DestinationMax.X, 5);
        Assert.Equal(0.3f, window.DestinationMax.Y, 5);
    }

    [Fact]
    public void WindowEntirelyOutsideTheViewportIsNotVisible()
    {
        var screen = new Rect(new Vector2(3000f, 100f), new Vector2(3400f, 900f));
        Assert.False(LiveBackdropPlan.Window(screen, Viewport).Visible);
    }

    [Fact]
    public void WindowWithNoViewportOrNoScreenIsNotVisible()
    {
        var screen = new Rect(new Vector2(100f, 100f), new Vector2(500f, 900f));
        Assert.False(LiveBackdropPlan.Window(screen, new Rect(Vector2.Zero, Vector2.Zero)).Visible);
        Assert.False(LiveBackdropPlan.Window(new Rect(screen.Min, screen.Min), Viewport).Visible);
    }

    [Fact]
    public void ViewportOffsetIsSubtractedBeforeMapping()
    {
        var offsetViewport = new Rect(new Vector2(100f, 50f), new Vector2(1100f, 1050f));
        var screen = new Rect(new Vector2(600f, 550f), new Vector2(1100f, 1050f));
        var window = LiveBackdropPlan.Window(screen, offsetViewport);
        Assert.Equal(new Vector2(0.5f, 0.5f), window.Uv0);
        Assert.Equal(Vector2.One, window.Uv1);
    }

    [Fact]
    public void CompositeCaptureIsDampedAndWorldCaptureIsNot()
    {
        Assert.Equal(0.7f, LiveBackdropPlan.FeedbackBlend(LiveGlassSource.Composite));
        Assert.Equal(1f, LiveBackdropPlan.FeedbackBlend(LiveGlassSource.World));
    }

    [Fact]
    public void PadGrowsEverySideByTheSameAmount()
    {
        var rect = new Rect(new Vector2(100f, 200f), new Vector2(300f, 500f));
        var padded = LiveBackdropPlan.Pad(rect, 24f);
        Assert.Equal(new Vector2(76f, 176f), padded.Min);
        Assert.Equal(new Vector2(324f, 524f), padded.Max);
        Assert.Equal(rect.Center, padded.Center);
    }

    [Fact]
    public void UnionSpansBothRects()
    {
        var screen = new Rect(new Vector2(120f, 80f), new Vector2(480f, 860f));
        var glass = new Rect(new Vector2(111f, 71f), new Vector2(489f, 869f));
        var union = LiveBackdropPlan.Union(screen, glass);
        Assert.Equal(glass, union);
        var elsewhere = new Rect(new Vector2(900f, 20f), new Vector2(980f, 180f));
        var wide = LiveBackdropPlan.Union(screen, elsewhere);
        Assert.Equal(new Vector2(120f, 20f), wide.Min);
        Assert.Equal(new Vector2(980f, 860f), wide.Max);
    }

    [Fact]
    public void CoversRequiresTheWholeRectInsideTheRegion()
    {
        var region = new Rect(new Vector2(0f, 0f), new Vector2(100f, 200f));
        Assert.True(LiveBackdropPlan.Covers(region, new Rect(new Vector2(10f, 10f), new Vector2(90f, 190f))));
        Assert.True(LiveBackdropPlan.Covers(region, region));
        Assert.False(LiveBackdropPlan.Covers(region, new Rect(new Vector2(-1f, 10f), new Vector2(90f, 190f))));
        Assert.False(LiveBackdropPlan.Covers(region, new Rect(new Vector2(10f, 10f), new Vector2(90f, 201f))));
    }

    [Fact]
    public void PaddedRequestStillCoversTheSurfaceAndItsReachAfterASmallMove()
    {
        const float scale = 1.25f;
        var body = new Rect(new Vector2(400f, 300f), new Vector2(502f, 495f));
        var region = LiveBackdropPlan.Pad(body, LiveBackdropPlan.RegionPadding * scale);
        var drift = (LiveBackdropPlan.RegionPadding - LiveBackdropPlan.SampleReach) * scale;
        var moved = body.Translate(new Vector2(drift, -drift));
        var sampled = LiveBackdropPlan.Pad(moved, LiveBackdropPlan.SampleReach * scale);
        Assert.True(LiveBackdropPlan.Covers(region, sampled));
    }

    [Fact]
    public void SurfaceMovedPastThePaddingIsNotCovered()
    {
        const float scale = 1f;
        var body = new Rect(new Vector2(400f, 300f), new Vector2(502f, 495f));
        var region = LiveBackdropPlan.Pad(body, LiveBackdropPlan.RegionPadding * scale);
        var moved = body.Translate(new Vector2(0f, LiveBackdropPlan.RegionPadding * scale + 1f));
        Assert.False(LiveBackdropPlan.Covers(region, moved));
    }

    [Fact]
    public void RegionPaddingLeavesRoomBeyondTheSampleReach()
    {
        Assert.True(LiveBackdropPlan.RegionPadding > LiveBackdropPlan.SampleReach);
    }
}
