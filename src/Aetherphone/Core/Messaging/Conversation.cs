namespace Aetherphone.Core.Messaging;

internal sealed class Conversation
{
    private readonly List<ChatLine> lines = new();
    public string Contact { get; }
    public string SendTarget { get; }
    public string World { get; }
    public IReadOnlyList<ChatLine> Lines => lines;
    public DateTime LastActivity { get; private set; }
    public int Unread { get; private set; }
    public ChatLine? Last => lines.Count > 0 ? lines[lines.Count - 1] : null;

    public Conversation(string contact, string sendTarget)
    {
        Contact = contact;
        SendTarget = sendTarget;
        var separator = sendTarget.IndexOf('@');
        World = separator >= 0 ? sendTarget.Substring(separator + 1) : string.Empty;
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
