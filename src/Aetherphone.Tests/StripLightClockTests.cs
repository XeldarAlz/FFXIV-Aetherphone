using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Casino.Strip;
using Aetherphone.Core.Animation;
using Xunit;

namespace Aetherphone.Tests;

public sealed class StripLightClockTests
{
    [Fact]
    public void FloorBulbsAdvanceWithoutOpeningAGameStage()
    {
        var clock = new StripLightClock();
        Assert.False(CasinoLights.BulbLit(0, clock.Phase));

        for (var frame = 0; frame < 12; frame++)
        {
            clock.Advance(frame, 1f / 60f);
        }

        Assert.True(CasinoLights.BulbLit(0, clock.Phase));
        Assert.Equal(0.2f, clock.Phase, 5);
    }

    [Fact]
    public void DrawingTwoRoutesInOneFrameDoesNotDoubleTheChaseSpeed()
    {
        var clock = new StripLightClock();
        clock.Advance(1, 0.05f);
        clock.Advance(1, 0.05f);
        Assert.Equal(0.05f, clock.Phase, 5);

        clock.Advance(2, 0.05f);
        Assert.Equal(0.1f, clock.Phase, 5);
    }

    [Fact]
    public void ReturningAfterHiddenFramesContinuesWithoutCatchingUpTheGap()
    {
        var clock = new StripLightClock();
        clock.Advance(1, 0.05f);
        clock.Advance(600, 30f);

        Assert.Equal(0.05f + TransitionTiming.MaxFrameSeconds, clock.Phase, 5);
    }

    [Fact]
    public void PausedOrNegativeDeltaDoesNotAdvanceTheLights()
    {
        var clock = new StripLightClock();
        clock.Advance(1, 0.05f);
        clock.Advance(2, 0f);
        clock.Advance(3, -1f);

        Assert.Equal(0.05f, clock.Phase, 5);
        clock.Advance(4, 0.05f);
        Assert.Equal(0.1f, clock.Phase, 5);
    }
}
