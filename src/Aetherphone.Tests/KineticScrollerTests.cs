using Aetherphone.Core.Animation;
using Xunit;

namespace Aetherphone.Tests;

public class KineticScrollerTests
{
    private static KineticScroller Scroller(float bounds)
    {
        var scroller = new KineticScroller();
        scroller.SetBounds(bounds);
        return scroller;
    }

    [Fact]
    public void MovementBelowThreshold_DoesNotDrag()
    {
        var scroller = Scroller(1000f);
        scroller.Press(100f);
        scroller.Move(103f, 0.016f);
        scroller.Move(104f, 0.016f);
        Assert.False(scroller.IsDragging);
        Assert.Equal(0.0, scroller.Offset, 3);
    }

    [Fact]
    public void MovementPastThreshold_DragsAndMovesOffset()
    {
        var scroller = Scroller(1000f);
        scroller.Press(100f);
        scroller.Move(90f, 0.016f);
        Assert.True(scroller.IsDragging);
        Assert.Equal(10.0, scroller.Offset, 3);
    }

    [Fact]
    public void Offset_ClampsToBounds()
    {
        var scroller = Scroller(50f);
        scroller.Press(100f);
        scroller.Move(0f, 0.1f);
        Assert.Equal(50.0, scroller.Offset, 3);
    }

    [Fact]
    public void ReleaseAfterDrag_StartsMomentumThatDecays()
    {
        var scroller = Scroller(5000f);
        scroller.Press(400f);
        scroller.Move(300f, 0.05f);
        scroller.Move(200f, 0.05f);
        scroller.Move(100f, 0.05f);
        scroller.Release();
        var first = scroller.Offset;
        scroller.Tick(0.05f);
        Assert.True(scroller.Offset > first);
        for (var tick = 0; tick < 500; tick++)
        {
            scroller.Tick(0.05f);
        }

        var rest = scroller.Offset;
        scroller.Tick(0.05f);
        Assert.Equal(rest, scroller.Offset, 3);
    }

    [Fact]
    public void PressDuringMomentum_StopsFling()
    {
        var scroller = Scroller(5000f);
        scroller.Press(400f);
        scroller.Move(300f, 0.05f);
        scroller.Move(200f, 0.05f);
        scroller.Release();
        scroller.Tick(0.05f);
        scroller.Press(200f);
        var held = scroller.Offset;
        scroller.Tick(0.05f);
        Assert.Equal(held, scroller.Offset, 3);
    }

    [Fact]
    public void DownwardDragAtTop_AccumulatesDampenedPull()
    {
        var scroller = Scroller(1000f);
        scroller.Press(100f);
        scroller.Move(110f, 0.05f);
        Assert.True(scroller.IsDragging);
        Assert.Equal(0.0, scroller.Offset, 3);
        Assert.Equal(5.0, scroller.PullDistance, 3);
        scroller.Move(120f, 0.05f);
        Assert.Equal(10.0, scroller.PullDistance, 3);
        Assert.True(scroller.PullDistance < 20.0);
    }

    [Fact]
    public void ReversingPull_ReturnsToZero()
    {
        var scroller = Scroller(1000f);
        scroller.Press(100f);
        scroller.Move(120f, 0.05f);
        scroller.Move(100f, 0.05f);
        Assert.Equal(0.0, scroller.PullDistance, 3);
    }

    [Fact]
    public void ReleaseSpringsPullBackToZero()
    {
        var scroller = Scroller(1000f);
        scroller.Press(100f);
        scroller.Move(120f, 0.05f);
        Assert.True(scroller.PullDistance > 0f);
        scroller.Release();
        Assert.True(scroller.PullDistance > 0f);
        Assert.True(scroller.IsControlling);
        for (var tick = 0; tick < 200; tick++)
        {
            scroller.Tick(0.05f);
        }

        Assert.Equal(0.0, scroller.PullDistance, 3);
        Assert.False(scroller.IsControlling);
    }
}
