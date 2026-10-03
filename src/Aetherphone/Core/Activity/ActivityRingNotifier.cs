using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Runtime;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Activity;

internal sealed class ActivityRingNotifier : IDisposable
{
    private const long TickIntervalMilliseconds = 2000;
    private const string GroupKey = "character.rings";
    private const int ProgressFlag = 1;
    private const int AdventureFlag = 2;
    private const int FortuneFlag = 4;
    private const int AllClosedFlag = 8;

    private static readonly Vector4 Accent = AppAccents.For("character");

    private readonly ActivityTracker tracker;
    private readonly Configuration configuration;
    private readonly NotificationService notifications;
    private readonly FrameworkTicker ticker;

    public ActivityRingNotifier(IFramework framework, ActivityTracker tracker, Configuration configuration,
        NotificationService notifications, AppGate gate)
    {
        this.tracker = tracker;
        this.configuration = configuration;
        this.notifications = notifications;
        ticker = new FrameworkTicker(framework, TickIntervalMilliseconds, OnTick, gate);
    }

    public void Dispose()
    {
        ticker.Dispose();
    }

    private void OnTick()
    {
        if (!tracker.IsTracking)
        {
            return;
        }

        var day = tracker.Today;
        var targets = ActivityTargets.From(configuration);
        SeedRecords(targets);
        NotifyRing(day, ActivityGoals.ProgressFraction(configuration, day), ProgressFlag, L.Character.RingProgress);
        NotifyRing(day, ActivityGoals.AdventureFraction(configuration, day), AdventureFlag, L.Character.RingAdventure);
        NotifyRing(day, ActivityGoals.FortuneFraction(configuration, day), FortuneFlag, L.Character.RingFortune);
        if ((day.RingsNotified & AllClosedFlag) != 0 || !ActivityGoals.AllClosed(configuration, day))
        {
            return;
        }

        day.RingsNotified |= AllClosedFlag;
        var records = tracker.Records;
        records.PerfectDays++;
        records.BestStreak = Math.Max(records.BestStreak,
            ActivityStats.CurrentStreak(tracker.Days, targets, DateTime.Now));
        tracker.MarkDirty();
        Notify(Loc.T(L.Character.AllRingsTitle), Loc.T(L.Character.AllRingsBody));
    }

    private void SeedRecords(in ActivityTargets targets)
    {
        var records = tracker.Records;
        if (records.Seeded)
        {
            return;
        }

        var days = tracker.Days;
        records.PerfectDays = Math.Max(records.PerfectDays, ActivityStats.CountFlagged(days, AllClosedFlag));
        records.BestStreak = Math.Max(records.BestStreak, ActivityStats.BestStreak(days, targets));
        records.Seeded = true;
        tracker.MarkDirty();
    }

    private void NotifyRing(ActivityDay day, float fraction, int flag, LocString ringName)
    {
        if (fraction < ActivityGoals.ClosedThreshold || (day.RingsNotified & flag) != 0)
        {
            return;
        }

        day.RingsNotified |= flag;
        tracker.MarkDirty();
        Notify(Loc.T(ringName), Loc.T(L.Character.RingClosedBody));
    }

    private void Notify(string title, string body)
    {
        notifications.Notify(new PhoneNotification("character", title, body, DateTime.Now, Accent, GroupKey));
    }
}
