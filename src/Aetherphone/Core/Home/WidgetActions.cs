using Aetherphone.Core.Apps;
using Aetherphone.Core.Notifications;

namespace Aetherphone.Core.Home;

internal sealed class WidgetActions
{
    private readonly Dictionary<string, IPhoneApp> appsById = new(StringComparer.Ordinal);
    private INavigator? navigator;
    private NotificationRouter? notificationRouter;

    public WidgetActions(WidgetServices services)
    {
        Services = services;
        var apps = services.Apps;
        for (var index = 0; index < apps.Count; index++)
        {
            appsById[apps[index].Id] = apps[index];
        }
    }

    public WidgetServices Services { get; }

    public PhoneServices Phone => Services.Phone;

    public void Bind(INavigator target) => navigator = target;

    public void BindNotifications(NotificationRouter router) => notificationRouter = router;

    public IPhoneApp? App(string appId) => appsById.TryGetValue(appId, out var app) ? app : null;

    public bool IsAvailable(string appId) => App(appId) is { IsAvailable: true };

    public void Open(in WidgetRoute route, Rect origin)
    {
        if (navigator is null || route.IsEmpty || App(route.AppId) is not { IsAvailable: true } app)
        {
            return;
        }

        if (route.Kind == WidgetRouteKind.Notification && notificationRouter is not null
            && FindNotification(route.Argument) is { } notification)
        {
            notificationRouter.Open(notification);
            return;
        }

        Request(route, app);
        navigator.OpenAppFrom(app, origin, LaunchOrigin.Surface);
    }

    private PhoneNotification? FindNotification(string? argument)
    {
        if (!long.TryParse(argument, out var id))
        {
            return null;
        }

        var recent = Services.Phone.Notifications.Recent;
        for (var index = recent.Count - 1; index >= 0; index--)
        {
            if (recent[index].Id == id)
            {
                return recent[index];
            }
        }

        return null;
    }

    private void Request(in WidgetRoute route, IPhoneApp app)
    {
        var argument = route.Argument ?? string.Empty;
        var phone = Services.Phone;
        switch (route.Kind)
        {
            case WidgetRouteKind.Tab when argument.Length > 0 && app is ITabRouteTarget target:
                target.OpenTab(argument);
                break;
            case WidgetRouteKind.Conversation when argument.Length > 0:
                phone.DmLauncher.RequestConversation(argument);
                break;
            case WidgetRouteKind.DirectUser when argument.Length > 0:
                phone.DmLauncher.RequestUser(argument);
                break;
            case WidgetRouteKind.Calls:
                phone.DmLauncher.RequestCalls();
                break;
            case WidgetRouteKind.Linkpearl when argument.Length > 0:
                phone.LinkpearlLauncher.Request(argument);
                break;
            case WidgetRouteKind.Muster when argument.Length > 0:
                phone.MusterLauncher.RequestDetail(argument);
                break;
            case WidgetRouteKind.MarketItem when route.Number != 0:
                phone.MarketLauncher.RequestItem(unchecked((uint)route.Number));
                break;
            case WidgetRouteKind.MarketSearch when argument.Length > 0:
                phone.MarketLauncher.RequestSearch(argument);
                break;
            case WidgetRouteKind.Hunt when argument.Length > 0:
                phone.HuntsLauncher.RequestDetail(argument, route.Secondary ?? string.Empty, route.Number);
                break;
            case WidgetRouteKind.RadioStation when argument.Length > 0:
                phone.RadioLauncher.RequestStation(argument);
                break;
            case WidgetRouteKind.Announcement when argument.Length > 0:
                phone.AnnouncementsLauncher.RequestDetail(argument);
                break;
            case WidgetRouteKind.Note when app is ISpotlightNotes notes && Guid.TryParse(argument, out var noteId):
                notes.RequestNote(noteId);
                break;
            case WidgetRouteKind.NewNote when app is ISpotlightNotes notes:
                notes.RequestNewNote();
                break;
            case WidgetRouteKind.Venue when app is ISpotlightVenues venues && argument.Length > 0:
                venues.RequestVenue(argument);
                break;
        }
    }
}
