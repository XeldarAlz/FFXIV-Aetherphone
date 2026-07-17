using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Notifications;

internal sealed class TimerNotifier : IDisposable
{
    private const long TickIntervalMilliseconds = 1000;
    private static readonly Vector4 Accent = new(0.40f, 0.45f, 0.92f, 1f);
    private readonly Configuration configuration;
    private readonly FrameworkTicker ticker;
    private readonly NotificationService notifications;
    private readonly List<RetainerVenture> retainers = new();
    private readonly Dictionary<ulong, long> seenPendingComplete = new();
    private readonly Dictionary<ulong, long> notifiedComplete = new();
    private DateTime nextDaily;
    private DateTime nextWeekly;
    private DateTime nextGrandCompany;

    public TimerNotifier(Configuration configuration, IFramework framework, NotificationService notifications)
    {
        this.configuration = configuration;
        this.notifications = notifications;
        var utcNow = DateTime.UtcNow;
        nextDaily = GameSchedule.NextDailyReset(utcNow);
        nextWeekly = GameSchedule.NextWeeklyReset(utcNow);
        nextGrandCompany = GameSchedule.NextGrandCompanyReset(utcNow);
        ticker = new FrameworkTicker(framework, TickIntervalMilliseconds, OnTick);
    }

    public void Dispose()
    {
        ticker.Dispose();
    }

    private void OnTick()
    {
        var utcNow = DateTime.UtcNow;
        CheckResets(utcNow);
        CheckRetainers(utcNow);
    }

    private void CheckResets(DateTime utcNow)
    {
        if (utcNow >= nextDaily)
        {
            if (configuration.NotifyDailyReset)
            {
                Notify(Loc.T(L.Timers.DailyReset), Loc.T(L.Timers.ResetNotice));
            }

            nextDaily = GameSchedule.NextDailyReset(utcNow);
        }

        if (utcNow >= nextGrandCompany)
        {
            if (configuration.NotifyGrandCompanyReset)
            {
                Notify(Loc.T(L.Timers.GrandCompanyReset), Loc.T(L.Timers.ResetNotice));
            }

            nextGrandCompany = GameSchedule.NextGrandCompanyReset(utcNow);
        }

        if (utcNow >= nextWeekly)
        {
            if (configuration.NotifyWeeklyReset)
            {
                Notify(Loc.T(L.Timers.WeeklyReset), Loc.T(L.Timers.ResetNotice));
            }

            nextWeekly = GameSchedule.NextWeeklyReset(utcNow);
        }
    }

    private void CheckRetainers(DateTime utcNow)
    {
        if (!configuration.NotifyRetainerVentures || !RetainerReader.TryRead(retainers))
        {
            return;
        }

        for (var index = 0; index < retainers.Count; index++)
        {
            var venture = retainers[index];
            if (!venture.HasVenture)
            {
                continue;
            }

            var completeTicks = venture.CompleteUtc.Ticks;
            if (venture.CompleteUtc > utcNow)
            {
                seenPendingComplete[venture.RetainerId] = completeTicks;
                continue;
            }

            var wasPending = seenPendingComplete.TryGetValue(venture.RetainerId, out var pending) &&
                             pending == completeTicks;
            var alreadyNotified = notifiedComplete.TryGetValue(venture.RetainerId, out var notified) &&
                                  notified == completeTicks;
            if (!wasPending || alreadyNotified)
            {
                continue;
            }

            notifiedComplete[venture.RetainerId] = completeTicks;
            Notify(venture.Name, Loc.T(L.Timers.VentureComplete));
        }
    }

    private void Notify(string title, string body)
    {
        notifications.Notify(new PhoneNotification("timers", title, body, DateTime.Now, Accent));
    }
}
