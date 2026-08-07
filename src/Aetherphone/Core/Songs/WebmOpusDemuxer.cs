using NEbml.Core;

namespace Aetherphone.Core.Songs;

internal sealed class WebmOpusDemuxer
{
    private const ulong EbmlHeaderId = 0x1A45DFA3;
    private const ulong SegmentId = 0x18538067;
    private const ulong InfoId = 0x1549A966;
    private const ulong TimestampScaleId = 0x2AD7B1;
    private const ulong DurationId = 0x4489;
    private const ulong TracksId = 0x1654AE6B;
    private const ulong TrackEntryId = 0xAE;
    private const ulong TrackNumberId = 0xD7;
    private const ulong CodecIdId = 0x86;
    private const ulong ClusterId = 0x1F43B675;
    private const ulong SimpleBlockId = 0xA3;
    private const ulong BlockGroupId = 0xA0;
    private const ulong BlockId = 0xA1;

    private readonly EbmlReader reader;
    private ulong timestampScale = 1_000_000;
    private ulong? opusTrackNumber;
    private bool hasPendingFirstCluster;
    private bool insideCluster;

    private byte[] blockBuffer = new byte[4096];

    public double? DurationSeconds { get; private set; }

    public WebmOpusDemuxer(Stream source)
    {
        reader = new EbmlReader(source);
    }

    public void ReadHeader()
    {
        if (!reader.ReadNext())
        {
            throw new InvalidDataException("Empty stream.");
        }

        if (reader.ElementId.EncodedValue == EbmlHeaderId)
        {
            if (!reader.ReadNext())
            {
                throw new InvalidDataException("Stream ended after EBML header.");
            }
        }

        if (reader.ElementId.EncodedValue != SegmentId)
        {
            throw new InvalidDataException(
                $"Expected top-level Segment element, got 0x{reader.ElementId.EncodedValue:X}.");
        }

        reader.EnterContainer();
        while (reader.ReadNext())
        {
            var id = reader.ElementId.EncodedValue;
            if (id == InfoId)
            {
                ReadInfo();
            }
            else if (id == TracksId)
            {
                ReadTracks();
            }
            else if (id == ClusterId)
            {
                hasPendingFirstCluster = true;
                return;
            }
        }
    }

    private void ReadInfo()
    {
        reader.EnterContainer();
        while (reader.ReadNext())
        {
            var id = reader.ElementId.EncodedValue;
            if (id == TimestampScaleId)
            {
                timestampScale = reader.ReadUInt();
            }
            else if (id == DurationId)
            {
                var ticks = reader.ReadFloat();
                DurationSeconds = ticks * timestampScale / 1_000_000_000.0;
            }
        }

        reader.LeaveContainer();
    }

    private void ReadTracks()
    {
        reader.EnterContainer();
        while (reader.ReadNext())
        {
            if (reader.ElementId.EncodedValue != TrackEntryId)
            {
                continue;
            }

            ulong? trackNumber = null;
            var isOpus = false;
            reader.EnterContainer();
            while (reader.ReadNext())
            {
                var id = reader.ElementId.EncodedValue;
                if (id == TrackNumberId)
                {
                    trackNumber = reader.ReadUInt();
                }
                else if (id == CodecIdId)
                {
                    isOpus = string.Equals(reader.ReadAscii(), "A_OPUS", StringComparison.Ordinal);
                }
            }

            reader.LeaveContainer();

            if (isOpus && trackNumber is not null)
            {
                opusTrackNumber = trackNumber;
            }
        }

        reader.LeaveContainer();
    }

    public ReadOnlyMemory<byte>? ReadNextPacket()
    {
        if (opusTrackNumber is null)
        {
            throw new InvalidOperationException("ReadHeader() must locate the Opus track before reading packets.");
        }

        while (true)
        {
            if (insideCluster)
            {
                var packet = ReadPacketsWithinCluster();
                if (packet is not null)
                {
                    return packet;
                }

                insideCluster = false;
                continue;
            }

            if (hasPendingFirstCluster)
            {
                hasPendingFirstCluster = false;
            }
            else if (!reader.ReadNext())
            {
                return null;
            }

            var id = reader.ElementId.EncodedValue;
            if (id == ClusterId)
            {
                reader.EnterContainer();
                insideCluster = true;
            }
        }
    }

    private ReadOnlyMemory<byte>? ReadPacketsWithinCluster()
    {
        while (reader.ReadNext())
        {
            var id = reader.ElementId.EncodedValue;
            if (id == SimpleBlockId)
            {
                var packet = ReadBlockPayload();
                if (packet is not null)
                {
                    return packet;
                }
            }
            else if (id == BlockGroupId)
            {
                reader.EnterContainer();
                ReadOnlyMemory<byte>? packet = null;
                while (reader.ReadNext())
                {
                    if (reader.ElementId.EncodedValue == BlockId)
                    {
                        packet = ReadBlockPayload();
                    }
                }

                reader.LeaveContainer();
                if (packet is not null)
                {
                    return packet;
                }
            }
        }

        reader.LeaveContainer();
        return null;
    }

    private ReadOnlyMemory<byte>? ReadBlockPayload()
    {
        var size = (int)reader.ElementSize;
        if (size > blockBuffer.Length)
        {
            blockBuffer = new byte[size];
        }

        var read = 0;
        while (read < size)
        {
            var bytesRead = reader.ReadBinary(blockBuffer, read, size - read);
            if (bytesRead < 0)
            {
                break;
            }

            read += bytesRead;
        }

        var offset = 0;
        var trackNumber = ReadMatroskaVint(blockBuffer, ref offset);
        offset += 2;
        var flags = blockBuffer[offset];
        offset += 1;

        var lacing = (flags >> 1) & 0x3;
        if (lacing != 0)
        {
            throw new NotSupportedException(
                $"Laced SimpleBlock encountered (lacing mode {lacing}); only unlaced frames are supported.");
        }

        if (trackNumber != opusTrackNumber)
        {
            return null;
        }

        return blockBuffer.AsMemory(offset, size - offset);
    }

    private static ulong ReadMatroskaVint(byte[] buffer, ref int offset)
    {
        var first = buffer[offset];
        var length = 1;
        var mask = (byte)0x80;
        while (mask != 0 && (first & mask) == 0)
        {
            mask >>= 1;
            length++;
        }

        if (mask == 0)
        {
            throw new InvalidDataException("Invalid Matroska vint in block header.");
        }

        ulong value = (ulong)(first & (mask - 1));
        for (var index = 1; index < length; index++)
        {
            value = (value << 8) | buffer[offset + index];
        }

        offset += length;
        return value;
    }
}
