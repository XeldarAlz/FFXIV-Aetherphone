namespace Aetherphone.Core.Home;

internal enum WidgetRouteKind : byte
{
    App,
    Tab,
    Conversation,
    DirectUser,
    Calls,
    Linkpearl,
    Muster,
    MarketItem,
    MarketSearch,
    Hunt,
    RadioStation,
    Announcement,
    Note,
    NewNote,
    Venue,
    Notification,
}

internal readonly struct WidgetRoute
{
    public readonly string AppId;
    public readonly WidgetRouteKind Kind;
    public readonly string Argument;
    public readonly string Secondary;
    public readonly int Number;

    public WidgetRoute(string appId, WidgetRouteKind kind, string argument, string secondary = "", int number = 0)
    {
        AppId = appId;
        Kind = kind;
        Argument = argument;
        Secondary = secondary;
        Number = number;
    }

    public bool IsEmpty => string.IsNullOrEmpty(AppId);

    public static WidgetRoute App(string appId) => new(appId, WidgetRouteKind.App, string.Empty);

    public static WidgetRoute Tab(string appId, string intent) => new(appId, WidgetRouteKind.Tab, intent);

    public static WidgetRoute To(string appId, WidgetRouteKind kind, string argument) => new(appId, kind, argument);

    public static WidgetRoute MarketItem(uint itemId) =>
        new("market", WidgetRouteKind.MarketItem, string.Empty, string.Empty, unchecked((int)itemId));

    public static WidgetRoute Hunt(string mobId, string worldId, int zoneInstance) =>
        new("hunts", WidgetRouteKind.Hunt, mobId, worldId, zoneInstance);
}
