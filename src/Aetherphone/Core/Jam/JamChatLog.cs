using Aetherphone.Core.Radio;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Jam;

internal sealed class JamChatLog : ILiveChatFeed
{
    public const int Capacity = RadioChatRing.Capacity;

    private readonly RadioChatRing ring = new();
    private readonly HashSet<string> hiddenUserIds = new(StringComparer.Ordinal);
    private readonly Func<long, string> formatClock;

    public JamChatLog(Func<long, string> formatClock)
    {
        this.formatClock = formatClock;
    }

    public int Version { get; private set; }

    public int Count => ring.Count;

    public RadioChatEntry At(int index) => ring.At(index);

    public RadioChatEntry? Find(long messageId) => ring.Find(messageId);

    public bool IsHidden(string userId) => hiddenUserIds.Contains(userId);

    public void Load(RadioChatMessage[]? messages, string me)
    {
        ring.Clear();
        if (messages is not null)
        {
            for (var index = 0; index < messages.Length; index++)
            {
                TryAppend(messages[index], me);
            }
        }

        Version++;
    }

    public bool Append(RadioChatMessage[]? messages, string me)
    {
        if (messages is null)
        {
            return false;
        }

        var appended = false;
        for (var index = 0; index < messages.Length; index++)
        {
            appended |= TryAppend(messages[index], me);
        }

        if (appended)
        {
            Version++;
        }

        return appended;
    }

    public bool Remove(long messageId)
    {
        if (ring.Remove(messageId, null) == 0)
        {
            return false;
        }

        Version++;
        return true;
    }

    public void HideUser(string userId)
    {
        if (userId.Length == 0 || !hiddenUserIds.Add(userId))
        {
            return;
        }

        if (ring.Remove(long.MinValue, userId) > 0)
        {
            Version++;
        }
    }

    public void Clear()
    {
        if (ring.Count == 0)
        {
            return;
        }

        ring.Clear();
        Version++;
    }

    private bool TryAppend(RadioChatMessage? wire, string me)
    {
        if (wire is null || string.IsNullOrEmpty(wire.UserId) || string.IsNullOrEmpty(wire.Text)
            || hiddenUserIds.Contains(wire.UserId) || wire.MessageId <= ring.NewestId)
        {
            return false;
        }

        ring.Append(RadioRoomWire.BuildEntry(wire.MessageId, wire.UserId, wire.DisplayName ?? string.Empty,
            wire.Handle ?? string.Empty, wire.AvatarUrl, wire.Text, wire.SentAtUnixMs,
            formatClock(wire.SentAtUnixMs), wire.IsDj, me));
        return true;
    }
}
