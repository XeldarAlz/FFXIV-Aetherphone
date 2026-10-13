using System.Runtime.InteropServices;
using Aetherphone.Core;
using Xunit;

namespace Aetherphone.Tests;

public sealed class GlyphCrestTests
{
    private const char Ink = '#';

    [Fact]
    public void FindsTheStemOfAD()
    {
        var crest = Find(
            "......##",
            "......##",
            "......##",
            "..######",
            ".##...##",
            ".##...##",
            "..######");

        Assert.False(crest.Twin);
        Assert.Equal(7f / 8f, crest.First.X, 3);
        Assert.Equal(0f, crest.First.Y, 3);
    }

    [Fact]
    public void FindsTheStemOfAB()
    {
        var crest = Find(
            "##......",
            "##......",
            "##......",
            "######..",
            "##...##.",
            "##...##.",
            "######..");

        Assert.False(crest.Twin);
        Assert.Equal(1f / 8f, crest.First.X, 3);
    }

    [Fact]
    public void LightsBothArmsOfAnH()
    {
        var crest = Find(
            "##....##",
            "##....##",
            "########",
            "##....##",
            "##....##");

        Assert.True(crest.Twin);
        Assert.Equal(1f / 8f, crest.First.X, 3);
        Assert.Equal(7f / 8f, crest.Second.X, 3);
    }

    [Fact]
    public void CentresOnTheBarOfAT()
    {
        var crest = Find(
            "#########",
            "....#....",
            "....#....",
            "....#....");

        Assert.False(crest.Twin);
        Assert.Equal(0.5f, crest.First.X, 3);
        Assert.Equal(1f, crest.FirstWidth, 3);
    }

    [Fact]
    public void KeepsARoundLetterCentred()
    {
        var crest = Find(
            "..####..",
            ".#....#.",
            "#......#",
            ".#....#.",
            "..####..");

        Assert.False(crest.Twin);
        Assert.Equal(0.5f, crest.First.X, 3);
    }

    [Fact]
    public void IgnoresASmallSecondPeak()
    {
        var crest = Find(
            "#####...........#",
            "#####...........#",
            "#################");

        Assert.False(crest.Twin);
        Assert.Equal(2.5f / 17f, crest.First.X, 3);
    }

    [Fact]
    public void ReportsNothingForAnEmptyGlyph()
    {
        var pixels = new byte[16];
        Assert.False(GlyphCrests.TryFind(pixels, 1, 4, 0, 0, 4, 4, out _));
    }

    [Fact]
    public void ReadsTheAlphaChannelOfColorPixels()
    {
        var pixels = new uint[]
        {
            0x00FFFFFF, 0x00FFFFFF, 0xFFFFFFFF,
            0x00FFFFFF, 0x00FFFFFF, 0xFFFFFFFF,
            0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF,
        };

        Assert.True(GlyphCrests.TryFind(MemoryMarshal.AsBytes(pixels.AsSpan()), 4, 3, 0, 0, 3, 3, out var crest));
        Assert.Equal(2.5f / 3f, crest.First.X, 3);
    }

    [Fact]
    public void ReadsAGlyphInsideALargerAtlas()
    {
        var rows = new[]
        {
            "..........",
            "......#...",
            "......#...",
            "...####...",
            "...#..#...",
            "...####...",
            "..........",
        };
        var stride = rows[0].Length;
        var pixels = Pixels(rows);

        Assert.True(GlyphCrests.TryFind(pixels, 1, stride, 3, 1, 4, 5, out var crest));
        Assert.Equal(3.5f / 4f, crest.First.X, 3);
    }

    private static GlyphCrest Find(params string[] rows)
    {
        Assert.True(GlyphCrests.TryFind(Pixels(rows), 1, rows[0].Length, 0, 0, rows[0].Length, rows.Length,
            out var crest));
        return crest;
    }

    private static byte[] Pixels(string[] rows)
    {
        var columns = rows[0].Length;
        var pixels = new byte[columns * rows.Length];
        for (var row = 0; row < rows.Length; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                pixels[row * columns + column] = rows[row][column] == Ink ? (byte)255 : (byte)0;
            }
        }

        return pixels;
    }
}
