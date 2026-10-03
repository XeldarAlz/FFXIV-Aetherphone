namespace Aetherphone.Core.Jam;

internal readonly record struct JamPendingMove(int EntryId, int ToIndex, long StartedAt);

internal sealed class JamPendingMoves
{
    public const int Capacity = 4;

    // The move rides the queue pacer, so it can sit behind a burst of adds before the server sees it.
    public const long TimeoutMilliseconds = 8_000;

    private readonly JamPendingMove[] moves = new JamPendingMove[Capacity];
    private int count;

    public int Count => count;

    public void Add(int entryId, int toIndex, long nowMilliseconds)
    {
        if (count == Capacity)
        {
            Array.Copy(moves, 1, moves, 0, Capacity - 1);
            count--;
        }

        moves[count] = new JamPendingMove(entryId, Math.Max(0, toIndex), nowMilliseconds);
        count++;
    }

    public bool HasExpired(long nowMilliseconds)
    {
        for (var index = 0; index < count; index++)
        {
            if (nowMilliseconds - moves[index].StartedAt > TimeoutMilliseconds)
            {
                return true;
            }
        }

        return false;
    }

    public void Clear()
    {
        Array.Clear(moves);
        count = 0;
    }

    public JamQueueItem[] Project(JamQueueItem[] server, long nowMilliseconds)
    {
        if (count == 0)
        {
            return server;
        }

        var working = (JamQueueItem[])server.Clone();
        var kept = 0;
        for (var index = 0; index < count; index++)
        {
            var move = moves[index];
            if (nowMilliseconds - move.StartedAt > TimeoutMilliseconds)
            {
                continue;
            }

            var from = JamWire.IndexOfEntry(working, move.EntryId);
            if (from < 0)
            {
                continue;
            }

            var target = Math.Min(move.ToIndex, working.Length - 1);
            if (from == target)
            {
                continue;
            }

            Shift(working, from, target);
            moves[kept] = move;
            kept++;
        }

        Array.Clear(moves, kept, count - kept);
        count = kept;
        return kept == 0 ? server : working;
    }

    private static void Shift(JamQueueItem[] items, int from, int target)
    {
        var moved = items[from];
        if (from < target)
        {
            Array.Copy(items, from + 1, items, from, target - from);
        }
        else
        {
            Array.Copy(items, target, items, target + 1, from - target);
        }

        items[target] = moved;
    }
}
