using System.Numerics;
using Aetherphone.Apps.Music.Widgets;
using Aetherphone.Core.Theme;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ArtworkWashTests
{
    [Fact]
    public void Average_OfOneColourIsThatColour()
    {
        var pixels = new byte[] { 200, 40, 40, 255, 200, 40, 40, 255 };

        var average = ArtworkWash.Average(pixels);

        Assert.Equal(200f / 255f, average.X, 3);
        Assert.Equal(40f / 255f, average.Y, 3);
        Assert.Equal(1f, average.W);
    }

    [Fact]
    public void Average_IgnoresTransparentPixels()
    {
        var pixels = new byte[] { 0, 0, 255, 255, 255, 0, 0, 0 };

        var average = ArtworkWash.Average(pixels);

        Assert.Equal(0f, average.X, 3);
        Assert.Equal(1f, average.Z, 3);
    }

    [Fact]
    public void Average_LeansTowardsVividColours()
    {
        var pixels = new byte[] { 128, 128, 128, 255, 255, 0, 0, 255 };

        var average = ArtworkWash.Average(pixels);

        Assert.True(average.X > 0.75f);
    }

    [Fact]
    public void Gradient_StaysDarkEnoughForWhiteText()
    {
        var white = new Vector4(1f, 1f, 1f, 1f);

        Assert.True(Palette.RelativeLuminance(ArtworkWash.Top(white)) <= ArtworkWash.TopLuminance + 0.01f);
        Assert.True(Palette.RelativeLuminance(ArtworkWash.Bottom(white)) <= ArtworkWash.BottomLuminance + 0.01f);
    }
}
