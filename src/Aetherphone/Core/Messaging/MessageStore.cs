namespace Aetherphone.Core.Messaging;

internal sealed class MessageStore
{
    private readonly Dictionary<string, Conversation> byTarget = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Conversation> ordered = new();
    private readonly MessageArchive archive;
    private readonly Configuration configuration;
    public IReadOnlyList<Conversation> Conversations => ordered;
    public event Action? Changed;

    public MessageStore(MessageArchive archive, Configuration configuration)
    {
        this.archive = archive;
        this.configuration = configuration;
        var stored = archive.LoadAll();
        for (var index = 0; index < stored.Count; index++)
        {
            var record = stored[index];
            var conversation = new Conversation(record.Contact, record.SendTarget);
            conversation.LoadHistory(record.Lines);
            byTarget[record.SendTarget] = conversation;
            ordered.Add(conversation);
        }
    }

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
        if (configuration.ArchiveTellsToDisk)
        {
            archive.Save(conversation.Contact, conversation.SendTarget, conversation.Lines);
        }

        Changed?.Invoke();
    }

    public void Remove(Conversation conversation)
    {
        byTarget.Remove(conversation.SendTarget);
        ordered.Remove(conversation);
        archive.Delete(conversation.SendTarget);
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
