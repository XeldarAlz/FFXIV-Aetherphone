namespace Aetherphone.Core.Jam;

internal sealed class JamInviteQueue
{
    // The server takes one jam.invite per socket every 2 s and silently drops the rest.
    internal const long SpacingMilliseconds = 2_100;
    internal const int Capacity = 32;

    private readonly List<string> waiting = new();
    private long sentAtMilliseconds;
    private bool sent;

    internal int Count => waiting.Count;

    internal void Enqueue(string userId)
    {
        if (waiting.Count >= Capacity || waiting.Contains(userId))
        {
            return;
        }

        waiting.Add(userId);
    }

    internal bool TryDequeue(long nowMilliseconds, out string userId)
    {
        if (waiting.Count == 0 || (sent && nowMilliseconds - sentAtMilliseconds < SpacingMilliseconds))
        {
            userId = string.Empty;
            return false;
        }

        userId = waiting[0];
        waiting.RemoveAt(0);
        sentAtMilliseconds = nowMilliseconds;
        sent = true;
        return true;
    }

    internal void Clear()
    {
        waiting.Clear();
        sentAtMilliseconds = 0;
        sent = false;
    }
}
