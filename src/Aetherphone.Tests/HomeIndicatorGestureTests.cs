using System.Numerics;
using Aetherphone.Core.Shell;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HomeIndicatorGestureTests
{
    private const float ScreenHeight = 800f;
    private const float Scale = 1f;
    private const float FrameSeconds = 1f / 60f;

    [Fact]
    public void ShortMovementStaysInsideTheDeadZone()
    {
        var gesture = new HomeIndicatorGesture();
        gesture.Press(new Vector2(200f, 780f));
        var frame = gesture.Track(new Vector2(200f, 776f), FrameSeconds, ScreenHeight, Scale);
        Assert.False(frame.Scrubbing);
        Assert.False(gesture.Scrubbing);
        var release = gesture.Release(ScreenHeight, Scale);
        Assert.False(release.WasScrubbing);
        Assert.False(gesture.Pressed);
    }

    [Fact]
    public void DraggingUpShrinksTheCoverMonotonically()
    {
        var gesture = new HomeIndicatorGesture();
        gesture.Press(new Vector2(200f, 780f));
        var previous = 1f;
        for (var step = 1; step <= 40; step++)
        {
            var frame = gesture.Track(new Vector2(200f, 780f - step * 10f), FrameSeconds, ScreenHeight, Scale);
            Assert.True(frame.Cover <= previous + 1e-6f, $"step {step} grew the cover: {previous} -> {frame.Cover}");
            Assert.True(frame.Cover > 0f);
            previous = frame.Cover;
        }

        Assert.True(gesture.Scrubbing);
        Assert.True(previous < 0.5f);
    }

    [Fact]
    public void CoverNeverDropsBelowTheHeldFloorHoweverFarTheDragGoes()
    {
        var full = HomeIndicatorGesture.CoverFor(ScreenHeight * 0.38f, ScreenHeight * 0.38f);
        var far = HomeIndicatorGesture.CoverFor(ScreenHeight * 5f, ScreenHeight * 0.38f);
        Assert.InRange(full, 0.44f, 0.46f);
        Assert.True(far < full);
        Assert.True(far > 0.3f);
        Assert.Equal(1f, HomeIndicatorGesture.CoverFor(-50f, 300f));
    }

    [Fact]
    public void SlowReleaseNearTheStartReturnsToTheApp()
    {
        var gesture = new HomeIndicatorGesture();
        gesture.Press(new Vector2(200f, 780f));
        gesture.Track(new Vector2(200f, 760f), FrameSeconds, ScreenHeight, Scale);
        for (var frame = 0; frame < 20; frame++)
        {
            gesture.Track(new Vector2(200f, 760f), FrameSeconds, ScreenHeight, Scale);
        }

        var release = gesture.Release(ScreenHeight, Scale);
        Assert.True(release.WasScrubbing);
        Assert.False(release.ToHome);
    }

    [Fact]
    public void FlickUpwardGoesHomeEvenFromAShortDrag()
    {
        var gesture = new HomeIndicatorGesture();
        gesture.Press(new Vector2(200f, 780f));
        gesture.Track(new Vector2(200f, 770f), FrameSeconds, ScreenHeight, Scale);
        gesture.Track(new Vector2(200f, 740f), FrameSeconds, ScreenHeight, Scale);
        gesture.Track(new Vector2(200f, 700f), FrameSeconds, ScreenHeight, Scale);
        var release = gesture.Release(ScreenHeight, Scale);
        Assert.True(release.WasScrubbing);
        Assert.True(release.ToHome);
        Assert.True(release.CoverVelocity < 0f);
    }

    [Fact]
    public void DeepDragReleasedAtRestGoesHome()
    {
        var gesture = new HomeIndicatorGesture();
        gesture.Press(new Vector2(200f, 780f));
        for (var step = 1; step <= 30; step++)
        {
            gesture.Track(new Vector2(200f, 780f - step * 10f), FrameSeconds, ScreenHeight, Scale);
        }

        for (var frame = 0; frame < 20; frame++)
        {
            gesture.Track(new Vector2(200f, 480f), FrameSeconds, ScreenHeight, Scale);
        }

        var release = gesture.Release(ScreenHeight, Scale);
        Assert.True(release.ToHome);
    }

    [Fact]
    public void PushingBackDownFromADeepDragReturnsToTheApp()
    {
        var gesture = new HomeIndicatorGesture();
        gesture.Press(new Vector2(200f, 780f));
        for (var step = 1; step <= 30; step++)
        {
            gesture.Track(new Vector2(200f, 780f - step * 10f), FrameSeconds, ScreenHeight, Scale);
        }

        for (var step = 1; step <= 6; step++)
        {
            gesture.Track(new Vector2(200f, 480f + step * 40f), FrameSeconds, ScreenHeight, Scale);
        }

        var release = gesture.Release(ScreenHeight, Scale);
        Assert.False(release.ToHome);
        Assert.True(release.CoverVelocity > 0f);
    }

    [Fact]
    public void HoldingStillFiresTheHoldExactlyOnce()
    {
        var gesture = new HomeIndicatorGesture();
        gesture.Press(new Vector2(200f, 780f));
        var firedCount = 0;
        for (var frame = 0; frame < 60; frame++)
        {
            if (gesture.TrackHold(new Vector2(200f, 780f), FrameSeconds, Scale))
            {
                firedCount++;
            }
        }

        Assert.Equal(1, firedCount);
        Assert.True(gesture.Pressed);
    }

    [Fact]
    public void MovingBeyondTheDeadZoneCancelsTheHold()
    {
        var gesture = new HomeIndicatorGesture();
        gesture.Press(new Vector2(200f, 780f));
        gesture.TrackHold(new Vector2(220f, 780f), FrameSeconds, Scale);
        var fired = false;
        for (var frame = 0; frame < 120; frame++)
        {
            fired |= gesture.TrackHold(new Vector2(200f, 780f), FrameSeconds, Scale);
        }

        Assert.False(fired);
    }

    [Fact]
    public void ScrubbingSuppressesTheHold()
    {
        var gesture = new HomeIndicatorGesture();
        gesture.Press(new Vector2(200f, 780f));
        gesture.Track(new Vector2(200f, 700f), FrameSeconds, ScreenHeight, Scale);
        var fired = false;
        for (var frame = 0; frame < 120; frame++)
        {
            fired |= gesture.TrackHold(new Vector2(200f, 700f), FrameSeconds, Scale);
        }

        Assert.False(fired);
        Assert.True(gesture.Scrubbing);
    }

    [Fact]
    public void DriftFollowsTheCursorAtHalfSpeed()
    {
        var gesture = new HomeIndicatorGesture();
        gesture.Press(new Vector2(200f, 780f));
        var frame = gesture.Track(new Vector2(260f, 680f), FrameSeconds, ScreenHeight, Scale);
        Assert.Equal(30f, frame.Drift.X, 3);
        Assert.Equal(-50f, frame.Drift.Y, 3);
    }
}
