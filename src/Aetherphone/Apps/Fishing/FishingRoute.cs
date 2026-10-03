using Aetherphone.Core.Game;

namespace Aetherphone.Apps.Fishing;

internal enum FishingScreen : byte
{
    Root,
    Voyage,
    Fish,
}

internal enum FishingTab : byte
{
    Voyages,
    Fish,
}

internal readonly record struct FishingRoute(FishingScreen Screen, long BoardingUnix = 0,
    OceanRoute OceanRoute = OceanRoute.Indigo, uint ItemId = 0)
{
    public static readonly FishingRoute Root = new(FishingScreen.Root);

    public static FishingRoute Voyage(long boardingUnix, OceanRoute route) =>
        new(FishingScreen.Voyage, boardingUnix, route);

    public static FishingRoute Fish(uint itemId) => new(FishingScreen.Fish, ItemId: itemId);
}
