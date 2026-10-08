using Aetherphone.Apps.Casino.Cabinets;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class DailySpinPlaybackTests
{
    private const float Frame = 1f / 60f;

    private static int Run(DailySpinPlayback playback, float seconds)
    {
        var ticks = 0;
        for (var elapsed = 0f; elapsed < seconds; elapsed += Frame)
        {
            playback.Update(Frame);
            ticks += playback.TakeTicks();
        }

        return ticks;
    }

    private static bool SameAngle(float expected, float actual)
    {
        var difference = WheelChoreography.Normalize(actual - expected);
        return MathF.Min(difference, WheelChoreography.Tau - difference) < 1e-3f;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(11)]
    [InlineData(15)]
    public void TheSpinLandsTheServersSegmentUnderThePointer(int segment)
    {
        var playback = new DailySpinPlayback();
        playback.Begin(segment, DailySpinRules.AwardOf(segment));
        Assert.True(playback.Spinning);

        var ticks = Run(playback, WheelChoreography.SpinSeconds + 0.2f);

        Assert.False(playback.Spinning);
        Assert.True(playback.Rested);
        Assert.True(playback.TakeLanded());
        Assert.False(playback.TakeLanded());
        var rest = WheelChoreography.RestAngleOf(segment, DailySpinRules.SegmentCount);
        Assert.True(SameAngle(rest, playback.Angle));
        Assert.True(ticks >= DailySpinPlayback.SpinTurns * DailySpinRules.SegmentCount);
    }

    [Fact]
    public void ThePointerKicksOnThePegsAndSettlesWithoutOvershoot()
    {
        var playback = new DailySpinPlayback();
        playback.Begin(3, DailySpinRules.AwardOf(3));
        Run(playback, 0.5f);
        Assert.True(playback.PointerDeflection < 0f);

        Run(playback, WheelChoreography.SpinSeconds + 1f);
        Assert.InRange(playback.PointerDeflection, -0.01f, 0f);
    }

    [Fact]
    public void ASpinTheServerAlreadyPaidRestsWithoutReplayingTheCelebration()
    {
        var playback = new DailySpinPlayback();
        playback.Adopt(11, 60);

        Run(playback, 1f);

        Assert.False(playback.Spinning);
        Assert.True(playback.Rested);
        Assert.False(playback.TakeLanded());
        Assert.Equal(60, playback.Amount);
        Assert.True(SameAngle(WheelChoreography.RestAngleOf(11, DailySpinRules.SegmentCount), playback.Angle));
    }

    [Fact]
    public void SnapToTruthFinishesTheSpinOnItsSegment()
    {
        var playback = new DailySpinPlayback();
        playback.Begin(5, DailySpinRules.AwardOf(5));
        Run(playback, 0.3f);

        playback.Snap();

        Assert.False(playback.Spinning);
        Assert.True(playback.TakeLanded());
        Assert.True(SameAngle(WheelChoreography.RestAngleOf(5, DailySpinRules.SegmentCount), playback.Angle));
    }

    [Fact]
    public void TheIdleWheelTurnsOnlyUntilTheDayIsSpent()
    {
        var playback = new DailySpinPlayback();
        playback.Idle(1f);
        Assert.NotEqual(0f, playback.Angle);

        playback.Adopt(2, DailySpinRules.AwardOf(2));
        var rest = playback.Angle;
        playback.Idle(1f);
        Assert.Equal(rest, playback.Angle);
    }

    [Fact]
    public void ASegmentOffTheRimNeverSpins()
    {
        var playback = new DailySpinPlayback();
        playback.Begin(DailySpinRules.SegmentCount, 0);

        Assert.False(playback.Spinning);
        Assert.False(playback.Rested);
    }
}
