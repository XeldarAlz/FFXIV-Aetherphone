using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SeekHoldTests
{
    private const long Start = 1_000_000L;

    [Fact]
    public void IdleHoldShowsTheReportedPosition()
    {
        var hold = new SeekHold();

        Assert.False(hold.Active);
        Assert.Equal(42f, hold.Shown(42f, Start));
    }

    [Fact]
    public void HeldTargetShowsUntilTheSourceCatchesUp()
    {
        var hold = new SeekHold();
        hold.Begin(120f, Start);

        Assert.Equal(120f, hold.Shown(30f, Start + 500));
        Assert.True(hold.Active);
        Assert.Equal(120.8f, hold.Shown(120.8f, Start + 900));
        Assert.False(hold.Active);
        Assert.Equal(31f, hold.Shown(31f, Start + 1000));
    }

    [Fact]
    public void HeldTargetGivesUpAfterTheHoldWindow()
    {
        var hold = new SeekHold();
        hold.Begin(120f, Start);

        Assert.Equal(120f, hold.Shown(30f, Start + SeekHold.HoldMilliseconds - 1));
        Assert.Equal(33f, hold.Shown(33f, Start + SeekHold.HoldMilliseconds));
        Assert.False(hold.Active);
    }

    [Fact]
    public void ClearDropsTheHeldTarget()
    {
        var hold = new SeekHold();
        hold.Begin(120f, Start);
        hold.Clear();

        Assert.False(hold.Active);
        Assert.Equal(30f, hold.Shown(30f, Start + 10));
    }
}
