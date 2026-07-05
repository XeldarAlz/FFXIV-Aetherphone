namespace Aetherphone.Core.Messaging;

internal sealed class MessageStore
{
    private readonly Dictionary<string, Conversation> byTarget = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Conversation> ordered = new();
    public IReadOnlyList<Conversation> Conversations => ordered;
    public event Action? Changed;

    public void Append(string display, string sendTarget, ChatLine line)
    {
        if (!byTarget.TryGetValue(sendTarget, out var conversation))
        {
            conversation = new Conversation(display, sendTarget);
            byTarget[sendTarget] = conversation;
        }

        conversation.Append(line);
        ordered.Remove(conversation);
        ordered.Insert(0, conversation);
        Changed?.Invoke();
    }

    public Conversation GetOrCreate(string display, string sendTarget)
    {
        if (byTarget.TryGetValue(sendTarget, out var existing))
        {
            return existing;
        }

        var conversation = new Conversation(display, sendTarget);
        byTarget[sendTarget] = conversation;
        ordered.Insert(0, conversation);
        Changed?.Invoke();
        return conversation;
    }

    public int TotalUnread()
    {
        var total = 0;
        for (var index = 0; index < ordered.Count; index++)
        {
            total += ordered[index].Unread;
        }

        return total;
    }
}
