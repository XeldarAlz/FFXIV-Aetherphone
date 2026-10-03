namespace Aetherphone.Core.Wallet;

internal readonly record struct WalletReading(uint ItemId, long Amount);

internal sealed class WalletChange
{
    public uint ItemId { get; set; }
    public long Delta { get; set; }
    public long Balance { get; set; }
    public long Unix { get; set; }
    public uint Territory { get; set; }
}

internal sealed class WalletDay
{
    public int Day { get; set; }
    public Dictionary<uint, long> Close { get; set; } = new();
}

internal sealed class WalletHistory
{
    public int Version { get; set; } = 1;
    public Dictionary<uint, long> Last { get; set; } = new();
    public List<WalletChange> Changes { get; set; } = new();
    public List<WalletDay> Days { get; set; } = new();
}

internal static class WalletJournal
{
    public const int MaxChanges = 400;
    public const int MaxDays = 60;
    public const long CoalesceSeconds = 120;

    public static bool Apply(WalletHistory history, ReadOnlySpan<WalletReading> readings, long nowUnix, int day,
        uint territory)
    {
        var changed = false;
        for (var index = 0; index < readings.Length; index++)
        {
            var reading = readings[index];
            if (!history.Last.TryGetValue(reading.ItemId, out var previous))
            {
                history.Last[reading.ItemId] = reading.Amount;
                changed = true;
                continue;
            }

            if (previous == reading.Amount)
            {
                continue;
            }

            history.Last[reading.ItemId] = reading.Amount;
            Record(history.Changes, reading.ItemId, reading.Amount - previous, reading.Amount, nowUnix, territory);
            changed = true;
        }

        changed |= CloseDay(history.Days, readings, day);
        Trim(history.Changes, MaxChanges);
        Trim(history.Days, MaxDays);
        return changed;
    }

    public static long NetSince(WalletHistory history, uint itemId, long sinceUnix)
    {
        var changes = history.Changes;
        var total = 0L;
        for (var index = changes.Count - 1; index >= 0; index--)
        {
            var change = changes[index];
            if (change.Unix < sinceUnix)
            {
                break;
            }

            if (change.ItemId == itemId)
            {
                total += change.Delta;
            }
        }

        return total;
    }

    public static int Series(WalletHistory history, uint itemId, int today, Span<long> into)
    {
        var days = history.Days;
        var length = into.Length;
        var firstDay = today - length + 1;
        var known = false;
        var carried = 0L;
        var firstFilled = -1;
        var dayIndex = 0;
        while (dayIndex < days.Count && days[dayIndex].Day < firstDay)
        {
            if (days[dayIndex].Close.TryGetValue(itemId, out var earlier))
            {
                carried = earlier;
                known = true;
            }

            dayIndex++;
        }

        for (var slot = 0; slot < length; slot++)
        {
            var slotDay = firstDay + slot;
            while (dayIndex < days.Count && days[dayIndex].Day <= slotDay)
            {
                if (days[dayIndex].Close.TryGetValue(itemId, out var close))
                {
                    carried = close;
                    known = true;
                }

                dayIndex++;
            }

            if (known && firstFilled < 0)
            {
                firstFilled = slot;
            }

            into[slot] = carried;
        }

        if (firstFilled < 0)
        {
            return 0;
        }

        var filled = length - firstFilled;
        if (firstFilled > 0)
        {
            into.Slice(firstFilled, filled).CopyTo(into);
        }

        return filled;
    }

    private static void Record(List<WalletChange> changes, uint itemId, long delta, long balance, long nowUnix,
        uint territory)
    {
        for (var index = changes.Count - 1; index >= 0; index--)
        {
            var candidate = changes[index];
            if (nowUnix - candidate.Unix > CoalesceSeconds)
            {
                break;
            }

            if (candidate.ItemId != itemId || candidate.Territory != territory ||
                Math.Sign(candidate.Delta) != Math.Sign(delta))
            {
                continue;
            }

            candidate.Delta += delta;
            candidate.Balance = balance;
            candidate.Unix = nowUnix;
            changes.RemoveAt(index);
            changes.Add(candidate);
            return;
        }

        changes.Add(new WalletChange
        {
            ItemId = itemId,
            Delta = delta,
            Balance = balance,
            Unix = nowUnix,
            Territory = territory,
        });
    }

    private static bool CloseDay(List<WalletDay> days, ReadOnlySpan<WalletReading> readings, int day)
    {
        if (readings.Length == 0)
        {
            return false;
        }

        if (days.Count == 0 || days[^1].Day < day)
        {
            var fresh = new WalletDay { Day = day };
            for (var index = 0; index < readings.Length; index++)
            {
                fresh.Close[readings[index].ItemId] = readings[index].Amount;
            }

            days.Add(fresh);
            return true;
        }

        var current = days[^1];
        if (current.Day != day)
        {
            return false;
        }

        var changed = false;
        for (var index = 0; index < readings.Length; index++)
        {
            var reading = readings[index];
            if (current.Close.TryGetValue(reading.ItemId, out var close) && close == reading.Amount)
            {
                continue;
            }

            current.Close[reading.ItemId] = reading.Amount;
            changed = true;
        }

        return changed;
    }

    private static void Trim<T>(List<T> list, int limit)
    {
        if (list.Count > limit)
        {
            list.RemoveRange(0, list.Count - limit);
        }
    }
}
