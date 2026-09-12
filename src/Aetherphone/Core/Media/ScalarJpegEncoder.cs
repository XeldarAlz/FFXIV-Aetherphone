namespace Aetherphone.Core.Media;

internal enum JpegChromaSubsampling
{
    Ratio420,
    Ratio444,
}

internal static class ScalarJpegEncoder
{
    private readonly struct HuffmanCode
    {
        public readonly ushort Code;
        public readonly byte Length;

        public HuffmanCode(ushort code, byte length)
        {
            Code = code;
            Length = length;
        }
    }

    public const int MinQuality = 1;
    public const int MaxQuality = 100;
    public const int MaxDimension = ushort.MaxValue;

    private const int BlockSide = 8;
    private const int BlockLength = 64;
    private const int McuSide420 = 16;
    private const int McuLength420 = 256;
    private const int HuffmanSymbolCount = 256;
    private const int HuffmanLengthCount = 16;
    private const int MaxQuantValue = 255;
    private const int QualityPivot = 50;
    private const float LevelShift = 128f;
    private const int BitWindow = 24;
    private const byte StuffedByte = 0xFF;

    private const byte MarkerStartOfImage = 0xD8;
    private const byte MarkerEndOfImage = 0xD9;
    private const byte MarkerApp0 = 0xE0;
    private const byte MarkerQuantTable = 0xDB;
    private const byte MarkerStartOfFrameBaseline = 0xC0;
    private const byte MarkerHuffmanTable = 0xC4;
    private const byte MarkerStartOfScan = 0xDA;

    // Tables and constants below are the ITU T.81 Annex K defaults and the AAN scaled DCT factors.
    private static readonly byte[] NaturalToZigzag =
    {
        0, 1, 5, 6, 14, 15, 27, 28,
        2, 4, 7, 13, 16, 26, 29, 42,
        3, 8, 12, 17, 25, 30, 41, 43,
        9, 11, 18, 24, 31, 40, 44, 53,
        10, 19, 23, 32, 39, 45, 52, 54,
        20, 22, 33, 38, 46, 51, 55, 60,
        21, 34, 37, 47, 50, 56, 59, 61,
        35, 36, 48, 49, 57, 58, 62, 63,
    };

    private static readonly byte[] LuminanceQuantBase =
    {
        16, 11, 10, 16, 24, 40, 51, 61,
        12, 12, 14, 19, 26, 58, 60, 55,
        14, 13, 16, 24, 40, 57, 69, 56,
        14, 17, 22, 29, 51, 87, 80, 62,
        18, 22, 37, 56, 68, 109, 103, 77,
        24, 35, 55, 64, 81, 104, 113, 92,
        49, 64, 78, 87, 103, 121, 120, 101,
        72, 92, 95, 98, 112, 100, 103, 99,
    };

    private static readonly byte[] ChrominanceQuantBase =
    {
        17, 18, 24, 47, 99, 99, 99, 99,
        18, 21, 26, 66, 99, 99, 99, 99,
        24, 26, 56, 99, 99, 99, 99, 99,
        47, 66, 99, 99, 99, 99, 99, 99,
        99, 99, 99, 99, 99, 99, 99, 99,
        99, 99, 99, 99, 99, 99, 99, 99,
        99, 99, 99, 99, 99, 99, 99, 99,
        99, 99, 99, 99, 99, 99, 99, 99,
    };

    private static readonly float[] AanScale =
    {
        1f * 2.828427125f,
        1.387039845f * 2.828427125f,
        1.306562965f * 2.828427125f,
        1.175875602f * 2.828427125f,
        1f * 2.828427125f,
        0.785694958f * 2.828427125f,
        0.541196100f * 2.828427125f,
        0.275899379f * 2.828427125f,
    };

    private static readonly byte[] DcLuminanceCounts = { 0, 1, 5, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0 };
    private static readonly byte[] DcLuminanceSymbols = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
    private static readonly byte[] DcChrominanceCounts = { 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0 };
    private static readonly byte[] DcChrominanceSymbols = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
    private static readonly byte[] AcLuminanceCounts = { 0, 2, 1, 3, 3, 2, 4, 3, 5, 5, 4, 4, 0, 0, 1, 0x7D };

