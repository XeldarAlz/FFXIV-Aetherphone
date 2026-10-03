using Aetherphone.Core.Venues;

namespace Aetherphone.Apps.Venues;

internal enum VenueScreen : byte
{
    Home,
    Filters,
    Detail,
    List,
    Scope,
}

internal enum VenueTab : byte
{
    Discover,
    Live,
    Events,
    Saved,
}

internal enum VenueListKind : byte
{
    Live,
    LaterToday,
    NearYou,
    Category,
    Directory,
}

internal readonly record struct VenueRoute(VenueScreen Screen, VenueEvent? Venue = null,
    VenueListKind List = VenueListKind.Directory, int Category = -1)
{
    public static readonly VenueRoute Home = new(VenueScreen.Home);
    public static readonly VenueRoute Filters = new(VenueScreen.Filters);
    public static readonly VenueRoute Scope = new(VenueScreen.Scope);

    public static VenueRoute Detail(VenueEvent venue) => new(VenueScreen.Detail, venue);

    public static VenueRoute ListOf(VenueListKind kind, int category = -1) =>
        new(VenueScreen.List, null, kind, category);
}
