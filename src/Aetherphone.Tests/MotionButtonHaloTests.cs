using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class MotionButtonHaloTests
{
    private const float Tolerance = 1e-4f;
    private static readonly Rect Window = new(new Vector2(0f, 0f), new Vector2(400f, 600f));
    private static readonly Rect SurfaceInnerClip = new(new Vector2(8f, 0f), new Vector2(392f, 600f));

    [Fact]
    public void ReachCoversEveryLayerAtScale()
    {
        Assert.Equal(15.4f, MotionButton.HaloReach(1f), Tolerance);
        Assert.Equal(30.8f, MotionButton.HaloReach(2f), Tolerance);
    }

    [Theory]
    [InlineData(0.5f, 280f)]
    [InlineData(1f, 320f)]
    [InlineData(2.23f, 292f)]
    [InlineData(3f, 640f)]
    public void FittedButtonContainsHoveredHaloInsideTheRow(float scale, float widthUnits)
    {
        var bounds = new Rect(new Vector2(108f, 802f), new Vector2(108f + widthUnits * scale, 846f));
        var fitted = MotionButton.FitHaloWidth(bounds, scale);
        var hoveredHalfWidth = fitted.Width * 1.03f * 0.5f;
        var haloHalfWidth = hoveredHalfWidth + MotionButton.HaloReach(scale);

        Assert.True(fitted.Center.X - haloHalfWidth >= bounds.Min.X + 0.99f);
        Assert.True(fitted.Center.X + haloHalfWidth <= bounds.Max.X - 0.99f);
        Assert.Equal(bounds.Center, fitted.Center);
        Assert.Equal(bounds.Min.Y, fitted.Min.Y);
        Assert.Equal(bounds.Max.Y, fitted.Max.Y);
        Assert.True(fitted.Width > 0f);
    }

    [Fact]
    public void FittingANarrowRowNeverInvertsTheButton()
    {
        var bounds = new Rect(new Vector2(10f, 20f), new Vector2(20f, 64f));
        var fitted = MotionButton.FitHaloWidth(bounds, 3f);
        Assert.Equal(0f, fitted.Width);
        Assert.Equal(bounds.Center, fitted.Center);
    }

    [Fact]
    public void SurfacePaddingInsetRelaxesToTheWindowEdge()
    {
        var clip = MotionButton.HaloClip(SurfaceInnerClip, SurfaceInnerClip, Window, 15.5f, 5.5f);
        Assert.Equal(Window, clip);
    }

    [Fact]
    public void RelaxStopsAtTheReachWhenTheWindowIsFurther()
    {
        var inner = new Rect(new Vector2(100f, 50f), new Vector2(300f, 550f));
        var clip = MotionButton.HaloClip(inner, inner, Window, 15f, 5f);
        Assert.Equal(new Rect(new Vector2(85f, 40f), new Vector2(315f, 570f)), clip);
    }

    [Fact]
    public void PushedTighterClipStaysPut()
    {
        var pushed = new Rect(new Vector2(14f, 120f), new Vector2(386f, 300f));
        var clip = MotionButton.HaloClip(pushed, SurfaceInnerClip, Window, 15.5f, 5.5f);
        Assert.Equal(pushed, clip);
    }

    [Fact]
    public void ExplicitLooserClipStaysPut()
    {
        var escaped = new Rect(new Vector2(-40f, -40f), new Vector2(440f, 640f));
        var clip = MotionButton.HaloClip(escaped, SurfaceInnerClip, Window, 15.5f, 5.5f);
        Assert.Equal(escaped, clip);
    }

    [Fact]
    public void BoundTighterThanTheClipNeverShrinksIt()
    {
        var bound = new Rect(new Vector2(10f, 0f), new Vector2(390f, 600f));
        var clip = MotionButton.HaloClip(SurfaceInnerClip, SurfaceInnerClip, bound, 15.5f, 5.5f);
        Assert.Equal(SurfaceInnerClip, clip);
    }
}
