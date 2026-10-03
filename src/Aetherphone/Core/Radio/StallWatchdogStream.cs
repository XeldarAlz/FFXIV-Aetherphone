namespace Aetherphone.Core.Radio;

internal sealed class RadioStallException : IOException
{
    public RadioStallException()
        : base("The station stopped sending audio")
    {
    }
}

internal sealed class StallWatchdogStream : Stream
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(12);
    private const long MinimumCheckMilliseconds = 25;

    private readonly Stream inner;
    private readonly Action abort;
    private readonly long timeoutMilliseconds;
    private readonly Timer timer;
    private long readStartedTick;
    private volatile bool stalled;
    private bool disposed;

    public StallWatchdogStream(Stream inner, Action abort, TimeSpan timeout)
    {
        this.inner = inner;
        this.abort = abort;
        timeoutMilliseconds = Math.Max(1L, (long)timeout.TotalMilliseconds);
        var period = Math.Max(MinimumCheckMilliseconds, timeoutMilliseconds / 4);
        timer = new Timer(_ => Check(), null, period, period);
    }

    public bool Stalled => stalled;

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
        if (stalled)
        {
            throw new RadioStallException();
        }

        Volatile.Write(ref readStartedTick, Environment.TickCount64);
        try
        {
            var read = inner.Read(buffer, offset, count);
            if (stalled)
            {
                throw new RadioStallException();
            }

            return read;
        }
        catch (Exception exception) when (stalled && exception is not RadioStallException)
        {
            throw new RadioStallException();
        }
        finally
        {
            Volatile.Write(ref readStartedTick, 0L);
        }
    }

    private void Check()
    {
        var started = Volatile.Read(ref readStartedTick);
        if (started == 0L || stalled || Environment.TickCount64 - started < timeoutMilliseconds)
        {
            return;
        }

        stalled = true;
        try
        {
            abort();
            inner.Dispose();
        }
        catch (Exception exception)
        {
            AepLog.Debug(exception, "Radio stall abort failed");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            timer.Dispose();
            inner.Dispose();
        }

        base.Dispose(disposing);
    }

    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
