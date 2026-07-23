namespace Aetherphone.Core.Songs;

/// <summary>
/// Wraps a forward-only network stream so NEbml's EbmlReader can skip past elements without
/// tearing down and re-opening the underlying HTTP connection.
///
/// NEbml's ReadNext() calls Skip() on every call, and Skip() always tries Stream.Seek() first
/// with no fallback if it throws. YoutubeExplode's MediaStream reports CanSeek = true, but
/// implements seeking by discarding the current HTTP connection and issuing a brand new
/// GetStreamAsync request for "current position to end of file" on the next Read() - meaning
/// every skipped WebM element (most TrackEntry sub-fields, SeekHead, etc.) was triggering a
/// fresh HTTP connection. A single TrackEntry has enough of those to open 5-10+ connections
/// just parsing the header, which is what was hanging (or getting throttled by YouTube's CDN)
/// instead of a normal single continuous download.
///
/// This wrapper reports CanSeek = true (so NEbml's fast path succeeds) but implements forward
/// Seek by reading-and-discarding from the same already-open connection.
/// </summary>
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
        // A zero-byte read must always return 0 with no I/O per the Stream contract - and NEbml
        // does this constantly (any 1-byte-length EBML VInt needs zero extra bytes). YoutubeExplode
        // 6.6.0's MediaStream.ReadAsync doesn't handle count=0 correctly: it treats "read 0 bytes"
        // as "connection needs resetting" and loops forever re-establishing the HTTP connection,
        // since a 0-byte request can only ever return 0. Short-circuiting here avoids ever handing
        // it a request it can't complete.
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
