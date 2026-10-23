namespace Aetherphone.Core.Aethernet;

internal sealed class BlockList
{
    private static readonly HashSet<string> Empty = new(StringComparer.Ordinal);
    private readonly object gate = new();
    private HashSet<string> blocked = Empty;
    private int version;
    private int epoch;
    private long loadedAtTicks;

    public int Version => Volatile.Read(ref version);

    public int Epoch => Volatile.Read(ref epoch);

    public bool Contains(string? userId) =>
        !string.IsNullOrEmpty(userId) && Volatile.Read(ref blocked).Contains(userId);

    public bool IsStale(DateTime nowUtc, TimeSpan maxAge)
    {
        var loaded = Volatile.Read(ref loadedAtTicks);
        return loaded == 0L || nowUtc.Ticks - loaded >= maxAge.Ticks;
    }

    public bool Replace(IReadOnlyList<string> userIds, int expectedEpoch, DateTime nowUtc)
    {
        var next = new HashSet<string>(userIds.Count, StringComparer.Ordinal);
        for (var index = 0; index < userIds.Count; index++)
        {
            if (userIds[index].Length > 0)
            {
                next.Add(userIds[index]);
            }
        }

        lock (gate)
        {
            if (epoch != expectedEpoch)
            {
                return false;
            }

            Publish(next);
            Volatile.Write(ref loadedAtTicks, nowUtc.Ticks);
            return true;
        }
    }

    public void Add(string userId)
    {
        if (userId.Length == 0)
        {
            return;
        }

        lock (gate)
        {
            if (blocked.Contains(userId))
            {
                return;
            }

            var next = new HashSet<string>(blocked, StringComparer.Ordinal) { userId };
            Publish(next);
        }
    }

    public void Remove(string userId)
    {
        lock (gate)
        {
            if (!blocked.Contains(userId))
            {
                return;
            }

            var next = new HashSet<string>(blocked, StringComparer.Ordinal);
            next.Remove(userId);
            Publish(next);
        }
    }

    public void Clear()
    {
        lock (gate)
        {
            epoch++;
            Volatile.Write(ref loadedAtTicks, 0L);
            Publish(Empty);
        }
    }

    private void Publish(HashSet<string> next)
    {
        Volatile.Write(ref blocked, next);
        Interlocked.Increment(ref version);
    }
}