    private static readonly byte[] AcLuminanceSymbols =
    {
        0x01, 0x02, 0x03, 0x00, 0x04, 0x11, 0x05, 0x12, 0x21, 0x31, 0x41, 0x06, 0x13, 0x51, 0x61, 0x07,
        0x22, 0x71, 0x14, 0x32, 0x81, 0x91, 0xA1, 0x08, 0x23, 0x42, 0xB1, 0xC1, 0x15, 0x52, 0xD1, 0xF0,
        0x24, 0x33, 0x62, 0x72, 0x82, 0x09, 0x0A, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x25, 0x26, 0x27, 0x28,
        0x29, 0x2A, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49,
        0x4A, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5A, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69,
        0x6A, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7A, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89,
        0x8A, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9A, 0xA2, 0xA3, 0xA4, 0xA5, 0xA6, 0xA7,
        0xA8, 0xA9, 0xAA, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6, 0xB7, 0xB8, 0xB9, 0xBA, 0xC2, 0xC3, 0xC4, 0xC5,
        0xC6, 0xC7, 0xC8, 0xC9, 0xCA, 0xD2, 0xD3, 0xD4, 0xD5, 0xD6, 0xD7, 0xD8, 0xD9, 0xDA, 0xE1, 0xE2,
        0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xE8, 0xE9, 0xEA, 0xF1, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6, 0xF7, 0xF8,
        0xF9, 0xFA,
    };

    private static readonly byte[] AcChrominanceCounts = { 0, 2, 1, 2, 4, 4, 3, 4, 7, 5, 4, 4, 0, 1, 2, 0x77 };

    private static readonly byte[] AcChrominanceSymbols =
    {
        0x00, 0x01, 0x02, 0x03, 0x11, 0x04, 0x05, 0x21, 0x31, 0x06, 0x12, 0x41, 0x51, 0x07, 0x61, 0x71,
        0x13, 0x22, 0x32, 0x81, 0x08, 0x14, 0x42, 0x91, 0xA1, 0xB1, 0xC1, 0x09, 0x23, 0x33, 0x52, 0xF0,
        0x15, 0x62, 0x72, 0xD1, 0x0A, 0x16, 0x24, 0x34, 0xE1, 0x25, 0xF1, 0x17, 0x18, 0x19, 0x1A, 0x26,
        0x27, 0x28, 0x29, 0x2A, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48,
        0x49, 0x4A, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5A, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68,
        0x69, 0x6A, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7A, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87,
        0x88, 0x89, 0x8A, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9A, 0xA2, 0xA3, 0xA4, 0xA5,
        0xA6, 0xA7, 0xA8, 0xA9, 0xAA, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6, 0xB7, 0xB8, 0xB9, 0xBA, 0xC2, 0xC3,
        0xC4, 0xC5, 0xC6, 0xC7, 0xC8, 0xC9, 0xCA, 0xD2, 0xD3, 0xD4, 0xD5, 0xD6, 0xD7, 0xD8, 0xD9, 0xDA,
        0xE2, 0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xE8, 0xE9, 0xEA, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6, 0xF7, 0xF8,
        0xF9, 0xFA,
    };

    private static readonly HuffmanCode[] DcLuminance = BuildHuffman(DcLuminanceCounts, DcLuminanceSymbols);
    private static readonly HuffmanCode[] AcLuminance = BuildHuffman(AcLuminanceCounts, AcLuminanceSymbols);
    private static readonly HuffmanCode[] DcChrominance = BuildHuffman(DcChrominanceCounts, DcChrominanceSymbols);
    private static readonly HuffmanCode[] AcChrominance = BuildHuffman(AcChrominanceCounts, AcChrominanceSymbols);

    public static byte[] Encode(ReadOnlySpan<byte> rgba, int width, int height, int quality,
        JpegChromaSubsampling subsampling)
    {
        if (width <= 0 || width > MaxDimension || height <= 0 || height > MaxDimension)
        {
            throw new ArgumentOutOfRangeException(nameof(width),
                $"JPEG dimensions must be within 1..{MaxDimension}, got {width}x{height}.");
        }

        var required = checked(width * height * 4);
        if (rgba.Length < required)
        {
            throw new ArgumentException($"Expected at least {required} RGBA bytes, got {rgba.Length}.", nameof(rgba));
        }

        var clampedQuality = Math.Clamp(quality, MinQuality, MaxQuality);
        Span<byte> luminanceQuant = stackalloc byte[BlockLength];
        Span<byte> chrominanceQuant = stackalloc byte[BlockLength];
        Span<float> luminanceScale = stackalloc float[BlockLength];
        Span<float> chrominanceScale = stackalloc float[BlockLength];
        BuildQuantization(LuminanceQuantBase, clampedQuality, luminanceQuant, luminanceScale);
        BuildQuantization(ChrominanceQuantBase, clampedQuality, chrominanceQuant, chrominanceScale);

        var writer = new JpegWriter(EstimateCapacity(width, height));
        WriteHeaders(ref writer, width, height, subsampling, luminanceQuant, chrominanceQuant);
        if (subsampling == JpegChromaSubsampling.Ratio420)
        {
            EncodeSubsampled(ref writer, rgba, width, height, luminanceScale, chrominanceScale);
        }
        else
        {
            EncodeFull(ref writer, rgba, width, height, luminanceScale, chrominanceScale);
        }

        writer.FlushBits();
        writer.WriteMarker(MarkerEndOfImage);
        return writer.ToArray();
    }

