using Aetherphone.Apps.Music;
using Aetherphone.Apps.Music.NowPlaying;
using System.Numerics;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ArtworkPaletteTests
{
    private const float Tolerance = 0.03f;
    private const int Side = 20;

    private static readonly byte[] Red = [220, 30, 30, 255];
    private static readonly byte[] Green = [30, 200, 60, 255];
    private static readonly byte[] Blue = [40, 60, 210, 255];
    private static readonly byte[] Yellow = [230, 210, 40, 255];
    private static readonly byte[] Black = [0, 0, 0, 255];

    [Fact]
    public void FindsFourBlocksOrderedByArea()
    {
        var pixels = new byte[Side * Side * 4];
        for (var y = 0; y < Side; y++)
        {
            for (var x = 0; x < Side; x++)
            {
                var color = x < 10 ? Red : y < 10 ? Green : x < 16 ? Blue : Yellow;
                Write(pixels, y * Side + x, color);
            }
        }

        var swatch = ArtworkPalette.Extract(pixels, Side, Side);

        AssertColor(Red, swatch.Primary);
        AssertColor(Green, swatch.Secondary);
        AssertColor(Blue, swatch.Tertiary);
        AssertColor(Yellow, swatch.Quaternary);
    }

    [Fact]
    public void IgnoresLetterboxBarsWhenColourIsPresent()
    {
        var pixels = new byte[Side * Side * 4];
        for (var index = 0; index < Side * Side; index++)
        {
            var row = index / Side;
            Write(pixels, index, row < 6 || row >= 14 ? Black : Blue);
        }

        var swatch = ArtworkPalette.Extract(pixels, Side, Side);

        for (var rank = 0; rank < ArtworkPalette.ColorCount; rank++)
        {
            AssertColor(Blue, swatch.At(rank));
        }
    }

    [Fact]
    public void KeepsDarkArtworkWhenItIsAllDark()
    {
        var pixels = new byte[Side * Side * 4];
        for (var index = 0; index < Side * Side; index++)
        {
            Write(pixels, index, Black);
        }

        var swatch = ArtworkPalette.Extract(pixels, Side, Side);

        AssertColor(Black, swatch.Primary);
    }

    [Fact]
    public void ReturnsNeutralForEmptyInput()
    {
        var swatch = ArtworkPalette.Extract(ReadOnlySpan<byte>.Empty, 0, 0);

        Assert.Equal(ArtworkPalette.Neutral, swatch);
    }

    private static void Write(byte[] pixels, int pixelIndex, byte[] color)
    {
        Array.Copy(color, 0, pixels, pixelIndex * 4, 4);
    }

    private static void AssertColor(byte[] expected, Vector4 actual)
    {
        Assert.InRange(actual.X, expected[0] / 255f - Tolerance, expected[0] / 255f + Tolerance);
        Assert.InRange(actual.Y, expected[1] / 255f - Tolerance, expected[1] / 255f + Tolerance);
        Assert.InRange(actual.Z, expected[2] / 255f - Tolerance, expected[2] / 255f + Tolerance);
    }
}
