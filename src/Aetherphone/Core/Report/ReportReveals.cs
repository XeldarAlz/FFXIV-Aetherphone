using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Report;

internal static class ReportReveals
{
    public const int Limit = 6;

    public static bool TryCollect<TMessage>(TMessage[] messages, string messageId,
        Func<TMessage, RevealedMessageDto?> resolve, out RevealedMessageDto[]? revealed)
        where TMessage : class, IIdentified
    {
        var targetIndex = -1;
        for (var index = 0; index < messages.Length; index++)
        {
            if (messages[index].Id == messageId)
            {
                targetIndex = index;
                break;
            }
        }

        if (targetIndex < 0)
        {
            revealed = null;
            return false;
        }

        if (resolve(messages[targetIndex]) is not { } target)
        {
            revealed = null;
            return false;
        }

        var reveals = new List<RevealedMessageDto>(Limit) { target };
        for (var index = targetIndex - 1; index >= 0 && reveals.Count < Limit; index--)
        {
            if (resolve(messages[index]) is { } reveal)
            {
                reveals.Add(reveal);
            }
        }

        revealed = reveals.ToArray();
        return true;
    }
}
