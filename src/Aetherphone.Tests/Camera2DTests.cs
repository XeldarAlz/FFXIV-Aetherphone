using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Xunit;

namespace Aetherphone.Tests;

public sealed class Camera2DTests
{
    private static readonly Rect View = new(new Vector2(100f, 50f), new Vector2(460f, 690f));

    [Fact]
    public void ContainFitsTheWholeWorldInsideTheView()
    {
        var camera = Camera2D.Create();
        camera.Fit(View, 10f, 20f, FitMode.Contain);

        Assert.Equal(32f, camera.Zoom);
        Assert.Equal(View.Center, camera.Anchor);
        Assert.Equal(new Vector2(5f, 10f), camera.Origin);
    }

    [Fact]
    public void FittingAWorldRectKeepsTheOriginOnItsCentreEveryFrame()
    {
        var camera = Camera2D.Create();
        var world = new Rect(new Vector2(-5f, -10f), new Vector2(5f, 10f));
        camera.Fit(View, world, FitMode.Contain);
        Assert.Equal(32f, camera.Zoom);
        Assert.Equal(Vector2.Zero, camera.Origin);

        camera.Place(new Vector2(3f, 3f));
        camera.Fit(View, world, FitMode.Contain);

        Assert.Equal(Vector2.Zero, camera.Origin);
        Assert.Equal(View.Center, camera.ToScreen(Vector2.Zero));
    }

    [Fact]
    public void CoverWidthAndCoverHeightUseOneAxis()
    {
        var wide = Camera2D.Create();
        wide.Fit(View, 10f, 20f, FitMode.CoverWidth);
        var tall = Camera2D.Create();
        tall.Fit(View, 10f, 20f, FitMode.CoverHeight);

        Assert.Equal(36f, wide.Zoom);
        Assert.Equal(32f, tall.Zoom);
    }

    [Fact]
    public void ToScreenAndToWorldRoundTrip()
    {
        var camera = Camera2D.Create();
        camera.Fit(View, 10f, 20f, FitMode.Contain);
        camera.Place(new Vector2(3f, 7f));

        var world = new Vector2(1.25f, 12.5f);
        var screen = camera.ToScreen(world);
        var back = camera.ToWorld(screen);

        Assert.InRange(back.X, world.X - 0.001f, world.X + 0.001f);
        Assert.InRange(back.Y, world.Y - 0.001f, world.Y + 0.001f);
        Assert.Equal(View.Center, camera.ToScreen(new Vector2(3f, 7f)));
        Assert.Equal(64f, camera.Px(2f));
        Assert.Equal(2f, camera.Units(64f));
    }

    [Fact]
    public void VisibleWorldSpansTheView()
    {
        var camera = Camera2D.Create();
        camera.Fit(View, 10f, 20f, FitMode.Contain);

        var visible = camera.VisibleWorld;

        Assert.InRange(visible.Min.X, -0.63f, -0.62f);
        Assert.InRange(visible.Max.X, 10.62f, 10.63f);
        Assert.InRange(visible.Min.Y, -0.001f, 0.001f);
        Assert.InRange(visible.Max.Y, 19.999f, 20.001f);
    }

    [Fact]
    public void FollowConvergesOnTheTargetPlusLead()
    {
        var camera = Camera2D.Create();
        camera.Fit(View, 10f, 20f, FitMode.Contain);
        camera.Place(Vector2.Zero);
        var target = new Vector2(6f, 14f);
        var lead = new Vector2(1f, -2f);
        var previousDistance = float.MaxValue;
        for (var frame = 0; frame < 240; frame++)
        {
            camera.Follow(target, lead, 0.3f, 1f / 60f);
            var distance = Vector2.Distance(camera.Origin, target + lead);
            Assert.True(distance <= previousDistance + 0.0001f);
            previousDistance = distance;
        }

        Assert.InRange(camera.Origin.X, 6.99f, 7.01f);
        Assert.InRange(camera.Origin.Y, 11.99f, 12.01f);
    }

    [Fact]
    public void FollowBeforePlacementSnapsInstantly()
    {
        var camera = Camera2D.Create();
        camera.Follow(new Vector2(4f, 4f), Vector2.Zero, 0.3f, 1f / 60f);

        Assert.Equal(new Vector2(4f, 4f), camera.Origin);
        Assert.True(camera.Placed);
    }

    [Fact]
    public void PunchKicksTheZoomAndDecays()
    {
        var camera = Camera2D.Create();
        camera.Fit(View, 10f, 20f, FitMode.Contain);
        camera.Punch(0.08f);

        Assert.InRange(camera.EffectiveZoom, 32f * 1.079f, 32f * 1.081f);
        for (var frame = 0; frame < 60; frame++)
        {
            camera.Update(1f / 60f, 1f);
        }

        Assert.InRange(camera.EffectiveZoom, 31.99f, 32.2f);
    }

    [Fact]
    public void ShakeDecaysToNothing()
    {
        var camera = Camera2D.Create();
        camera.Fit(View, 10f, 20f, FitMode.Contain);
        camera.Shake(1f);
        camera.Update(1f / 60f, 1f);

        Assert.True(camera.ShakeOffset.Length() > 0f);
        for (var frame = 0; frame < 120; frame++)
        {
            camera.Update(1f / 60f, 1f);
        }

        Assert.Equal(Vector2.Zero, camera.ShakeOffset);
        Assert.Equal(0f, camera.Trauma);
    }
}
