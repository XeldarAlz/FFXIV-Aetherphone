using Aetherphone.Apps.Skywatcher.Sky;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SkyNoiseTests
{
    [Fact]
    public void PerlinRepeatsAcrossItsPeriod()
    {
        const int period = 6;
        for (var sample = 0; sample < 50; sample++)
        {
            var x = sample * 0.37f;
            var y = sample * 0.53f;
            Assert.Equal(SkyNoise.Perlin(x, y, period, 11), SkyNoise.Perlin(x + period, y, period, 11), 4);
            Assert.Equal(SkyNoise.Perlin(x, y, period, 11), SkyNoise.Perlin(x, y + period, period, 11), 4);
        }
    }

    [Fact]
    public void BakedTexturesTileWithoutVisibleSeams()
    {
        AssertSeamless(SkyTextures.BakeMist(), SkyTextures.NoiseSize, SkyTextures.NoiseSize);
        AssertSeamless(SkyTextures.BakeBillow(), SkyTextures.NoiseSize, SkyTextures.NoiseSize);
    }

    [Fact]
    public void GlowFadesToNothingAtItsEdge()
    {
        var pixels = SkyTextures.BakeGlow();
        var size = SkyTextures.GlowSize;
        var center = (size / 2 * size + size / 2) * 4 + 3;
        Assert.True(pixels[center] > 200);
        for (var index = 0; index < size; index++)
        {
            Assert.True(pixels[index * 4 + 3] <= 2);
            Assert.True(pixels[(index * size) * 4 + 3] <= 2);
        }
    }

    private static void AssertSeamless(byte[] pixels, int width, int height)
    {
        var columnSteps = new float[width];
        var rowSteps = new float[height];
        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                var alpha = Alpha(pixels, width, column, row);
                columnSteps[column] += MathF.Abs(alpha - Alpha(pixels, width, (column + 1) % width, row));
                rowSteps[row] += MathF.Abs(alpha - Alpha(pixels, width, column, (row + 1) % height));
            }
        }

        Assert.True(columnSteps[width - 1] <= Largest(columnSteps, width - 1) * 1.1f);
        Assert.True(rowSteps[height - 1] <= Largest(rowSteps, height - 1) * 1.1f);
    }

    private static float Largest(float[] values, int count)
    {
        var largest = 0f;
        for (var index = 0; index < count; index++)
        {
            largest = MathF.Max(largest, values[index]);
        }

        return largest;
    }

    private static float Alpha(byte[] pixels, int width, int column, int row) => pixels[(row * width + column) * 4 + 3];
}
