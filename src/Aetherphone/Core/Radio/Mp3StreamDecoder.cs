using NAudio.Wave;

namespace Aetherphone.Core.Radio;

/// Frame at a time through the Windows ACM decoder, which is what has always played internet radio
/// here. Moved behind the decoder seam unchanged: same frame loop, same decompressor, same
/// end-of-stream behaviour.
internal sealed class Mp3StreamDecoder : IStreamDecoder
{
    public const int MaxDecodedBytes = 16384 * 4;

    private readonly ReadFullyStream source;
    private IMp3FrameDecompressor? decompressor;

    public Mp3StreamDecoder(Stream source)
    {
        this.source = new ReadFullyStream(source);
    }

    public WaveFormat? WaveFormat => decompressor?.OutputFormat;

    public int Read(byte[] buffer)
    {
        // Zero decoded bytes is not the end of the stream: the ACM decoder returns nothing while it
        // primes on the first frames, so keep pulling frames until one produces audio.
        while (true)
        {
            Mp3Frame? frame;
            try
            {
                frame = Mp3Frame.LoadFromStream(source);
            }
            catch (EndOfStreamException)
            {
                return 0;
            }

            if (frame is null)
            {
                return 0;
            }

            decompressor ??= CreateDecompressor(frame);
            var decoded = decompressor.DecompressFrame(frame, buffer, 0);
            if (decoded > 0)
            {
                return decoded;
            }
        }
    }

    private static IMp3FrameDecompressor CreateDecompressor(Mp3Frame frame)
    {
        var format = new Mp3WaveFormat(frame.SampleRate, frame.ChannelMode == ChannelMode.Mono ? 1 : 2,
            frame.FrameLength, frame.BitRate);
        return new AcmMp3FrameDecompressor(format);
    }

    public void Dispose()
    {
        decompressor?.Dispose();
    }
}

internal sealed class ReadFullyStream : Stream
{
    private readonly Stream source;

    public ReadFullyStream(Stream source)
    {
        this.source = source;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => 0;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var total = 0;
        while (total < count)
        {
            var read = source.Read(buffer, offset + total, count - total);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
