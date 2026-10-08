using Aetherphone.Core.Housing;
using Aetherphone.Core.Maps;

namespace Aetherphone.Core.Casino;

internal sealed class HousingPositionReader : IHousingPositionSource
{
    private const long ReadIntervalMilliseconds = 1_000;

    private CasinoHousingPosition held;
    private long readAtTick;

    public CasinoHousingPosition Read()
    {
        var now = Environment.TickCount64;
        if (readAtTick != 0 && now - readAtTick < ReadIntervalMilliseconds)
        {
            return held;
        }

        readAtTick = now;
        held = Capture();
        return held;
    }

    private static CasinoHousingPosition Capture()
    {
        if (!Plugin.ClientState.IsLoggedIn)
        {
            return default;
        }

        var world = (int)LocationShare.CurrentWorldId();
        var (ward, plot, room) = LocationShare.CurrentHousing();
        if (world <= 0 || ward <= 0)
        {
            return default;
        }

        var territory = TerritoryOf();
        return territory <= 0 ? default : new CasinoHousingPosition(world, territory, ward, plot, 0, room);
    }

    private static int TerritoryOf()
    {
        var district = LocationShare.CurrentHouseDistrict();
        if (district != 0 && HousingDistricts.TryGet(district, out _))
        {
            return (int)district;
        }

        var current = (uint)Plugin.ClientState.TerritoryType;
        return HousingDistricts.TryGet(current, out _) ? (int)current : 0;
    }
}
