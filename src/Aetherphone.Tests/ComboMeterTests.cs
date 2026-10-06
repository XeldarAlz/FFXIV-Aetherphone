using Aetherphone.Apps.Games.Framework;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ComboMeterTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(7, 2)]
    [InlineData(8, 3)]
    [InlineData(11, 3)]
    [InlineData(12, 5)]
    [InlineData(19, 5)]
    [InlineData(20, 8)]
    [InlineData(40, 8)]
    public void TiersRiseAtOneFourEightTwelveAndTwenty(int hits, int expected)
    {
        var meter = ComboMeter.Create();
        var multiplier = 1;
        for (var hit = 0; hit < hits; hit++)
        {
            multiplier = meter.Hit();
        }

        Assert.Equal(expected, multiplier);
        Assert.Equal(expected, meter.Multiplier);
        Assert.Equal(hits, meter.Count);
    }

    [Fact]
    public void AFreshMeterStartsAtMultiplierOne()
    {
        var meter = ComboMeter.Create();

        Assert.Equal(1, meter.Multiplier);
        Assert.Equal(0, meter.Count);
        Assert.False(meter.Active);
    }

    [Fact]
    public void HitsInsideTheWindowKeepTheCombo()
    {
        var meter = ComboMeter.Create();
        meter.Hit();
        meter.Update(1.5f);
        meter.Hit();
        meter.Update(1.5f);

        Assert.Equal(2, meter.Count);
        Assert.True(meter.Active);
    }

    [Fact]
    public void SilenceLongerThanTheWindowResetsTheCount()
    {
        var meter = ComboMeter.Create();
        for (var hit = 0; hit < 4; hit++)
        {
            meter.Hit();
        }

        Assert.Equal(2, meter.Multiplier);
        meter.Update(ComboMeter.DefaultWindowSeconds + 0.1f);

        Assert.Equal(0, meter.Count);
        Assert.Equal(1, meter.Multiplier);
        Assert.Equal(0f, meter.WindowFraction);
    }

    [Fact]
    public void HeatDecaysAfterTheComboDrops()
    {
        var meter = ComboMeter.Create();
        for (var hit = 0; hit < 10; hit++)
        {
            meter.Hit();
        }

        var hot = meter.Heat;
        Assert.InRange(hot, 0.49f, 0.51f);
        meter.Update(ComboMeter.DefaultWindowSeconds + 0.1f);
        var cooler = meter.Heat;
        meter.Update(0.2f);

        Assert.True(cooler < hot);
        Assert.True(meter.Heat < cooler);
        meter.Update(5f);
        Assert.Equal(0f, meter.Heat);
    }

    [Fact]
    public void WindowFractionDrainsBetweenHits()
    {
        var meter = new ComboMeter(2f);
        meter.Hit();
        Assert.Equal(1f, meter.WindowFraction);
        meter.Update(1f);

        Assert.InRange(meter.WindowFraction, 0.49f, 0.51f);
    }

    [Fact]
    public void ResetClearsEverything()
    {
        var meter = ComboMeter.Create();
        meter.Hit();
        meter.Hit();
        meter.Reset();

        Assert.Equal(0, meter.Count);
        Assert.Equal(1, meter.Multiplier);
        Assert.Equal(0f, meter.Heat);
    }
}
