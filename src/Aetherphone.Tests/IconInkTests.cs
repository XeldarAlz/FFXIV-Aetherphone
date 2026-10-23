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

        Assert.True(IconInk.TryMeasure(pixels, 1, 5, 0, 0, 5, 1, Vector2.One, out var box));
        Assert.Equal((1.498f + 4f) * 0.5f / 5f, box.Centre.X, 3);
    }

    [Fact]
    public void ReportsNothingForAnEmptyGlyph()
    {
        var pixels = new byte[16];

        Assert.False(IconInk.TryMeasure(pixels, 1, 4, 0, 0, 4, 4, Vector2.One, out var box));
        Assert.Equal(IconInk.BoxCentre, box.Centre);
    }

    [Fact]
    public void ReadsTheAlphaChannelOfColorPixels()
    {
        var pixels = new uint[]
        {
            0x00FFFFFF, 0x00FFFFFF, 0xFFFFFFFF,
            0x00FFFFFF, 0x00FFFFFF, 0xFFFFFFFF,
        };

        Assert.True(IconInk.TryMeasure(MemoryMarshal.AsBytes(pixels.AsSpan()), 4, 3, 0, 0, 3, 2, Vector2.One,
            out var box));
        Assert.Equal(2.5f / 3f, box.Centre.X, 3);
        Assert.Equal(0.5f, box.Centre.Y, 3);
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

        Assert.True(IconInk.TryMeasure(Pixels(rows), 1, rows[0].Length, 1, 1, 5, 3, Vector2.One, out var box));
        Assert.Equal(3.5f / 5f, box.Centre.X, 3);
        Assert.Equal(0.5f, box.Centre.Y, 3);
    }

    [Fact]
    public void MeasuresInkSizeAreaAndEdgeInEms()
    {
        var box = Measure(0.25f,
            "......",
            ".####.",
            ".####.",
            "......");

        Assert.Equal(1f, box.Size.X, 3);
        Assert.Equal(0.5f, box.Size.Y, 3);
        Assert.Equal(0.5f, box.Area, 3);
        Assert.InRange(box.Edge, 2.8f, 3f);
    }

    [Fact]
    public void LeavesATablerOutlineAtItsDesignedSize()
    {
        var box = new IconInkBox(IconInk.BoxCentre, new Vector2(20f / 24f), Outline(20f / 24f, 2f / 24f),
            OutlineEdge(20f / 24f, 2f / 24f));

        Assert.Equal(1f, IconInk.OpticalScale(box, true), 3);
    }

    [Fact]
    public void ScalesAnOversizedGlyphToTheTablerOpticalSize()
    {
        var box = new IconInkBox(IconInk.BoxCentre, new Vector2(1f), Outline(1f, 2f / 24f), OutlineEdge(1f, 2f / 24f));

        Assert.Equal(IconInk.OpticalSize, IconInk.OpticalScale(box, false), 3);
    }

    [Fact]
    public void KeepsAWideGlyphInsideTheTablerGrid()
    {
        var box = new IconInkBox(IconInk.BoxCentre, new Vector2(1.25f, 0.5f), Outline(0.5f, 2f / 24f),
            OutlineEdge(0.5f, 2f / 24f) * 2f);

        Assert.Equal(IconInk.MaximumExtent / 1.25f, IconInk.OpticalScale(box, false), 3);
    }

    [Fact]
    public void NeverGrowsASmallGlyph()
    {
        var box = new IconInkBox(IconInk.BoxCentre, new Vector2(0.5f), Outline(0.5f, 2f / 24f),
            OutlineEdge(0.5f, 2f / 24f));

        Assert.Equal(1f, IconInk.OpticalScale(box, false), 3);
    }

    [Fact]
    public void DrawsASolidGlyphSlightlySmaller()
    {
        var side = 20f / 24f;
        var box = new IconInkBox(IconInk.BoxCentre, new Vector2(side), side * side, 4f * side);

        Assert.Equal(1f - IconInk.SolidAllowance, IconInk.OpticalScale(box, true), 3);
    }

    [Fact]
    public void ReadsAThinBarAsAStrokeNotASolid()
    {
        var box = new IconInkBox(IconInk.BoxCentre, new Vector2(16f / 24f, 2f / 24f), 32f / 576f, 36f / 24f);

        Assert.Equal(1f, IconInk.OpticalScale(box, true), 3);
    }

    private static float Outline(float side, float stroke) => side * side - (side - 2f * stroke) * (side - 2f * stroke);

    private static float OutlineEdge(float side, float stroke) => 4f * side + 4f * (side - 2f * stroke);

    private static Vector2 Find(params string[] rows) => Measure(1f, rows).Centre;

    private static IconInkBox Measure(float texel, params string[] rows)
    {
        Assert.True(IconInk.TryMeasure(Pixels(rows), 1, rows[0].Length, 0, 0, rows[0].Length, rows.Length,
            new Vector2(texel), out var box));
        return box;
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
