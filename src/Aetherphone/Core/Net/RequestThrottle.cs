using System.Threading;
using System.Threading.Tasks;

namespace Aetherphone.Core.Net;

internal sealed class RequestThrottle : IDisposable
{
    private readonly SemaphoreSlim gate;
    private readonly long minIntervalTicks;

    private long nextAllowedTick;

    public RequestThrottle(int maxConcurrency, TimeSpan minInterval)
    {
        gate = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        minIntervalTicks = minInterval.Ticks;
    }

    public async Task<IDisposable> EnterAsync(CancellationToken token)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);

        while (true)
        {
            var now = DateTime.UtcNow.Ticks;
            var allowed = Interlocked.Read(ref nextAllowedTick);
            if (now >= allowed)
            {
                Interlocked.Exchange(ref nextAllowedTick, now + minIntervalTicks);
                return new Lease(gate);
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
