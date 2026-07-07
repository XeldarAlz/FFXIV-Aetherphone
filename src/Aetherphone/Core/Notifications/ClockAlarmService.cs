using System.Numerics;
using Aetherphone.Apps.Clock;
using Aetherphone.Core.Localization;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Notifications;

internal sealed class ClockAlarmService : IDisposable
{
    private const long TickIntervalMilliseconds = 1000;
    private static readonly Vector4 Accent = new(1.00f, 0.58f, 0.00f, 1f);
    private readonly Configuration configuration;
    private readonly FrameworkTicker ticker;
    private readonly NotificationService notifications;

    public ClockAlarmService(Configuration configuration, IFramework framework, NotificationService notifications)
    {
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
            if (!alarm.Enabled || nowLocal.Hour != alarm.Hour || nowLocal.Minute != alarm.Minute)
            {
                continue;
            }

            if (alarm.Repeats && !alarm.RepeatsOn(nowLocal.DayOfWeek))
            {
                continue;
            }

            var key = AlarmSchedule.MinuteKey(nowLocal);
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
            notifications.Notify(new PhoneNotification("clock", title, $"{alarm.Hour:D2}:{alarm.Minute:D2}",
                DateTime.Now, Accent));
        }

        return dirty;
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
