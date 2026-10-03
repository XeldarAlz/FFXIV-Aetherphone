using Aetherphone.Core.Radio;
using Xunit;

namespace Aetherphone.Tests;

public sealed class StreamSnifferTests
{
    [Fact]
    public void AdtsBytesBeatAnMpegContentType()
    {
        var head = RadioStreamFixtures.ToneAac().AsSpan(0, StreamSniffer.HeadLength);

        Assert.Equal(StreamCodec.Aac, StreamSniffer.Detect("audio/mpeg", "MP3", head));
    }

    [Fact]
    public void Mp3BytesBeatAnAacLabel()
    {
        var head = RadioStreamFixtures.ToneMp3();

        Assert.Equal(StreamCodec.Mp3, StreamSniffer.Detect("audio/aac", "AAC", head));
    }

    [Fact]
    public void FindsFramesAfterJoiningMidFrame()
    {
        var head = RadioStreamFixtures.ToneAac().AsSpan(333, StreamSniffer.HeadLength);

        Assert.Equal(StreamCodec.Aac, StreamSniffer.FromBytes(head));
    }

    [Fact]
    public void OggPagesAreOpus()
    {
        var head = RadioStreamFixtures.OggOpus(4, 2, "a", "b");

        Assert.Equal(StreamCodec.Opus, StreamSniffer.FromBytes(head));
    }

    [Fact]
    public void NoiseFallsBackToTheLabels()
    {
        var noise = new byte[2048];
        new Random(7).NextBytes(noise);
        for (var index = 0; index < noise.Length; index++)
        {
            if (noise[index] == 0xFF)
            {
                noise[index] = 0;
            }
        }

        Assert.Equal(StreamCodec.Unknown, StreamSniffer.FromBytes(noise));
        Assert.Equal(StreamCodec.Aac, StreamSniffer.Detect("audio/aacp", null, noise));
        Assert.Equal(StreamCodec.Opus, StreamSniffer.Detect(null, "OGG", noise));
        Assert.Equal(StreamCodec.Mp3, StreamSniffer.Detect(null, null, noise));
    }

    [Fact]
    public void SkipsALeadingId3Tag()
    {
        var aac = RadioStreamFixtures.ToneAac();
        var tagged = new byte[10 + 20 + 2048];
        tagged[0] = (byte)'I';
        tagged[1] = (byte)'D';
        tagged[2] = (byte)'3';
        tagged[3] = 4;
        tagged[9] = 20;
        Array.Copy(aac, 0, tagged, 30, 2048);

        Assert.Equal(30, StreamSniffer.Id3TagLength(tagged));
        Assert.Equal(StreamCodec.Aac, StreamSniffer.FromBytes(tagged));
    }

    [Fact]
    public void Mp3FrameLengthsMatchTheStandardFormula()
    {
        Assert.True(MpegAudioHeader.TryParse(new byte[] { 0xFF, 0xFB, 0x90, 0x64 }, out var mpeg1Layer3));
        Assert.Equal(417, mpeg1Layer3);
        Assert.True(MpegAudioHeader.TryParse(new byte[] { 0xFF, 0xF3, 0x80, 0xC4 }, out var mpeg2Layer3));
        Assert.Equal(72 * 64000 / 22050, mpeg2Layer3);
        Assert.False(MpegAudioHeader.TryParse(new byte[] { 0xFF, 0xF1, 0x50, 0x80 }, out _));
    }
}
