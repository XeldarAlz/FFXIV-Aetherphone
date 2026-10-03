using System.Numerics;
using Aetherphone.Core.Media;
using Xunit;

namespace Aetherphone.Tests;

public sealed class IconBakeTests
{
    private const int Side = 16;
    private static readonly Vector4 Accent = new(0.13f, 0.66f, 0.22f, 1f);

    [Fact]
    public void AWhiteSubjectOnTransparencyCountsAsWhite()
    {
        Assert.True(IconBake.IsWhiteForeground(Subject(255, 255, 255)));
    }

    [Fact]
    public void ANearWhiteSubjectStillCountsAsWhite()
    {
        Assert.True(IconBake.IsWhiteForeground(Subject(248, 248, 250)));
    }

    [Fact]
    public void AColouredSubjectDoesNotCountAsWhite()
    {
        Assert.False(IconBake.IsWhiteForeground(Subject(240, 90, 60)));
    }

    [Fact]
    public void TransparentPixelsDoNotVoteOnWhiteness()
    {
        var foreground = Subject(255, 255, 255);
        PaintCorner(foreground, 0, 0, 0, 0);
        Assert.True(IconBake.IsWhiteForeground(foreground));
    }

    [Fact]
    public void AnEmptyForegroundIsNotWhite()
    {
        Assert.False(IconBake.IsWhiteForeground(PixelImage.Empty));
        Assert.False(IconBake.IsWhiteForeground(Transparent()));
    }

    [Fact]
    public void DarkBakeRecoloursAWhiteSubjectWithTheAccent()
    {
        var baked = IconBake.BakeDark(Subject(255, 255, 255), Accent);

        var (red, green, blue, alpha) = PixelAt(baked, Side / 2, Side / 2);
        Assert.Equal(255, alpha);
        Assert.InRange(red, Channel(Accent.X) - 1, Channel(Accent.X) + 1);
        Assert.InRange(green, Channel(Accent.Y) - 1, Channel(Accent.Y) + 1);
        Assert.InRange(blue, Channel(Accent.Z) - 1, Channel(Accent.Z) + 1);
    }

    [Fact]
    public void DarkBakeKeepsAColouredSubjectAsPainted()
    {
        var baked = IconBake.BakeDark(Subject(240, 90, 60), Accent);

        var (red, green, blue, _) = PixelAt(baked, Side / 2, Side / 2);
        Assert.Equal(240, red);
        Assert.Equal(90, green);
        Assert.Equal(60, blue);
    }

    [Fact]
    public void DarkBakeFillsTheBackgroundWithTheGraphiteGradient()
    {
        var baked = IconBake.BakeDark(Subject(255, 255, 255), Accent);

        var (topRed, _, _, topAlpha) = PixelAt(baked, 0, 0);
        var (bottomRed, _, _, bottomAlpha) = PixelAt(baked, 0, Side - 1);
        Assert.Equal(255, topAlpha);
        Assert.Equal(255, bottomAlpha);
        Assert.Equal(Channel(IconBake.GraphiteTop.X), topRed);
        Assert.Equal(Channel(IconBake.GraphiteBottom.X), bottomRed);
        Assert.True(topRed > bottomRed, "the graphite tile is lit from the top");
    }

    [Fact]
    public void TintedBakeFillsTheSubjectWithTheAccentOverGraphite()
    {
        var baked = IconBake.BakeTinted(Subject(255, 255, 255), Accent, false);

        var (red, green, blue, alpha) = PixelAt(baked, Side / 2, Side / 2);
        Assert.Equal(255, alpha);
        Assert.InRange(red, Channel(Accent.X) - 1, Channel(Accent.X) + 1);
        Assert.InRange(green, Channel(Accent.Y) - 1, Channel(Accent.Y) + 1);
        Assert.InRange(blue, Channel(Accent.Z) - 1, Channel(Accent.Z) + 1);
        var (cornerRed, _, _, _) = PixelAt(baked, 0, 0);
        Assert.Equal(Channel(IconBake.GraphiteTop.X), cornerRed);
    }

