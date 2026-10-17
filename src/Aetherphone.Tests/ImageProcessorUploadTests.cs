using Aetherphone.Core.Media;
using Aetherphone.Core.Wallpapers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ImageProcessorUploadTests
{
    [Fact]
    public void TransparentUploadKeepsClearAndSoftEdgesWithoutBakingABackground()
    {
        using var source = new Image<Rgba32>(48, 32, new Rgba32(220, 130, 40, 255));
        source[8, 8] = new Rgba32(220, 130, 40, 0);
        source[9, 8] = new Rgba32(220, 130, 40, 128);
        var sourcePath = WritePng(source);
        try
        {
            var baked = ImageProcessor.BakeUpload(sourcePath, 48);
            using var decoded = Image.Load<Rgba32>(baked.Bytes);

            Assert.Equal("image/png", baked.ContentType);
            Assert.Equal(48, baked.Width);
            Assert.Equal(32, baked.Height);
            Assert.Equal(0, decoded[8, 8].A);
            Assert.Equal(new Rgba32(220, 130, 40, 128), decoded[9, 8]);
            Assert.Equal(new Rgba32(220, 130, 40, 255), decoded[10, 8]);
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    [Fact]
    public void CroppedUploadKeepsTransparencyAfterRotationAndColorEdits()
    {
        using var source = new Image<Rgba32>(48, 32, new Rgba32(220, 130, 40, 255));
        source[8, 8] = new Rgba32(220, 130, 40, 0);
        source[9, 8] = new Rgba32(220, 130, 40, 128);
        var sourcePath = WritePng(source);
        try
        {
            var edit = PhotoEdit.None.WithLook(PhotoLook.Mono, 1f).RotatedClockwise();
            var baked = ImageProcessor.BakeCroppedUpload(sourcePath, WallpaperCrop.Cover, 32, 48, false, edit);
            using var decoded = Image.Load<Rgba32>(baked.Bytes);

            Assert.Equal("image/png", baked.ContentType);
            Assert.Equal(32, decoded.Width);
            Assert.Equal(48, decoded.Height);
            Assert.Equal(0, decoded[23, 8].A);
            var softEdge = decoded[23, 9];
            Assert.Equal(128, softEdge.A);
            Assert.Equal(softEdge.R, softEdge.G);
            Assert.Equal(softEdge.R, softEdge.B);
            Assert.InRange(softEdge.R, 140, 150);
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    [Fact]
    public void ResizingTransparentUploadsDoesNotDarkenSoftPixels()
    {
        using var source = new Image<Rgba32>(48, 32, new Rgba32(220, 130, 40, 128));
        var sourcePath = WritePng(source);
        try
        {
            var baked = ImageProcessor.BakeUpload(sourcePath, 24);
            using var decoded = Image.Load<Rgba32>(baked.Bytes);

            Assert.Equal("image/png", baked.ContentType);
            Assert.Equal(24, baked.Width);
            Assert.Equal(16, baked.Height);
            Assert.Equal(new Rgba32(220, 130, 40, 128), decoded[0, 0]);
            Assert.Equal(new Rgba32(220, 130, 40, 128), decoded[12, 8]);
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    [Fact]
    public void OpaqueUploadBakesTheScalarJpegAtUploadQuality()
    {
        using var source = Image.LoadPixelData<Rgba32>(ScalarJpegEncoderTests.BuildGradient(48, 32), 48, 32);
        var sourcePath = WritePng(source);
        try
        {
            var baked = ImageProcessor.BakeUpload(sourcePath, 24);
            var jpeg = ImageProcessor.BakeJpeg(sourcePath, 24, ImageProcessor.UploadJpegQuality);
            var smallImageJpeg = ImageProcessor.BakeJpeg(sourcePath, 24);

            Assert.Equal("image/jpeg", baked.ContentType);
            Assert.Equal(jpeg.Bytes, baked.Bytes);
            Assert.True(smallImageJpeg.Bytes.Length < baked.Bytes.Length);
            Assert.Equal(24, baked.Width);
            Assert.Equal(16, baked.Height);
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    [Fact]
    public void TransparentUploadRemovesPrivateSourceMetadata()
    {
        using var source = new Image<Rgba32>(16, 16, new Rgba32(220, 130, 40, 128));
        source.Metadata.ExifProfile = new ExifProfile();
        source.Metadata.ExifProfile.SetValue(ExifTag.ImageDescription, "private source information");
        var sourcePath = WritePng(source);
        try
        {
            using var original = Image.Load<Rgba32>(sourcePath);
            Assert.NotNull(original.Metadata.ExifProfile);

            var baked = ImageProcessor.BakeUpload(sourcePath, 16);
            using var decoded = Image.Load<Rgba32>(baked.Bytes);

            Assert.Equal("image/png", baked.ContentType);
            Assert.Null(decoded.Metadata.ExifProfile);
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    [Fact]
    public void LargeTransparentUploadFitsTheServerLimitAndReportsItsFinalSize()
    {
        const int size = 1600;
        const int maxBytes = 8 * 1024 * 1024;
        var pixels = new byte[size * size * 4];
        new Random(42).NextBytes(pixels);
        using var source = Image.LoadPixelData<Rgba32>(pixels, size, size);
        var sourcePath = WritePng(source);
        try
        {
            Assert.True(new FileInfo(sourcePath).Length > maxBytes);

            var baked = ImageProcessor.BakeUpload(sourcePath, size);
            using var decoded = Image.Load<Rgba32>(baked.Bytes);

            Assert.Equal("image/png", baked.ContentType);
            Assert.InRange(baked.Bytes.Length, 1, maxBytes);
            Assert.InRange(baked.Width, 1, size - 1);
            Assert.Equal(baked.Width, baked.Height);
            Assert.Equal(baked.Width, decoded.Width);
            Assert.Equal(baked.Height, decoded.Height);
            Assert.InRange(decoded[0, 0].A, 1, 254);
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    private static string WritePng(Image<Rgba32> image)
    {
        var sourcePath = Path.Combine(Path.GetTempPath(), $"aetherphone-upload-{Guid.NewGuid():N}.png");
        image.SaveAsPng(sourcePath);
        return sourcePath;
    }
}
