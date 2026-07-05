using Aetherphone.Core.Game;

namespace Aetherphone.Core.Dailies;

internal sealed class DailyCheckStore
{
    private readonly Configuration configuration;

    public DailyCheckStore(Configuration configuration)
    {
        this.configuration = configuration;
    }

    public bool IsChecked(in DailyItem item, DateTime utcNow)
    {
        var record = Find(item.Id);
        if (record is null)
        {
            return false;
        }

        return record.PeriodResetUnix == CurrentPeriodResetUnix(item.Cadence, utcNow);
    }

    public void SetChecked(in DailyItem item, bool value, DateTime utcNow)
    {
        var record = Find(item.Id);
        if (!value)
        {
            if (record is null)
            {
                return;
            }

            configuration.DailyChecks.Remove(record);
            configuration.Save();
            return;
        }

        var periodReset = CurrentPeriodResetUnix(item.Cadence, utcNow);
        if (record is null)
        {
            configuration.DailyChecks.Add(new DailyCheckRecord { ItemId = item.Id, PeriodResetUnix = periodReset, });
            configuration.Save();
            return;
        }

        if (record.PeriodResetUnix == periodReset)
        {
            return;
        }

        record.PeriodResetUnix = periodReset;
        configuration.Save();
    }

    private DailyCheckRecord? Find(string itemId)
    {
        var records = configuration.DailyChecks;
        for (var index = 0; index < records.Count; index++)
        {
            if (string.Equals(records[index].ItemId, itemId, StringComparison.Ordinal))
            {
                return records[index];
            }
        }

        return null;
    }

    private static long CurrentPeriodResetUnix(DailyCadence cadence, DateTime utcNow)
    {
        var next = cadence == DailyCadence.Weekly
            ? GameSchedule.NextWeeklyReset(utcNow)
            : GameSchedule.NextDailyReset(utcNow);
        var period = cadence == DailyCadence.Weekly ? TimeSpan.FromDays(7) : TimeSpan.FromDays(1);
        var current = next - period;
        return new DateTimeOffset(current, TimeSpan.Zero).ToUnixTimeSeconds();
    }
}
