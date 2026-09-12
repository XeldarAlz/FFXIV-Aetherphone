using Aetherphone.Core.Media;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ScalarJpegEncoderTests
{
    private const int Quality = 88;
    private const int StartOfFrameOffset = 154;
    private const int HeightOffset = StartOfFrameOffset + 5;
    private const int WidthOffset = StartOfFrameOffset + 7;
    private const int LuminanceSamplingOffset = StartOfFrameOffset + 11;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RoundtripsASmoothGradient(bool subsampled)
    {
        const int width = 64;
        const int height = 64;
        var source = BuildGradient(width, height);

        var encoded = ScalarJpegEncoder.Encode(source, width, height, Quality, SubsamplingFor(subsampled));
        var (decoded, decodedWidth, decodedHeight) = Decode(encoded);

        Assert.Equal(width, decodedWidth);
        Assert.Equal(height, decodedHeight);
        Assert.True(Psnr(source, decoded, width, height) >= 38d);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(13, 7)]
    [InlineData(17, 33)]
    [InlineData(8, 16)]
    [InlineData(31, 2)]
    public void HandlesDimensionsThatAreNotBlockMultiples(int width, int height)
    {
        var source = BuildGradient(width, height);

        var encoded = ScalarJpegEncoder.Encode(source, width, height, Quality, JpegChromaSubsampling.Ratio444);
        var (decoded, decodedWidth, decodedHeight) = Decode(encoded);

        Assert.Equal(width, decodedWidth);
        Assert.Equal(height, decodedHeight);
        Assert.True(Psnr(source, decoded, width, height) >= 30d);
    }

    [Theory]
    [InlineData(13, 7)]
    [InlineData(64, 64)]
    [InlineData(150, 90)]
    public void MatchesImageSharpQualityAndSize(int width, int height)
    {
        const double psnrSlack = 0.5d;
        const double sizeSlack = 1.1d;
        var source = BuildGradient(width, height);

        var encoded = ScalarJpegEncoder.Encode(source, width, height, Quality, JpegChromaSubsampling.Ratio420);
        var reference = EncodeWithImageSharp(source, width, height);
        var (decoded, _, _) = Decode(encoded);
        var (referenceDecoded, _, _) = Decode(reference);

        Assert.True(Psnr(source, decoded, width, height) >= Psnr(source, referenceDecoded, width, height) - psnrSlack);
        Assert.True(encoded.Length <= reference.Length * sizeSlack);
    }

    [Fact]
    public void ReproducesAFlatSaturatedColorWithinQuantizationTolerance()
    {
        const int width = 24;
        const int height = 24;
        const int tolerance = 6;
        var source = BuildFlat(width, height, 255, 0, 0);

        var encoded = ScalarJpegEncoder.Encode(source, width, height, Quality, JpegChromaSubsampling.Ratio420);
        var (decoded, _, _) = Decode(encoded);

        for (var index = 0; index < source.Length; index += 4)
        {
            Assert.InRange(decoded[index], source[index] - tolerance, source[index] + tolerance);
            Assert.InRange(decoded[index + 1], source[index + 1] - tolerance, source[index + 1] + tolerance);
            Assert.InRange(decoded[index + 2], source[index + 2] - tolerance, source[index + 2] + tolerance);
        }
    }

    [Fact]
    public void ProducesIdenticalBytesForIdenticalInput()
    {
        const int width = 40;
        const int height = 24;
        var source = BuildTexture(width, height);

        var first = ScalarJpegEncoder.Encode(source, width, height, Quality, JpegChromaSubsampling.Ratio420);
        var second = ScalarJpegEncoder.Encode(source, width, height, Quality, JpegChromaSubsampling.Ratio420);

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData(true, 0x22)]
    [InlineData(false, 0x11)]
    public void WritesBaselineMarkersAndSamplingFactors(bool subsampled, byte expectedSampling)
    {
        const int width = 300;
        const int height = 200;
        var source = BuildGradient(width, height);

        var encoded = ScalarJpegEncoder.Encode(source, width, height, Quality, SubsamplingFor(subsampled));

        Assert.Equal(0xFF, encoded[0]);
        Assert.Equal(0xD8, encoded[1]);
        Assert.Equal(0xFF, encoded[^2]);
        Assert.Equal(0xD9, encoded[^1]);
        Assert.Equal(0xFF, encoded[StartOfFrameOffset]);
        Assert.Equal(0xC0, encoded[StartOfFrameOffset + 1]);
        Assert.Equal(height, (encoded[HeightOffset] << 8) | encoded[HeightOffset + 1]);
        Assert.Equal(width, (encoded[WidthOffset] << 8) | encoded[WidthOffset + 1]);
        Assert.Equal(expectedSampling, encoded[LuminanceSamplingOffset]);
    }

    [Fact]
    public void CompressesTexturedContentWellBelowRawSize()
    {
        const int width = 256;
        const int height = 256;
        var source = BuildTexture(width, height);
        var rawBytes = width * height * 3;

        var encoded = ScalarJpegEncoder.Encode(source, width, height, Quality, JpegChromaSubsampling.Ratio420);

        Assert.True(encoded.Length < rawBytes / 4);
    }

    [Fact]
    public void LowerQualityProducesSmallerFiles()
    {
        const int width = 128;
        const int height = 128;
        var source = BuildTexture(width, height);

        var coarse = ScalarJpegEncoder.Encode(source, width, height, 50, JpegChromaSubsampling.Ratio420);
        var fine = ScalarJpegEncoder.Encode(source, width, height, 95, JpegChromaSubsampling.Ratio420);

        Assert.True(coarse.Length < fine.Length);
    }

    [Fact]
    public void HigherQualityRoundtripsCloser()
    {
        const int width = 96;
        const int height = 96;
        var source = BuildTexture(width, height);

        var coarse = ScalarJpegEncoder.Encode(source, width, height, 40, JpegChromaSubsampling.Ratio444);
        var fine = ScalarJpegEncoder.Encode(source, width, height, 95, JpegChromaSubsampling.Ratio444);
        var (coarseDecoded, _, _) = Decode(coarse);
        var (fineDecoded, _, _) = Decode(fine);

        Assert.True(Psnr(source, fineDecoded, width, height) > Psnr(source, coarseDecoded, width, height));
    }

    [Fact]
    public void RejectsABufferShorterThanTheImage()
    {
        var tooShort = new byte[8 * 8 * 4 - 1];

        Assert.Throws<ArgumentException>(() =>
            ScalarJpegEncoder.Encode(tooShort, 8, 8, Quality, JpegChromaSubsampling.Ratio420));
    }

    [Theory]
    [InlineData(0, 8)]
    [InlineData(8, 0)]
    [InlineData(-4, 8)]
    public void RejectsEmptyDimensions(int width, int height)
    {
        var pixels = new byte[64 * 4];

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ScalarJpegEncoder.Encode(pixels, width, height, Quality, JpegChromaSubsampling.Ratio420));
    }

    private static JpegChromaSubsampling SubsamplingFor(bool subsampled)
    {
        return subsampled ? JpegChromaSubsampling.Ratio420 : JpegChromaSubsampling.Ratio444;
    }

    private static byte[] EncodeWithImageSharp(byte[] source, int width, int height)
    {
        using var image = Image.LoadPixelData<Rgba32>(source, width, height);
        using var stream = new MemoryStream();
        image.SaveAsJpeg(stream, new JpegEncoder { Quality = Quality });
        return stream.ToArray();
    }

    internal static (byte[] Pixels, int Width, int Height) Decode(byte[] encoded)
    {
        using var image = Image.Load<Rgba32>(encoded);
        var pixels = new byte[image.Width * image.Height * 4];
        image.CopyPixelDataTo(pixels);
        return (pixels, image.Width, image.Height);
    }

    internal static double Psnr(ReadOnlySpan<byte> source, ReadOnlySpan<byte> decoded, int width, int height)
    {
        var squaredError = 0d;
        var sampleCount = width * height * 3;
        for (var pixelIndex = 0; pixelIndex < width * height; pixelIndex++)
        {
            var offset = pixelIndex * 4;
            for (var channel = 0; channel < 3; channel++)
            {
                double delta = source[offset + channel] - decoded[offset + channel];
                squaredError += delta * delta;
            }
        }

        var meanSquaredError = squaredError / sampleCount;
        return meanSquaredError <= 0d
            ? double.PositiveInfinity
            : 10d * Math.Log10(255d * 255d / meanSquaredError);
    }

    internal static byte[] BuildGradient(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var pixelY = 0; pixelY < height; pixelY++)
        {
            for (var pixelX = 0; pixelX < width; pixelX++)
            {
                var index = ((pixelY * width) + pixelX) * 4;
                pixels[index] = (byte)(pixelX * 255 / Math.Max(1, width - 1));
                pixels[index + 1] = (byte)(pixelY * 255 / Math.Max(1, height - 1));
                pixels[index + 2] = (byte)(255 - (pixelX * 255 / Math.Max(1, width - 1)) / 2);
                pixels[index + 3] = 255;
            }
        }

        return pixels;
    }

    private static byte[] BuildTexture(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var pixelY = 0; pixelY < height; pixelY++)
        {
            for (var pixelX = 0; pixelX < width; pixelX++)
            {
                var index = ((pixelY * width) + pixelX) * 4;
                var wave = MathF.Sin(pixelX * 0.21f) * MathF.Cos(pixelY * 0.17f);
                var ripple = MathF.Sin((pixelX + pixelY) * 0.05f);
                pixels[index] = (byte)(128 + (wave * 90f));
                pixels[index + 1] = (byte)(128 + (ripple * 100f));
                pixels[index + 2] = (byte)(96 + ((pixelX ^ pixelY) & 63));
                pixels[index + 3] = 255;
            }
        }

        return pixels;
    }

    private static byte[] BuildFlat(int width, int height, byte red, byte green, byte blue)
    {
        var pixels = new byte[width * height * 4];
        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = red;
            pixels[index + 1] = green;
            pixels[index + 2] = blue;
            pixels[index + 3] = 255;
        }

        return pixels;
    }
}
