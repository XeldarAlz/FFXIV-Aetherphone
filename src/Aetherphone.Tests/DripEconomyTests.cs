using Aetherphone.Apps.Games.Framework.World;
using Xunit;

namespace Aetherphone.Tests;

public sealed class DripEconomyTests
{
    [Fact]
    public void AdvanceAccruesAtTheRate()
    {
        var drip = new DripEconomy(50f, 10f, 1000f);

        drip.Advance(2.5f);

        Assert.Equal(75f, drip.Amount, 0.001f);
        Assert.Equal(75, drip.Whole);
    }

    [Fact]
    public void AdvanceNeverPassesTheCap()
    {
        var drip = new DripEconomy(90f, 25f, 100f);

        drip.Advance(10f);

        Assert.Equal(100f, drip.Amount);
        Assert.True(drip.Full);
        drip.Advance(10f);
        Assert.Equal(100f, drip.Amount);
    }

    [Fact]
    public void PickupsAreCappedAndReportWhatTheyAdded()
    {
        var drip = new DripEconomy(80f, 0f, 100f);

        Assert.Equal(15f, drip.Add(15f), 0.001f);
        Assert.Equal(5f, drip.Add(25f), 0.001f);
        Assert.Equal(0f, drip.Add(25f));
        Assert.Equal(0f, drip.Add(-5f));
        Assert.Equal(100f, drip.Amount);
    }

    [Fact]
    public void SpendingDeductsOnlyWhenAffordable()
    {
        var drip = new DripEconomy(120f, 0f, 500f);

        Assert.True(drip.CanAfford(100f));
        Assert.True(drip.TrySpend(100f));
        Assert.Equal(20f, drip.Amount, 0.001f);
        Assert.False(drip.TrySpend(50f));
        Assert.Equal(20f, drip.Amount, 0.001f);
        Assert.False(drip.TrySpend(-10f));
        Assert.True(drip.TrySpend(20f));
        Assert.Equal(0f, drip.Amount, 0.001f);
    }

    [Fact]
    public void SpendingBelowTheCapLetsTheDripResume()
    {
        var drip = new DripEconomy(100f, 10f, 100f);
        drip.TrySpend(50f);

        drip.Advance(1f);

        Assert.Equal(60f, drip.Amount, 0.001f);
        Assert.False(drip.Full);
    }

    [Fact]
    public void RaisingTheRateSpeedsTheDrip()
    {
        var drip = new DripEconomy(0f, 5f, 1000f);
        drip.Advance(1f);
        drip.RatePerSecond += 20f;

        drip.Advance(1f);

        Assert.Equal(30f, drip.Amount, 0.001f);
    }

    [Fact]
    public void TheStartingAmountIsClampedIntoRange()
    {
        Assert.Equal(100f, new DripEconomy(400f, 1f, 100f).Amount);
        Assert.Equal(0f, new DripEconomy(-5f, 1f, 100f).Amount);
        Assert.True(new DripEconomy(0f, 1f, 0f).Full);
    }
}
