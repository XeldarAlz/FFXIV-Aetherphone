namespace Aetherphone.Core.Radio;

internal sealed class TsDemuxer
{
    public const int PacketLength = 188;
    private const byte SyncByte = 0x47;
    private const int PatPid = 0;
    private const int NoPid = -1;
    private const byte PatTableId = 0x00;
    private const byte PmtTableId = 0x02;
    private const int PesHeaderLength = 9;

    // ISO/IEC 13818-1 table 2-34 stream_type values.
    private const byte StreamTypeMpeg1Audio = 0x03;
    private const byte StreamTypeMpeg2Audio = 0x04;
    private const byte StreamTypeAdtsAac = 0x0F;
    private const byte StreamTypeLatmAac = 0x11;
    private const byte StreamTypeAc3 = 0x81;
    private const byte StreamTypeEac3 = 0x87;

    private int pmtPid = NoPid;
    private int audioPid = NoPid;
    private bool inPes;

    public StreamCodec Codec { get; private set; }

    public bool HasUnsupportedAudio { get; private set; }

    public void Reset()
    {
        pmtPid = NoPid;
        audioPid = NoPid;
        inPes = false;
        Codec = StreamCodec.Unknown;
        HasUnsupportedAudio = false;
    }

    public void Demux(ReadOnlySpan<byte> data, ByteQueue output)
    {
        var offset = FindSync(data, 0);
        while (offset >= 0 && offset + PacketLength <= data.Length)
        {
            if (data[offset] != SyncByte)
            {
                offset = FindSync(data, offset + 1);
                continue;
            }

            ProcessPacket(data.Slice(offset, PacketLength), output);
            offset += PacketLength;
        }
    }

    public static bool LooksLikeTransportStream(ReadOnlySpan<byte> data)
    {
        if (data.Length < PacketLength || data[0] != SyncByte)
        {
            return false;
        }

        return data.Length < PacketLength * 2 || data[PacketLength] == SyncByte;
    }

    private static int FindSync(ReadOnlySpan<byte> data, int from)
    {
        for (var offset = from; offset + PacketLength <= data.Length; offset++)
        {
            if (data[offset] != SyncByte)
            {
                continue;
            }

            var next = offset + PacketLength;
            if (next >= data.Length || data[next] == SyncByte)
            {
                return offset;
            }
        }

        return -1;
    }

    private void ProcessPacket(ReadOnlySpan<byte> packet, ByteQueue output)
    {
        var unitStart = (packet[1] & 0x40) != 0;
        var pid = ((packet[1] & 0x1F) << 8) | packet[2];
        var adaptation = (packet[3] >> 4) & 0x03;
        if ((adaptation & 0x01) == 0)
        {
            return;
        }

        var payloadStart = 4;
        if ((adaptation & 0x02) != 0)
        {
            payloadStart += 1 + packet[4];
        }

        if (payloadStart >= PacketLength)
        {
            return;
        }

        var payload = packet[payloadStart..];
        if (pid == PatPid)
        {
            ParsePat(SectionOf(payload, unitStart));
            return;
        }

        if (pid == pmtPid)
        {
            ParsePmt(SectionOf(payload, unitStart));
            return;
        }

        if (pid != audioPid)
        {
            return;
        }

        if (unitStart)
        {
            inPes = TryStripPesHeader(payload, out payload);
        }

        if (inPes)
        {
            output.Append(payload);
        }
    }

    private static ReadOnlySpan<byte> SectionOf(ReadOnlySpan<byte> payload, bool unitStart)
    {
        if (!unitStart || payload.IsEmpty)
        {
            return ReadOnlySpan<byte>.Empty;
        }

        var pointer = payload[0];
        return 1 + pointer < payload.Length ? payload[(1 + pointer)..] : ReadOnlySpan<byte>.Empty;
    }

    private static int SectionEnd(ReadOnlySpan<byte> section)
    {
        var sectionLength = ((section[1] & 0x0F) << 8) | section[2];
        return Math.Min(section.Length, 3 + sectionLength - 4);
    }

    private void ParsePat(ReadOnlySpan<byte> section)
    {
        if (section.Length < 8 || section[0] != PatTableId)
        {
            return;
        }

        var end = SectionEnd(section);
        for (var entry = 8; entry + 4 <= end; entry += 4)
        {
            var programNumber = (section[entry] << 8) | section[entry + 1];
            if (programNumber == 0)
            {
                continue;
            }

            pmtPid = ((section[entry + 2] & 0x1F) << 8) | section[entry + 3];
            return;
        }
    }

    private void ParsePmt(ReadOnlySpan<byte> section)
    {
        if (section.Length < 12 || section[0] != PmtTableId)
        {
            return;
        }

        var end = SectionEnd(section);
        var programInfoLength = ((section[10] & 0x0F) << 8) | section[11];
        var unsupported = false;
        for (var entry = 12 + programInfoLength; entry + 5 <= end;)
        {
            var streamType = section[entry];
            var elementaryPid = ((section[entry + 1] & 0x1F) << 8) | section[entry + 2];
            var infoLength = ((section[entry + 3] & 0x0F) << 8) | section[entry + 4];
            entry += 5 + infoLength;

            var codec = CodecOf(streamType);
            if (codec != StreamCodec.Unknown)
            {
                if (elementaryPid != audioPid)
                {
                    audioPid = elementaryPid;
                    inPes = false;
                }

                Codec = codec;
                HasUnsupportedAudio = false;
                return;
            }

            unsupported |= streamType is StreamTypeLatmAac or StreamTypeAc3 or StreamTypeEac3;
        }

        HasUnsupportedAudio = unsupported;
    }

    private static StreamCodec CodecOf(byte streamType)
    {
        return streamType switch
        {
            StreamTypeAdtsAac => StreamCodec.Aac,
            StreamTypeMpeg1Audio or StreamTypeMpeg2Audio => StreamCodec.Mp3,
            _ => StreamCodec.Unknown,
        };
    }

    private static bool TryStripPesHeader(ReadOnlySpan<byte> payload, out ReadOnlySpan<byte> body)
    {
        body = ReadOnlySpan<byte>.Empty;
        if (payload.Length < PesHeaderLength || payload[0] != 0 || payload[1] != 0 || payload[2] != 1)
        {
            return false;
        }

        var start = PesHeaderLength + payload[8];
        if (start > payload.Length)
        {
            return false;
        }

        body = payload[start..];
        return true;
    }
}
