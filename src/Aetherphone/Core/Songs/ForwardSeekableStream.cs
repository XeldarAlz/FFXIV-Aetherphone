namespace Aetherphone.Core.Songs;

internal sealed class ForwardSeekableStream : Stream
{
    private readonly Stream inner;
    private readonly byte[] discardBuffer = new byte[8192];
    private long position;

    public ForwardSeekableStream(Stream inner) => this.inner = inner;

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => inner.Length;

    public override long Position
    {
        get => position;
        set => throw new NotSupportedException("Only Seek(offset, SeekOrigin.Current) with a non-negative offset is supported.");
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (count == 0)
        {
            return 0;
        }

        var read = inner.Read(buffer, offset, count);
        position += read;
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        if (origin != SeekOrigin.Current || offset < 0)
        {
            throw new NotSupportedException(
                "ForwardSeekableStream only supports forward seeks relative to the current position.");
        }

        var remaining = offset;
        while (remaining > 0)
        {
            var chunk = Read(discardBuffer, 0, (int)Math.Min(remaining, discardBuffer.Length));
            if (chunk <= 0)
            {
                throw new EndOfStreamException("Reached end of stream while skipping forward.");
            }

            remaining -= chunk;
        }

        return position;
    }

    public override void Flush() => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
