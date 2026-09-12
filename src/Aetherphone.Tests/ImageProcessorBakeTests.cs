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
}
