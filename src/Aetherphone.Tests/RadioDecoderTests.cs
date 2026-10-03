using System.Text;
using Aetherphone.Core.Radio;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RadioDecoderTests
{
    [Fact]
    public void AnAdtsStreamDecodesThroughMediaFoundation()
    {
        using var decoder = new AacStreamDecoder(new MemoryStream(RadioStreamFixtures.ToneAac()));
        var total = TsDemuxerTests.DecodeAll(decoder);

        Assert.NotNull(decoder.WaveFormat);
        Assert.Equal(44100, decoder.WaveFormat!.SampleRate);
        Assert.Equal(1, decoder.WaveFormat.Channels);
        var seconds = (double)total / decoder.WaveFormat.AverageBytesPerSecond;
        Assert.InRange(seconds, 1.3, 1.7);
    }

    [Fact]
    public void AnAdtsStreamWithRubbishInsideStillPlays()
    {
        var aac = RadioStreamFixtures.ToneAac();
        var corrupted = new byte[aac.Length + 50];
        Array.Copy(aac, 0, corrupted, 0, 4000);
        for (var index = 4000; index < 4050; index++)
        {
            corrupted[index] = 0x55;
        }

        Array.Copy(aac, 4000, corrupted, 4050, aac.Length - 4000);
        using var decoder = new AacStreamDecoder(new MemoryStream(corrupted));

        Assert.True(TsDemuxerTests.DecodeAll(decoder) > 0);
    }

    [Fact]
    public void AnOggOpusStreamDecodesAndPublishesItsTitle()
    {
        var titles = new List<string>();
        var ogg = RadioStreamFixtures.OggOpus(50, 2, "Nobuo Uematsu", "Answers");
        using var decoder = new OggOpusStreamDecoder(new MemoryStream(ogg), titles.Add);

        var total = TsDemuxerTests.DecodeAll(decoder);

        Assert.Equal(50 * 960 * 2 * sizeof(short), total);
        Assert.Equal(48000, decoder.WaveFormat!.SampleRate);
        Assert.Equal(2, decoder.WaveFormat.Channels);
        Assert.Equal(new[] { "Nobuo Uematsu - Answers" }, titles);
    }

    [Fact]
    public void AChainedOggStreamKeepsPlayingAcrossTheTrackChange()
    {
        var titles = new List<string>();
        var chained = RadioStreamFixtures.OggOpus(10, 1, "A", "First")
            .Concat(RadioStreamFixtures.OggOpus(10, 2, "B", "Second")).ToArray();
        using var decoder = new OggOpusStreamDecoder(new MemoryStream(chained), titles.Add);

        var total = TsDemuxerTests.DecodeAll(decoder);

        Assert.Equal(10 * 960 * 1 * sizeof(short) + 10 * 960 * 2 * sizeof(short), total);
        Assert.Equal(2, decoder.WaveFormat!.Channels);
        Assert.Equal(new[] { "A - First", "B - Second" }, titles);
    }

    [Fact]
    public void JoiningAnOggStreamMidPageResynchronisesAndGuessesChannelsFromTheToc()
    {
        var ogg = RadioStreamFixtures.OggOpus(20, 2, "A", "B");
        using var decoder = new OggOpusStreamDecoder(new MemoryStream(ogg[7..]), null);

        var total = TsDemuxerTests.DecodeAll(decoder);

        Assert.Equal(2, decoder.WaveFormat!.Channels);
        Assert.Equal(20 * 960 * 2 * sizeof(short), total);
    }

    [Fact]
    public void OggVorbisIsRefusedLoudly()
    {
        var page = new MemoryStream();
        page.Write("OggS"u8);
        page.Write(new byte[22]);
        page.WriteByte(1);
        page.WriteByte(30);
        page.Write("\u0001vorbis"u8);
        page.Write(new byte[23]);
        using var decoder = new OggOpusStreamDecoder(new MemoryStream(page.ToArray()), null);

        Assert.Throws<NotSupportedException>(() => decoder.Read(new byte[16384]));
    }

    [Fact]
    public void TheFactoryHonoursTheSniffedCodec()
    {
        using var opus = StreamDecoders.Create(StreamCodec.Opus, Stream.Null, null);
        using var aac = StreamDecoders.Create(StreamCodec.Aac, Stream.Null, null);
        using var mp3 = StreamDecoders.Create(StreamCodec.Unknown, Stream.Null, null);

        Assert.IsType<OggOpusStreamDecoder>(opus);
        Assert.IsType<AacStreamDecoder>(aac);
        Assert.IsType<Mp3StreamDecoder>(mp3);
    }

    [Fact]
    public void ByteQueueGrowsAndCompactsWithoutLosingOrder()
    {
        var queue = new ByteQueue(4);
        var expected = Encoding.ASCII.GetBytes("the quick brown fox jumps over the lazy dog");
        queue.Append(expected.AsSpan(0, 10));
        var head = new byte[3];
        queue.Read(head, 0, 3);
        queue.Append(expected.AsSpan(10));

        var rest = new byte[queue.Count];
        queue.Read(rest, 0, rest.Length);

        Assert.Equal(expected, head.Concat(rest).ToArray());
        Assert.Equal(0, queue.Count);
    }
}