    private static int EstimateCapacity(int width, int height)
    {
        const int headerAllowance = 2048;
        return (int)Math.Min(int.MaxValue / 2, ((long)width * height / 2) + headerAllowance);
    }

    private static void BuildQuantization(byte[] baseTable, int quality, Span<byte> zigzagTable, Span<float> scaleTable)
    {
        var scale = quality < QualityPivot ? 5000 / quality : 200 - (quality * 2);
        Span<int> natural = stackalloc int[BlockLength];
        for (var index = 0; index < BlockLength; index++)
        {
            var value = Math.Clamp(((baseTable[index] * scale) + 50) / 100, 1, MaxQuantValue);
            natural[index] = value;
            zigzagTable[NaturalToZigzag[index]] = (byte)value;
        }

        for (var row = 0; row < BlockSide; row++)
        {
            for (var column = 0; column < BlockSide; column++)
            {
                var index = (row * BlockSide) + column;
                scaleTable[index] = 1f / (natural[index] * AanScale[row] * AanScale[column]);
            }
        }
    }

    private static HuffmanCode[] BuildHuffman(byte[] counts, byte[] symbols)
    {
        var table = new HuffmanCode[HuffmanSymbolCount];
        var code = 0;
        var symbolIndex = 0;
        for (var length = 1; length <= HuffmanLengthCount; length++)
        {
            var count = counts[length - 1];
            for (var repeat = 0; repeat < count; repeat++)
            {
                table[symbols[symbolIndex]] = new HuffmanCode((ushort)code, (byte)length);
                symbolIndex++;
                code++;
            }

            code <<= 1;
        }

        return table;
    }

    private static void WriteHeaders(ref JpegWriter writer, int width, int height, JpegChromaSubsampling subsampling,
        scoped ReadOnlySpan<byte> luminanceQuant, scoped ReadOnlySpan<byte> chrominanceQuant)
    {
        const int app0Length = 16;
        const byte jfifMajor = 1;
        const byte jfifMinor = 1;
        const byte densityUnitsNone = 0;
        const int densityOne = 1;
        const byte samplePrecision = 8;
        const byte componentCount = 3;
        const byte samplingFull = 0x11;
        const byte samplingHalved = 0x22;
        const byte tableSelectorLuminance = 0x00;
        const byte tableSelectorChrominance = 0x11;
        const byte spectralEnd = 63;

        writer.WriteMarker(MarkerStartOfImage);

        writer.WriteMarker(MarkerApp0);
        writer.WriteUInt16(app0Length);
        writer.WriteBytes("JFIF\0"u8);
        writer.WriteByte(jfifMajor);
        writer.WriteByte(jfifMinor);
        writer.WriteByte(densityUnitsNone);
        writer.WriteUInt16(densityOne);
        writer.WriteUInt16(densityOne);
        writer.WriteByte(0);
        writer.WriteByte(0);

        writer.WriteMarker(MarkerQuantTable);
        writer.WriteUInt16(2 + ((1 + BlockLength) * 2));
        writer.WriteByte(0);
        writer.WriteBytes(luminanceQuant);
        writer.WriteByte(1);
        writer.WriteBytes(chrominanceQuant);

        writer.WriteMarker(MarkerStartOfFrameBaseline);
        writer.WriteUInt16(8 + (componentCount * 3));
        writer.WriteByte(samplePrecision);
        writer.WriteUInt16(height);
        writer.WriteUInt16(width);
        writer.WriteByte(componentCount);
        writer.WriteByte(1);
        writer.WriteByte(subsampling == JpegChromaSubsampling.Ratio420 ? samplingHalved : samplingFull);
        writer.WriteByte(0);
        writer.WriteByte(2);
        writer.WriteByte(samplingFull);
        writer.WriteByte(1);
        writer.WriteByte(3);
        writer.WriteByte(samplingFull);
        writer.WriteByte(1);

        writer.WriteMarker(MarkerHuffmanTable);
        writer.WriteUInt16(2
            + HuffmanSegmentLength(DcLuminanceSymbols)
            + HuffmanSegmentLength(AcLuminanceSymbols)
            + HuffmanSegmentLength(DcChrominanceSymbols)
            + HuffmanSegmentLength(AcChrominanceSymbols));
        WriteHuffmanSegment(ref writer, 0x00, DcLuminanceCounts, DcLuminanceSymbols);
        WriteHuffmanSegment(ref writer, 0x10, AcLuminanceCounts, AcLuminanceSymbols);
        WriteHuffmanSegment(ref writer, 0x01, DcChrominanceCounts, DcChrominanceSymbols);
        WriteHuffmanSegment(ref writer, 0x11, AcChrominanceCounts, AcChrominanceSymbols);

        writer.WriteMarker(MarkerStartOfScan);
        writer.WriteUInt16(6 + (componentCount * 2));
        writer.WriteByte(componentCount);
        writer.WriteByte(1);
        writer.WriteByte(tableSelectorLuminance);
        writer.WriteByte(2);
        writer.WriteByte(tableSelectorChrominance);
        writer.WriteByte(3);
        writer.WriteByte(tableSelectorChrominance);
        writer.WriteByte(0);
        writer.WriteByte(spectralEnd);
        writer.WriteByte(0);
    }

