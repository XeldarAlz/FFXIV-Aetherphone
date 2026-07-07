using System.Numerics;
using Aetherphone.Core.Localization;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Notifications;

internal sealed class ReminderService : IDisposable
{
    private const long TickIntervalMilliseconds = 1000;
    private static readonly Vector4 Accent = new(1.00f, 0.79f, 0.16f, 1f);
    private readonly Configuration configuration;
    private readonly FrameworkTicker ticker;
    private readonly NotificationService notifications;

    public ReminderService(Configuration configuration, IFramework framework, NotificationService notifications)
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
        var reminders = configuration.Reminders;
        var dirty = false;
        var nowLocal = DateTime.Now;
        for (var index = 0; index < reminders.Count; index++)
        {
            var reminder = reminders[index];
            if (reminder.Notified || reminder.Done || reminder.DueAt is not { } due || due > nowLocal)
            {
                continue;
            }

            reminder.Notified = true;
            dirty = true;
            notifications.Notify(new PhoneNotification("notes", reminder.Title, due.ToString("t", Loc.Culture),
                DateTime.Now, Accent));
        }

        if (dirty)
        {
            configuration.Save();
        }
    }
}
