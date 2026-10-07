using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PolygonTests
{
    private static readonly Vector2[] Hexagon =
    {
        new(2f, 0f), new(4f, 1f), new(4f, 3f), new(2f, 4f), new(0f, 3f), new(0f, 1f),
    };

    private static readonly Vector2[] LShape =
    {
        new(0f, 0f), new(2f, 0f), new(2f, 4f), new(5f, 4f), new(5f, 6f), new(0f, 6f),
    };

    private static readonly Vector2[] UShape =
    {
        new(0f, 0f), new(6f, 0f), new(6f, 6f), new(4f, 6f), new(4f, 2f), new(2f, 2f), new(2f, 6f), new(0f, 6f),
    };

    public static TheoryData<int> ShapeIndices => new() { 0, 1, 2 };

    [Theory]
    [MemberData(nameof(ShapeIndices))]
    public void EveryShapeSplitsIntoNMinusTwoTrianglesCoveringItsArea(int shape)
    {
        var polygon = ShapeOf(shape);

        AssertCovers(polygon, Polygon.Triangulate(polygon));
        AssertCovers(Reversed(polygon), Polygon.Triangulate(Reversed(polygon)));
    }

    [Fact]
    public void NoTriangleOfAConcaveShapeLeavesTheShape()
    {
        var triangles = Polygon.Triangulate(UShape);

        for (var index = 0; index < triangles.Length; index += 3)
        {
            var centroid = (UShape[triangles[index]] + UShape[triangles[index + 1]] + UShape[triangles[index + 2]]) / 3f;
            Assert.True(Geometry2D.PointInPolygon(centroid, UShape), $"triangle {index / 3} sits in the notch");
        }
    }

    [Fact]
    public void TheSpanFormWritesIntoCallerBuffersAndRefusesShortOnes()
    {
        Span<int> order = stackalloc int[LShape.Length];
        Span<int> triangles = stackalloc int[Polygon.TriangleIndexCount(LShape.Length)];

        Assert.Equal(12, Polygon.Triangulate(LShape, order, triangles));
        Assert.Equal(Polygon.Triangulate(LShape), triangles.ToArray());
        Assert.Equal(0, Polygon.Triangulate(LShape, order[..2], triangles));
        Assert.Equal(0, Polygon.Triangulate(LShape, order, triangles[..3]));
        Assert.Equal(0, Polygon.TriangleIndexCount(2));
        Assert.Empty(Polygon.Triangulate(LShape.AsSpan(0, 2)));
        Assert.Equal(new[] { 0, 1, 2 }, Polygon.Triangulate(LShape.AsSpan(0, 3)));
    }

    private static Vector2[] ShapeOf(int shape) => shape switch
    {
        0 => Hexagon,
        1 => LShape,
        _ => UShape,
    };

    private static Vector2[] Reversed(Vector2[] polygon)
    {
        var reversed = new Vector2[polygon.Length];
        for (var index = 0; index < polygon.Length; index++)
        {
            reversed[index] = polygon[polygon.Length - 1 - index];
        }

        return reversed;
    }

    private static void AssertCovers(Vector2[] polygon, int[] triangles)
    {
        Assert.Equal((polygon.Length - 2) * 3, triangles.Length);
        var covered = 0f;
        for (var index = 0; index < triangles.Length; index += 3)
        {
            var first = polygon[triangles[index]];
            var second = polygon[triangles[index + 1]];
            var third = polygon[triangles[index + 2]];
            covered += MathF.Abs(Geometry2D.Cross(second - first, third - first)) * 0.5f;
        }

        Assert.Equal(MathF.Abs(Geometry2D.SignedArea(polygon)), covered, 3);
    }
}
