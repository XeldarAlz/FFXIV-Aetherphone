namespace Aetherphone.Core.Songs;

internal static class OpusPacket
{
    private const int ReferenceSampleRate = 48_000;
    private static readonly int[] SilkFrameSamples = [480, 960, 1920, 2880];
    private static readonly int[] HybridFrameSamples = [480, 960];
    private static readonly int[] CeltFrameSamples = [120, 240, 480, 960];

    public static int SamplesPerChannel(ReadOnlySpan<byte> packet, int sampleRate)
    {
        if (packet.IsEmpty)
        {
            return 0;
        }

        var toc = packet[0];
        var frames = FrameCount(packet, toc);
        if (frames == 0)
        {
            return 0;
        }

        return (int)((long)frames * FrameSamples(toc >> 3) * sampleRate / ReferenceSampleRate);
    }

    private static int FrameCount(ReadOnlySpan<byte> packet, byte toc)
    {
        return (toc & 0x03) switch
        {
            0 => 1,
            1 or 2 => 2,
            _ => packet.Length < 2 ? 0 : packet[1] & 0x3F,
        };
    }

    private static int FrameSamples(int configuration)
    {
        if (configuration < 12)
        {
            return SilkFrameSamples[configuration & 0x03];
        }

        return configuration < 16
            ? HybridFrameSamples[configuration & 0x01]
            : CeltFrameSamples[configuration & 0x03];
    }
}
