namespace Aetherphone.Core.Message;

internal readonly record struct ReadThrough(long LastMessageAt, int Unread, string? LastMessageId = null);

internal enum ReadWatermarkVerdict : byte
{
    Hold = 0,
    Settled = 1,
    Superseded = 2,
}

internal static class ReadWatermark
{
    public static ReadWatermarkVerdict Judge(ReadThrough mark, string? lastMessageId, long lastMessageAt,
        int serverUnread)
    {
        if (IsNewer(mark, lastMessageId, lastMessageAt, serverUnread))
        {
            return ReadWatermarkVerdict.Superseded;
        }

        return serverUnread > 0 ? ReadWatermarkVerdict.Hold : ReadWatermarkVerdict.Settled;
    }

    private static bool IsNewer(ReadThrough mark, string? lastMessageId, long lastMessageAt, int serverUnread)
    {
        if (!string.IsNullOrEmpty(mark.LastMessageId) && !string.IsNullOrEmpty(lastMessageId))
        {
            return !string.Equals(mark.LastMessageId, lastMessageId, StringComparison.Ordinal);
        }

        return lastMessageAt > mark.LastMessageAt || serverUnread > mark.Unread;
    }
}
