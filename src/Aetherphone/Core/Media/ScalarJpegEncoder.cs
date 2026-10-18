using System.Buffers;

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

    private sealed class HuffmanTable
    {
        public readonly byte[] Counts;
        public readonly byte[] Symbols;
        public readonly HuffmanCode[] Codes;

        public HuffmanTable(byte[] counts, byte[] symbols)
        {
            Counts = counts;
            Symbols = symbols;
            Codes = BuildHuffman(counts, symbols);
        }
    }

    private interface ISymbolSink
    {
        void Symbol(int table, int symbol);

        void Bits(uint bits, int length);
    }

    public const int MinQuality = 1;
    public const int MaxQuality = 100;
    public const int MaxDimension = ushort.MaxValue;

    private const int BlockSide = 8;
    private const int BlockLength = 64;
    private const int McuSide420 = 16;
    private const int McuLength420 = 256;
    private const int BlocksPerMcu420 = 6;
    private const int BlocksPerMcu444 = 3;
    private const int HuffmanSymbolCount = 256;
    private const int HuffmanLengthCount = 16;
    private const int MaxQuantValue = 255;
    private const int QualityPivot = 50;
    private const float LevelShift = 128f;
    private const int BitWindow = 24;
    private const byte StuffedByte = 0xFF;

    private const int TableDcLuminance = 0;
    private const int TableAcLuminance = 1;
    private const int TableDcChrominance = 2;
    private const int TableAcChrominance = 3;
    private const int TableCount = 4;

    private const byte MarkerStartOfImage = 0xD8;
    private const byte MarkerEndOfImage = 0xD9;
    private const byte MarkerApp0 = 0xE0;
    private const byte MarkerQuantTable = 0xDB;
    private const byte MarkerStartOfFrameBaseline = 0xC0;
    private const byte MarkerHuffmanTable = 0xC4;
    private const byte MarkerStartOfScan = 0xDA;

    // The zigzag order, chrominance table and AAN scaled DCT factors are the ITU T.81 Annex K defaults; the
    // luminance table is N. Robidoux's, mozjpeg's default, which kept more luma detail per byte on game screenshots.
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

    private static readonly ushort[] LuminanceQuantBase =
    {
        16, 16, 16, 18, 25, 37, 56, 85,
        16, 17, 20, 27, 34, 40, 53, 75,
        16, 20, 24, 31, 43, 62, 91, 135,
        18, 27, 31, 40, 53, 74, 106, 156,
        25, 34, 43, 53, 69, 94, 131, 189,
        37, 40, 62, 74, 94, 124, 169, 238,
        56, 53, 91, 106, 131, 169, 226, 311,
        85, 75, 135, 156, 189, 238, 311, 418,
    };

    private static readonly ushort[] ChrominanceQuantBase =
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

        var subsampled = subsampling == JpegChromaSubsampling.Ratio420;
        var mcuSide = subsampled ? McuSide420 : BlockSide;
        var blocksPerMcu = subsampled ? BlocksPerMcu420 : BlocksPerMcu444;
        var mcuCount = ((width + mcuSide - 1) / mcuSide) * ((height + mcuSide - 1) / mcuSide);
        var coefficientCount = checked(mcuCount * blocksPerMcu * BlockLength);
        var coefficients = ArrayPool<short>.Shared.Rent(coefficientCount);
        try
        {
            var blocks = coefficients.AsSpan(0, coefficientCount);
            Quantize(rgba, width, height, subsampled, luminanceScale, chrominanceScale, blocks);
            var tables = BuildOptimalTables(blocks, blocksPerMcu);
            var writer = new JpegWriter(EstimateCapacity(width, height), tables);
            WriteHeaders(ref writer, width, height, subsampling, luminanceQuant, chrominanceQuant, tables);
            Walk(blocks, blocksPerMcu, ref writer);
            writer.FlushBits();
            writer.WriteMarker(MarkerEndOfImage);
            return writer.ToArray();
        }
        finally
        {
            ArrayPool<short>.Shared.Return(coefficients);
        }
    }

    private static int EstimateCapacity(int width, int height)
    {
        const int headerAllowance = 2048;
        return (int)Math.Min(int.MaxValue / 2, ((long)width * height / 2) + headerAllowance);
    }

    private static void BuildQuantization(ushort[] baseTable, int quality, Span<byte> zigzagTable,
        Span<float> scaleTable)
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

    private static HuffmanTable[] BuildOptimalTables(ReadOnlySpan<short> blocks, int blocksPerMcu)
    {
        var sink = new FrequencySink(new long[TableCount * HuffmanSymbolCount]);
        Walk(blocks, blocksPerMcu, ref sink);
        var tables = new HuffmanTable[TableCount];
        for (var table = 0; table < TableCount; table++)
        {
            tables[table] = BuildOptimalTable(sink.Frequencies.AsSpan(table * HuffmanSymbolCount, HuffmanSymbolCount));
        }

        return tables;
    }

    // ITU T.81 Annex K.2, as in libjpeg's jpeg_gen_optimal_table: the reserved pseudo symbol keeps the all-ones code
    // unused, and lengths past 16 bits are folded back into shorter ones.
    private static HuffmanTable BuildOptimalTable(ReadOnlySpan<long> symbolFrequencies)
    {
        const int reserved = HuffmanSymbolCount;
        const int extendedCount = HuffmanSymbolCount + 1;
        Span<long> frequency = stackalloc long[extendedCount];
        Span<int> codeSize = stackalloc int[extendedCount];
        Span<int> chain = stackalloc int[extendedCount];
        symbolFrequencies.CopyTo(frequency);
        frequency[reserved] = 1;
        codeSize.Clear();
        chain.Fill(-1);
        while (true)
        {
            var smallest = LeastFrequent(frequency, -1);
            var nextSmallest = LeastFrequent(frequency, smallest);
            if (nextSmallest < 0)
            {
                break;
            }

            frequency[smallest] += frequency[nextSmallest];
            frequency[nextSmallest] = 0;
            codeSize[smallest]++;
            while (chain[smallest] >= 0)
            {
                smallest = chain[smallest];
                codeSize[smallest]++;
            }

            chain[smallest] = nextSmallest;
            codeSize[nextSmallest]++;
            while (chain[nextSmallest] >= 0)
            {
                nextSmallest = chain[nextSmallest];
                codeSize[nextSmallest]++;
            }
        }

        Span<int> lengthCounts = stackalloc int[extendedCount + 1];
        lengthCounts.Clear();
        var longest = 0;
        for (var symbol = 0; symbol < extendedCount; symbol++)
        {
            if (codeSize[symbol] > 0)
            {
                lengthCounts[codeSize[symbol]]++;
                longest = Math.Max(longest, codeSize[symbol]);
            }
        }

        for (var length = longest; length > HuffmanLengthCount; length--)
        {
            while (lengthCounts[length] > 0)
            {
                var donor = length - 2;
                while (lengthCounts[donor] == 0)
                {
                    donor--;
                }

                lengthCounts[length] -= 2;
                lengthCounts[length - 1]++;
                lengthCounts[donor + 1] += 2;
                lengthCounts[donor]--;
            }
        }

        var reservedLength = HuffmanLengthCount;
        while (lengthCounts[reservedLength] == 0)
        {
            reservedLength--;
        }

        lengthCounts[reservedLength]--;
        var counts = new byte[HuffmanLengthCount];
        var symbolTotal = 0;
        for (var length = 1; length <= HuffmanLengthCount; length++)
        {
            counts[length - 1] = (byte)lengthCounts[length];
            symbolTotal += lengthCounts[length];
        }

        var symbols = new byte[symbolTotal];
        var written = 0;
        for (var length = 1; length <= longest; length++)
        {
            for (var symbol = 0; symbol < HuffmanSymbolCount; symbol++)
            {
                if (codeSize[symbol] == length)
                {
                    symbols[written] = (byte)symbol;
                    written++;
                }
            }
        }

        return new HuffmanTable(counts, symbols);
    }

    private static int LeastFrequent(ReadOnlySpan<long> frequency, int excluded)
    {
        var found = -1;
        var lowest = long.MaxValue;
        for (var symbol = 0; symbol < frequency.Length; symbol++)
        {
            if (frequency[symbol] != 0 && frequency[symbol] <= lowest && symbol != excluded)
            {
                lowest = frequency[symbol];
                found = symbol;
            }
        }

        return found;
    }

    private static void WriteHeaders(ref JpegWriter writer, int width, int height, JpegChromaSubsampling subsampling,
        scoped ReadOnlySpan<byte> luminanceQuant, scoped ReadOnlySpan<byte> chrominanceQuant, HuffmanTable[] tables)
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
            + HuffmanSegmentLength(tables[TableDcLuminance])
            + HuffmanSegmentLength(tables[TableAcLuminance])
            + HuffmanSegmentLength(tables[TableDcChrominance])
            + HuffmanSegmentLength(tables[TableAcChrominance]));
        WriteHuffmanSegment(ref writer, 0x00, tables[TableDcLuminance]);
        WriteHuffmanSegment(ref writer, 0x10, tables[TableAcLuminance]);
        WriteHuffmanSegment(ref writer, 0x01, tables[TableDcChrominance]);
        WriteHuffmanSegment(ref writer, 0x11, tables[TableAcChrominance]);

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

    private static int HuffmanSegmentLength(HuffmanTable table)
    {
        return 1 + HuffmanLengthCount + table.Symbols.Length;
    }

    private static void WriteHuffmanSegment(ref JpegWriter writer, byte classAndId, HuffmanTable table)
    {
        writer.WriteByte(classAndId);
        writer.WriteBytes(table.Counts);
        writer.WriteBytes(table.Symbols);
    }

    private static void Quantize(ReadOnlySpan<byte> rgba, int width, int height, bool subsampled,
        scoped ReadOnlySpan<float> luminanceScale, scoped ReadOnlySpan<float> chrominanceScale, Span<short> blocks)
    {
        var side = subsampled ? McuSide420 : BlockSide;
        Span<float> luma = stackalloc float[McuLength420];
        Span<float> chromaBlue = stackalloc float[McuLength420];
        Span<float> chromaRed = stackalloc float[McuLength420];
        Span<float> blueBlock = stackalloc float[BlockLength];
        Span<float> redBlock = stackalloc float[BlockLength];
        var offset = 0;
        for (var originY = 0; originY < height; originY += side)
        {
            for (var originX = 0; originX < width; originX += side)
            {
                FillMacroblock(rgba, width, height, originX, originY, side, luma, chromaBlue, chromaRed);
                if (!subsampled)
                {
                    QuantizeBlock(luma, 0, BlockSide, luminanceScale, blocks.Slice(offset, BlockLength));
                    QuantizeBlock(chromaBlue, 0, BlockSide, chrominanceScale,
                        blocks.Slice(offset + BlockLength, BlockLength));
                    QuantizeBlock(chromaRed, 0, BlockSide, chrominanceScale,
                        blocks.Slice(offset + (2 * BlockLength), BlockLength));
                    offset += BlocksPerMcu444 * BlockLength;
                    continue;
                }

                QuantizeBlock(luma, 0, McuSide420, luminanceScale, blocks.Slice(offset, BlockLength));
                QuantizeBlock(luma, BlockSide, McuSide420, luminanceScale,
                    blocks.Slice(offset + BlockLength, BlockLength));
                QuantizeBlock(luma, BlockSide * McuSide420, McuSide420, luminanceScale,
                    blocks.Slice(offset + (2 * BlockLength), BlockLength));
                QuantizeBlock(luma, (BlockSide * McuSide420) + BlockSide, McuSide420, luminanceScale,
                    blocks.Slice(offset + (3 * BlockLength), BlockLength));
                Downsample(chromaBlue, blueBlock);
                Downsample(chromaRed, redBlock);
                QuantizeBlock(blueBlock, 0, BlockSide, chrominanceScale,
                    blocks.Slice(offset + (4 * BlockLength), BlockLength));
                QuantizeBlock(redBlock, 0, BlockSide, chrominanceScale,
                    blocks.Slice(offset + (5 * BlockLength), BlockLength));
                offset += BlocksPerMcu420 * BlockLength;
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

    private static void QuantizeBlock(Span<float> samples, int offset, int stride, scoped ReadOnlySpan<float> scale,
        Span<short> quantized)
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
                quantized[NaturalToZigzag[natural]] = (short)(value < 0f ? value - 0.5f : value + 0.5f);
            }
        }
    }

    private static void Walk<TSink>(ReadOnlySpan<short> blocks, int blocksPerMcu, ref TSink sink)
        where TSink : ISymbolSink, allows ref struct
    {
        var previousLuma = 0;
        var previousBlue = 0;
        var previousRed = 0;
        var blockCount = blocks.Length / BlockLength;
        for (var blockIndex = 0; blockIndex < blockCount; blockIndex++)
        {
            var block = blocks.Slice(blockIndex * BlockLength, BlockLength);
            var slot = blockIndex % blocksPerMcu;
            if (slot < blocksPerMcu - 2)
            {
                EmitBlock(block, previousLuma, TableDcLuminance, TableAcLuminance, ref sink);
                previousLuma = block[0];
            }
            else if (slot == blocksPerMcu - 2)
            {
                EmitBlock(block, previousBlue, TableDcChrominance, TableAcChrominance, ref sink);
                previousBlue = block[0];
            }
            else
            {
                EmitBlock(block, previousRed, TableDcChrominance, TableAcChrominance, ref sink);
                previousRed = block[0];
            }
        }
    }

    private static void EmitBlock<TSink>(ReadOnlySpan<short> quantized, int previousDc, int dcTable, int acTable,
        ref TSink sink)
        where TSink : ISymbolSink, allows ref struct
    {
        var difference = quantized[0] - previousDc;
        if (difference == 0)
        {
            sink.Symbol(dcTable, 0);
        }
        else
        {
            var (bits, length) = MagnitudeBits(difference);
            sink.Symbol(dcTable, length);
            sink.Bits(bits, length);
        }

        var lastNonZero = BlockLength - 1;
        while (lastNonZero > 0 && quantized[lastNonZero] == 0)
        {
            lastNonZero--;
        }

        if (lastNonZero == 0)
        {
            sink.Symbol(acTable, 0x00);
            return;
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
                sink.Symbol(acTable, 0xF0);
                zeroRun -= 16;
            }

            var (bits, length) = MagnitudeBits(quantized[index]);
            sink.Symbol(acTable, (zeroRun << 4) + length);
            sink.Bits(bits, length);
        }

        if (lastNonZero != BlockLength - 1)
        {
            sink.Symbol(acTable, 0x00);
        }
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

    private readonly struct FrequencySink : ISymbolSink
    {
        public readonly long[] Frequencies;

        public FrequencySink(long[] frequencies)
        {
            Frequencies = frequencies;
        }

        public void Symbol(int table, int symbol)
        {
            Frequencies[(table * HuffmanSymbolCount) + symbol]++;
        }

        public void Bits(uint bits, int length)
        {
        }
    }

    private ref struct JpegWriter : ISymbolSink
    {
        private readonly HuffmanTable[] tables;
        private byte[] buffer;
        private int length;
        private uint bitBuffer;
        private int bitCount;

        public JpegWriter(int capacity, HuffmanTable[] tables)
        {
            this.tables = tables;
            buffer = new byte[Math.Max(capacity, 1)];
            length = 0;
            bitBuffer = 0;
            bitCount = 0;
        }

        public void Symbol(int table, int symbol)
        {
            var code = tables[table].Codes[symbol];
            WriteBits(code.Code, code.Length);
        }

        public void Bits(uint bits, int count)
        {
            WriteBits(bits, count);
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
