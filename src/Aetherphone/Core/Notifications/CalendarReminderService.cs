using System.Numerics;
using Aetherphone.Apps.Calendar;
using Aetherphone.Core.Localization;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Notifications;

internal sealed class CalendarReminderService : IDisposable
{
    private const long TickIntervalMilliseconds = 1000;
    private static readonly Vector4 Accent = new(1.000f, 0.231f, 0.188f, 1f);
    private readonly Configuration configuration;
    private readonly IFramework framework;
    private readonly NotificationService notifications;
    private long lastTickMilliseconds;

    public CalendarReminderService(Configuration configuration, IFramework framework, NotificationService notifications)
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
        var events = configuration.CalendarCustomEvents;
        var dirty = false;
        var nowLocal = DateTime.Now;
        for (var index = 0; index < events.Count; index++)
        {
            var calendarEvent = events[index];
            if (calendarEvent.Notified || calendarEvent.When > nowLocal)
            {
                continue;
            }

            calendarEvent.Notified = true;
            dirty = true;
            notifications.Notify(new PhoneNotification("calendar", calendarEvent.Title,
                calendarEvent.When.ToString("t", Loc.Culture), DateTime.Now, Accent));
        }

        if (dirty)
        {
            configuration.Save();
        }
    }
}
