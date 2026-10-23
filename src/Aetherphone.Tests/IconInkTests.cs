using System.Numerics;
using System.Runtime.InteropServices;
using Aetherphone.Core;
using Xunit;

namespace Aetherphone.Tests;

public sealed class IconInkTests
{
    [Fact]
    public void CentresASymmetricGlyphOnItsBox()
    {
        var centre = Find(
            "......",
            ".####.",
            ".####.",
            "......");

        Assert.Equal(0.5f, centre.X, 3);
        Assert.Equal(0.5f, centre.Y, 3);
    }

    [Fact]
    public void FollowsInkThatSitsOffCentre()
    {
        var centre = Find(
            "....##",
            "....##",
            "......",
            "......");

        Assert.Equal(5f / 6f, centre.X, 3);
        Assert.Equal(0.25f, centre.Y, 3);
    }

    [Fact]
    public void ReadsPartialCoverageAsAFractionOfAPixel()
    {
        var pixels = new byte[]
        {
            0, 128, 255, 255, 0,
        };

        Assert.True(IconInk.TryFindCentre(pixels, 1, 5, 0, 0, 5, 1, out var centre));
        Assert.Equal((1.498f + 4f) * 0.5f / 5f, centre.X, 3);
    }

    [Fact]
    public void ReportsNothingForAnEmptyGlyph()
    {
        var pixels = new byte[16];

        Assert.False(IconInk.TryFindCentre(pixels, 1, 4, 0, 0, 4, 4, out var centre));
        Assert.Equal(IconInk.BoxCentre, centre);
    }

    [Fact]
    public void ReadsTheAlphaChannelOfColorPixels()
    {
        var pixels = new uint[]
        {
            0x00FFFFFF, 0x00FFFFFF, 0xFFFFFFFF,
            0x00FFFFFF, 0x00FFFFFF, 0xFFFFFFFF,
        };

        Assert.True(IconInk.TryFindCentre(MemoryMarshal.AsBytes(pixels.AsSpan()), 4, 3, 0, 0, 3, 2, out var centre));
        Assert.Equal(2.5f / 3f, centre.X, 3);
        Assert.Equal(0.5f, centre.Y, 3);
    }

    [Fact]
    public void ReadsAGlyphInsideALargerAtlas()
    {
        var rows = new[]
        {
            "########",
            "#.....##",
            "#...#.##",
            "#.....##",
            "########",
        };

        Assert.True(IconInk.TryFindCentre(Pixels(rows), 1, rows[0].Length, 1, 1, 5, 3, out var centre));
        Assert.Equal(3.5f / 5f, centre.X, 3);
        Assert.Equal(0.5f, centre.Y, 3);
    }

    private static Vector2 Find(params string[] rows)
    {
        Assert.True(IconInk.TryFindCentre(Pixels(rows), 1, rows[0].Length, 0, 0, rows[0].Length, rows.Length,
            out var centre));
        return centre;
    }

    private static byte[] Pixels(string[] rows)
    {
        var columns = rows[0].Length;
        var pixels = new byte[columns * rows.Length];
        for (var row = 0; row < rows.Length; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                pixels[row * columns + column] = rows[row][column] == '#' ? (byte)255 : (byte)0;
            }
        }

        return pixels;
    }
}
