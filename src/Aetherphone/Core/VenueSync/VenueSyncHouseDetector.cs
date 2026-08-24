using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace Aetherphone.Core.VenueSync;

internal readonly record struct VenueSyncHouse(long HouseId, string Label);

internal static unsafe class VenueSyncHouseDetector
{
    public static VenueSyncHouse? Current(GameData gameData)
    {
        var housingManager = HousingManager.Instance();
        if (housingManager is null || !(housingManager->IsInside() || housingManager->IsOutside()))
        {
            return null;
        }

        var plotIndex = housingManager->GetCurrentPlot();
        if (plotIndex == NoPlotSentinel)
        {
            return null;
        }

        var wardIndex = housingManager->GetCurrentWard();
        var room = housingManager->GetCurrentRoom();
        var territoryTypeId = HousingManager.GetOriginalHouseTerritoryTypeId();
        var worldId = gameData.LocalCurrentWorldId;
        var storedHouseId = BuildHouseId(worldId, territoryTypeId, wardIndex, plotIndex, room);
        HouseId houseId = (ulong)storedHouseId;
        return new VenueSyncHouse(storedHouseId, LabelFor(houseId, gameData));
    }

    public static string LabelFor(long storedHouseId, GameData gameData)
    {
        HouseId houseId = (ulong)storedHouseId;
        return LabelFor(houseId, gameData);
    }

    private const sbyte NoPlotSentinel = -1;
    private const sbyte ApartmentMainDivisionSentinel = -128;
    private const sbyte ApartmentSubdivisionSentinel = -127;

    private static long BuildHouseId(uint worldId, uint territoryTypeId, int wardIndex, int plotIndex, int room)
    {
        byte unit;
        if (plotIndex is ApartmentMainDivisionSentinel or ApartmentSubdivisionSentinel)
        {
            var division = plotIndex == ApartmentMainDivisionSentinel ? 0 : 1;
            unit = (byte)(0x80 | (division & 0x7F));
        }
        else
        {
            unit = (byte)(plotIndex & 0x7F);
        }

        var id = (ulong)unit;
        var data2 = (ushort)((wardIndex & 0x3F) | ((room & 0x3FF) << 6));
        id |= (ulong)data2 << 16;
        id |= (ulong)(ushort)territoryTypeId << 32;
        id |= (ulong)(ushort)worldId << 48;
        return (long)id;
    }

    private static string LabelFor(HouseId houseId, GameData gameData)
    {
        var district = gameData.TerritoryName(houseId.TerritoryTypeId);
        var ward = houseId.WardIndex + 1;
        var room = houseId.RoomNumber;
        if (houseId.IsApartment)
        {
            return Loc.T(L.VenueSync.ApartmentLabel, district, ward, room);
        }

        var plot = houseId.PlotIndex + 1;
        return room > 0
            ? Loc.T(L.VenueSync.HouseLabelWithRoom, district, ward, plot, room)
            : Loc.T(L.VenueSync.HouseLabel, district, ward, plot);
    }
}
