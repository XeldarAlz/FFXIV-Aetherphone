namespace Aetherphone.Core.Message;

internal sealed class BlockedUnreadLedger
{
    private readonly object gate = new();
    private readonly Dictionary<string, HashSet<string>> unreadByThread = new(StringComparer.Ordinal);

    public void NoteArrival(string threadKey, string messageId)
    {
        if (threadKey.Length == 0 || messageId.Length == 0)
        {
            return;
        }

        lock (gate)
        {
            if (!unreadByThread.TryGetValue(threadKey, out var messages))
            {
                messages = new HashSet<string>(StringComparer.Ordinal);
                unreadByThread[threadKey] = messages;
            }

            messages.Add(messageId);
        }
    }

    public int Effective(string threadKey, int rawUnread, string? blockedLastMessageId)
    {
        if (rawUnread <= 0)
        {
            Reset(threadKey);
            return rawUnread;
        }

        if (!string.IsNullOrEmpty(blockedLastMessageId))
        {
            NoteArrival(threadKey, blockedLastMessageId);
        }

        lock (gate)
        {
            if (!unreadByThread.TryGetValue(threadKey, out var messages))
            {
                return rawUnread;
            }

            return Math.Max(0, rawUnread - messages.Count);
        }
    }

    public void Reset(string threadKey)
    {
        lock (gate)
        {
            unreadByThread.Remove(threadKey);
        }
    }

    public void Clear()
    {
        lock (gate)
        {
            unreadByThread.Clear();
        }
    }
}
