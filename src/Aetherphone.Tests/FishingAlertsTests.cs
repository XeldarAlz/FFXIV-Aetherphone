using Aetherphone.Core.Fishing;
using Xunit;

namespace Aetherphone.Tests;

public sealed class FishingAlertsTests
{
    private const long Start = 10_000;
    private const long End = 12_000;
    private const long Lead = 300;

    private static readonly FishWindow Window = new(Start, End);

    [Fact]
    public void AnOpenWindowCountsAsRunning()
    {
        Assert.True(FishingAlerts.AlertWindowRunning(Window, Start, Lead));
        Assert.True(FishingAlerts.AlertWindowRunning(Window, End - 1, Lead));
    }

    [Fact]
    public void TheLeadTimeBeforeAWindowCountsAsRunning()
    {
        Assert.True(FishingAlerts.AlertWindowRunning(Window, Start - Lead, Lead));
        Assert.False(FishingAlerts.AlertWindowRunning(Window, Start - Lead - 1, Lead));
        Assert.True(FishingAlerts.AlertWindowRunning(Window, Start - 3_600, 3_600));
        Assert.False(FishingAlerts.AlertWindowRunning(Window, Start - 3_600, Lead));
    }

    [Fact]
    public void TheLeadTimeIsClampedToAMinuteAndAnHour()
    {
        Assert.Equal(5, FishingAlerts.DefaultLeadMinutes);
        Assert.Equal(1, FishingAlerts.MinLeadMinutes);
        Assert.Equal(60, FishingAlerts.MaxLeadMinutes);
    }

    [Fact]
    public void AClosedOrMissingWindowIsNotRunning()
    {
        Assert.False(FishingAlerts.AlertWindowRunning(Window, End, Lead));
        Assert.False(FishingAlerts.AlertWindowRunning(FishWindow.None, Start, Lead));
    }
}
