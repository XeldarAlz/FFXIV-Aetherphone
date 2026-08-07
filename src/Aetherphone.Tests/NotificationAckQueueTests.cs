using Aetherphone.Core.Notifications;
using Xunit;

namespace Aetherphone.Tests;

public sealed class NotificationAckQueueTests
{
    private const string Account = "user-1";
    private const string Other = "user-2";

    private static NotificationAckQueue Build(out Dictionary<string, long> pending, out Ref<int> saves)
    {
        var store = new Dictionary<string, long>();
        var counter = new Ref<int>();
        pending = store;
        saves = counter;
        return new NotificationAckQueue(store, () => counter.Value++);
    }

    public sealed class Ref<T>
        where T : struct
    {
        public T Value;
    }

    [Fact]
    public void QueuedAckSurvivesUntilConfirmed()
    {
        var queue = Build(out var pending, out _);
        Assert.True(queue.Enqueue(Account, "chirper", 100));
        Assert.Single(pending);

        Assert.True(queue.TryPeek(Account, out var key, out var app, out var watermark));
        Assert.Equal("chirper", app);
        Assert.Equal(100, watermark);

        Assert.True(queue.ConfirmSent(key, watermark));
        Assert.Empty(pending);
    }

    [Fact]
    public void UnconfirmedAckStaysQueuedForRetry()
    {
        var queue = Build(out var pending, out _);
        queue.Enqueue(Account, "chirper", 100);
        queue.TryPeek(Account, out _, out _, out _);

        Assert.Single(pending);
        Assert.True(queue.TryPeek(Account, out _, out _, out var watermark));
        Assert.Equal(100, watermark);
    }

    [Fact]
    public void WatermarkOnlyMovesForward()
    {
        var queue = Build(out var pending, out var saves);
        Assert.True(queue.Enqueue(Account, "chirper", 200));
        Assert.False(queue.Enqueue(Account, "chirper", 150));
        Assert.Equal(200, pending[NotificationAckQueue.KeyFor(Account, "chirper")]);
        Assert.Equal(1, saves.Value);
    }

    [Fact]
    public void RepeatedEnqueueOfTheSameWatermarkDoesNotResave()
    {
        var queue = Build(out _, out var saves);
        queue.Enqueue(Account, "velvet", 500);
        for (var attempt = 0; attempt < 20; attempt++)
        {
            Assert.False(queue.Enqueue(Account, "velvet", 500));
        }

        Assert.Equal(1, saves.Value);
    }

    [Fact]
    public void ConfirmKeepsANewerWatermarkQueued()
    {
        var queue = Build(out var pending, out _);
        queue.Enqueue(Account, "chirper", 100);
        var key = NotificationAckQueue.KeyFor(Account, "chirper");
        queue.Enqueue(Account, "chirper", 300);

        Assert.False(queue.ConfirmSent(key, 100));
        Assert.Equal(300, pending[key]);
    }

    [Fact]
    public void GlobalAckCoversEveryApp()
    {
        var queue = Build(out _, out _);
        queue.Enqueue(Account, null, 900);

        Assert.Equal(900, queue.WatermarkFor(Account, "chirper"));
        Assert.Equal(900, queue.WatermarkFor(Account, "aethergram"));
    }

    [Fact]
    public void GlobalAckRetiresTheScopesItCovers()
    {
        var queue = Build(out var pending, out _);
        queue.Enqueue(Account, "chirper", 100);
        queue.Enqueue(Account, "velvet", 900);
        queue.Enqueue(Account, null, 500);

        Assert.False(pending.ContainsKey(NotificationAckQueue.KeyFor(Account, "chirper")));
        Assert.Equal(900, pending[NotificationAckQueue.KeyFor(Account, "velvet")]);
        Assert.Equal(500, pending[NotificationAckQueue.KeyFor(Account, null)]);
    }

    [Fact]
    public void ScopedWatermarkWinsWhenItIsNewerThanTheGlobalOne()
    {
        var queue = Build(out _, out _);
        queue.Enqueue(Account, null, 400);
        queue.Enqueue(Account, "chirper", 800);

        Assert.Equal(800, queue.WatermarkFor(Account, "chirper"));
        Assert.Equal(400, queue.WatermarkFor(Account, "velvet"));
    }

    [Fact]
    public void AcksNeverLeakAcrossAccounts()
    {
        var queue = Build(out _, out _);
        queue.Enqueue(Account, "chirper", 700);

        Assert.Equal(0, queue.WatermarkFor(Other, "chirper"));
        Assert.False(queue.TryPeek(Other, out _, out _, out _));
        Assert.True(queue.TryPeek(Account, out _, out _, out _));
    }

    [Fact]
    public void OtherAccountsKeepTheirQueueWhileOneFlushes()
    {
        var queue = Build(out var pending, out _);
        queue.Enqueue(Account, "chirper", 100);
        queue.Enqueue(Other, "chirper", 200);

        Assert.True(queue.TryPeek(Account, out var key, out _, out var watermark));
        queue.ConfirmSent(key, watermark);

        Assert.Single(pending);
        Assert.Equal(200, queue.WatermarkFor(Other, "chirper"));
    }

    [Fact]
    public void EmptyAndNonPositiveWatermarksAreRejected()
    {
        var queue = Build(out var pending, out _);
        Assert.False(queue.Enqueue(Account, "chirper", 0));
        Assert.False(queue.Enqueue(Account, "chirper", -5));
        Assert.False(queue.Enqueue(string.Empty, "chirper", 100));
        Assert.Empty(pending);
    }
}
