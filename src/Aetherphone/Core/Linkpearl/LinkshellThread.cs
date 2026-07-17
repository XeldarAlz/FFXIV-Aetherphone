namespace Aetherphone.Core.Linkpearl;

internal sealed class LinkshellThread
{
    private readonly List<ChatLine> lines = new();
    public LinkshellChannel Channel { get; }
    public string Name { get; private set; }
    public IReadOnlyList<ChatLine> Lines => lines;
    public DateTime LastActivity { get; private set; }
    public int Unread { get; private set; }
    public ChatLine? Last => lines.Count > 0 ? lines[lines.Count - 1] : null;

    public LinkshellThread(LinkshellChannel channel, string name)
    {
        Channel = channel;
        Name = name;
    }

    public void Rename(string name)
    {
        if (!string.IsNullOrEmpty(name))
        {
            Name = name;
        }
    }

    public void Append(ChatLine line)
    {
        lines.Add(line);
        LastActivity = line.At;
        if (line.Direction == MessageDirection.Incoming)
        {
            Unread++;
        }
    }

    public void MarkRead() => Unread = 0;
}
