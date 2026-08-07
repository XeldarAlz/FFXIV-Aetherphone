using NAudio.Wave;

namespace Aetherphone.Core.Radio;

/// One codec's worth of knowledge and nothing else. The decoder owns framing and format discovery;
/// connection, buffering, backpressure, reconnect and volume stay with the player, so every codec
/// gets the same behaviour instead of each one growing its own.
internal interface IStreamDecoder : IDisposable
{
    /// Valid only once Read has returned a positive count, since the format is discovered from the
    /// first frame on the wire rather than announced up front.
    WaveFormat? WaveFormat { get; }

    /// Decodes at most one frame into the buffer. Returns 0 when the stream has ended, which the
    /// player treats as a dropped source rather than an error.
    int Read(byte[] buffer);
}

internal static class StreamDecoders
{
    /// What the server says beats what a directory claims, since the codec column describes the
    /// station and the content type describes the bytes actually arriving.
    public static IStreamDecoder Create(string? contentType, string? declaredCodec, Stream source)
    {
        return IsAac(contentType, declaredCodec) ? new AacStreamDecoder(source) : new Mp3StreamDecoder(source);
    }

    private static bool IsAac(string? contentType, string? declaredCodec)
    {
        var type = (contentType ?? string.Empty).ToLowerInvariant();
        if (type.Contains("mpeg") || type.Contains("mp3"))
        {
            return false;
        }

        if (type.Contains("aac"))
        {
            return true;
        }

        return (declaredCodec ?? string.Empty).ToLowerInvariant().Contains("aac");
    }
}
