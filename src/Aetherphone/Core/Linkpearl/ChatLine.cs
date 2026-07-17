namespace Aetherphone.Core.Linkpearl;

internal enum MessageDirection
{
    Incoming,
    Outgoing,
}

internal sealed record MessageAuthor(string Name, string World);

internal sealed record ChatLine(MessageDirection Direction, string Text, DateTime At, MessageAuthor? Author = null);
