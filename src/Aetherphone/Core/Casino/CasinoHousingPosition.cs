using Aetherphone.Core.Aethernet.Contracts;

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
}

internal interface IHousingPositionSource
{
    CasinoHousingPosition Read();
}
