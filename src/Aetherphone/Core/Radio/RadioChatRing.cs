namespace Aetherphone.Core.Radio;

internal sealed class RadioChatRing
{
    public const int Capacity = 100;

    private readonly RadioChatEntry?[] entries = new RadioChatEntry?[Capacity];
    private int start;
    private int count;

    public int Count => count;

    public long NewestId => count == 0 ? long.MinValue : At(count - 1).MessageId;

    public RadioChatEntry At(int index)
    {
        return entries[(start + index) % Capacity]!;
    }

    public void Append(RadioChatEntry entry)
    {
        if (count < Capacity)
        {
            entries[(start + count) % Capacity] = entry;
            count++;
            return;
        }

        entries[start] = entry;
        start = (start + 1) % Capacity;
    }

    public int Remove(long messageId, string? userId)
    {
        var kept = 0;
        for (var read = 0; read < count; read++)
        {
            var entry = entries[(start + read) % Capacity]!;
            var matches = entry.MessageId == messageId
                || (userId is not null && string.Equals(entry.UserId, userId, StringComparison.Ordinal));
            if (matches)
            {
                continue;
            }

            entries[(start + kept) % Capacity] = entry;
            kept++;
        }

        for (var clear = kept; clear < count; clear++)
        {
            entries[(start + clear) % Capacity] = null;
        }

        var removed = count - kept;
        count = kept;
        return removed;
    }

    public RadioChatEntry? Find(long messageId)
    {
        for (var index = 0; index < count; index++)
        {
            var entry = At(index);
            if (entry.MessageId == messageId)
            {
                return entry;
            }
        }

        return null;
    }

    public void Clear()
    {
        Array.Clear(entries);
        start = 0;
        count = 0;
    }
}
