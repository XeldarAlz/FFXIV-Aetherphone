using System.Text;
using Aetherphone.Core.Radio;
using Xunit;

namespace Aetherphone.Tests;

public sealed class IcyMetadataStreamTests
{
    [Fact]
    public void AudioComesOutIdenticalWithTheMetadataRemoved()
    {
        var audio = Audio(300);
        var wire = Wire(interval: 100, audio, ("StreamTitle='Nocturne - Midnight';", 100), (null, 200));

        var titles = new List<string>();
        using var stream = new IcyMetadataStream(new MemoryStream(wire), 100, titles.Add);
        var read = ReadAll(stream);

        Assert.Equal(audio, read);
        Assert.Equal(new[] { "Nocturne - Midnight" }, titles);
    }

    [Fact]
    public void AnEmptyBlockMeansTheTrackHasNotChanged()
    {
        var audio = Audio(200);
        var wire = Wire(interval: 100, audio, (null, 100), (null, 200));

        var titles = new List<string>();
        using var stream = new IcyMetadataStream(new MemoryStream(wire), 100, titles.Add);
        var read = ReadAll(stream);

        Assert.Equal(audio, read);
        Assert.Empty(titles);
    }

    [Fact]
    public void EveryTrackChangeIsReported()
    {
        var audio = Audio(300);
        var wire = Wire(interval: 100, audio,
            ("StreamTitle='First';", 100),
            ("StreamTitle='Second';", 200));

        var titles = new List<string>();
        using var stream = new IcyMetadataStream(new MemoryStream(wire), 100, titles.Add);
        ReadAll(stream);

        Assert.Equal(new[] { "First", "Second" }, titles);
    }

    [Fact]
    public void ReadsSmallerThanTheIntervalStillLandOnTheBoundary()
    {
        // The decoder asks for whatever it wants, not for whole metadata intervals.
        var audio = Audio(250);
        var wire = Wire(interval: 100, audio, ("StreamTitle='Boundary';", 100), (null, 200));

        var titles = new List<string>();
        using var stream = new IcyMetadataStream(new MemoryStream(wire), 100, titles.Add);
        var read = ReadAll(stream, chunk: 7);

        Assert.Equal(audio, read);
        Assert.Equal(new[] { "Boundary" }, titles);
    }

    [Theory]
    [InlineData("StreamTitle='Artist - Track';StreamUrl='http://x';", "Artist - Track")]
    [InlineData("StreamTitle='Quoted ' inside';", "Quoted ' inside")]
    [InlineData("StreamUrl='http://x';", "")]
    public void TitlesAreLiftedOutOfTheBlock(string block, string expected)
    {
        var parsed = IcyMetadataStream.TryParseTitle(Encoding.UTF8.GetBytes(block), out var title);

        Assert.Equal(expected.Length > 0, parsed);
        Assert.Equal(expected, title);
    }

    [Theory]
    [InlineData("16000", 16000)]
    [InlineData("0", 0)]
    [InlineData("", 0)]
    [InlineData(null, 0)]
    [InlineData("nonsense", 0)]
    public void OnlyAUsableIntervalTurnsMetadataOn(string? header, int expected)
    {
        Assert.Equal(expected, IcyMetadataStream.ParseInterval(header));
    }

    private static byte[] Audio(int length)
    {
        var audio = new byte[length];
        for (var index = 0; index < length; index++)
        {
            audio[index] = (byte)(index % 251 + 1);
        }

        return audio;
    }

    /// Builds what the server actually sends: audio with metadata blocks spliced in at each interval.
    private static byte[] Wire(int interval, byte[] audio, params (string? Block, int At)[] blocks)
    {
        var wire = new MemoryStream();
        var written = 0;
        foreach (var (block, at) in blocks.OrderBy(entry => entry.At))
        {
            wire.Write(audio, written, at - written);
            written = at;
            if (block is null)
            {
                wire.WriteByte(0);
                continue;
            }

            var bytes = Encoding.UTF8.GetBytes(block);
            var padded = (bytes.Length + 15) / 16 * 16;
            wire.WriteByte((byte)(padded / 16));
            wire.Write(bytes);
            wire.Write(new byte[padded - bytes.Length]);
        }

        wire.Write(audio, written, audio.Length - written);
        return wire.ToArray();
    }

    private static byte[] ReadAll(Stream stream, int chunk = 64)
    {
        var output = new MemoryStream();
        var buffer = new byte[chunk];
        while (true)
        {
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read <= 0)
            {
                break;
            }

            output.Write(buffer, 0, read);
        }

        return output.ToArray();
    }
}