    [Fact]
    public void TintedBakeUsesTheSubjectLuminanceAsTheMask()
    {
        var baked = IconBake.BakeTinted(Subject(128, 128, 128), Accent, false);

        var green = PixelAt(baked, Side / 2, Side / 2).Green;
        var full = Channel(Accent.Y);
        var graphite = Channel(IconBake.GraphiteTop.Y);
        Assert.True(green < full && green > graphite, $"a half-luminance subject blends halfway, got {green}");
    }

    [Fact]
    public void TintedLightBakeSitsOnPaperWithADarkenedAccent()
    {
        var baked = IconBake.BakeTinted(Subject(255, 255, 255), Accent, true);

        var (cornerRed, cornerGreen, cornerBlue, _) = PixelAt(baked, 0, 0);
        Assert.Equal(255, cornerRed);
        Assert.Equal(255, cornerGreen);
        Assert.Equal(255, cornerBlue);
        var (_, _, bottomBlue, _) = PixelAt(baked, 0, Side - 1);
        Assert.Equal(Channel(IconBake.PaperBottom.Z), bottomBlue);
        var (_, green, _, _) = PixelAt(baked, Side / 2, Side / 2);
        Assert.True(green < Channel(Accent.Y), "the light tint darkens the accent so it reads on paper");
    }

    [Fact]
    public void MaskBakeIsWhiteWithLuminanceTimesAlphaAsCoverage()
    {
        var foreground = Subject(128, 128, 128);
        var baked = IconBake.BakeMask(foreground);

        var (red, green, blue, alpha) = PixelAt(baked, Side / 2, Side / 2);
        Assert.Equal(255, red);
        Assert.Equal(255, green);
        Assert.Equal(255, blue);
        Assert.InRange(alpha, 127, 129);
        var (_, _, _, cornerAlpha) = PixelAt(baked, 0, 0);
        Assert.Equal(0, cornerAlpha);
    }

    [Fact]
    public void BakesKeepTheForegroundDimensions()
    {
        var foreground = Subject(255, 255, 255);

        Assert.Equal(Side, IconBake.BakeDark(foreground, Accent).Width);
        Assert.Equal(Side, IconBake.BakeTinted(foreground, Accent, false).Height);
        Assert.Equal(foreground.Length, IconBake.BakeMask(foreground).Pixels.Length);
    }

    private static PixelImage Transparent() => new(new byte[Side * Side * PixelImage.BytesPerPixel], Side, Side);

    private static PixelImage Subject(byte red, byte green, byte blue)
    {
        var image = Transparent();
        var inset = Side / 4;
        for (var row = inset; row < Side - inset; row++)
        {
            for (var column = inset; column < Side - inset; column++)
            {
                var offset = (row * Side + column) * PixelImage.BytesPerPixel;
                image.Pixels[offset] = red;
                image.Pixels[offset + 1] = green;
                image.Pixels[offset + 2] = blue;
                image.Pixels[offset + 3] = 255;
            }
        }

        return image;
    }

    private static void PaintCorner(in PixelImage image, byte red, byte green, byte blue, byte alpha)
    {
        var offset = 0;
        image.Pixels[offset] = red;
        image.Pixels[offset + 1] = green;
        image.Pixels[offset + 2] = blue;
        image.Pixels[offset + 3] = alpha;
    }

    private static (int Red, int Green, int Blue, int Alpha) PixelAt(in PixelImage image, int column, int row)
    {
        var offset = (row * image.Width + column) * PixelImage.BytesPerPixel;
        return (image.Pixels[offset], image.Pixels[offset + 1], image.Pixels[offset + 2], image.Pixels[offset + 3]);
    }

    private static int Channel(float unit) => (int)MathF.Round(unit * 255f);
}
