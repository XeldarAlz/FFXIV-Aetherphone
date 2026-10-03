namespace Aetherphone.Core.Radio;

internal sealed class PrefixedStream : Stream
{
    private readonly byte[] prefix;
    private readonly int prefixLength;
    private readonly Stream source;
    private int prefixOffset;

    public PrefixedStream(byte[] prefix, int prefixLength, Stream source)
    {
        this.prefix = prefix;
        this.prefixLength = prefixLength;
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
        if (prefixOffset >= prefixLength)
        {
            return source.Read(buffer, offset, count);
        }

        var take = Math.Min(count, prefixLength - prefixOffset);
        Array.Copy(prefix, prefixOffset, buffer, offset, take);
        prefixOffset += take;
        return take;
    }

    public static int ReadHead(Stream source, byte[] head)
    {
        return source.ReadAtLeast(head, head.Length, false);
    }

    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
