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
        Assert.Equal(8, BarkeepRules.MinPatrons);
        Assert.Equal(1_900, BarkeepRules.MaxPayout);
        Assert.Equal(2_000, BarkeepRules.EntryChips);
        Assert.Equal(100_000, BarkeepRules.DailyNetWinCap);
        Assert.Equal(9_500, BarkeepRules.ReturnBasisPointsAtPerfectPlay);
        Assert.Equal(new[] { 0, 40, 70, 100 }, BarkeepRules.GradeDomain);
        Assert.Equal(new[] { 8, 8, 8, 8, 9, 9, 9, 10, 10, 10, 11, 11, 11, 12, 13, 14 },
            BarkeepRules.PatronBuckets);
        Assert.Equal(new[] { 450, 700, 850, 950 }, BarkeepRules.LadderRatioFloors);
        Assert.Equal(new long[] { 650, 1_000, 1_450, 1_900 }, BarkeepRules.LadderPays);
    }

    [Fact]
    public void LadderMirrorsTheBackendBandsOnTheShareOfTheCeiling()
    {
        Assert.Equal(0, BarkeepRules.LadderPayout(0, 1000));
        Assert.Equal(0, BarkeepRules.LadderPayout(449, 1000));
        Assert.Equal(650, BarkeepRules.LadderPayout(450, 1000));
        Assert.Equal(650, BarkeepRules.LadderPayout(699, 1000));
        Assert.Equal(1_000, BarkeepRules.LadderPayout(700, 1000));
        Assert.Equal(1_450, BarkeepRules.LadderPayout(850, 1000));
        Assert.Equal(1_450, BarkeepRules.LadderPayout(949, 1000));
        Assert.Equal(1_900, BarkeepRules.LadderPayout(950, 1000));
        Assert.Equal(1_900, BarkeepRules.LadderPayout(1000, 1000));
        Assert.Equal(0, BarkeepRules.LadderPayout(500, 0));
    }

    [Fact]
    public void AQuietShiftReachesTheTopBandAsEasilyAsABusyOne()
    {
        Assert.Equal(1_900, BarkeepRules.LadderPayout(760, 800));
        Assert.Equal(1_450, BarkeepRules.LadderPayout(759, 800));
        Assert.Equal(1_900, BarkeepRules.LadderPayout(1330, 1400));
        for (var bucket = 0; bucket < BarkeepRules.PatronBuckets.Length; bucket++)
        {
            var maxScore = BarkeepRules.PatronBuckets[bucket] * BarkeepRules.PerfectGrade;
            Assert.Equal(BarkeepRules.MaxPayout, BarkeepRules.LadderPayout(maxScore, maxScore));
        }
    }

    [Fact]
    public void AFlawlessShiftReturnsNinetyFivePercentAndNoShiftBeatsTheEntry()
    {
        Assert.Equal(BarkeepRules.EntryChips * BarkeepRules.ReturnBasisPointsAtPerfectPlay / 10_000,
            BarkeepRules.MaxPayout);
        for (var band = 0; band < BarkeepRules.LadderPays.Length; band++)
        {
            Assert.True(BarkeepRules.LadderPays[band] < BarkeepRules.EntryChips);
        }
    }

    [Fact]
    public void LadderBandTracksTheReachedFloor()
    {
        Assert.Equal(-1, BarkeepRules.LadderBand(449, 1000));
        Assert.Equal(0, BarkeepRules.LadderBand(450, 1000));
        Assert.Equal(1, BarkeepRules.LadderBand(750, 1000));
        Assert.Equal(2, BarkeepRules.LadderBand(900, 1000));
        Assert.Equal(3, BarkeepRules.LadderBand(1000, 1000));
    }

    [Fact]
    public void PointsForBandIsTheLeastScoreThatReachesIt()
    {
        for (var bucket = 0; bucket < BarkeepRules.PatronBuckets.Length; bucket++)
        {
            var maxScore = BarkeepRules.PatronBuckets[bucket] * BarkeepRules.PerfectGrade;
            for (var band = 0; band < BarkeepRules.LadderRatioFloors.Length; band++)
            {
                var points = BarkeepRules.PointsForBand(band, maxScore);
                Assert.Equal(band, BarkeepRules.LadderBand(points, maxScore));
                Assert.True(BarkeepRules.LadderBand(points - 1, maxScore) < band);
            }
        }

        Assert.Equal(0, BarkeepRules.PointsForBand(4, 1000));
        Assert.Equal(0, BarkeepRules.PointsForBand(0, 0));
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
