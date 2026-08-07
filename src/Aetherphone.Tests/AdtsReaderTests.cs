using Aetherphone.Core.Radio;
using Xunit;

namespace Aetherphone.Tests;

public sealed class AdtsReaderTests
{
    [Fact]
    public void ParsesTheFormatOutOfAFrameHeader()
    {
        var header = Header(sampleRateIndex: 4, channelConfig: 2, frameLength: 512);

        Assert.True(AdtsReader.TryParseHeader(header, out var frame));
        Assert.Equal(44100, frame.SampleRate);
        Assert.Equal(2, frame.Channels);
        Assert.Equal(512, frame.Length);
    }

    [Theory]
    [InlineData(3, 48000)]
    [InlineData(5, 32000)]
    [InlineData(8, 16000)]
    public void ReadsEveryRateWeCanBeSent(int index, int expected)
    {
        Assert.True(AdtsReader.TryParseHeader(Header(index, 2, 400), out var frame));
        Assert.Equal(expected, frame.SampleRate);
    }

    [Fact]
    public void RejectsAHeaderThatIsNotAFrame()
    {
        var header = Header(4, 2, 512);
        header[0] = 0xFE;

        Assert.False(AdtsReader.TryParseHeader(header, out _));
    }

    [Fact]
    public void RejectsAReservedSampleRate()
    {
        Assert.False(AdtsReader.TryParseHeader(Header(13, 2, 512), out _));
    }

    [Fact]
    public void RejectsAFrameShorterThanItsOwnHeader()
    {
        // The length field is thirteen bits, so it cannot exceed the buffer; a nonsense small
        // length is the case that actually reaches us, from a corrupt or misaligned stream.
        Assert.False(AdtsReader.TryParseHeader(Header(4, 2, 4), out _));
    }

    [Fact]
    public void ReadsWholeFramesBackToBack()
    {
        var stream = new MemoryStream(Frames(300, 512, 700));
        var buffer = new byte[AdtsReader.MaxFrameLength];

        foreach (var expected in new[] { 300, 512, 700 })
        {
            Assert.True(AdtsReader.TryRead(stream, buffer, out var frame));
            Assert.Equal(expected, frame.Length);
            Assert.Equal(0xFF, buffer[0]);
        }

        Assert.False(AdtsReader.TryRead(stream, buffer, out _));
    }

    [Fact]
    public void ResynchronisesPastRubbishBeforeTheFirstFrame()
    {
        // A station joined mid-frame, or one that injects metadata, hands us bytes that are not a frame.
        var rubbish = new byte[] { 0x49, 0x44, 0x33, 0x04, 0x00, 0xFF, 0x00, 0x12 };
        var payload = Frames(400);
        var stream = new MemoryStream(rubbish.Concat(payload).ToArray());
        var buffer = new byte[AdtsReader.MaxFrameLength];

        Assert.True(AdtsReader.TryRead(stream, buffer, out var frame));
        Assert.Equal(400, frame.Length);
    }

    [Fact]
    public void ATruncatedFrameIsNotReturned()
    {
        var full = Frames(600);
        var stream = new MemoryStream(full.Take(200).ToArray());
        var buffer = new byte[AdtsReader.MaxFrameLength];

        Assert.False(AdtsReader.TryRead(stream, buffer, out var frame));
        Assert.False(frame.IsValid);
    }

    private static byte[] Header(int sampleRateIndex, int channelConfig, int frameLength)
    {
        var header = new byte[AdtsReader.HeaderLength];
        header[0] = 0xFF;
        header[1] = 0xF1;
        header[2] = (byte)(((sampleRateIndex & 0x0F) << 2) | ((channelConfig & 0x04) >> 2));
        header[3] = (byte)(((channelConfig & 0x03) << 6) | ((frameLength >> 11) & 0x03));
        header[4] = (byte)((frameLength >> 3) & 0xFF);
        header[5] = (byte)(((frameLength & 0x07) << 5) | 0x1F);
        header[6] = 0xFC;
        return header;
    }

    private static byte[] Frames(params int[] lengths)
    {
        var stream = new MemoryStream();
        foreach (var length in lengths)
        {
            stream.Write(Header(4, 2, length));
            stream.Write(new byte[length - AdtsReader.HeaderLength]);
        }

        return stream.ToArray();
    }
}
