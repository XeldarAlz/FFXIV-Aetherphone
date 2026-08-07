namespace Aetherphone.Core.Radio;

internal readonly struct AdtsFrame
{
    public readonly int SampleRate;
    public readonly int Channels;
    public readonly int Length;

    public AdtsFrame(int sampleRate, int channels, int length)
    {
        SampleRate = sampleRate;
        Channels = channels;
        Length = length;
    }

    public bool IsValid => Length > 0;
}

/// Reads AAC frames the way the mp3 path reads mp3 frames: straight off the socket, one frame at a
/// time, with no container and nothing to seek. Media Foundation's AAC decoder takes whole ADTS
/// frames, so the header is parsed only to learn the format and the frame is passed through intact.
internal static class AdtsReader
{
    // The length field is thirteen bits, so a frame can never exceed this and the buffer is safe.
    public const int MaxFrameLength = 8192;
    public const int HeaderLength = 7;

    private static readonly int[] SampleRates =
    {
        96000, 88200, 64000, 48000, 44100, 32000, 24000, 22050,
        16000, 12000, 11025, 8000, 7350, 0, 0, 0,
    };

    private static readonly int[] ChannelCounts = { 0, 1, 2, 3, 4, 5, 6, 8 };

    public static bool TryParseHeader(ReadOnlySpan<byte> header, out AdtsFrame frame)
    {
        frame = default;
        if (header.Length < HeaderLength)
        {
            return false;
        }

        // Syncword is twelve set bits, and layer must be zero for AAC.
        if (header[0] != 0xFF || (header[1] & 0xF0) != 0xF0 || (header[1] & 0x06) != 0)
        {
            return false;
        }

        var sampleRate = SampleRates[(header[2] & 0x3C) >> 2];
        if (sampleRate == 0)
        {
            return false;
        }

        var channels = ChannelCounts[((header[2] & 0x01) << 2) | ((header[3] & 0xC0) >> 6)];
        if (channels == 0)
        {
            return false;
        }

        var length = ((header[3] & 0x03) << 11) | (header[4] << 3) | ((header[5] & 0xE0) >> 5);
        if (length < HeaderLength || length > MaxFrameLength)
        {
            return false;
        }

        frame = new AdtsFrame(sampleRate, channels, length);
        return true;
    }

    /// Fills buffer with one whole ADTS frame, resynchronising past anything that is not a frame,
    /// which is what a station that starts mid-frame or injects metadata leaves in the stream.
    public static bool TryRead(Stream source, byte[] buffer, out AdtsFrame frame)
    {
        frame = default;
        if (buffer.Length < MaxFrameLength)
        {
            return false;
        }

        var header = 0;
        while (true)
        {
            while (header < HeaderLength)
            {
                var read = source.Read(buffer, header, HeaderLength - header);
                if (read <= 0)
                {
                    return false;
                }

                header += read;
            }

            if (TryParseHeader(buffer.AsSpan(0, HeaderLength), out frame))
            {
                break;
            }

            // Not a frame start: drop the first byte and try again from the next one.
            Array.Copy(buffer, 1, buffer, 0, HeaderLength - 1);
            header = HeaderLength - 1;
        }

        var filled = HeaderLength;
        while (filled < frame.Length)
        {
            var read = source.Read(buffer, filled, frame.Length - filled);
            if (read <= 0)
            {
                frame = default;
                return false;
            }

            filled += read;
        }

        return true;
    }
}
