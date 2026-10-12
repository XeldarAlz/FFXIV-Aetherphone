using Aetherphone.Core.Net;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RequestThrottleTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task PriorityWaiterEntersBeforeAQueuedNormalWaiter()
    {
        using var throttle = new RequestThrottle(1, TimeSpan.Zero);
        var holder = await throttle.EnterAsync(CancellationToken.None);
        var normal = throttle.EnterAsync(CancellationToken.None);
        var priority = throttle.EnterAsync(CancellationToken.None, true);
        holder.Dispose();

        var first = await Task.WhenAny(normal, priority).WaitAsync(TestTimeout);

        Assert.Same(priority, first);
        Assert.False(normal.IsCompleted);
        (await priority).Dispose();
        (await normal.WaitAsync(TestTimeout)).Dispose();
    }

    [Fact]
    public async Task NormalWaiterEntersWhenNoPriorityWaiterIsPending()
    {
        using var throttle = new RequestThrottle(1, TimeSpan.Zero);
        var holder = await throttle.EnterAsync(CancellationToken.None);
        var normal = throttle.EnterAsync(CancellationToken.None);
        holder.Dispose();

        (await normal.WaitAsync(TestTimeout)).Dispose();
    }

    [Fact]
    public async Task CancellingDuringTheIntervalWaitReleasesTheGate()
    {
        using var throttle = new RequestThrottle(1, TimeSpan.FromSeconds(30));
        (await throttle.EnterAsync(CancellationToken.None)).Dispose();
        using var cancellation = new CancellationTokenSource();
        var waiting = throttle.EnterAsync(cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.WaitAsync(TestTimeout));
        var next = throttle.EnterAsync(CancellationToken.None, true);
        Assert.False(next.IsCompleted);
    }
}
