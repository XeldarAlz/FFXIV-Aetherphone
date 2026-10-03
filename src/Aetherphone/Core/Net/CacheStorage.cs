namespace Aetherphone.Core.Net;

internal sealed class CacheStorage
{
    public const long UnknownSize = -1;
    private const long SizeMaxAgeMilliseconds = 10_000;
    private readonly DiskCache[] caches;
    private long sizeBytes = UnknownSize;
    private long nextMeasureAtMilliseconds;
    private int measuring;

    public CacheStorage(DiskCache[] caches)
    {
        this.caches = caches;
    }

    public long SizeBytes()
    {
        if (Environment.TickCount64 >= Interlocked.Read(ref nextMeasureAtMilliseconds)
            && Interlocked.CompareExchange(ref measuring, 1, 0) == 0)
        {
            _ = Task.Run(Measure);
        }

        return Interlocked.Read(ref sizeBytes);
    }

    public void Clear()
    {
        for (var index = 0; index < caches.Length; index++)
        {
            try
            {
                caches[index].Clear();
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "[Media] clearing a disk cache failed");
            }
        }

        Interlocked.Exchange(ref sizeBytes, MeasureBytes());
        Interlocked.Exchange(ref nextMeasureAtMilliseconds, 0);
        AepLog.Info("[Media] cleared the image and media caches");
    }

    private long MeasureBytes()
    {
        long total = 0;
        for (var index = 0; index < caches.Length; index++)
        {
            total += caches[index].SizeBytes();
        }

        return total;
    }

    private void Measure()
    {
        try
        {
            Interlocked.Exchange(ref sizeBytes, MeasureBytes());
            Interlocked.Exchange(ref nextMeasureAtMilliseconds, Environment.TickCount64 + SizeMaxAgeMilliseconds);
        }
        finally
        {
            Interlocked.Exchange(ref measuring, 0);
        }
    }
}
