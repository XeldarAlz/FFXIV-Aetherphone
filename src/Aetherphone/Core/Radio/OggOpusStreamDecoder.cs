using System.Buffers.Binary;
using System.Text;
using Concentus;
using NAudio.Wave;

namespace Aetherphone.Core.Radio;

internal sealed class OggOpusStreamDecoder : IStreamDecoder
{
    private const int SampleRate = 48000;
    private const int MaxFrameSamples = 5760;
    private const int PcmBits = 16;
    private const int MaxConsecutiveBadPackets = 64;
    private const int OpusHeadChannelsOffset = 9;
    private const int OpusHeadMappingOffset = 18;

    // RFC 6716 section 3.1: the s bit of every packet's TOC byte, which lets a listener who joined
    // after the OpusHead page still pick the right channel count.
    private const int StereoTocFlag = 0x04;
    private const string TitleKey = "TITLE=";
    private const string ArtistKey = "ARTIST=";
    private static readonly byte[] OpusHeadMagic = "OpusHead"u8.ToArray();
    private static readonly byte[] OpusTagsMagic = "OpusTags"u8.ToArray();
    private static readonly byte[] VorbisMagic = "\u0001vorbis"u8.ToArray();

    private readonly OggPacketReader reader;
    private readonly Action<string>? onTitle;
    private IOpusDecoder? decoder;
    private short[] pcm = Array.Empty<short>();
    private int channels;
    private WaveFormat? waveFormat;
    private int badPackets;

    static OggOpusStreamDecoder()
    {
        OpusCodecFactory.AttemptToUseNativeLibrary = false;
    }

    public OggOpusStreamDecoder(Stream source, Action<string>? onTitle)
    {
        reader = new OggPacketReader(source);
        this.onTitle = onTitle;
    }

    public WaveFormat? WaveFormat => waveFormat;

    public int Read(byte[] buffer)
    {
        while (true)
        {
            var length = reader.ReadPacket();
            if (length < 0)
            {
                return 0;
            }

            var packet = reader.Packet.AsSpan(0, length);
            if (packet.StartsWith(OpusHeadMagic))
            {
                Begin(packet);
                continue;
            }

            if (packet.StartsWith(OpusTagsMagic))
            {
                PublishTitle(packet);
                continue;
            }

            if (packet.StartsWith(VorbisMagic))
            {
                throw new NotSupportedException("Ogg Vorbis streams are not supported");
            }

            if (packet.Length == 0)
            {
                continue;
            }

            if (decoder is null)
            {
                CreateDecoder((packet[0] & StereoTocFlag) != 0 ? 2 : 1);
            }

            var produced = Decode(packet, buffer);
            if (produced > 0)
            {
                return produced;
            }

            if (badPackets >= MaxConsecutiveBadPackets)
            {
                return 0;
            }
        }
    }

    private int Decode(ReadOnlySpan<byte> packet, byte[] buffer)
    {
        int samples;
        try
        {
            samples = decoder!.Decode(packet, pcm.AsSpan(), MaxFrameSamples, false);
        }
        catch (Exception exception)
        {
            AepLog.Debug(exception, "[Radio] the opus decoder rejected a packet");
            badPackets++;
            return 0;
        }

        badPackets = 0;
        var bytes = Math.Min(samples * channels * sizeof(short), buffer.Length);
        Buffer.BlockCopy(pcm, 0, buffer, 0, bytes);
        return bytes;
    }

    // Icecast starts a fresh logical stream, header and all, at every track change, so a second
    // OpusHead is normal and only a channel count change needs a new decoder.
    private void Begin(ReadOnlySpan<byte> head)
    {
        if (head.Length <= OpusHeadMappingOffset)
        {
            return;
        }

        var announced = head[OpusHeadChannelsOffset];
        if (head[OpusHeadMappingOffset] != 0 || announced is < 1 or > 2)
        {
            throw new NotSupportedException("Multichannel Opus streams are not supported");
        }

        if (decoder is not null && announced == channels)
        {
            decoder.ResetState();
            return;
        }

        CreateDecoder(announced);
    }

    private void CreateDecoder(int channelCount)
    {
        decoder?.Dispose();
        channels = channelCount;
        decoder = OpusCodecFactory.CreateDecoder(SampleRate, channels);
        pcm = new short[MaxFrameSamples * channels];
        waveFormat = new WaveFormat(SampleRate, PcmBits, channels);
    }

