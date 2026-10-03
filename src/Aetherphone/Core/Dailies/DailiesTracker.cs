using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Runtime;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Dailies;

internal sealed class DailiesTracker : IDisposable
{
    public const string AppId = "dailies";
    public const long DailyReminderLeadSeconds = 7200;
    public const long WeeklyReminderLeadSeconds = 86400;

    private const long TickIntervalMilliseconds = 2000;
    private const string ReminderGroup = "dailies:reminders";
    private const int CadenceCount = 2;

    private readonly Configuration configuration;
    private readonly GameData gameData;
    private readonly CharacterWatch characterWatch;
    private readonly NotificationService notifications;
    private readonly AppGate gate;
    private readonly FrameworkTicker ticker;
    private readonly DailyAutoStatus[] statuses = new DailyAutoStatus[DailyCatalog.Items.Length];
    private readonly DailyRowState[] states = new DailyRowState[DailyCatalog.Items.Length];
    private readonly int[] tracked = new int[CadenceCount];
    private readonly int[] done = new int[CadenceCount];
    private long lastTickUnix;
    private ulong readFor;

    public DailiesTracker(Configuration configuration, IFramework framework, GameData gameData,
        CharacterWatch characterWatch, NotificationService notifications, AppGate gate)
    {
        this.configuration = configuration;
        this.gameData = gameData;
        this.characterWatch = characterWatch;
        this.notifications = notifications;
        this.gate = gate;
        lastTickUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Array.Fill(statuses, DailyAutoStatus.Unavailable);
        ticker = new FrameworkTicker(framework, TickIntervalMilliseconds, OnTick);
        Recount(DateTime.UtcNow);
    }

    public int Version { get; private set; }

    public ulong ContentId => characterWatch.CurrentContentId;

    public bool LoggedIn => ContentId != 0;

    public bool HasData => LoggedIn && readFor == ContentId;

    public string RegionCode { get; private set; } = string.Empty;

    public List<DailyCustomTask> CustomTasks => configuration.DailyCustomTasks;

    public int Outstanding => LoggedIn ? Remaining(DailyCadence.Daily) + Remaining(DailyCadence.Weekly) : 0;

    public bool RemindDaily => configuration.NotifyDailiesBeforeDailyReset;

    public bool RemindWeekly => configuration.NotifyDailiesBeforeWeeklyReset;

    public void Dispose()
    {
        ticker.Dispose();
    }

    public DailyAutoStatus Status(int itemIndex) => statuses[itemIndex];

    public DailyRowState State(int itemIndex) => states[itemIndex];

    public int Tracked(DailyCadence cadence) => tracked[(int)cadence];

    public int Done(DailyCadence cadence) => done[(int)cadence];

    public int Remaining(DailyCadence cadence) => tracked[(int)cadence] - done[(int)cadence];

