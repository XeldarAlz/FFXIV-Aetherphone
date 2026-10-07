using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Xunit;

namespace Aetherphone.Tests;

public sealed class Geometry2DTests
{
    private static readonly Vector2[] Square =
    {
        new(0f, 0f), new(4f, 0f), new(4f, 4f), new(0f, 4f),
    };

    private static readonly Vector2[] Notched =
    {
        new(0f, 0f), new(6f, 0f), new(6f, 6f), new(4f, 6f), new(4f, 2f), new(2f, 2f), new(2f, 6f), new(0f, 6f),
    };

    [Fact]
    public void TheClosestPointClampsToTheSegmentEnds()
    {
        var from = new Vector2(0f, 0f);
        var to = new Vector2(10f, 0f);

        Assert.Equal(new Vector2(4f, 0f), Geometry2D.ClosestPoint(new Vector2(4f, 3f), from, to));
        Assert.Equal(from, Geometry2D.ClosestPoint(new Vector2(-5f, 2f), from, to));
        Assert.Equal(to, Geometry2D.ClosestPoint(new Vector2(15f, -2f), from, to));
        Assert.Equal(0.4f, Geometry2D.ClosestAlong(new Vector2(4f, 3f), from, to), 4);
        Assert.Equal(from, Geometry2D.ClosestPoint(new Vector2(3f, 3f), from, from));
        Assert.Equal(0f, Geometry2D.ClosestAlong(new Vector2(3f, 3f), from, from));
    }

    [Fact]
    public void SegmentDistanceMeasuresToTheNearestPointOfTheSegment()
    {
        var from = new Vector2(0f, 0f);
        var to = new Vector2(10f, 0f);

        Assert.Equal(3f, Geometry2D.SegmentDistance(new Vector2(4f, 3f), from, to), 4);
        Assert.Equal(5f, Geometry2D.SegmentDistance(new Vector2(-3f, 4f), from, to), 4);
        Assert.Equal(0f, Geometry2D.SegmentDistance(new Vector2(10f, 0f), from, to), 4);
    }

    [Fact]
    public void ChainDistanceClosesTheLoopOnlyWhenAsked()
    {
        var point = new Vector2(-1f, 2f);

        Assert.Equal(1f, Geometry2D.ChainDistance(point, Square, true), 4);
        Assert.Equal(MathF.Sqrt(5f), Geometry2D.ChainDistance(point, Square, false), 4);
        Assert.Equal(float.MaxValue, Geometry2D.ChainDistance(point, Square.AsSpan(0, 1), false));
    }

    [Fact]
    public void ASegmentHitsACircleItCrossesTouchesOrEndsIn()
    {
        var center = new Vector2(4f, 4f);

        Assert.True(Geometry2D.SegmentCircle(new Vector2(2f, 4f), new Vector2(6f, 4f), center, 0.5f));
        Assert.True(Geometry2D.SegmentCircle(new Vector2(2f, 4.5f), new Vector2(6f, 4.5f), center, 0.5f));
        Assert.True(Geometry2D.SegmentCircle(new Vector2(3.8f, 4f), new Vector2(4.1f, 4f), center, 0.5f));
        Assert.True(Geometry2D.SegmentCircle(new Vector2(4f, 4.2f), new Vector2(4f, 4.2f), center, 0.5f));
        Assert.False(Geometry2D.SegmentCircle(new Vector2(2f, 4.6f), new Vector2(6f, 4.6f), center, 0.5f));
        Assert.False(Geometry2D.SegmentCircle(new Vector2(1f, 4f), new Vector2(3.4f, 4f), center, 0.5f));
    }