    private void PublishTitle(ReadOnlySpan<byte> tags)
    {
        if (onTitle is null)
        {
            return;
        }

        var title = string.Empty;
        var artist = string.Empty;
        var offset = OpusTagsMagic.Length;
        if (!TrySkipString(tags, ref offset) || !TryReadInt(tags, ref offset, out var count))
        {
            return;
        }

        for (var commentIndex = 0; commentIndex < count; commentIndex++)
        {
            if (!TryReadInt(tags, ref offset, out var length) || length < 0 || offset + length > tags.Length)
            {
                break;
            }

            var comment = Encoding.UTF8.GetString(tags.Slice(offset, length));
            offset += length;
            if (comment.StartsWith(TitleKey, StringComparison.OrdinalIgnoreCase))
            {
                title = comment[TitleKey.Length..].Trim();
            }
            else if (comment.StartsWith(ArtistKey, StringComparison.OrdinalIgnoreCase))
            {
                artist = comment[ArtistKey.Length..].Trim();
            }
        }

        if (title.Length == 0)
        {
            return;
        }

        onTitle(artist.Length > 0 ? $"{artist} - {title}" : title);
    }

    private static bool TryReadInt(ReadOnlySpan<byte> data, ref int offset, out int value)
    {
        value = 0;
        if (offset + sizeof(int) > data.Length)
        {
            return false;
        }

        value = BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);
        offset += sizeof(int);
        return true;
    }

    private static bool TrySkipString(ReadOnlySpan<byte> data, ref int offset)
    {
        if (!TryReadInt(data, ref offset, out var length) || length < 0 || offset + length > data.Length)
        {
            return false;
        }

        offset += length;
        return true;
    }

    public void Dispose()
    {
        decoder?.Dispose();
        decoder = null;
    }
}

internal sealed class OggPacketReader
{
    private const int PageHeaderLength = 27;
    private const int SegmentCountOffset = 26;
    private const int HeaderTypeOffset = 5;
    private const int SerialOffset = 14;
    private const int ContinuedPacketFlag = 0x01;
    private const int MaxPacketLength = 1 << 20;
    private const int InitialPacketLength = 8192;

    private readonly Stream source;
    private readonly byte[] header = new byte[PageHeaderLength];
    private readonly byte[] segments = new byte[255];
    private readonly byte[] skip = new byte[255];
    private byte[] packet = new byte[InitialPacketLength];
    private int packetLength;
    private int segmentCount;
    private int segmentIndex;
    private int serial;
    private bool midPacket;
    private bool discarding;

    public OggPacketReader(Stream source)
    {
        this.source = source;
    }

    public byte[] Packet => packet;

    public int ReadPacket()
    {
        while (true)
        {
            if (segmentIndex >= segmentCount && !ReadPage())
            {
                return -1;
            }

            var lace = segments[segmentIndex++];
            if (!AppendSegment(lace))
            {
                return -1;
            }

            if (lace == 255)
            {
                midPacket = true;
                continue;
            }

            var length = packetLength;
            var dropped = discarding;
            packetLength = 0;
            midPacket = false;
            discarding = false;
            if (!dropped)
            {
                return length;
            }
        }
    }

    // A huge OpusTags packet carrying cover art is legal but useless to us, so anything past the cap
    // is read off the wire and thrown away instead of growing the buffer without bound.
    private bool AppendSegment(int lace)
    {
        if (discarding || packetLength + lace > MaxPacketLength)
        {
            discarding = true;
            return ReadExactly(skip, 0, lace);
        }

        if (packetLength + lace > packet.Length)
        {
            Array.Resize(ref packet, Math.Min(MaxPacketLength, packet.Length * 2));
        }

        if (!ReadExactly(packet, packetLength, lace))
        {
            return false;
        }

        packetLength += lace;
        return true;
    }

    private bool ReadPage()
    {
        if (!Synchronise())
        {
            return false;
        }

        if (!ReadExactly(header, 4, PageHeaderLength - 4))
        {
            return false;
        }

        segmentCount = header[SegmentCountOffset];
        segmentIndex = 0;
        if (!ReadExactly(segments, 0, segmentCount))
        {
            return false;
        }

        var pageSerial = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(SerialOffset));
        var continued = (header[HeaderTypeOffset] & ContinuedPacketFlag) != 0;
        if (pageSerial != serial || (!continued && midPacket))
        {
            packetLength = 0;
            midPacket = false;
            discarding = false;
        }

        if (continued && !midPacket)
        {
            discarding = true;
        }

        serial = pageSerial;
        return true;
    }

    private bool Synchronise()
    {
        var matched = 0;
        while (matched < 4)
        {
            var value = source.ReadByte();
            if (value < 0)
            {
                return false;
            }

            if (value == "OggS"[matched])
            {
                header[matched++] = (byte)value;
                continue;
            }

            matched = value == 'O' ? 1 : 0;
            header[0] = (byte)'O';
        }

        return true;
    }

    private bool ReadExactly(byte[] target, int offset, int count)
    {
        var filled = 0;
        while (filled < count)
        {
            var read = source.Read(target, offset + filled, count - filled);
            if (read <= 0)
            {
                return false;
            }

            filled += read;
        }

        return true;
    }
}