    private static int HuffmanSegmentLength(byte[] symbols)
    {
        return 1 + HuffmanLengthCount + symbols.Length;
    }

    private static void WriteHuffmanSegment(ref JpegWriter writer, byte classAndId, byte[] counts, byte[] symbols)
    {
        writer.WriteByte(classAndId);
        writer.WriteBytes(counts);
        writer.WriteBytes(symbols);
    }

    private static void EncodeSubsampled(ref JpegWriter writer, scoped ReadOnlySpan<byte> rgba, int width,
        int height, scoped ReadOnlySpan<float> luminanceScale, scoped ReadOnlySpan<float> chrominanceScale)
    {
        Span<float> luma = stackalloc float[McuLength420];
        Span<float> chromaBlue = stackalloc float[McuLength420];
        Span<float> chromaRed = stackalloc float[McuLength420];
        Span<float> blueBlock = stackalloc float[BlockLength];
        Span<float> redBlock = stackalloc float[BlockLength];
        Span<int> quantized = stackalloc int[BlockLength];
        var previousLuma = 0;
        var previousBlue = 0;
        var previousRed = 0;
        for (var originY = 0; originY < height; originY += McuSide420)
        {
            for (var originX = 0; originX < width; originX += McuSide420)
            {
                FillMacroblock(rgba, width, height, originX, originY, McuSide420, luma, chromaBlue, chromaRed);
                previousLuma = EncodeBlock(ref writer, luma, 0, McuSide420, luminanceScale, previousLuma,
                    DcLuminance, AcLuminance, quantized);
                previousLuma = EncodeBlock(ref writer, luma, BlockSide, McuSide420, luminanceScale, previousLuma,
                    DcLuminance, AcLuminance, quantized);
                previousLuma = EncodeBlock(ref writer, luma, BlockSide * McuSide420, McuSide420, luminanceScale,
                    previousLuma, DcLuminance, AcLuminance, quantized);
                previousLuma = EncodeBlock(ref writer, luma, (BlockSide * McuSide420) + BlockSide, McuSide420,
                    luminanceScale, previousLuma, DcLuminance, AcLuminance, quantized);
                Downsample(chromaBlue, blueBlock);
                Downsample(chromaRed, redBlock);
                previousBlue = EncodeBlock(ref writer, blueBlock, 0, BlockSide, chrominanceScale, previousBlue,
                    DcChrominance, AcChrominance, quantized);
                previousRed = EncodeBlock(ref writer, redBlock, 0, BlockSide, chrominanceScale, previousRed,
                    DcChrominance, AcChrominance, quantized);
            }
        }
    }