    public bool IsHidden(string itemId)
    {
        var hidden = configuration.DailyHiddenItems;
        for (var index = 0; index < hidden.Count; index++)
        {
            if (string.Equals(hidden[index], itemId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsCustomDone(DailyCustomTask task) =>
        DailyLedger.IsChecked(configuration.DailyChecks, task.Id, ContentId,
            DailyLedger.PeriodStartUnix(task.Cadence, DateTime.UtcNow));

    public void SetChecked(string itemId, DailyCadence cadence, bool value)
    {
        if (!LoggedIn)
        {
            return;
        }

        var utcNow = DateTime.UtcNow;
        var records = configuration.DailyChecks;
        var changed = DailyLedger.SetChecked(records, itemId, ContentId, DailyLedger.PeriodStartUnix(cadence, utcNow),
            value);
        changed |= DailyLedger.Prune(records, DailyLedger.OldestLiveUnix(utcNow)) > 0;
        if (!changed)
        {
            return;
        }

        configuration.Save();
        Recount(utcNow);
    }

    public void SetHidden(string itemId, bool hidden)
    {
        if (IsHidden(itemId) == hidden)
        {
            return;
        }

        if (hidden)
        {
            configuration.DailyHiddenItems.Add(itemId);
        }
        else
        {
            configuration.DailyHiddenItems.Remove(itemId);
        }

        configuration.Save();
        Recount(DateTime.UtcNow);
    }

    public bool AddTask(string rawTitle, DailyCadence cadence)
    {
        var title = DailyLedger.CleanTitle(rawTitle);
        if (title.Length == 0 || CustomTasks.Count >= DailyLedger.MaxCustomTasks)
        {
            return false;
        }

        CustomTasks.Add(new DailyCustomTask { Id = Guid.NewGuid().ToString("N"), Title = title, Cadence = cadence });
        configuration.Save();
        Recount(DateTime.UtcNow);
        return true;
    }

    public void RemoveTask(string taskId)
    {
        var tasks = CustomTasks;
        for (var index = 0; index < tasks.Count; index++)
        {
            if (!string.Equals(tasks[index].Id, taskId, StringComparison.Ordinal))
            {
                continue;
            }

            tasks.RemoveAt(index);
            DailyLedger.RemoveEverywhere(configuration.DailyChecks, taskId);
            configuration.Save();
            Recount(DateTime.UtcNow);
            return;
        }
    }

    public void SetReminder(DailyCadence cadence, bool value)
    {
        if (cadence == DailyCadence.Weekly)
        {
            configuration.NotifyDailiesBeforeWeeklyReset = value;
        }
        else
        {
            configuration.NotifyDailiesBeforeDailyReset = value;
        }

        configuration.Save();
    }

    private void OnTick()
    {
        var utcNow = DateTime.UtcNow;
        var nowUnix = new DateTimeOffset(utcNow).ToUnixTimeSeconds();
        if (!gate.Open)
        {
            lastTickUnix = nowUnix;
            return;
        }

        ReadGame();
        Recount(utcNow);
        var previousUnix = lastTickUnix;
        lastTickUnix = nowUnix;
        if (HasData && nowUnix > previousUnix)
        {
            CheckReminders(previousUnix, nowUnix, utcNow);
        }
    }

    private void ReadGame()
    {
        var contentId = ContentId;
        if (contentId == 0)
        {
            readFor = 0;
            Array.Fill(statuses, DailyAutoStatus.Unavailable);
            return;
        }

        var items = DailyCatalog.Items;
        for (var index = 0; index < items.Length; index++)
        {
            statuses[index] = DailyProgress.ReadStatus(gameData, items[index]);
        }

        var region = gameData.LocalRegionCode();
        if (region.Length > 0)
        {
            RegionCode = region;
        }

        readFor = contentId;
    }

    private void Recount(DateTime utcNow)
    {
        var contentId = ContentId;
        var records = configuration.DailyChecks;
        var dailyStart = DailyLedger.PeriodStartUnix(DailyCadence.Daily, utcNow);
        var weeklyStart = DailyLedger.PeriodStartUnix(DailyCadence.Weekly, utcNow);
        var trackedDaily = 0;
        var trackedWeekly = 0;
        var doneDaily = 0;
        var doneWeekly = 0;
        var stateChanged = false;
        var items = DailyCatalog.Items;
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var periodStart = item.Cadence == DailyCadence.Weekly ? weeklyStart : dailyStart;
            var manualChecked = item.Tracking == DailyTracking.Manual &&
                                DailyLedger.IsChecked(records, item.Id, contentId, periodStart);
            var state = DailyProgress.State(item, statuses[index], IsHidden(item.Id), manualChecked);
            stateChanged |= states[index] != state;
            states[index] = state;
            if (!DailyProgress.Counts(state))
            {
                continue;
            }

            Tally(item.Cadence, state == DailyRowState.Done, ref trackedDaily, ref trackedWeekly, ref doneDaily,
                ref doneWeekly);
        }

        var tasks = CustomTasks;
        for (var index = 0; index < tasks.Count; index++)
        {
            var task = tasks[index];
            var periodStart = task.Cadence == DailyCadence.Weekly ? weeklyStart : dailyStart;
            Tally(task.Cadence, DailyLedger.IsChecked(records, task.Id, contentId, periodStart), ref trackedDaily,
                ref trackedWeekly, ref doneDaily, ref doneWeekly);
        }

        var countsChanged = tracked[0] != trackedDaily || tracked[1] != trackedWeekly || done[0] != doneDaily ||
                            done[1] != doneWeekly;
        tracked[0] = trackedDaily;
        tracked[1] = trackedWeekly;
        done[0] = doneDaily;
        done[1] = doneWeekly;
        if (stateChanged || countsChanged)
        {
            Version++;
        }
    }

    private static void Tally(DailyCadence cadence, bool isDone, ref int trackedDaily, ref int trackedWeekly,
        ref int doneDaily, ref int doneWeekly)
    {
        if (cadence == DailyCadence.Weekly)
        {
            trackedWeekly++;
            doneWeekly += isDone ? 1 : 0;
            return;
        }

        trackedDaily++;
        doneDaily += isDone ? 1 : 0;
    }

    private void CheckReminders(long previousUnix, long nowUnix, DateTime utcNow)
    {
        if (RemindDaily)
        {
            Remind(DailyCadence.Daily, DailyReminderLeadSeconds, L.Dailies.ReminderDaily, previousUnix, nowUnix,
                utcNow);
        }

        if (RemindWeekly)
        {
            Remind(DailyCadence.Weekly, WeeklyReminderLeadSeconds, L.Dailies.ReminderWeekly, previousUnix, nowUnix,
                utcNow);
        }
    }

    private void Remind(DailyCadence cadence, long leadSeconds, LocString title, long previousUnix, long nowUnix,
        DateTime utcNow)
    {
        var remaining = Remaining(cadence);
        if (remaining <= 0)
        {
            return;
        }

        var reset = DailyLedger.NextReset(cadence, utcNow);
        var resetUnix = new DateTimeOffset(reset, TimeSpan.Zero).ToUnixTimeSeconds();
        if (!DailyLedger.ReminderDue(previousUnix, nowUnix, resetUnix, leadSeconds))
        {
            return;
        }

        notifications.Notify(new PhoneNotification(AppId, Loc.T(title, TimeText.Until(reset - utcNow)),
            Loc.Plural(L.Dailies.OpenTasks, remaining), DateTime.Now, AppAccents.For(AppId), ReminderGroup));
    }
}
