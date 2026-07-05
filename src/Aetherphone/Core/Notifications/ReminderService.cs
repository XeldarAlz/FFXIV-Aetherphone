using System.Numerics;
using Aetherphone.Core.Localization;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Notifications;

internal sealed class ReminderService : IDisposable
{
    private const long TickIntervalMilliseconds = 1000;
    private static readonly Vector4 Accent = new(1.00f, 0.79f, 0.16f, 1f);
    private readonly Configuration configuration;
    private readonly IFramework framework;
    private readonly NotificationService notifications;
    private long lastTickMilliseconds;

    public ReminderService(Configuration configuration, IFramework framework, NotificationService notifications)
    {
        this.configuration = configuration;
        this.framework = framework;
        this.notifications = notifications;
        this.framework.Update += OnUpdate;
    }

    public void Dispose()
    {
        framework.Update -= OnUpdate;
    }

    private void OnUpdate(IFramework owner)
    {
        var now = Environment.TickCount64;
        if (now - lastTickMilliseconds < TickIntervalMilliseconds)
        {
            return;
        }

        lastTickMilliseconds = now;
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
