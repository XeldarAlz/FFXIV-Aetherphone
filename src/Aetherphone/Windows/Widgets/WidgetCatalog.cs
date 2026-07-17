using Aetherphone.Apps.Calendar;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Photos;

namespace Aetherphone.Windows.Widgets;

internal static class WidgetCatalog
{
    public static WidgetRegistry Build(PhoneServices services, PhotoLibrary photos, CalendarEvents calendarEvents,
        IReadOnlyList<IPhoneApp> apps)
    {
        var widgets = new List<IHomeWidget>
        {
            new SkywatcherWidget(services.Weather),
            new ClockWidget(),
            new CalendarWidget(services.Configuration, calendarEvents),
            new PhotosWidget(photos),
            new ResetsWidget(),
            new ActivityRingsWidget(services.Activity, services.Configuration),
        };

        return new WidgetRegistry(widgets, apps);
    }
}
