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
    private readonly FrameworkTicker ticker;
    private readonly NotificationService notifications;

    public CalendarReminderService(Configuration configuration, IFramework framework, NotificationService notifications)
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
