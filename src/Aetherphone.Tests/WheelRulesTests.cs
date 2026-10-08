using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class WheelRulesTests
{
    [Fact]
    public void TheRimHasFiftySegmentsSplitTheRatifiedWay()
    {
        Assert.Equal(50, WheelRules.SegmentCount);
        Assert.Equal(WheelRules.SegmentCount, WheelRules.Segments.Length);
        Assert.Equal(new[] { 1, 3, 5, 11, 23 }, WheelRules.Multipliers);
        Assert.Equal(500, WheelRules.MinStakePerSpot);
        Assert.Equal(50_000, WheelRules.MaxStakePerSpot);
        Assert.Equal(200_000, WheelRules.MaxStakePerRound);
        Assert.Equal(new[] { 24, 12, 8, 4, 2 }, WheelRules.SegmentCounts);
    }

    [Fact]
    public void EverySpotOwnsExactlyTheSegmentsItAdvertises()
    {
        var counted = new int[WheelRules.SpotCount];
        for (var segment = 0; segment < WheelRules.SegmentCount; segment++)
        {
            var spot = WheelRules.SpotAt(segment);
            Assert.InRange(spot, 0, WheelRules.SpotCount - 1);
            counted[spot]++;
        }

        Assert.Equal(WheelRules.SegmentCounts, counted);
        var total = 0;
        for (var spot = 0; spot < WheelRules.SpotCount; spot++)
        {
            total += counted[spot];
        }

        Assert.Equal(WheelRules.SegmentCount, total);
    }

    [Fact]
    public void NoTwoNeighbouringSegmentsShareASpot()
    {
        for (var segment = 0; segment < WheelRules.SegmentCount; segment++)
        {
            var next = (segment + 1) % WheelRules.SegmentCount;
            Assert.NotEqual(WheelRules.SpotAt(segment), WheelRules.SpotAt(next));
        }
    }

    [Theory]
    [InlineData(0, 48)]
    [InlineData(1, 48)]
    [InlineData(2, 48)]
    [InlineData(3, 48)]
    [InlineData(4, 48)]
    public void EachSpotReturnsItsPublishedShareOfFifty(int spot, int expectedPerFifty)
    {
        var returned = WheelRules.SegmentsOn(spot) * (WheelRules.MultiplierOf(spot) + 1);
        Assert.Equal(expectedPerFifty, returned);
    }

    [Fact]
    public void EverySpotReturnsNinetySixPercent()
    {
        var totalReturn = 0;
        for (var spot = 0; spot < WheelRules.SpotCount; spot++)
        {
            totalReturn += WheelRules.SegmentsOn(spot) * (WheelRules.MultiplierOf(spot) + 1);
        }

        Assert.Equal(240, totalReturn);
        Assert.Equal(250, WheelRules.SegmentCount * WheelRules.SpotCount);
        for (var spot = 0; spot < WheelRules.SpotCount; spot++)
        {
            Assert.Equal(9_600, WheelRules.ReturnBasisPointsFor(spot));
        }

        Assert.Equal(0, WheelRules.ReturnBasisPointsFor(WheelRules.SpotCount));
    }

    [Fact]
    public void AWinningSpotHandsBackTheStakeAlongsideTheWinnings()
    {
        for (var segment = 0; segment < WheelRules.SegmentCount; segment++)
        {
            var winner = WheelRules.SpotAt(segment);
            Assert.Equal(10L * (WheelRules.MultiplierOf(winner) + 1), WheelRules.Returned(segment, winner, 10));
            for (var spot = 0; spot < WheelRules.SpotCount; spot++)
            {
                if (spot != winner)
                {
                    Assert.Equal(0, WheelRules.Returned(segment, spot, 10));
                }
            }
        }
    }

    [Fact]
    public void OnlyStakesInsideThePerSpotBandAreAccepted()
    {
        Assert.False(WheelRules.IsStakeInRange(WheelRules.MinStakePerSpot - 1));
        Assert.True(WheelRules.IsStakeInRange(WheelRules.MinStakePerSpot));
        Assert.True(WheelRules.IsStakeInRange(WheelRules.MaxStakePerSpot));
        Assert.False(WheelRules.IsStakeInRange(WheelRules.MaxStakePerSpot + 1));
    }

    [Fact]
    public void HeadroomNarrowsAsTheRoundCapFillsAndClosesAtIt()
    {
        Assert.Equal(WheelRules.MaxStakePerSpot, WheelRules.Headroom(0));
        Assert.Equal(WheelRules.MaxStakePerSpot, WheelRules.Headroom(WheelRules.MaxStakePerRound - 60_000));
        Assert.Equal(2000, WheelRules.Headroom(WheelRules.MaxStakePerRound - 2000));
        Assert.Equal(0, WheelRules.Headroom(WheelRules.MaxStakePerRound));
        Assert.Equal(0, WheelRules.Headroom(WheelRules.MaxStakePerRound + 4000));
    }

    [Fact]
    public void ClampCutsABetToWhatTheRoundCapAndTheStackAllow()
    {
        Assert.Equal(3000, WheelRules.Clamp(3000, 0, 100_000));
        Assert.Equal(WheelRules.MaxStakePerSpot, WheelRules.Clamp(80_000, 0, 100_000));
        Assert.Equal(2000, WheelRules.Clamp(5000, WheelRules.MaxStakePerRound - 2000, 100_000));
        Assert.Equal(1200, WheelRules.Clamp(5000, 0, 1200));
        Assert.Equal(0, WheelRules.Clamp(5000, WheelRules.MaxStakePerRound, 100_000));
        Assert.Equal(0, WheelRules.Clamp(5000, 0, WheelRules.MinStakePerSpot - 1));
        Assert.Equal(0, WheelRules.Clamp(WheelRules.MinStakePerSpot - 1, 0, 100_000));
    }

    [Fact]
    public void OutOfRangeSegmentsAndSpotsNeverPay()
    {
        Assert.Equal(-1, WheelRules.SpotAt(-1));
        Assert.Equal(-1, WheelRules.SpotAt(WheelRules.SegmentCount));
        Assert.False(WheelRules.Wins(0, -1));
        Assert.False(WheelRules.Wins(0, WheelRules.SpotCount));
        Assert.Equal(0, WheelRules.Returned(WheelRules.SegmentCount, 0, 50));
        Assert.Equal(0, WheelRules.MultiplierOf(WheelRules.SpotCount));
        Assert.Equal(0, WheelRules.SegmentsOn(-1));
    }
}
