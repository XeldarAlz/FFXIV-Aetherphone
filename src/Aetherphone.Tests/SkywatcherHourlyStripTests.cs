using Aetherphone.Apps.Skywatcher;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SkywatcherHourlyStripTests
{
    [Fact]
    public void KeepsSixColumnsWhenTheWidestLabelFits()
    {
        Assert.Equal(6, SkywatcherApp.HourlyColumns(8, 300f, 40f, 2f));
    }

    [Fact]
    public void DropsColumnsUntilTheWidestLabelFits()
    {
        Assert.Equal(5, SkywatcherApp.HourlyColumns(8, 300f, 55f, 2f));
        Assert.Equal(4, SkywatcherApp.HourlyColumns(8, 300f, 70f, 2f));
    }

    [Fact]
    public void NeverDropsBelowFourColumns()
    {
        Assert.Equal(4, SkywatcherApp.HourlyColumns(8, 300f, 200f, 2f));
    }

    [Fact]
    public void NeverShowsMoreColumnsThanWindows()
    {
        Assert.Equal(3, SkywatcherApp.HourlyColumns(3, 300f, 200f, 2f));
        Assert.Equal(5, SkywatcherApp.HourlyColumns(5, 300f, 10f, 2f));
        Assert.Equal(0, SkywatcherApp.HourlyColumns(0, 300f, 10f, 2f));
    }
}
