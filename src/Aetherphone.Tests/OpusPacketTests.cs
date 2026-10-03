using Aetherphone.Core.Songs;
using Xunit;

namespace Aetherphone.Tests;

public sealed class OpusPacketTests
{
    [Theory]
    [InlineData(new byte[] { 0xFC }, 960)]
    [InlineData(new byte[] { 0xF8 }, 960)]
    [InlineData(new byte[] { 0xFD }, 1920)]
    [InlineData(new byte[] { 0x80 }, 120)]
    [InlineData(new byte[] { 0x18 }, 2880)]
    [InlineData(new byte[] { 0x60 }, 480)]
    [InlineData(new byte[] { 0xFB, 0x03 }, 2880)]
    public void The_sample_count_comes_from_the_table_of_contents_byte(byte[] packet, int expected)
    {
        Assert.Equal(expected, OpusPacket.SamplesPerChannel(packet, 48_000));
    }

    [Fact]
    public void An_empty_or_truncated_packet_counts_as_no_samples()
    {
        Assert.Equal(0, OpusPacket.SamplesPerChannel(ReadOnlySpan<byte>.Empty, 48_000));
        Assert.Equal(0, OpusPacket.SamplesPerChannel(new byte[] { 0xFB }, 48_000));
    }
}
