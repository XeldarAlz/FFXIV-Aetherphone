using Aetherphone.Core.Radio;
using Xunit;

namespace Aetherphone.Tests;

public sealed class TsDemuxerTests
{
    [Fact]
    public void RecoversTheAdtsStreamFromTransportPackets()
    {
        var aac = RadioStreamFixtures.ToneAac();
        var transport = RadioStreamFixtures.TransportStream(aac, 0x0F);
        var demuxer = new TsDemuxer();
        var output = new ByteQueue(1024);

        demuxer.Demux(transport, output);

        Assert.Equal(StreamCodec.Aac, demuxer.Codec);
        Assert.Equal(aac, Drain(output));
    }

    [Theory]
    [InlineData(0x03)]
    [InlineData(0x04)]
    public void MpegAudioStreamTypesAreMp3(byte streamType)
    {
        var demuxer = new TsDemuxer();
        demuxer.Demux(RadioStreamFixtures.TransportStream(new byte[500], streamType), new ByteQueue(1024));

        Assert.Equal(StreamCodec.Mp3, demuxer.Codec);
    }

    [Fact]
    public void AnAc3OnlyProgramIsReportedAsUnsupported()
    {
        var demuxer = new TsDemuxer();
        var output = new ByteQueue(1024);
        demuxer.Demux(RadioStreamFixtures.TransportStream(new byte[500], 0x81), output);

        Assert.Equal(StreamCodec.Unknown, demuxer.Codec);
        Assert.True(demuxer.HasUnsupportedAudio);
        Assert.Equal(0, output.Count);
    }

    [Fact]
    public void SkipsRubbishBeforeTheFirstSyncByte()
    {
        var aac = RadioStreamFixtures.ToneAac();
        var transport = RadioStreamFixtures.TransportStream(aac, 0x0F);
        var shifted = new byte[transport.Length + 37];
        Array.Copy(transport, 0, shifted, 37, transport.Length);
        var demuxer = new TsDemuxer();
        var output = new ByteQueue(1024);

        demuxer.Demux(shifted, output);

        Assert.Equal(aac, Drain(output));
    }

    [Fact]
    public void ADemuxedStreamDecodesToPcm()
    {
        var transport = RadioStreamFixtures.TransportStream(RadioStreamFixtures.ToneAac(), 0x0F);
        var demuxer = new TsDemuxer();
        var output = new ByteQueue(1024);
        demuxer.Demux(transport, output);

        using var decoder = new AacStreamDecoder(new MemoryStream(Drain(output)));
        var total = DecodeAll(decoder);

        Assert.NotNull(decoder.WaveFormat);
        Assert.Equal(44100, decoder.WaveFormat!.SampleRate);
        Assert.True(total > decoder.WaveFormat.AverageBytesPerSecond, $"decoded {total} bytes");
    }

    internal static byte[] Drain(ByteQueue queue)
    {
        var bytes = new byte[queue.Count];
        queue.Read(bytes, 0, bytes.Length);
        return bytes;
    }

    internal static long DecodeAll(IStreamDecoder decoder)
    {
        var buffer = new byte[Mp3StreamDecoder.MaxDecodedBytes];
        long total = 0;
        while (true)
        {
            var count = decoder.Read(buffer);
            if (count <= 0)
            {
                return total;
            }

            total += count;
        }
    }
}
