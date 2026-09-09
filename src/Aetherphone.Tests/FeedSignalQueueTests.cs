using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Social;
using Xunit;

namespace Aetherphone.Tests;

public sealed class FeedSignalQueueTests
{
    private const int Arrives = 5000;

    private const int StaysQuiet = 250;

    [Fact]
    public async Task AFullBatchGoesOutWithoutWaitingForTheTimer()
    {
        var sent = new TaskCompletionSource<string[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var work = new StoreWork("FeedSignalQueueTests");
        var queue = new FeedSignalQueue(
            (postIds, _) =>
            {
                sent.TrySetResult(postIds);
                return Task.FromResult(true);
            },
            (_, _, _) => Task.FromResult(true),
            work);

        for (var index = 0; index < 12; index++)
        {
            queue.MarkSeen("post-" + index);
        }

        queue.Tick(DateTime.UtcNow);

        Assert.True(await Waited(sent.Task, Arrives));
        var batch = await sent.Task;
        Assert.Equal(12, batch.Length);
        Assert.Equal(0, queue.PendingCount);
    }

    [Fact]
    public void ASmallBatchWaitsForTheTimer()
    {
        using var work = new StoreWork("FeedSignalQueueTests");
        var queue = new FeedSignalQueue(
            (_, _) => Task.FromResult(true),
            (_, _, _) => Task.FromResult(true),
            work);

        queue.MarkSeen("post-one");
        queue.MarkSeen("post-two");
        queue.Tick(DateTime.UtcNow);
        Assert.Equal(2, queue.PendingCount);

        queue.Tick(DateTime.UtcNow.AddSeconds(10));
        Assert.Equal(0, queue.PendingCount);
    }

    [Fact]
    public async Task AForcedFlushSendsWhatIsPending()
    {
        var sent = new TaskCompletionSource<string[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var work = new StoreWork("FeedSignalQueueTests");
        var queue = new FeedSignalQueue(
            (postIds, _) =>
            {
                sent.TrySetResult(postIds);
                return Task.FromResult(true);
            },
            (_, _, _) => Task.FromResult(true),
            work);

        queue.MarkSeen("post-one");
        queue.Flush(DateTime.UtcNow);

        Assert.True(await Waited(sent.Task, Arrives));
        var batch = await sent.Task;
        Assert.Equal("post-one", Assert.Single(batch));
    }

    [Fact]
    public async Task TheSameSignalOnTheSamePostIsSentOnce()
    {
        using var gate = new SemaphoreSlim(0);
        using var work = new StoreWork("FeedSignalQueueTests");
        var queue = new FeedSignalQueue(
            (_, _) => Task.FromResult(true),
            (_, _, _) =>
            {
                gate.Release();
                return Task.FromResult(true);
            },
            work);

        queue.Signal("post-one", FeedSignalKinds.ProfileOpen);
        queue.Signal("post-one", FeedSignalKinds.ProfileOpen);

        Assert.True(await gate.WaitAsync(Arrives));
        Assert.False(await gate.WaitAsync(StaysQuiet));

        queue.Signal("post-one", FeedSignalKinds.Send);
        Assert.True(await gate.WaitAsync(Arrives));
    }

    [Fact]
    public async Task AFailedBatchIsQueuedAgain()
    {
        var attempts = 0;
        using var gate = new SemaphoreSlim(0);
        using var work = new StoreWork("FeedSignalQueueTests");
        var queue = new FeedSignalQueue(
            (_, _) =>
            {
                attempts++;
                gate.Release();
                return Task.FromResult(false);
            },
            (_, _, _) => Task.FromResult(true),
            work);

        queue.MarkSeen("post-one");
        queue.Flush(DateTime.UtcNow);
        Assert.True(await gate.WaitAsync(Arrives));

        var requeued = false;
        for (var attempt = 0; attempt < 50 && !requeued; attempt++)
        {
            requeued = queue.PendingCount == 1;
            if (!requeued)
            {
                await Task.Delay(20);
            }
        }

        Assert.True(requeued);

        queue.Flush(DateTime.UtcNow);
        Assert.True(await gate.WaitAsync(Arrives));
        Assert.Equal(2, attempts);
    }

    [Fact]
    public void PendingStopsGrowingAtTheCap()
    {
        using var work = new StoreWork("FeedSignalQueueTests");
        var queue = new FeedSignalQueue(
            (_, _) => Task.FromResult(true),
            (_, _, _) => Task.FromResult(true),
            work);

        for (var index = 0; index < FeedSignalQueue.PendingCap + 50; index++)
        {
            queue.MarkSeen("post-" + index);
        }

        Assert.Equal(FeedSignalQueue.PendingCap, queue.PendingCount);
    }

    private static async Task<bool> Waited(Task task, int milliseconds)
    {
        var finished = await Task.WhenAny(task, Task.Delay(milliseconds));
        return finished == task;
    }
}
