using Aetherphone.Core.Localization;
using Aetherphone.Core.Radio;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Jam;

internal sealed partial class JamSession
{
    private readonly JamChatLog chat = new(FormatChatClock);
    private readonly RadioChatPacer chatPacer = new();

    public JamChatLog Chat => chat;
    public JamChatRefusal LastChatRefusal { get; private set; }
    public int ChatRefusalVersion { get; private set; }

    public bool CanSendChat()
    {
        return ChatReachable() && chatPacer.CanSend(Environment.TickCount64);
    }

    public bool SendChat(string text)
    {
        if (!InJam)
        {
            RefuseChat(JamChatRefusal.NotInJam);
            return false;
        }

        if (JamWire.ValidateChat(text, out var refusal) is not { } trimmed)
        {
            RefuseChat(refusal);
            return false;
        }

        if (!ChatReachable())
        {
            return false;
        }

        var now = Environment.TickCount64;
        if (!chatPacer.CanSend(now))
        {
            RefuseChat(JamChatRefusal.Cooldown);
            return false;
        }

        signals.Chat(trimmed);
        chatPacer.Take(now);
        return true;
    }

    public bool CanDelete(RadioChatEntry entry)
    {
        return InJam && (entry.IsMine || IsHost);
    }

    public bool DeleteMessage(long messageId)
    {
        if (chat.Find(messageId) is not { } entry || !CanDelete(entry) || !ChatReachable())
        {
            return false;
        }

        signals.DeleteMessage(messageId);
        return true;
    }

    public void HideUser(string userId)
    {
        chat.HideUser(userId);
    }

    private bool ChatReachable()
    {
        return InJam && signals.Connected && disconnectedSinceTicks == 0 && awaitingSinceTicks == 0;
    }

    private void LoadChat(CallControl message, bool sameJam)
    {
        if (message.Messages is not null)
        {
            chat.Load(message.Messages, MyUserId);
            return;
        }

        if (!sameJam)
        {
            chat.Clear();
        }
    }

    private void OnChatMessage(CallControl message)
    {
        if (InJam)
        {
            chat.Append(message.Messages, MyUserId);
        }
    }

    private void OnChatDeleted(CallControl message)
    {
        if (message.MessageId is { } messageId)
        {
            chat.Remove(messageId);
        }
    }

    private void OnRefused(CallControl message)
    {
        RefuseChat(JamChatRefusalCodes.Parse(message.Reason));
    }

    private void ResetChat()
    {
        chat.Clear();
        chatPacer.Reset();
    }

    private void RefuseChat(JamChatRefusal refusal)
    {
        LastChatRefusal = refusal;
        ChatRefusalVersion++;
    }

    private static string FormatChatClock(long unixMilliseconds)
    {
        return TimeText.Clock(unixMilliseconds / 1000);
    }
}
