using Aetherphone.Core.Game;

namespace Aetherphone.Core.Linkpearl;

internal sealed class MessageStore
{
    private readonly Dictionary<string, Conversation> byTarget = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Conversation> ordered = new();
    private readonly MessageArchive archive;
    private readonly Configuration configuration;
    public IReadOnlyList<Conversation> Conversations => ordered;
    public event Action? Changed;

    public MessageStore(MessageArchive archive, Configuration configuration, CharacterWatch characterWatch)
    {
        this.archive = archive;
        this.configuration = configuration;
        characterWatch.Changed += OnCharacterChanged;
    }

    private void OnCharacterChanged(ulong contentId)
    {
        if (contentId != 0 && !configuration.MessagesPerCharacterMigrated)
        {
            archive.MigrateLegacyTo(contentId);
            configuration.MessagesPerCharacterMigrated = true;
            configuration.Save();
        }

        archive.SetCharacter(contentId);
        byTarget.Clear();
        ordered.Clear();
        var stored = archive.LoadAll();
        for (var index = 0; index < stored.Count; index++)
        {
            var record = stored[index];
            var conversation = new Conversation(record.Contact, record.SendTarget);
            conversation.LoadHistory(record.Lines);
            byTarget[record.SendTarget] = conversation;
            ordered.Add(conversation);
        }

        Changed?.Invoke();
    }

    public bool Contains(Conversation conversation)
    {
        for (var index = 0; index < ordered.Count; index++)
        {
            if (ReferenceEquals(ordered[index], conversation))
            {
                return true;
            }
        }

        return false;
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
