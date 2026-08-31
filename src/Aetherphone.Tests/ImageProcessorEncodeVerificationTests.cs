using Aetherphone.Core.Media;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ImageProcessorEncodeVerificationTests
{
    private const int Width = 64;
    private const int Height = 64;

    [Fact]
    public void PassesACleanRoundtripOfAPhotographicImage()
    {
        var source = BuildGradient();
        var decoded = EncodeAndDecode(source);

        Assert.False(ImageProcessor.HasEncodeCorruption(source, decoded, Width, Height));
    }

    [Fact]
    public void PassesTheChromaLossOfSharpColoredDetail()
    {
        var source = BuildWhiteWithSaturatedLines();
        var decoded = EncodeAndDecode(source);

        Assert.False(ImageProcessor.HasEncodeCorruption(source, decoded, Width, Height));
    }

    [Fact]
    public void FlagsALumaDashShiftedByPlus128()
    {
        var source = BuildFlat(10, 10, 14);
        var decoded = (byte[])source.Clone();
        for (var dashIndex = 0; dashIndex < 4; dashIndex++)
        {
            var pixelIndex = ((16 * Width) + 20 + dashIndex) * 4;
            decoded[pixelIndex] = (byte)Math.Min(255, decoded[pixelIndex] + 128);
            decoded[pixelIndex + 1] = (byte)Math.Min(255, decoded[pixelIndex + 1] + 128);
            decoded[pixelIndex + 2] = (byte)Math.Min(255, decoded[pixelIndex + 2] + 128);
        }

        Assert.True(ImageProcessor.HasEncodeCorruption(source, decoded, Width, Height));
    }

    [Fact]
    public void FlagsAChromaDashShiftedByPlus128()
    {
        var source = BuildFlat(128, 128, 128);
        var decoded = (byte[])source.Clone();
        for (var rowOffset = 0; rowOffset < 2; rowOffset++)
        {
            for (var dashIndex = 0; dashIndex < 8; dashIndex++)
            {
                var pixelIndex = (((16 + rowOffset) * Width) + 24 + dashIndex) * 4;
                decoded[pixelIndex] = 128;
                decoded[pixelIndex + 1] = 84;
                decoded[pixelIndex + 2] = 255;
            }
        }

        Assert.True(ImageProcessor.HasEncodeCorruption(source, decoded, Width, Height));
    }

    [Fact]
    public void FlagsATwoPixelDashOnAFlatArea()
    {
        var source = BuildFlat(10, 10, 14);
        var decoded = (byte[])source.Clone();
        for (var dashIndex = 0; dashIndex < 2; dashIndex++)
        {
            var pixelIndex = ((16 * Width) + 20 + dashIndex) * 4;
            decoded[pixelIndex] = (byte)Math.Min(255, decoded[pixelIndex] + 128);
            decoded[pixelIndex + 1] = (byte)Math.Min(255, decoded[pixelIndex + 1] + 128);
            decoded[pixelIndex + 2] = (byte)Math.Min(255, decoded[pixelIndex + 2] + 128);
        }

        Assert.True(ImageProcessor.HasEncodeCorruption(source, decoded, Width, Height));
    }

    [Fact]
    public void FlagsASingleSaturatedSpeckleOnAFlatArea()
    {
        var source = BuildFlat(128, 128, 128);
        var decoded = (byte[])source.Clone();
        var pixelIndex = ((20 * Width) + 32) * 4;
        decoded[pixelIndex] = 0;
        decoded[pixelIndex + 1] = 0;
        decoded[pixelIndex + 2] = 255;

        Assert.True(ImageProcessor.HasEncodeCorruption(source, decoded, Width, Height));
    }

    [Fact]
    public void FlagsARedSpeckleTheLumaCheckCannotSee()
    {
        var source = BuildFlat(128, 128, 128);
        var decoded = (byte[])source.Clone();
        var pixelIndex = ((20 * Width) + 32) * 4;
        decoded[pixelIndex] = 255;
        decoded[pixelIndex + 1] = 0;
        decoded[pixelIndex + 2] = 0;

        Assert.True(ImageProcessor.HasEncodeCorruption(source, decoded, Width, Height));
    }

    [Fact]
    public void IgnoresASpeckleSizedDeltaInsideBusyDetail()
    {
        var source = BuildCheckerboard();
        var decoded = (byte[])source.Clone();
        var pixelIndex = ((20 * Width) + 32) * 4;
        decoded[pixelIndex] = (byte)Math.Min(255, decoded[pixelIndex] + 128);
        decoded[pixelIndex + 1] = (byte)Math.Min(255, decoded[pixelIndex + 1] + 128);
        decoded[pixelIndex + 2] = (byte)Math.Min(255, decoded[pixelIndex + 2] + 128);

        Assert.False(ImageProcessor.HasEncodeCorruption(source, decoded, Width, Height));
    }

    [Fact]
    public void BakesAVerifiedJpegFromDisk()
    {
        var sourcePath = Path.Combine(Path.GetTempPath(), $"aetherphone-test-{Guid.NewGuid():N}.png");
        try
        {
            using (var image = Image.LoadPixelData<Rgba32>(BuildGradient(), Width, Height))
            {
                image.SaveAsPng(sourcePath);
            }

            var baked = ImageProcessor.Bake(sourcePath, Width);

            Assert.NotEmpty(baked.Bytes);
            Assert.Equal(Width, baked.Width);
            Assert.Equal(Height, baked.Height);
            Assert.Equal(ImageProcessor.JpegContentType, baked.ContentType);
            using var roundtrip = Image.Load<Rgba32>(baked.Bytes);
            var decodedPixels = new byte[Width * Height * 4];
            roundtrip.CopyPixelDataTo(decodedPixels);
            Assert.False(ImageProcessor.HasEncodeCorruption(BuildGradient(), decodedPixels, Width, Height));
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    private static byte[] BuildFlat(byte red, byte green, byte blue)
    {
        var pixels = new byte[Width * Height * 4];
        for (var pixelIndex = 0; pixelIndex < pixels.Length; pixelIndex += 4)
        {
            pixels[pixelIndex] = red;
            pixels[pixelIndex + 1] = green;
            pixels[pixelIndex + 2] = blue;
            pixels[pixelIndex + 3] = 255;
        }

        return pixels;
    }

    private static byte[] BuildCheckerboard()
    {
        var pixels = new byte[Width * Height * 4];
        for (var pixelY = 0; pixelY < Height; pixelY++)
        {
            for (var pixelX = 0; pixelX < Width; pixelX++)
            {
                var pixelIndex = ((pixelY * Width) + pixelX) * 4;
                var shade = (pixelX + pixelY) % 2 == 0 ? (byte)0 : (byte)255;
                pixels[pixelIndex] = shade;
                pixels[pixelIndex + 1] = shade;
                pixels[pixelIndex + 2] = shade;
                pixels[pixelIndex + 3] = 255;
            }
        }

        return pixels;
    }

    private static byte[] BuildGradient()
    {
        var pixels = new byte[Width * Height * 4];
        for (var pixelY = 0; pixelY < Height; pixelY++)
        {
            for (var pixelX = 0; pixelX < Width; pixelX++)
            {
                var pixelIndex = ((pixelY * Width) + pixelX) * 4;
                pixels[pixelIndex] = (byte)(pixelX * 4);
                pixels[pixelIndex + 1] = (byte)(pixelY * 4);
                pixels[pixelIndex + 2] = (byte)(255 - (pixelX * 2));
                pixels[pixelIndex + 3] = 255;
            }
        }

        return pixels;
    }

    private static byte[] BuildWhiteWithSaturatedLines()
    {
        var pixels = BuildFlat(255, 255, 255);
        for (var pixelX = 0; pixelX < Width; pixelX++)
        {
            var redLineIndex = ((10 * Width) + pixelX) * 4;
            pixels[redLineIndex] = 255;
            pixels[redLineIndex + 1] = 0;
            pixels[redLineIndex + 2] = 0;

            var blueLineIndex = ((30 * Width) + pixelX) * 4;
            pixels[blueLineIndex] = 0;
            pixels[blueLineIndex + 1] = 0;
            pixels[blueLineIndex + 2] = 255;
        }

        return pixels;
    }

    private static byte[] EncodeAndDecode(byte[] sourceRgba)
    {
        using var image = Image.LoadPixelData<Rgba32>(sourceRgba, Width, Height);
        using var stream = new MemoryStream();
        image.SaveAsJpeg(stream, new JpegEncoder { Quality = 88 });
        stream.Position = 0;
        using var decoded = Image.Load<Rgba32>(stream);
        var decodedPixels = new byte[Width * Height * 4];
        decoded.CopyPixelDataTo(decodedPixels);
        return decodedPixels;
    }
}
