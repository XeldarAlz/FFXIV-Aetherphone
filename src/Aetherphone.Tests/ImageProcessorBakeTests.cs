using Aetherphone.Core.Media;
using Aetherphone.Core.Wallpapers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ImageProcessorBakeTests
{
    private const int Width = 64;
    private const int Height = 48;

    [Fact]
    public void BakesAJpegFromDiskThatRoundtripsCleanly()
    {
        var sourcePath = WriteGradientPng();
        try
        {
            var source = ScalarJpegEncoderTests.BuildGradient(Width, Height);

            var baked = ImageProcessor.BakeJpeg(sourcePath, Width);
            var (decoded, decodedWidth, decodedHeight) = ScalarJpegEncoderTests.Decode(baked.Bytes);

            Assert.Equal(Width, baked.Width);
            Assert.Equal(Height, baked.Height);
            Assert.Equal(Width, decodedWidth);
            Assert.Equal(Height, decodedHeight);
            Assert.True(ScalarJpegEncoderTests.Psnr(source, decoded, Width, Height) >= 38d);
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    [Fact]
    public void BakeJpegScalesWithinTheMaximumDimension()
    {
        var sourcePath = WriteGradientPng();
        try
        {
            var baked = ImageProcessor.BakeJpeg(sourcePath, Width / 2);
            var (_, decodedWidth, decodedHeight) = ScalarJpegEncoderTests.Decode(baked.Bytes);

            Assert.Equal(Width / 2, baked.Width);
            Assert.Equal(Height / 2, baked.Height);
            Assert.Equal(baked.Width, decodedWidth);
            Assert.Equal(baked.Height, decodedHeight);
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    [Fact]
    public void BakeCroppedJpegAppliesTheEditBeforeCropping()
    {
        const int tolerance = 3;
        var sourcePath = WriteGradientPng();
        try
        {
            var mono = PhotoEdit.None.WithLook(PhotoLook.Mono, 1f).RotatedClockwise();

            var baked = ImageProcessor.BakeCroppedJpeg(sourcePath, WallpaperCrop.Cover, Height, Width, false, mono);
            var (decoded, decodedWidth, decodedHeight) = ScalarJpegEncoderTests.Decode(baked.Bytes);

            Assert.Equal(Height, decodedWidth);
            Assert.Equal(Width, decodedHeight);
            for (var index = 0; index < decoded.Length; index += 4)
            {
                Assert.InRange(decoded[index + 1], decoded[index] - tolerance, decoded[index] + tolerance);
                Assert.InRange(decoded[index + 2], decoded[index] - tolerance, decoded[index] + tolerance);
            }
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    private static string WriteGradientPng()
    {
        var sourcePath = Path.Combine(Path.GetTempPath(), $"aetherphone-test-{Guid.NewGuid():N}.png");
        using var image = Image.LoadPixelData<Rgba32>(ScalarJpegEncoderTests.BuildGradient(Width, Height), Width,
            Height);
        image.SaveAsPng(sourcePath);
        return sourcePath;
    }

    [Fact]
    public void BakeJpegPremultipliesAlphaOntoBlack()
    {
        const int size = 16;
        const byte red = 200;
        const byte green = 100;
        const byte blue = 50;
        const byte alpha = 128;
        const int tolerance = 4;
        var expectedRed = (byte)MathF.Round(red * (alpha / 255f));
        var expectedGreen = (byte)MathF.Round(green * (alpha / 255f));
        var expectedBlue = (byte)MathF.Round(blue * (alpha / 255f));
        var sourcePath = Path.Combine(Path.GetTempPath(), $"aetherphone-test-{Guid.NewGuid():N}.png");
        var pixels = new byte[size * size * 4];
        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = red;
            pixels[index + 1] = green;
            pixels[index + 2] = blue;
            pixels[index + 3] = alpha;
        }
        using (var image = Image.LoadPixelData<Rgba32>(pixels, size, size))
        {
            image.SaveAsPng(sourcePath);
        }
        try
        {
            var baked = ImageProcessor.BakeJpeg(sourcePath, size);
            var (decoded, decodedWidth, decodedHeight) = ScalarJpegEncoderTests.Decode(baked.Bytes);
            Assert.Equal(size, decodedWidth);
            Assert.Equal(size, decodedHeight);
            for (var index = 0; index < decoded.Length; index += 4)
            {
                Assert.InRange(decoded[index], expectedRed - tolerance, expectedRed + tolerance);
                Assert.InRange(decoded[index + 1], expectedGreen - tolerance, expectedGreen + tolerance);
                Assert.InRange(decoded[index + 2], expectedBlue - tolerance, expectedBlue + tolerance);
            }
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }
}
