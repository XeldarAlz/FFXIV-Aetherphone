using System.Buffers.Binary;
using Aetherphone.Core.Media;
using Aetherphone.Core.Platform;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ClipboardImageTests
{
    private const int InfoHeaderSize = 40;
    private const int RgbCompression = 0;
    private const int BitFieldsCompression = 3;

    [Fact]
    public void ABottomUpTwentyFourBitDibDecodesWithItsColors()
    {
        var dib = Dib(24, RgbCompression, Array.Empty<byte>(), new byte[]
        {
            0, 0, 255, 0, 255, 0, 0, 0,
            255, 0, 0, 255, 255, 255, 0, 0,
        });

        var (pixels, width, height) = ImageProcessor.DecodeRgba32(ClipboardImage.DibToBmp(dib)!);

        Assert.Equal(2, width);
        Assert.Equal(2, height);
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, pixels[..4]);
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, pixels[4..8]);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, pixels[8..12]);
        Assert.Equal(new byte[] { 0, 255, 0, 255 }, pixels[12..16]);
    }

    [Fact]
    public void ABitFieldsDibSkipsItsColorMasks()
    {
        var masks = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(masks, 0x00FF0000);
        BinaryPrimitives.WriteUInt32LittleEndian(masks.AsSpan(4), 0x0000FF00);
        BinaryPrimitives.WriteUInt32LittleEndian(masks.AsSpan(8), 0x000000FF);
        var dib = Dib(32, BitFieldsCompression, masks, new byte[]
        {
            10, 20, 30, 255, 40, 50, 60, 255,
            70, 80, 90, 255, 100, 110, 120, 255,
        });

        var bmp = ClipboardImage.DibToBmp(dib)!;
        var (pixels, _, _) = ImageProcessor.DecodeRgba32(bmp);

        Assert.Equal(14 + InfoHeaderSize + masks.Length, BinaryPrimitives.ReadInt32LittleEndian(bmp.AsSpan(10)));
        Assert.Equal(new byte[] { 90, 80, 70 }, pixels[..3]);
    }

    [Fact]
    public void AThirtyTwoBitDibWithEmptyAlphaStaysOpaque()
    {
        var dib = Dib(32, RgbCompression, Array.Empty<byte>(), new byte[]
        {
            10, 20, 30, 0, 40, 50, 60, 0,
            70, 80, 90, 0, 100, 110, 120, 0,
        });

        var (pixels, _, _) = ImageProcessor.DecodeRgba32(ClipboardImage.DibToBmp(dib)!);

        Assert.Equal(new byte[] { 90, 80, 70, 255 }, pixels[..4]);
    }

    [Fact]
    public void ATruncatedDibIsRejected()
    {
        Assert.Null(ClipboardImage.DibToBmp(new byte[12]));
    }

    private static byte[] Dib(ushort bitCount, int compression, byte[] masks, byte[] rows)
    {
        var dib = new byte[InfoHeaderSize + masks.Length + rows.Length];
        BinaryPrimitives.WriteInt32LittleEndian(dib, InfoHeaderSize);
        BinaryPrimitives.WriteInt32LittleEndian(dib.AsSpan(4), 2);
        BinaryPrimitives.WriteInt32LittleEndian(dib.AsSpan(8), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(dib.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(dib.AsSpan(14), bitCount);
        BinaryPrimitives.WriteInt32LittleEndian(dib.AsSpan(16), compression);
        BinaryPrimitives.WriteInt32LittleEndian(dib.AsSpan(20), rows.Length);
        masks.CopyTo(dib, InfoHeaderSize);
        rows.CopyTo(dib, InfoHeaderSize + masks.Length);
        return dib;
    }
}
