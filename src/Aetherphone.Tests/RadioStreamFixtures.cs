using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Aetherphone.Core.Radio;
using Concentus;

namespace Aetherphone.Tests;

internal static class RadioStreamFixtures
{
    public const int AudioPid = 0x101;
    private const int PmtPid = 0x100;

    public static byte[] ToneAac() => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "tone.aac"));

    public static byte[] ToneMp3() => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "tone.mp3"));

    public static byte[] TransportStream(byte[] elementary, byte streamType, int pesChunk = 2000)
    {
        var output = new MemoryStream();
        var continuity = 0;
        WritePacket(output, 0, true, Section(Pat()), ref continuity);
        var pmtContinuity = 0;
        WritePacket(output, PmtPid, true, Section(Pmt(streamType)), ref pmtContinuity);
        var audioContinuity = 0;
        for (var start = 0; start < elementary.Length; start += pesChunk)
        {
            var length = Math.Min(pesChunk, elementary.Length - start);
            var pes = Pes(elementary.AsSpan(start, length));
            var first = true;
            for (var offset = 0; offset < pes.Length; offset += 184)
            {
                var take = Math.Min(184, pes.Length - offset);
                WritePacket(output, AudioPid, first, pes.AsSpan(offset, take), ref audioContinuity);
                first = false;
            }
        }

        return output.ToArray();
    }

    private static byte[] Section(byte[] table)
    {
        var section = new byte[table.Length + 1];
        Array.Copy(table, 0, section, 1, table.Length);
        return section;
    }

    private static byte[] Pat()
    {
        var table = new byte[] { 0x00, 0xB0, 0x0D, 0x00, 0x01, 0xC1, 0x00, 0x00, 0x00, 0x01, 0xE0, 0x00, 0, 0, 0, 0 };
        table[10] = (byte)(0xE0 | (PmtPid >> 8));
        table[11] = PmtPid & 0xFF;
        return table;
    }

    private static byte[] Pmt(byte streamType)
    {
        var table = new byte[]
        {
            0x02, 0xB0, 0x12, 0x00, 0x01, 0xC1, 0x00, 0x00, 0xE1, 0x01, 0xF0, 0x00,
            streamType, (byte)(0xE0 | (AudioPid >> 8)), AudioPid & 0xFF, 0xF0, 0x00, 0, 0, 0, 0,
        };
        return table;
    }

    private static byte[] Pes(ReadOnlySpan<byte> payload)
    {
        var pes = new byte[14 + payload.Length];
        pes[2] = 0x01;
        pes[3] = 0xC0;
        var packetLength = 8 + payload.Length;
        pes[4] = (byte)(packetLength > 0xFFFF ? 0 : packetLength >> 8);
        pes[5] = (byte)(packetLength > 0xFFFF ? 0 : packetLength & 0xFF);
        pes[6] = 0x80;
        pes[7] = 0x80;
        pes[8] = 5;
        pes[9] = 0x21;
        pes[11] = 0x01;
        pes[13] = 0x01;
        payload.CopyTo(pes.AsSpan(14));
        return pes;
    }

    private static void WritePacket(MemoryStream output, int pid, bool unitStart, ReadOnlySpan<byte> payload,
        ref int continuity)
    {
        var packet = new byte[TsDemuxer.PacketLength];
        packet[0] = 0x47;
        packet[1] = (byte)((unitStart ? 0x40 : 0) | ((pid >> 8) & 0x1F));
        packet[2] = (byte)(pid & 0xFF);
        var stuffing = 184 - payload.Length;
        if (stuffing > 0)
        {
            packet[3] = (byte)(0x30 | (continuity & 0x0F));
            packet[4] = (byte)(stuffing - 1);
            if (stuffing > 1)
            {
                packet[5] = 0x00;
                for (var index = 6; index < 4 + stuffing; index++)
                {
                    packet[index] = 0xFF;
                }
            }
        }
        else
        {
            packet[3] = (byte)(0x10 | (continuity & 0x0F));
        }

        payload.CopyTo(packet.AsSpan(4 + Math.Max(0, stuffing)));
        continuity++;
        output.Write(packet);
    }

    public static byte[] OggOpus(int frames, int channels, string artist, string title)
    {
        var encoder = OpusCodecFactory.CreateEncoder(48000, channels, Concentus.Enums.OpusApplication.OPUS_APPLICATION_AUDIO);
        var pcm = new short[960 * channels];
        var packet = new byte[1275];
        var output = new MemoryStream();
        var sequence = 0;
        WritePage(output, 0x02, ref sequence, OpusHead(channels));
        WritePage(output, 0x00, ref sequence, OpusTags(artist, title));
        for (var frame = 0; frame < frames; frame++)
        {
            for (var sample = 0; sample < 960; sample++)
            {
                var value = (short)(Math.Sin((frame * 960 + sample) * 2 * Math.PI * 440 / 48000) * 8000);
                for (var channel = 0; channel < channels; channel++)
                {
                    pcm[sample * channels + channel] = value;
                }
            }

            var length = encoder.Encode(pcm, 960, packet, packet.Length);
            WritePage(output, 0x00, ref sequence, packet.AsSpan(0, length));
        }

        return output.ToArray();
    }

    private static byte[] OpusHead(int channels)
    {
        var head = new byte[19];
        "OpusHead"u8.CopyTo(head);
        head[8] = 1;
        head[9] = (byte)channels;
        BinaryPrimitives.WriteUInt32LittleEndian(head.AsSpan(12), 48000);
        return head;
    }

    private static byte[] OpusTags(string artist, string title)
    {
        var stream = new MemoryStream();
        stream.Write("OpusTags"u8);
        WriteString(stream, "Aetherphone tests");
        WriteInt(stream, 2);
        WriteString(stream, "ARTIST=" + artist);
        WriteString(stream, "TITLE=" + title);
        return stream.ToArray();
    }

    private static void WriteString(MemoryStream stream, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteInt(stream, bytes.Length);
        stream.Write(bytes);
    }

    private static void WriteInt(MemoryStream stream, int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WritePage(MemoryStream output, byte headerType, ref int sequence, ReadOnlySpan<byte> packet)
    {
        var laces = packet.Length / 255 + 1;
        var header = new byte[27 + laces];
        "OggS"u8.CopyTo(header);
        header[5] = headerType;
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(14), 0x1234);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(18), sequence++);
        header[26] = (byte)laces;
        for (var lace = 0; lace < laces - 1; lace++)
        {
            header[27 + lace] = 255;
        }

        header[27 + laces - 1] = (byte)(packet.Length % 255);
        output.Write(header);
        output.Write(packet);
    }
}

internal sealed class FakeRadioHandler : HttpMessageHandler
{
    private readonly Dictionary<string, (string ContentType, byte[] Body, string? MetaInterval)> routes = new();

    public int RequestCount { get; private set; }

    public void Add(string url, string contentType, byte[] body, string? metaInterval = null)
    {
        routes[url] = (contentType, body, metaInterval);
    }

    public void Add(string url, string contentType, string body)
    {
        Add(url, contentType, Encoding.UTF8.GetBytes(body));
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        var key = request.RequestUri!.ToString();
        if (!routes.TryGetValue(key, out var route))
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request };
        }

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            RequestMessage = request, Content = new ByteArrayContent(route.Body),
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue(route.ContentType);
        if (route.MetaInterval is not null)
        {
            response.Headers.TryAddWithoutValidation(IcyMetadataStream.IntervalHeader, route.MetaInterval);
        }

        return response;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(Send(request, cancellationToken));
    }
}
