using Aetherphone.Core.Venues;

namespace Aetherphone.Apps.Venues;

internal enum VenueScreen : byte
{
    List,
    Tags,
    Detail,
}

internal readonly record struct VenueRoute(VenueScreen Screen, VenueEvent? Venue = null)
{
    public static readonly VenueRoute List = new(VenueScreen.List);
    public static readonly VenueRoute Tags = new(VenueScreen.Tags);

    public static VenueRoute Detail(VenueEvent venue) => new(VenueScreen.Detail, venue);
}
