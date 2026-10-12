namespace Aetherphone.Core.Net;

internal sealed class RequestThrottle : IDisposable
{
    private static readonly TimeSpan PriorityYield = TimeSpan.FromMilliseconds(25);
    private readonly SemaphoreSlim gate;
    private readonly long minIntervalTicks;
    private long nextAllowedTick;
    private int priorityWaiters;

    public RequestThrottle(int maxConcurrency, TimeSpan minInterval)
    {
        gate = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        minIntervalTicks = minInterval.Ticks;
    }

    public Task<IDisposable> EnterAsync(CancellationToken token) => EnterAsync(token, false);

    public async Task<IDisposable> EnterAsync(CancellationToken token, bool priority)
    {
        if (priority)
        {
            Interlocked.Increment(ref priorityWaiters);
        }

        try
        {
            await AcquireGateAsync(token, priority).ConfigureAwait(false);
        }
        finally
        {
            if (priority)
            {
                Interlocked.Decrement(ref priorityWaiters);
            }
        }

        try
        {
            await WaitForIntervalAsync(token).ConfigureAwait(false);
        }
        catch
        {
            gate.Release();
            throw;
        }

        return new Lease(gate);
    }

    private async Task AcquireGateAsync(CancellationToken token, bool priority)
    {
        while (true)
        {
            await gate.WaitAsync(token).ConfigureAwait(false);
            if (priority || Volatile.Read(ref priorityWaiters) == 0)
            {
                return;
            }

            gate.Release();
            await Task.Delay(PriorityYield, token).ConfigureAwait(false);
        }
    }

    private async Task WaitForIntervalAsync(CancellationToken token)
    {
        while (true)
        {
            var now = DateTime.UtcNow.Ticks;
            var allowed = Interlocked.Read(ref nextAllowedTick);
            if (now >= allowed)
            {
                Interlocked.Exchange(ref nextAllowedTick, now + minIntervalTicks);
                return;
            }

            await Task.Delay(TimeSpan.FromTicks(allowed - now), token).ConfigureAwait(false);
        }
    }

    private sealed class Lease : IDisposable
    {
        private SemaphoreSlim? gate;
        public Lease(SemaphoreSlim gate) => this.gate = gate;

        public void Dispose()
        {
            gate?.Release();
            gate = null;
        }
    }

    public void Dispose() => gate.Dispose();
}
