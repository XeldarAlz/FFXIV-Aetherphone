using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Housing;
using Aetherphone.Core.Venues;

namespace Aetherphone.Core.Casino;

internal readonly record struct CasinoHousingPosition(int World, int Territory, int Ward, int Plot, int ApartmentWing,
    int Room)
{
    public bool InWard => World > 0 && Territory > 0 && Ward > 0;

    public bool AtPlot => InWard && (Plot > 0 || ApartmentWing > 0);

    public CasinoTableLocationDto? ToLocation()
    {
        return InWard ? new CasinoTableLocationDto(World, Territory, Ward, Plot, ApartmentWing, Room) : null;
    }

    public bool SameWard(CasinoTableLocationDto? location)
    {
        return location is not null && InWard && location.World == World && location.Territory == Territory
            && location.Ward == Ward;
    }

    public static VenueAddress AddressOf(CasinoTableLocationDto? location, Func<uint, string> worldName)
    {
        if (location is null || location.World <= 0 || location.Ward <= 0 || location.Plot <= 0
            || !HousingDistricts.TryGet((uint)location.Territory, out var district))
        {
            return default;
        }

        return VenueAddress.Of(worldName((uint)location.World), district.Name, location.Ward, location.Plot);
    }
}

internal interface IHousingPositionSource
{
    CasinoHousingPosition Read();
}
