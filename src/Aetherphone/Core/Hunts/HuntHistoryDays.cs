namespace Aetherphone.Core.Hunts;

internal readonly record struct HuntHistoryDay(DateTime LocalDate, int Start, int Count);

internal static class HuntHistoryDays
{
    public static DateTimeOffset? MomentOf(HuntLogEntryDto entry) => entry.KilledAt ?? entry.SpawnedAt;

    public static TimeSpan? Lifetime(HuntLogEntryDto entry) =>
        entry is { SpawnedAt: { } spawned, KilledAt: { } killed } && killed > spawned ? killed - spawned : null;

    public static void Group(HuntLogEntryDto[] entries, Func<DateTimeOffset, DateTime> localDate,
        List<HuntLogEntryDto> sorted, List<HuntHistoryDay> days)
    {
        sorted.Clear();
        days.Clear();
        for (var index = 0; index < entries.Length; index++)
        {
            if (MomentOf(entries[index]) is not null)
            {
                sorted.Add(entries[index]);
            }
        }

        sorted.Sort(static (left, right) => MomentOf(right)!.Value.CompareTo(MomentOf(left)!.Value));
        var start = 0;
        for (var index = 1; index <= sorted.Count; index++)
        {
            var startDate = localDate(MomentOf(sorted[start])!.Value).Date;
            if (index < sorted.Count && localDate(MomentOf(sorted[index])!.Value).Date == startDate)
            {
                continue;
            }

            days.Add(new HuntHistoryDay(startDate, start, index - start));
            start = index;
        }
    }
}
