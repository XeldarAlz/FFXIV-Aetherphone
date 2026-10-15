using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class FeedAutoAdvanceTests
{
    private const double Start = 100d;

    [Fact]
    public void WaitsForTheIntervalBeforeStepping()
    {
        Assert.False(FeedVirtualizer.AutoAdvanceDue(Start + 7.9d, Start, Start, 8));
        Assert.True(FeedVirtualizer.AutoAdvanceDue(Start + 8d, Start, Start, 8));
    }

    [Fact]
    public void ClampsTheIntervalToTheAllowedRange()
    {
        Assert.False(FeedVirtualizer.AutoAdvanceDue(Start + 1d, Start, Start, 1));
        Assert.True(FeedVirtualizer.AutoAdvanceDue(Start + FeedVirtualizer.MinAutoAdvanceSeconds, Start, Start, 1));
        Assert.False(FeedVirtualizer.AutoAdvanceDue(Start + FeedVirtualizer.MaxAutoAdvanceSeconds - 0.1d, Start, Start, 60));
        Assert.True(FeedVirtualizer.AutoAdvanceDue(Start + FeedVirtualizer.MaxAutoAdvanceSeconds, Start, Start, 60));
    }

    [Fact]
    public void KeepsSteppingUntilTheIdleLimit()
    {
        var now = Start + FeedVirtualizer.AutoAdvanceIdleLimitSeconds - 1d;
        Assert.True(FeedVirtualizer.AutoAdvanceDue(now, now - 8d, Start, 8));
    }

    [Fact]
    public void StopsOnceTheIdleLimitPassesWithoutInput()
    {
        var now = Start + FeedVirtualizer.AutoAdvanceIdleLimitSeconds;
        Assert.False(FeedVirtualizer.AutoAdvanceDue(now, now - 8d, Start, 8));
        Assert.False(FeedVirtualizer.AutoAdvanceDue(now + 3600d, now - 8d, Start, 8));
    }

    [Fact]
    public void ResumesAfterFreshInput()
    {
        var now = Start + FeedVirtualizer.AutoAdvanceIdleLimitSeconds * 2d;
        Assert.True(FeedVirtualizer.AutoAdvanceDue(now, now - 8d, now - 8d, 8));
    }
}