    private static void EncodeFull(ref JpegWriter writer, scoped ReadOnlySpan<byte> rgba, int width, int height,
        scoped ReadOnlySpan<float> luminanceScale, scoped ReadOnlySpan<float> chrominanceScale)
    {
        Span<float> luma = stackalloc float[BlockLength];
        Span<float> chromaBlue = stackalloc float[BlockLength];
        Span<float> chromaRed = stackalloc float[BlockLength];
        Span<int> quantized = stackalloc int[BlockLength];
        var previousLuma = 0;
        var previousBlue = 0;
        var previousRed = 0;
        for (var originY = 0; originY < height; originY += BlockSide)
        {
            for (var originX = 0; originX < width; originX += BlockSide)
            {
                FillMacroblock(rgba, width, height, originX, originY, BlockSide, luma, chromaBlue, chromaRed);
                previousLuma = EncodeBlock(ref writer, luma, 0, BlockSide, luminanceScale, previousLuma,
                    DcLuminance, AcLuminance, quantized);
                previousBlue = EncodeBlock(ref writer, chromaBlue, 0, BlockSide, chrominanceScale, previousBlue,
                    DcChrominance, AcChrominance, quantized);
                previousRed = EncodeBlock(ref writer, chromaRed, 0, BlockSide, chrominanceScale, previousRed,
                    DcChrominance, AcChrominance, quantized);
            }
        }
    }

    private static void FillMacroblock(ReadOnlySpan<byte> rgba, int width, int height, int originX, int originY,
        int side, Span<float> luma, Span<float> chromaBlue, Span<float> chromaRed)
    {
        for (var row = 0; row < side; row++)
        {
            var sourceY = Math.Min(originY + row, height - 1);
            var rowOffset = sourceY * width;
            var targetRow = row * side;
            for (var column = 0; column < side; column++)
            {
                var sourceX = Math.Min(originX + column, width - 1);
                var index = (rowOffset + sourceX) * 4;
                float red = rgba[index];
                float green = rgba[index + 1];
                float blue = rgba[index + 2];
                var target = targetRow + column;
                luma[target] = (0.299f * red) + (0.587f * green) + (0.114f * blue) - LevelShift;
                chromaBlue[target] = (-0.168736f * red) - (0.331264f * green) + (0.5f * blue);
                chromaRed[target] = (0.5f * red) - (0.418688f * green) - (0.081312f * blue);
            }
        }
    }

    private static void Downsample(ReadOnlySpan<float> source, Span<float> target)
    {
        for (var row = 0; row < BlockSide; row++)
        {
            var sourceRow = row * 2 * McuSide420;
            for (var column = 0; column < BlockSide; column++)
            {
                var index = sourceRow + (column * 2);
                target[(row * BlockSide) + column] = (source[index] + source[index + 1]
                    + source[index + McuSide420] + source[index + McuSide420 + 1]) * 0.25f;
            }
        }
    }

    private static int EncodeBlock(ref JpegWriter writer, scoped Span<float> samples, int offset, int stride,
        scoped ReadOnlySpan<float> scale, int previousDc, HuffmanCode[] dcTable, HuffmanCode[] acTable,
        scoped Span<int> quantized)
    {
        for (var row = 0; row < BlockSide; row++)
        {
            Transform(samples, offset + (row * stride), 1);
        }

        for (var column = 0; column < BlockSide; column++)
        {
            Transform(samples, offset + column, stride);
        }

        for (var row = 0; row < BlockSide; row++)
        {
            for (var column = 0; column < BlockSide; column++)
            {
                var natural = (row * BlockSide) + column;
                var value = samples[offset + (row * stride) + column] * scale[natural];
                quantized[NaturalToZigzag[natural]] = (int)(value < 0f ? value - 0.5f : value + 0.5f);
            }
        }

        var difference = quantized[0] - previousDc;
        if (difference == 0)
        {
            writer.WriteCode(dcTable[0]);
        }
        else
        {
            var (bits, length) = MagnitudeBits(difference);
            writer.WriteCode(dcTable[length]);
            writer.WriteBits(bits, length);
        }

        var lastNonZero = BlockLength - 1;
        while (lastNonZero > 0 && quantized[lastNonZero] == 0)
        {
            lastNonZero--;
        }

        if (lastNonZero == 0)
        {
            writer.WriteCode(acTable[0x00]);
            return quantized[0];
        }

        for (var index = 1; index <= lastNonZero; index++)
        {
            var runStart = index;
            while (quantized[index] == 0)
            {
                index++;
            }

            var zeroRun = index - runStart;
            while (zeroRun >= 16)
            {
                writer.WriteCode(acTable[0xF0]);
                zeroRun -= 16;
            }

            var (bits, length) = MagnitudeBits(quantized[index]);
            writer.WriteCode(acTable[(zeroRun << 4) + length]);
            writer.WriteBits(bits, length);
        }

        if (lastNonZero != BlockLength - 1)
        {
            writer.WriteCode(acTable[0x00]);
        }

        return quantized[0];
    }

