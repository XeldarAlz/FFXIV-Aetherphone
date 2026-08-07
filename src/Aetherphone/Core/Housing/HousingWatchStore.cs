namespace Aetherphone.Core.Housing;

internal sealed class HousingWatchStore
{
    private readonly Configuration configuration;
    private readonly HashSet<HousingPlotKey> watchedKeys = new();

    public HousingWatchStore(Configuration configuration)
    {
        this.configuration = configuration;
        Rebuild();
    }

    public int Revision { get; private set; }

    public IReadOnlyList<HousingWatchRecord> Watched => configuration.HousingWatched;

    public IReadOnlyList<HousingReminderRecord> Reminders => configuration.HousingReminders;

    public int PendingReminderCount
    {
        get
        {
            var reminders = configuration.HousingReminders;
            var count = 0;
            for (var index = 0; index < reminders.Count; index++)
            {
                if (!reminders[index].Notified && reminders[index].FireAtUtc is not null)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public int FiredReminderCount
    {
        get
        {
            var reminders = configuration.HousingReminders;
            var count = 0;
            for (var index = 0; index < reminders.Count; index++)
            {
                if (reminders[index].Notified)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public bool IsWatched(HousingPlotKey key) => watchedKeys.Contains(key);

    public bool ToggleWatch(HousingPlot plot, string worldName)
    {
        if (IsWatched(plot.Key))
        {
            Unwatch(plot.Key);
            return false;
        }

        Watch(plot, worldName);
        return true;
    }

    public void Watch(HousingPlot plot, string worldName)
    {
        if (!plot.Key.IsValid || IsWatched(plot.Key))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        configuration.HousingWatched.Add(new HousingWatchRecord
        {
            WorldId = plot.Key.WorldId,
            DistrictId = plot.Key.DistrictId,
            Ward = plot.Key.Ward,
            Plot = plot.Key.Plot,
            WorldName = worldName,
            Size = (byte)plot.Size,
            Phase = (byte)plot.Phase,
            Eligibility = (byte)plot.Eligibility,
            Price = plot.Price,
            Entries = plot.Entries,
            PhaseEndUnix = ToUnix(plot.PhaseEndsUtc),
            LastSeenUnix = ToUnix(plot.LastSeenUtc),
            FirstSeenUnix = ToUnix(plot.FirstSeenUtc),
            AddedUnix = now,
            StillReported = true,
            LastReportedUnix = now,
        });
        watchedKeys.Add(plot.Key);
        Commit();
    }

    public void Unwatch(HousingPlotKey key)
    {
        var watched = configuration.HousingWatched;
        var removed = false;
        for (var index = watched.Count - 1; index >= 0; index--)
        {
            if (watched[index].Key != key)
            {
                continue;
            }

            watched.RemoveAt(index);
            removed = true;
        }

        if (!removed)
        {
            return;
        }

        watchedKeys.Remove(key);
        Commit();
    }

    public void ClearWatched()
    {
        if (configuration.HousingWatched.Count == 0)
        {
            return;
        }

        configuration.HousingWatched.Clear();
        watchedKeys.Clear();
        Commit();
    }

    public HousingWatchRecord? Find(HousingPlotKey key)
    {
        var watched = configuration.HousingWatched;
        for (var index = 0; index < watched.Count; index++)
        {
            if (watched[index].Key == key)
            {
                return watched[index];
            }
        }

        return null;
    }

    public void Reconcile(HousingDistrictSnapshot snapshot)
    {
        var watched = configuration.HousingWatched;
        if (watched.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var dirty = false;
        for (var index = 0; index < watched.Count; index++)
        {
            var record = watched[index];
            if (record.WorldId != snapshot.WorldId || record.DistrictId != snapshot.DistrictId)
            {
                continue;
            }

            var match = FindPlot(snapshot, record.Key);
            if (match is not null)
            {
                dirty |= SyncReminderTo(match);
            }

            if (match is null)
            {
                if (record.StillReported)
                {
                    record.StillReported = false;
                    dirty = true;
                }

                continue;
            }

            var phaseEnd = ToUnix(match.PhaseEndsUtc);
            var lastSeen = ToUnix(match.LastSeenUtc);
            if (record.StillReported && record.Phase == (byte)match.Phase && record.PhaseEndUnix == phaseEnd &&
                record.Entries == match.Entries && record.LastSeenUnix == lastSeen && record.Price == match.Price)
            {
                record.LastReportedUnix = now;
                continue;
            }

            record.StillReported = true;
            record.Size = (byte)match.Size;
            record.Phase = (byte)match.Phase;
            record.Eligibility = (byte)match.Eligibility;
            record.Price = match.Price;
            record.Entries = match.Entries;
            record.PhaseEndUnix = phaseEnd;
            record.LastSeenUnix = lastSeen;
            record.FirstSeenUnix = ToUnix(match.FirstSeenUtc);
            record.LastReportedUnix = now;
            dirty = true;
        }

        if (dirty)
        {
            Commit();
        }
    }

    public HousingReminderRecord? FindReminder(HousingPlotKey key)
    {
        var reminders = configuration.HousingReminders;
        for (var index = 0; index < reminders.Count; index++)
        {
            if (reminders[index].Key == key)
            {
                return reminders[index];
            }
        }

        return null;
    }

    public bool SetReminder(HousingPlot plot, string worldName, int offsetMinutes)
    {
        if (plot.PhaseEndsUtc is not { } deadline || deadline == default)
        {
            return false;
        }

        var phaseEnd = ToUnix(deadline);
        var existing = FindReminder(plot.Key);
        if (existing is not null)
        {
            var samePhase = existing.Phase == (byte)plot.Phase && existing.PhaseEndUnix == phaseEnd;
            existing.Phase = (byte)plot.Phase;
            existing.PhaseEndUnix = phaseEnd;
            existing.WorldName = worldName;
            existing.Entries = plot.Entries;
            existing.LastSeenUnix = ToUnix(plot.LastSeenUtc);
            if (!samePhase || existing.OffsetMinutes != offsetMinutes)
            {
                existing.Notified = false;
            }

            existing.OffsetMinutes = offsetMinutes;
            Commit();
            return true;
        }

        configuration.HousingReminders.Add(new HousingReminderRecord
        {
            WorldId = plot.Key.WorldId,
            DistrictId = plot.Key.DistrictId,
            Ward = plot.Key.Ward,
            Plot = plot.Key.Plot,
            WorldName = worldName,
            Phase = (byte)plot.Phase,
            PhaseEndUnix = phaseEnd,
            OffsetMinutes = offsetMinutes,
            Notified = false,
            CreatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Entries = plot.Entries,
            LastSeenUnix = ToUnix(plot.LastSeenUtc),
        });
        Commit();
        return true;
    }

    public void CancelReminder(HousingPlotKey key)
    {
        var reminders = configuration.HousingReminders;
        var removed = false;
        for (var index = reminders.Count - 1; index >= 0; index--)
        {
            if (reminders[index].Key != key)
            {
                continue;
            }

            reminders.RemoveAt(index);
            removed = true;
        }

        if (removed)
        {
            Commit();
        }
    }
    public void SyncReminder(HousingPlot plot)
    {
        if (SyncReminderTo(plot))
        {
            Commit();
        }
    }

    private bool SyncReminderTo(HousingPlot plot)
    {
        var reminder = FindReminder(plot.Key);
        if (reminder is null || plot.PhaseEndsUtc is not { } deadline || deadline == default)
        {
            return false;
        }

        var phaseEnd = ToUnix(deadline);
        if (reminder.Phase == (byte)plot.Phase && reminder.PhaseEndUnix == phaseEnd)
        {
            return false;
        }

        reminder.Phase = (byte)plot.Phase;
        reminder.PhaseEndUnix = phaseEnd;
        reminder.Entries = plot.Entries;
        reminder.LastSeenUnix = ToUnix(plot.LastSeenUtc);
        reminder.Notified = false;
        return true;
    }

    public void MarkNotified(HousingReminderRecord reminder)
    {
        if (reminder.Notified)
        {
            return;
        }

        reminder.Notified = true;
        Commit();
    }

    public void PruneFiredReminders(DateTime nowUtc)
    {
        var reminders = configuration.HousingReminders;
        var cutoff = ToUnix(nowUtc);
        var removed = false;
        for (var index = reminders.Count - 1; index >= 0; index--)
        {
            var reminder = reminders[index];
            if (!reminder.Notified || reminder.PhaseEndUnix <= 0L || reminder.PhaseEndUnix > cutoff)
            {
                continue;
            }

            reminders.RemoveAt(index);
            removed = true;
        }

        if (removed)
        {
            Commit();
        }
    }

    private static HousingPlot? FindPlot(HousingDistrictSnapshot snapshot, HousingPlotKey key)
    {
        var plots = snapshot.Plots;
        for (var index = 0; index < plots.Count; index++)
        {
            if (plots[index].Key == key)
            {
                return plots[index];
            }
        }

        return null;
    }

    private void Rebuild()
    {
        watchedKeys.Clear();
        var watched = configuration.HousingWatched;
        for (var index = 0; index < watched.Count; index++)
        {
            watchedKeys.Add(watched[index].Key);
        }
    }

    private void Commit()
    {
        Revision++;
        configuration.Save();
    }

    private static long ToUnix(DateTime? moment) =>
        moment is null || moment.Value == default
            ? 0L
            : new DateTimeOffset(DateTime.SpecifyKind(moment.Value, DateTimeKind.Utc)).ToUnixTimeSeconds();
}
