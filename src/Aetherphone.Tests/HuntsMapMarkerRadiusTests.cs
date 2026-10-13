using Aetherphone.Core.Hunts;
using Aetherphone.Core.Maps;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HuntsMapMarkerRadiusTests
{
    private const int FullScale = 100;

    [Fact]
    public void DenseSpawnsKeepTheSmallestDisc()
    {
        var points = Grid(spacing: 50f, columns: 7, rows: 6);

        Assert.Equal(HuntsMapMarkerRadius.CandidateMinimum, HuntsMapMarkerRadius.CandidateFor(points, FullScale));
    }

    [Fact]
    public void SparseSpawnsGrowTheDiscWithTheNearestPair()
    {
        var points = new[]
        {
            Candidate(100f, 100f),
            Candidate(400f, 100f),
            Candidate(100f, 1400f),
        };

        Assert.Equal(120, HuntsMapMarkerRadius.CandidateFor(points, FullScale));
    }

    [Fact]
    public void TheDiscNeverGrowsPastTheCap()
    {
        var points = new[] { Candidate(0f, 0f), Candidate(1200f, 0f) };

        Assert.Equal(HuntsMapMarkerRadius.CandidateMaximum, HuntsMapMarkerRadius.CandidateFor(points, FullScale));
    }

    [Fact]
    public void ASinglePointGetsTheLargestDisc()
    {
        var points = new[] { Candidate(512f, 512f) };

        Assert.Equal(HuntsMapMarkerRadius.CandidateMaximum, HuntsMapMarkerRadius.CandidateFor(points, FullScale));
    }

    [Fact]
    public void NoPointsStillAnswerTheLargestDisc()
    {
        Assert.Equal(HuntsMapMarkerRadius.CandidateMaximum,
            HuntsMapMarkerRadius.CandidateFor(ReadOnlySpan<HuntsMapMarkerPoint>.Empty, FullScale));
    }

    [Fact]
    public void MapScaleTurnsCanvasPixelsIntoWorldUnits()
    {
        var points = new[] { Candidate(0f, 0f), Candidate(300f, 0f) };

        Assert.Equal(120, HuntsMapMarkerRadius.CandidateFor(points, FullScale));
        Assert.Equal(60, HuntsMapMarkerRadius.CandidateFor(points, 200));
    }

    [Fact]
    public void TheNearestPairCountsWhateverStateItHolds()
    {
        var points = new[]
        {
            Candidate(0f, 0f),
            new HuntsMapMarkerPoint(0f, 60f, HuntsMapMarkerState.Sighted),
            Candidate(0f, 900f),
        };

        Assert.Equal(HuntsMapMarkerRadius.CandidateMinimum, HuntsMapMarkerRadius.CandidateFor(points, FullScale));
    }

    [Fact]
    public void AFateBesideASparseCandidateDoesNotShrinkTheDisc()
    {
        var points = new[]
        {
            Candidate(100f, 100f),
            new HuntsMapMarkerPoint(110f, 100f, HuntsMapMarkerState.FateActive),
            new HuntsMapMarkerPoint(400f, 95f, HuntsMapMarkerState.FateInactive),
            Candidate(400f, 100f),
        };

        Assert.Equal(120, HuntsMapMarkerRadius.CandidateFor(points, FullScale));
    }

    [Fact]
    public void FatesAloneLeaveTheLargestDisc()
    {
        var points = new[]
        {
            new HuntsMapMarkerPoint(100f, 100f, HuntsMapMarkerState.FateActive),
            new HuntsMapMarkerPoint(120f, 100f, HuntsMapMarkerState.FateInactive),
            Candidate(900f, 900f),
        };

        Assert.Equal(HuntsMapMarkerRadius.CandidateMaximum, HuntsMapMarkerRadius.CandidateFor(points, FullScale));
    }

    [Fact]
    public void OverlappingPointsStayAtTheFloor()
    {
        var points = new[] { Candidate(700f, 700f), Candidate(700f, 700f) };

        Assert.Equal(HuntsMapMarkerRadius.CandidateMinimum, HuntsMapMarkerRadius.CandidateFor(points, FullScale));
    }

    [Fact]
    public void PlainCandidatesFollowTheDensityDisc()
    {
        Assert.Equal(64, HuntsMapMarkerRadius.ScaleFor(HuntsMapMarkerState.Candidate, 64));
    }

    [Fact]
    public void TrackedStatesShareTheLargerDisc()
    {
        var tracked = new[]
        {
            HuntsMapMarkerState.Sighted,
            HuntsMapMarkerState.Confirmed,
            HuntsMapMarkerState.ActiveMinion,
            HuntsMapMarkerState.SsSpawn,
        };

        for (var index = 0; index < tracked.Length; index++)
        {
            Assert.Equal(HuntsMapMarkerRadius.TrackedSpot, HuntsMapMarkerRadius.ScaleFor(tracked[index], 64));
        }
    }

    [Fact]
    public void FatesKeepTheirOwnDisc()
    {
        Assert.Equal(HuntsMapMarkerRadius.Fate, HuntsMapMarkerRadius.ScaleFor(HuntsMapMarkerState.FateInactive, 64));
        Assert.Equal(HuntsMapMarkerRadius.Fate, HuntsMapMarkerRadius.ScaleFor(HuntsMapMarkerState.FateActive, 64));
    }

    [Fact]
    public void TrackedSpotsOutgrowTheDensestCandidateDisc()
    {
        Assert.True(HuntsMapMarkerRadius.TrackedSpot > HuntsMapMarkerRadius.CandidateMinimum);
        Assert.True(HuntsMapMarkerRadius.TrackedSpot < HuntsMapMarkerRadius.CandidateMaximum);
    }

    private static HuntsMapMarkerPoint Candidate(float rawX, float rawY) =>
        new(rawX, rawY, HuntsMapMarkerState.Candidate);

    private static HuntsMapMarkerPoint[] Grid(float spacing, int columns, int rows)
    {
        var points = new HuntsMapMarkerPoint[columns * rows];
        for (var rowIndex = 0; rowIndex < rows; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < columns; columnIndex++)
            {
                points[rowIndex * columns + columnIndex] =
                    Candidate(200f + columnIndex * spacing, 200f + rowIndex * spacing);
            }
        }

        return points;
    }
}
