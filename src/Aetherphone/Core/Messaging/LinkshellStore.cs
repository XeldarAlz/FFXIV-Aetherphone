namespace Aetherphone.Core.Messaging;

internal sealed class LinkshellStore
{
    private readonly Dictionary<string, LinkshellThread> byChannel = new(StringComparer.Ordinal);
    private readonly List<LinkshellThread> ordered = new();
    public IReadOnlyList<LinkshellThread> Threads => ordered;
    public event Action? Changed;

    public void Append(LinkshellChannel channel, string name, ChatLine line)
    {
        var thread = GetOrAdd(channel, name);
        thread.Rename(name);
        thread.Append(line);
        ordered.Remove(thread);
        ordered.Insert(0, thread);
        Changed?.Invoke();
    }

    public LinkshellThread? Find(LinkshellChannel channel)
    {
        return byChannel.TryGetValue(channel.Key, out var thread) ? thread : null;
    }

    public LinkshellThread GetOrCreate(LinkshellChannel channel, string name)
    {
        if (byChannel.TryGetValue(channel.Key, out var existing))
        {
            existing.Rename(name);
            return existing;
        }

        var thread = new LinkshellThread(channel, name);
        byChannel[channel.Key] = thread;
        ordered.Insert(0, thread);
        Changed?.Invoke();
        return thread;
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

    private LinkshellThread GetOrAdd(LinkshellChannel channel, string name)
    {
        if (byChannel.TryGetValue(channel.Key, out var existing))
        {
            return existing;
        }

        var thread = new LinkshellThread(channel, name);
        byChannel[channel.Key] = thread;
        return thread;
    }
}
