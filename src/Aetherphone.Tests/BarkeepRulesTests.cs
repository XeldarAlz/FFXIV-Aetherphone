using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class BarkeepRulesTests
{
    [Fact]
    public void ConstantsMatchTheBackendEngineAndEconomy()
    {
        Assert.Equal(60, BarkeepRules.ShiftSeconds);
        Assert.Equal(60, BarkeepRules.MinFinishSeconds);
        Assert.Equal(300, BarkeepRules.MaxFinishSeconds);
        Assert.Equal(4, BarkeepRules.SecondsPerOrder);
        Assert.Equal(14, BarkeepRules.MaxPatrons);
        Assert.Equal(4, BarkeepRules.StepKindCount);
        Assert.Equal(100, BarkeepRules.PerfectGrade);
        Assert.Equal(30_000, BarkeepRules.MaxPayout);
        Assert.Equal(20_000, BarkeepRules.EntryChips);
        Assert.Equal(100_000, BarkeepRules.DailyNetWinCap);
        Assert.Equal(new[] { 0, 40, 70, 100 }, BarkeepRules.GradeDomain);
        Assert.Equal(new[] { 8, 8, 8, 8, 9, 9, 9, 10, 10, 10, 11, 11, 11, 12, 13, 14 },
            BarkeepRules.PatronBuckets);
    }

    [Fact]
    public void LadderMirrorsTheBackendBands()
    {
        Assert.Equal(0, BarkeepRules.LadderPayout(0));
        Assert.Equal(0, BarkeepRules.LadderPayout(449));
        Assert.Equal(9_000, BarkeepRules.LadderPayout(450));
        Assert.Equal(9_000, BarkeepRules.LadderPayout(749));
        Assert.Equal(15_000, BarkeepRules.LadderPayout(750));
        Assert.Equal(15_000, BarkeepRules.LadderPayout(999));
        Assert.Equal(23_000, BarkeepRules.LadderPayout(1000));
        Assert.Equal(23_000, BarkeepRules.LadderPayout(1199));
        Assert.Equal(30_000, BarkeepRules.LadderPayout(1200));
        Assert.Equal(30_000, BarkeepRules.LadderPayout(1400));
    }

    [Fact]
    public void LadderBandTracksTheReachedFloor()
    {
        Assert.Equal(-1, BarkeepRules.LadderBand(449));
        Assert.Equal(0, BarkeepRules.LadderBand(450));
        Assert.Equal(1, BarkeepRules.LadderBand(750));
        Assert.Equal(2, BarkeepRules.LadderBand(1000));
        Assert.Equal(3, BarkeepRules.LadderBand(1250));
    }

    [Fact]
    public void GradeValidationAcceptsOnlyTheDomain()
    {
        Assert.True(BarkeepRules.IsValidGrade(0));
        Assert.True(BarkeepRules.IsValidGrade(40));
        Assert.True(BarkeepRules.IsValidGrade(70));
        Assert.True(BarkeepRules.IsValidGrade(100));
        Assert.False(BarkeepRules.IsValidGrade(50));
        Assert.False(BarkeepRules.IsValidGrade(-1));
        Assert.False(BarkeepRules.IsValidGrade(101));
    }

    [Fact]
    public void OrderScoreUsesTheServerIntegerAverage()
    {
        Assert.Equal(70, BarkeepRules.OrderScore(new[] { 100, 70, 40 }));
        Assert.Equal(100, BarkeepRules.OrderScore(new[] { 100, 100 }));
        Assert.Equal(20, BarkeepRules.OrderScore(new[] { 0, 40 }));
        Assert.Equal(52, BarkeepRules.OrderScore(new[] { 70, 70, 70, 0 }));
    }

    [Fact]
    public void MaxOrdersForElapsedMirrorsTheServerClamp()
    {
        Assert.Equal(0, BarkeepRules.MaxOrdersForElapsed(0, 8));
        Assert.Equal(2, BarkeepRules.MaxOrdersForElapsed(8, 14));
        Assert.Equal(14, BarkeepRules.MaxOrdersForElapsed(61, 14));
        Assert.Equal(8, BarkeepRules.MaxOrdersForElapsed(300, 8));
    }
}
