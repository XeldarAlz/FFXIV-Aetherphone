using Aetherphone.Core.Songs;
using Xunit;

namespace Aetherphone.Tests;

public sealed class WebmOpusDemuxerTests
{
    [Fact]
    public void ReadsDurationAndSinglePacketFromMinimalContainer()
    {
        var payload = new byte[] { 1, 2, 3, 4, 5 };
        var bytes = BuildMinimalWebm(trackNumber: 1, durationSeconds: 12.5, payload);

        using var stream = new MemoryStream(bytes);
        var demuxer = new WebmOpusDemuxer(stream);
        demuxer.ReadHeader();

        Assert.NotNull(demuxer.DurationSeconds);
        Assert.Equal(12.5, demuxer.DurationSeconds!.Value, 3);

        var packet = demuxer.ReadNextPacket();
        Assert.NotNull(packet);
        Assert.Equal(payload, packet.Value.ToArray());

        Assert.Null(demuxer.ReadNextPacket());
    }

    [Fact]
    public void SkipsBlocksForOtherTracks()
    {
        var wantedPayload = new byte[] { 9, 9, 9 };
        var bytes = BuildMinimalWebm(
            trackNumber: 2,
            durationSeconds: 1.0,
            wantedPayload,
            extraBlockTrackNumber: 1,
            extraBlockPayload: new byte[] { 0, 0 });

        using var stream = new MemoryStream(bytes);
        var demuxer = new WebmOpusDemuxer(stream);
        demuxer.ReadHeader();

        var packet = demuxer.ReadNextPacket();
        Assert.NotNull(packet);
        Assert.Equal(wantedPayload, packet.Value.ToArray());
        Assert.Null(demuxer.ReadNextPacket());
    }

    private static byte[] BuildMinimalWebm(
        ulong trackNumber, double durationSeconds, byte[] payload,
        ulong? extraBlockTrackNumber = null, byte[]? extraBlockPayload = null)
    {
        const ulong timestampScale = 1_000_000;
        var durationTicks = durationSeconds * 1_000_000_000.0 / timestampScale;

        var info = Element(0x1549A966,
            Concat(
                Element(0x2AD7B1, UInt(timestampScale)),
                Element(0x4489, Float(durationTicks))));

        var trackEntry = Element(0xAE,
            Concat(
                Element(0xD7, UInt(trackNumber)),
                Element(0x86, Ascii("A_OPUS"))));
        var tracks = Element(0x1654AE6B, trackEntry);

        var blocks = new List<byte[]>();
        if (extraBlockTrackNumber is not null)
        {
            blocks.Add(SimpleBlock(extraBlockTrackNumber.Value, extraBlockPayload ?? Array.Empty<byte>()));
        }

        blocks.Add(SimpleBlock(trackNumber, payload));
        var cluster = Element(0x1F43B675, Concat(blocks.ToArray()));

        var segment = Element(0x18538067, Concat(info, tracks, cluster));

        var ebmlHeader = Element(0x1A45DFA3, Array.Empty<byte>());
        return Concat(ebmlHeader, segment);
    }

    private static byte[] SimpleBlock(ulong trackNumber, byte[] payload)
    {
        var header = Concat(
            MatroskaVint(trackNumber),
            new byte[] { 0, 0 },
            new byte[] { 0x00 });
        return Element(0xA3, Concat(header, payload));
    }

    private static byte[] Element(ulong id, byte[] content)
    {
        return Concat(EbmlId(id), EbmlSize((ulong)content.Length), content);
    }

    private static byte[] EbmlId(ulong id)
    {
        var length = id switch
        {
            <= 0xFF => 1,
            <= 0xFFFF => 2,
            <= 0xFFFFFF => 3,
            _ => 4,
        };
        var bytes = new byte[length];
        for (var index = length - 1; index >= 0; index--)
        {
            bytes[index] = (byte)(id & 0xFF);
            id >>= 8;
        }

        return bytes;
    }

    private static byte[] EbmlSize(ulong size)
    {
        if (size > 0x7E)
        {
            throw new InvalidOperationException("Test helper only supports sizes < 0x7F.");
        }

        return new[] { (byte)(0x80 | size) };
    }

    private static byte[] MatroskaVint(ulong value)
    {
        if (value > 0x7E)
        {
            throw new InvalidOperationException("Test helper only supports track numbers < 0x7F.");
        }

        return new[] { (byte)(0x80 | value) };
    }

    private static byte[] UInt(ulong value)
    {
        var bytes = new List<byte>();
        while (value > 0)
        {
            bytes.Insert(0, (byte)(value & 0xFF));
            value >>= 8;
        }

        return bytes.Count == 0 ? new byte[] { 0 } : bytes.ToArray();
    }

    private static byte[] Float(double value)
    {
        var bits = BitConverter.DoubleToInt64Bits(value);
        var bytes = BitConverter.GetBytes(bits);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        return bytes;
    }

    private static byte[] Ascii(string value) => System.Text.Encoding.ASCII.GetBytes(value);

    private static byte[] Concat(params byte[][] parts)
    {
        var total = 0;
        foreach (var part in parts)
        {
            total += part.Length;
        }

        var result = new byte[total];
        var offset = 0;
        foreach (var part in parts)
        {
            Array.Copy(part, 0, result, offset, part.Length);
            offset += part.Length;
        }

        return result;
    }
}
