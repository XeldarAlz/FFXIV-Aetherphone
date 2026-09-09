using Aetherphone.Core.Aethernet;

namespace Aetherphone.Core.Social;

internal sealed class FeedSignalQueue
{
    public const int BatchMax = 40;

    public const int PendingCap = 200;

    private const int FlushAtCount = 12;

    private const double FlushSeconds = 4d;

    private readonly Func<string[], CancellationToken, Task<bool>> sendSeen;
    private readonly Func<string, int, CancellationToken, Task<bool>> sendSignal;
    private readonly StoreWork work;
    private readonly HashSet<string> pending = new(StringComparer.Ordinal);
    private readonly HashSet<(string PostId, int Kind)> signalled = new();
    private readonly object gate = new();
    private DateTime lastFlush = DateTime.UtcNow;
    private bool flushing;

    public FeedSignalQueue(
        Func<string[], CancellationToken, Task<bool>> sendSeen,
        Func<string, int, CancellationToken, Task<bool>> sendSignal,
        StoreWork work)
    {
        this.sendSeen = sendSeen;
        this.sendSignal = sendSignal;
        this.work = work;
    }

    public int PendingCount
    {
        get
        {
            lock (gate)
            {
                return pending.Count;
            }
        }
    }

    public void MarkSeen(string postId)
    {
        if (postId.Length == 0)
        {
            return;
        }

        lock (gate)
        {
            if (pending.Count >= PendingCap && !pending.Contains(postId))
            {
                return;
            }

            pending.Add(postId);
        }
    }

    public void Signal(string postId, int kind)
    {
        if (postId.Length == 0)
        {
            return;
        }

        lock (gate)
        {
            if (!signalled.Add((postId, kind)))
            {
                return;
            }
        }

        work.Run("feed signal", token => sendSignal(postId, kind, token));
    }

    public void Tick(DateTime now)
    {
        Dispatch(now, false);
    }

    public void Flush(DateTime now)
    {
        Dispatch(now, true);
    }

    public void Reset()
    {
        lock (gate)
        {
            pending.Clear();
            signalled.Clear();
        }
    }

    private void Dispatch(DateTime now, bool force)
    {
        string[] batch;
        lock (gate)
        {
            if (flushing || pending.Count == 0)
            {
                return;
            }

            if (!force && pending.Count < FlushAtCount && (now - lastFlush).TotalSeconds < FlushSeconds)
            {
                return;
            }

            batch = Take();
            flushing = true;
            lastFlush = now;
        }

        work.Run("feed seen", token => sendSeen(batch, token), sent =>
        {
            lock (gate)
            {
                flushing = false;
                if (sent)
                {
                    return;
                }

                for (var index = 0; index < batch.Length; index++)
                {
                    if (pending.Count < PendingCap)
                    {
                        pending.Add(batch[index]);
                    }
                }
            }
        });
    }

    private string[] Take()
    {
        var count = pending.Count < BatchMax ? pending.Count : BatchMax;
        var batch = new string[count];
        var index = 0;
        foreach (var postId in pending)
        {
            batch[index] = postId;
            index++;
            if (index == count)
            {
                break;
            }
        }

        for (var removed = 0; removed < count; removed++)
        {
            pending.Remove(batch[removed]);
        }

        return batch;
    }
}
