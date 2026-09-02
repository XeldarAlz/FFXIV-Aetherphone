using Aetherphone.Core.Maps;
using Xunit;

namespace Aetherphone.Tests;

public sealed class MapPixelMathTests
{
    private static float ToRawPixel(float worldCoordinate, int sizeFactor, int offset)
    {
        var scale = Math.Max(sizeFactor, 1) / 100f;
        return (worldCoordinate + offset) * scale + MapPixelMath.FullCanvasSize / 2f;
    }

    [Theory]
    [InlineData(1024f, 100, 0)]
    [InlineData(512f, 100, 0)]
    [InlineData(1536f, 200, 0)]
    [InlineData(900f, 140, 25)]
    [InlineData(300f, 50, -400)]
    public void ToWorldCoordinateInvertsTheForwardPixelFormula(float rawPixel, int sizeFactor, int offset)
    {
        var world = MapPixelMath.ToWorldCoordinate(rawPixel, sizeFactor, offset);
        var roundTripped = ToRawPixel(world, sizeFactor, offset);

        Assert.Equal(rawPixel, roundTripped, 3);
    }

    [Fact]
    public void ToWorldCoordinateTreatsCanvasCenterAsTheOffsetOrigin()
    {
        var world = MapPixelMath.ToWorldCoordinate(MapPixelMath.FullCanvasSize / 2f, 100, 0);

        Assert.Equal(0f, world, 3);
    }
}
