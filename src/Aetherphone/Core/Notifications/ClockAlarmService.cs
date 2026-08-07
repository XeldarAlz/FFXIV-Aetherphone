using Aetherphone.Apps.Clock;
using Aetherphone.Core.Clock;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Notifications;

internal sealed class ClockAlarmService : IDisposable
{
    private const long TickIntervalMilliseconds = 1000;
    private static readonly TimeSpan CatchUpWindow = TimeSpan.FromMinutes(10);
    private static readonly Vector4 Accent = new(1.00f, 0.58f, 0.00f, 1f);
    private readonly Configuration configuration;
    private readonly FrameworkTicker ticker;
    private readonly NotificationService notifications;

    public ClockAlarmService(Configuration configuration, IFramework framework, NotificationService notifications,
        AppGate gate)
    {
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
        var dirty = CheckAlarms(DateTime.Now);
        dirty |= CheckTimer(DateTime.UtcNow);
        if (dirty)
        {
            configuration.Save();
        }
    }

    private bool CheckAlarms(DateTime nowLocal)
    {
        var dirty = false;
        var alarms = configuration.Alarms;
        for (var index = 0; index < alarms.Count; index++)
        {
            var alarm = alarms[index];
            if (!alarm.Enabled || !TryResolveDue(alarm, nowLocal, out var due))
            {
                continue;
            }

            var key = AlarmSchedule.MinuteKey(due);
            if (alarm.LastFiredEpochMinute == key)
            {
                continue;
            }

            alarm.LastFiredEpochMinute = key;
            if (!alarm.Repeats)
            {
                alarm.Enabled = false;
            }

            dirty = true;
            var title = alarm.Label.Length > 0 ? alarm.Label : Loc.T(L.Clock.Alarm);
            notifications.Notify(new PhoneNotification("clock", title, TimeText.Clock(due), DateTime.Now, Accent));
        }

        return dirty;
    }

    private static bool TryResolveDue(AlarmEntry alarm, DateTime nowLocal, out DateTime due)
    {
        due = default;
        for (var dayOffset = 0; dayOffset <= 1; dayOffset++)
        {
            var candidate = nowLocal.Date.AddDays(-dayOffset).AddHours(alarm.Hour).AddMinutes(alarm.Minute);
            var elapsed = nowLocal - candidate;
            if (elapsed < TimeSpan.Zero || elapsed > CatchUpWindow)
            {
                continue;
            }

            if (alarm.Repeats && !alarm.RepeatsOn(candidate.DayOfWeek))
            {
                continue;
            }

            due = candidate;
            return true;
        }

        return false;
    }

    private bool CheckTimer(DateTime utcNow)
    {
        if (configuration.TimerEndsAtUtc is not { } end || configuration.TimerNotified || utcNow < end)
        {
            return false;
        }

        configuration.TimerNotified = true;
        notifications.Notify(new PhoneNotification("clock", Loc.T(L.Clock.TimerTitle), Loc.T(L.Clock.TimerFinished),
            DateTime.Now, Accent));
        return true;
    }
}
