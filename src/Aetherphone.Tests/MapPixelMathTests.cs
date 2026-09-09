using Aetherphone.Core.Maps;
using Xunit;

namespace Aetherphone.Tests;

public sealed class MapPixelMathTests
{
    private const float Tolerance = 1e-3f;

    [Theory]
    [InlineData(1024f, 100, 0)]
    [InlineData(512f, 100, 0)]
    [InlineData(1536f, 200, 0)]
    [InlineData(900f, 140, 25)]
    [InlineData(300f, 50, -400)]
    public void ToWorldCoordinateInvertsTheForwardPixelFormula(float rawPixel, int sizeFactor, int offset)
    {
        var world = MapPixelMath.ToWorldCoordinate(rawPixel, sizeFactor, offset);
        var roundTripped = MapPixelMath.ToCanvasPixel(world, sizeFactor, offset);

        Assert.Equal(rawPixel, roundTripped, 3);
    }

    [Fact]
    public void ToWorldCoordinateTreatsCanvasCenterAsTheOffsetOrigin()
    {
        var world = MapPixelMath.ToWorldCoordinate(MapPixelMath.FullCanvasSize / 2f, 100, 0);

        Assert.Equal(0f, world, 3);
    }

    public static TheoryData<float, int, int> Positions()
    {
        var worlds = new[] { -1200f, -400.5f, -12f, 0f, 7.25f, 340f, 1180f };
        var sizeFactors = new[] { 100, 200, 400, 800 };
        var offsets = new[] { -1024, -224, 0, 96, 512 };
        var data = new TheoryData<float, int, int>();
        for (var worldIndex = 0; worldIndex < worlds.Length; worldIndex++)
        {
            for (var sizeIndex = 0; sizeIndex < sizeFactors.Length; sizeIndex++)
            {
                for (var offsetIndex = 0; offsetIndex < offsets.Length; offsetIndex++)
                {
                    data.Add(worlds[worldIndex], sizeFactors[sizeIndex], offsets[offsetIndex]);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Positions))]
    public void CanvasPixelRoundTripsBackToWorld(float world, int sizeFactor, int offset)
    {
        var pixel = MapPixelMath.ToCanvasPixel(world, sizeFactor, offset);
        var roundTripped = MapPixelMath.ToWorldCoordinate(pixel, sizeFactor, offset);

        Assert.InRange(roundTripped - world, -Tolerance, Tolerance);
    }

    [Fact]
    public void MapCentreSitsAtTheCanvasCentre()
    {
        var pixel = MapPixelMath.ToCanvasPixel(-64f, 200, 64);

        Assert.Equal(MapPixelMath.FullCanvasSize * 0.5f, pixel, 3);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(200)]
    [InlineData(400)]
    public void OneYalmMovesTheCanvasByTheMapScale(int sizeFactor)
    {
        const int offset = -180;
        var start = MapPixelMath.ToCanvasPixel(40f, sizeFactor, offset);
        var moved = MapPixelMath.ToCanvasPixel(41f, sizeFactor, offset);

        Assert.Equal(sizeFactor / 100f, moved - start, 3);
    }
}
