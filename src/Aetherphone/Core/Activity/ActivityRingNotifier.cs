using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
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
        NotificationService notifications)
    {
        this.tracker = tracker;
        this.configuration = configuration;
        this.notifications = notifications;
        ticker = new FrameworkTicker(framework, TickIntervalMilliseconds, OnTick);
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
        NotifyRing(day, ActivityGoals.ProgressFraction(configuration, day), ProgressFlag, L.Character.RingProgress);
        NotifyRing(day, ActivityGoals.AdventureFraction(configuration, day), AdventureFlag, L.Character.RingAdventure);
        NotifyRing(day, ActivityGoals.FortuneFraction(configuration, day), FortuneFlag, L.Character.RingFortune);
        if ((day.RingsNotified & AllClosedFlag) != 0 || !ActivityGoals.AllClosed(configuration, day))
        {
            return;
        }

        day.RingsNotified |= AllClosedFlag;
        tracker.MarkDirty();
        Notify(Loc.T(L.Character.AllRingsTitle), Loc.T(L.Character.AllRingsBody));
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
