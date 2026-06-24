using System.IO;
using System.IO.Compression;

namespace Aetherphone.Core.Photos;

internal static class PngWriter
{
    private const byte BitDepth = 8;
    private const byte ColorTypeRgba = 6;

    private static readonly uint[] CrcTable = BuildCrcTable();

    public static byte[] Encode(byte[] rgba, int width, int height)
    {
        var stride = width * 4;
        var filtered = new byte[(stride + 1) * height];
        for (var row = 0; row < height; row++)
        {
            filtered[row * (stride + 1)] = 0;
            Array.Copy(rgba, row * stride, filtered, row * (stride + 1) + 1, stride);
        }

        byte[] compressed;
        using (var buffer = new MemoryStream())
        {
            using (var zlib = new ZLibStream(buffer, CompressionLevel.Optimal, true))
            {
                zlib.Write(filtered, 0, filtered.Length);
            }

            compressed = buffer.ToArray();
        }

        using var output = new MemoryStream();
        WriteSignature(output);
        WriteHeader(output, width, height);
        WriteChunk(output, "IDAT", compressed);
        WriteChunk(output, "IEND", Array.Empty<byte>());
        return output.ToArray();
    }

    private static void WriteSignature(Stream stream)
    {
        ReadOnlySpan<byte> signature = stackalloc byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        stream.Write(signature);
    }

    private static void WriteHeader(Stream stream, int width, int height)
    {
        var header = new byte[13];
        WriteBigEndian(header, 0, (uint)width);
        WriteBigEndian(header, 4, (uint)height);
        header[8] = BitDepth;
        header[9] = ColorTypeRgba;
        header[10] = 0;
        header[11] = 0;
        header[12] = 0;
        WriteChunk(stream, "IHDR", header);
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var typeBytes = new byte[4];
        for (var index = 0; index < 4; index++)
        {
            typeBytes[index] = (byte)type[index];
        }

        var lengthBytes = new byte[4];
        WriteBigEndian(lengthBytes, 0, (uint)data.Length);
        stream.Write(lengthBytes, 0, 4);
        stream.Write(typeBytes, 0, 4);
        stream.Write(data, 0, data.Length);

        var crcBytes = new byte[4];
        WriteBigEndian(crcBytes, 0, Crc(typeBytes, data));
        stream.Write(crcBytes, 0, 4);
    }

    private static void WriteBigEndian(byte[] target, int offset, uint value)
    {
        target[offset + 0] = (byte)((value >> 24) & 0xFF);
        target[offset + 1] = (byte)((value >> 16) & 0xFF);
        target[offset + 2] = (byte)((value >> 8) & 0xFF);
        target[offset + 3] = (byte)(value & 0xFF);
    }

    private static uint Crc(byte[] type, byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        for (var index = 0; index < type.Length; index++)
        {
            crc = CrcTable[(crc ^ type[index]) & 0xFF] ^ (crc >> 8);
        }

        for (var index = 0; index < data.Length; index++)
        {
            crc = CrcTable[(crc ^ data[index]) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (var index = 0; index < 256; index++)
        {
            var crc = (uint)index;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }

            table[index] = crc;
        }

        return table;
    }
}