    private static (uint Bits, int Length) MagnitudeBits(int value)
    {
        var magnitude = value < 0 ? -value : value;
        var coded = value < 0 ? value - 1 : value;
        var length = 1;
        while ((magnitude >>= 1) != 0)
        {
            length++;
        }

        return ((uint)(coded & ((1 << length) - 1)), length);
    }

    private static void Transform(Span<float> data, int start, int step)
    {
        var sample0 = data[start];
        var sample1 = data[start + step];
        var sample2 = data[start + (2 * step)];
        var sample3 = data[start + (3 * step)];
        var sample4 = data[start + (4 * step)];
        var sample5 = data[start + (5 * step)];
        var sample6 = data[start + (6 * step)];
        var sample7 = data[start + (7 * step)];

        var sum07 = sample0 + sample7;
        var diff07 = sample0 - sample7;
        var sum16 = sample1 + sample6;
        var diff16 = sample1 - sample6;
        var sum25 = sample2 + sample5;
        var diff25 = sample2 - sample5;
        var sum34 = sample3 + sample4;
        var diff34 = sample3 - sample4;

        var evenSum = sum07 + sum34;
        var evenDiff = sum07 - sum34;
        var innerSum = sum16 + sum25;
        var innerDiff = sum16 - sum25;

        var even0 = evenSum + innerSum;
        var even4 = evenSum - innerSum;
        var evenRotated = (innerDiff + evenDiff) * 0.707106781f;
        var even2 = evenDiff + evenRotated;
        var even6 = evenDiff - evenRotated;

        var odd0 = diff34 + diff25;
        var odd1 = diff25 + diff16;
        var odd2 = diff16 + diff07;
        var rotationBase = (odd0 - odd2) * 0.382683433f;
        var rotationLow = (odd0 * 0.541196100f) + rotationBase;
        var rotationHigh = (odd2 * 1.306562965f) + rotationBase;
        var oddCenter = odd1 * 0.707106781f;
        var oddPlus = diff07 + oddCenter;
        var oddMinus = diff07 - oddCenter;

        data[start] = even0;
        data[start + step] = oddPlus + rotationHigh;
        data[start + (2 * step)] = even2;
        data[start + (3 * step)] = oddMinus - rotationLow;
        data[start + (4 * step)] = even4;
        data[start + (5 * step)] = oddMinus + rotationLow;
        data[start + (6 * step)] = even6;
        data[start + (7 * step)] = oddPlus - rotationHigh;
    }

    private ref struct JpegWriter
    {
        private byte[] buffer;
        private int length;
        private uint bitBuffer;
        private int bitCount;

        public JpegWriter(int capacity)
        {
            buffer = new byte[Math.Max(capacity, 1)];
            length = 0;
            bitBuffer = 0;
            bitCount = 0;
        }

        public void WriteByte(byte value)
        {
            if (length == buffer.Length)
            {
                Grow();
            }

            buffer[length] = value;
            length++;
        }

        public void WriteBytes(scoped ReadOnlySpan<byte> bytes)
        {
            for (var index = 0; index < bytes.Length; index++)
            {
                WriteByte(bytes[index]);
            }
        }

        public void WriteUInt16(int value)
        {
            WriteByte((byte)(value >> 8));
            WriteByte((byte)value);
        }

        public void WriteMarker(byte marker)
        {
            WriteByte(StuffedByte);
            WriteByte(marker);
        }

        public void WriteCode(HuffmanCode code)
        {
            WriteBits(code.Code, code.Length);
        }

        public void WriteBits(uint bits, int count)
        {
            bitCount += count;
            bitBuffer |= bits << (BitWindow - bitCount);
            while (bitCount >= 8)
            {
                var value = (byte)((bitBuffer >> 16) & 0xFF);
                WriteByte(value);
                if (value == StuffedByte)
                {
                    WriteByte(0);
                }

                bitBuffer <<= 8;
                bitCount -= 8;
            }
        }

        public void FlushBits()
        {
            const uint padding = 0x7F;
            const int paddingLength = 7;
            WriteBits(padding, paddingLength);
        }

        public byte[] ToArray()
        {
            var result = new byte[length];
            for (var index = 0; index < length; index++)
            {
                result[index] = buffer[index];
            }

            return result;
        }

        private void Grow()
        {
            var grown = new byte[buffer.Length * 2];
            for (var index = 0; index < length; index++)
            {
                grown[index] = buffer[index];
            }

            buffer = grown;
        }
    }
}