    [Fact]
    public void ASweptCircleReportsWhereItFirstTouches()
    {
        var center = new Vector2(10f, 0f);

        Assert.True(Geometry2D.SweepCircle(Vector2.Zero, new Vector2(20f, 0f), center, 2f, out var along));
        Assert.Equal(0.4f, along, 4);
        Assert.True(Geometry2D.SweepCircle(new Vector2(9f, 0f), new Vector2(5f, 0f), center, 2f, out var inside));
        Assert.Equal(0f, inside);
        Assert.False(Geometry2D.SweepCircle(Vector2.Zero, new Vector2(5f, 0f), center, 2f, out _));
        Assert.False(Geometry2D.SweepCircle(Vector2.Zero, new Vector2(-20f, 0f), center, 2f, out _));
        Assert.False(Geometry2D.SweepCircle(Vector2.Zero, new Vector2(20f, 0f), new Vector2(10f, 3f), 2f, out _));
        Assert.True(Geometry2D.SweepCircle(Vector2.Zero, new Vector2(20f, 0f), new Vector2(10f, 2f), 2f, out var grazing));
        Assert.Equal(0.5f, grazing, 3);
        Assert.False(Geometry2D.SweepCircle(Vector2.Zero, Vector2.Zero, center, 2f, out _));
    }

    [Fact]
    public void SegmentsCrossOnlyWithinBothSpans()
    {
        Assert.True(Geometry2D.SegmentSegment(new Vector2(0f, 0f), new Vector2(4f, 4f), new Vector2(0f, 4f),
            new Vector2(4f, 0f), out var along));
        Assert.Equal(0.5f, along, 4);
        Assert.True(Geometry2D.SegmentSegment(new Vector2(0f, 0f), new Vector2(4f, 0f), new Vector2(4f, -2f),
            new Vector2(4f, 2f)));
        Assert.True(Geometry2D.SegmentSegment(new Vector2(0f, 0f), new Vector2(4f, 0f), new Vector2(2f, 0f),
            new Vector2(2f, 3f)));
        Assert.False(Geometry2D.SegmentSegment(new Vector2(0f, 0f), new Vector2(4f, 0f), new Vector2(5f, -2f),
            new Vector2(5f, 2f)));
        Assert.False(Geometry2D.SegmentSegment(new Vector2(0f, 0f), new Vector2(4f, 0f), new Vector2(0f, 1f),
            new Vector2(4f, 1f)));
        Assert.False(Geometry2D.SegmentSegment(new Vector2(0f, 0f), new Vector2(4f, 0f), new Vector2(2f, 0f),
            new Vector2(6f, 0f), out var collinear));
        Assert.Equal(0f, collinear);
    }

    [Fact]
    public void PointsInsideAConcaveNotchAreOutsideThePolygon()
    {
        Assert.True(Geometry2D.PointInPolygon(new Vector2(2f, 2f), Square));
        Assert.False(Geometry2D.PointInPolygon(new Vector2(5f, 2f), Square));
        Assert.False(Geometry2D.PointInPolygon(new Vector2(-0.5f, 2f), Square));
        Assert.True(Geometry2D.PointInPolygon(new Vector2(1f, 5f), Notched));
        Assert.True(Geometry2D.PointInPolygon(new Vector2(5f, 5f), Notched));
        Assert.True(Geometry2D.PointInPolygon(new Vector2(3f, 1f), Notched));
        Assert.False(Geometry2D.PointInPolygon(new Vector2(3f, 4f), Notched));
        Assert.False(Geometry2D.PointInPolygon(new Vector2(3f, 7f), Notched));
    }

    [Fact]
    public void SignedAreaFollowsTheWindingAndInTriangleIncludesTheEdges()
    {
        Assert.Equal(16f, Geometry2D.SignedArea(Square), 4);
        var reversed = new[] { Square[3], Square[2], Square[1], Square[0] };
        Assert.Equal(-16f, Geometry2D.SignedArea(reversed), 4);
        Assert.Equal(-12f, Geometry2D.Cross(new Vector2(0f, 3f), new Vector2(4f, 0f)), 4);

        var first = new Vector2(0f, 0f);
        var second = new Vector2(4f, 0f);
        var third = new Vector2(0f, 4f);
        Assert.True(Geometry2D.InTriangle(new Vector2(1f, 1f), first, second, third));
        Assert.True(Geometry2D.InTriangle(new Vector2(2f, 0f), first, second, third));
        Assert.False(Geometry2D.InTriangle(new Vector2(3f, 3f), first, second, third));
    }
}
