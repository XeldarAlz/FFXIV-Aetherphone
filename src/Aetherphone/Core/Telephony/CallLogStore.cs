namespace Aetherphone.Core.Telephony;

internal sealed class CallLogStore
{
    private const int MaxEntries = 50;

    private readonly Configuration configuration;
    private readonly object gate = new();
    private volatile CallLogEntry[] entries;

    public CallLogStore(Configuration configuration)
    {
        this.configuration = configuration;
        entries = configuration.CallLog.ToArray();
    }

    public CallLogEntry[] Entries => entries;

    public int UnseenMissed
    {
        get
        {
            var seen = configuration.CallLogSeenUnix;
            var log = entries;
            var total = 0;
            for (var index = 0; index < log.Length; index++)
            {
                if (log[index].Direction == CallDirection.Missed && log[index].TimestampUnix > seen)
                {
                    total += log[index].Count;
                }
            }

            return total;
        }
    }

    public void MarkSeen()
    {
        if (UnseenMissed == 0)
        {
            return;
        }

        configuration.CallLogSeenUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        configuration.Save();
    }

    public void Add(CallContact contact, CallDirection direction)
    {
        lock (gate)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var current = entries;
            if (current.Length > 0 && current[0].UserId == contact.UserId && current[0].Direction == direction)
            {
                var merged = (CallLogEntry[])current.Clone();
                merged[0] = new CallLogEntry
                {
                    UserId = contact.UserId,
                    Name = contact.Name,
                    World = contact.World,
                    DisplayName = contact.DisplayName,
                    Direction = direction,
                    TimestampUnix = now,
                    Count = current[0].Count + 1,
                };
                entries = merged;
            }
            else
            {
                var length = Math.Min(current.Length + 1, MaxEntries);
                var next = new CallLogEntry[length];
                next[0] = new CallLogEntry
                {
                    UserId = contact.UserId,
                    Name = contact.Name,
                    World = contact.World,
                    DisplayName = contact.DisplayName,
                    Direction = direction,
                    TimestampUnix = now,
                };
                for (var index = 1; index < length; index++)
                {
                    next[index] = current[index - 1];
                }

                entries = next;
            }

            configuration.CallLog.Clear();
            configuration.CallLog.AddRange(entries);
        }

        configuration.Save();
    }
}
