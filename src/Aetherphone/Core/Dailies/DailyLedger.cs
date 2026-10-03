using Aetherphone.Core.Game;

namespace Aetherphone.Core.Dailies;

internal static class DailyLedger
{
    public const int MaxCustomTasks = 30;
    public const int MaxTitleLength = 60;

    private static readonly TimeSpan Day = TimeSpan.FromDays(1);
    private static readonly TimeSpan Week = TimeSpan.FromDays(7);

    public static DateTime NextReset(DailyCadence cadence, DateTime utcNow) =>
        cadence == DailyCadence.Weekly ? GameSchedule.NextWeeklyReset(utcNow) : GameSchedule.NextDailyReset(utcNow);

    public static long PeriodStartUnix(DailyCadence cadence, DateTime utcNow)
    {
        var start = NextReset(cadence, utcNow) - (cadence == DailyCadence.Weekly ? Week : Day);
        return new DateTimeOffset(start, TimeSpan.Zero).ToUnixTimeSeconds();
    }

    public static long OldestLiveUnix(DateTime utcNow) =>
        Math.Min(PeriodStartUnix(DailyCadence.Daily, utcNow), PeriodStartUnix(DailyCadence.Weekly, utcNow));

    public static bool IsChecked(List<DailyCheckRecord> records, string itemId, ulong contentId, long periodStartUnix)
    {
        for (var index = 0; index < records.Count; index++)
        {
            var record = records[index];
            if (record.PeriodResetUnix == periodStartUnix && BelongsTo(record, itemId, contentId))
            {
                return true;
            }
        }

        return false;
    }

    public static bool SetChecked(List<DailyCheckRecord> records, string itemId, ulong contentId,
        long periodStartUnix, bool value)
    {
        if (!value)
        {
            return RemoveItem(records, itemId, contentId) > 0;
        }

        for (var index = 0; index < records.Count; index++)
        {
            var record = records[index];
            if (record.ContentId != contentId || !string.Equals(record.ItemId, itemId, StringComparison.Ordinal))
            {
                continue;
            }

            if (record.PeriodResetUnix == periodStartUnix)
            {
                return false;
            }

            record.PeriodResetUnix = periodStartUnix;
            return true;
        }

        records.Add(new DailyCheckRecord { ItemId = itemId, ContentId = contentId, PeriodResetUnix = periodStartUnix });
        return true;
    }

    public static int RemoveItem(List<DailyCheckRecord> records, string itemId, ulong contentId)
    {
        var removed = 0;
        for (var index = records.Count - 1; index >= 0; index--)
        {
            if (BelongsTo(records[index], itemId, contentId))
            {
                records.RemoveAt(index);
                removed++;
            }
        }

        return removed;
    }

    public static int RemoveEverywhere(List<DailyCheckRecord> records, string itemId)
    {
        var removed = 0;
        for (var index = records.Count - 1; index >= 0; index--)
        {
            if (string.Equals(records[index].ItemId, itemId, StringComparison.Ordinal))
            {
                records.RemoveAt(index);
                removed++;
            }
        }

        return removed;
    }

    public static int Prune(List<DailyCheckRecord> records, long oldestKeptUnix)
    {
        var removed = 0;
        for (var index = records.Count - 1; index >= 0; index--)
        {
            if (records[index].PeriodResetUnix < oldestKeptUnix)
            {
                records.RemoveAt(index);
                removed++;
            }
        }

        return removed;
    }

    public static string CleanTitle(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.Length <= MaxTitleLength)
        {
            return trimmed;
        }

        var cut = MaxTitleLength;
        if (char.IsHighSurrogate(trimmed[cut - 1]))
        {
            cut--;
        }

        return trimmed[..cut].TrimEnd();
    }

    public static bool ReminderDue(long previousUnix, long nowUnix, long resetUnix, long leadSeconds)
    {
        var moment = resetUnix - leadSeconds;
        return previousUnix < moment && moment <= nowUnix;
    }

    private static bool BelongsTo(DailyCheckRecord record, string itemId, ulong contentId) =>
        (record.ContentId == contentId || record.ContentId == 0) &&
        string.Equals(record.ItemId, itemId, StringComparison.Ordinal);
}
