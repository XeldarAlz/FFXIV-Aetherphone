using NAudio.Wave;

namespace Aetherphone.Core.Radio;

/// One codec's worth of knowledge and nothing else. The decoder owns framing and format discovery;
/// connection, buffering, backpressure, reconnect and volume stay with the player, so every codec
/// gets the same behaviour instead of each one growing its own.
internal interface IStreamDecoder : IDisposable
{
    /// Valid only once Read has returned a positive count, since the format is discovered from the
    /// first frame on the wire rather than announced up front. A chained Ogg stream or an HE-AAC
    /// renegotiation can change it later, so the player compares it after every read.
    WaveFormat? WaveFormat { get; }

    /// Decodes at most one frame into the buffer. Returns 0 when the stream has ended, which the
    /// player treats as a dropped source rather than an error.
    int Read(byte[] buffer);
}

internal static class StreamDecoders
{
    public static IStreamDecoder Create(string? contentType, string? declaredCodec, Stream source)
    {
        return Create(StreamSniffer.Detect(contentType, declaredCodec, ReadOnlySpan<byte>.Empty), source, null);
    }

    public static IStreamDecoder Create(StreamCodec codec, Stream source, Action<string>? onTitle)
    {
        return codec switch
        {
            StreamCodec.Aac => new AacStreamDecoder(source),
            StreamCodec.Opus => new OggOpusStreamDecoder(source, onTitle),
            _ => new Mp3StreamDecoder(source),
        };
    }
}
