using System.Globalization;
using Aetherphone.Core.Localization;
using Lumina.Excel.Sheets;

namespace Aetherphone.Core.Maps;

internal sealed partial class MapData
{
    private const string LineSeparator = " · ";

    private LocationKey locationKey;
    private CultureInfo? locationCulture;
    private MapLocation location;

    private readonly record struct LocationKey(
        bool LoggedIn,
        uint TerritoryId,
        uint DistrictId,
        short Ward,
        short Plot,
        short Room,
        uint WorldId);

    public MapLocation CurrentLocation()
    {
        var key = ReadLocationKey();
        var culture = Loc.Culture;
        if (locationCulture is not null && ReferenceEquals(culture, locationCulture) && key == locationKey)
        {
            return location;
        }

        locationKey = key;
        locationCulture = culture;
        location = ResolveLocation(in key);
        return location;
    }

    private LocationKey ReadLocationKey()
    {
        if (!clientState.IsLoggedIn)
        {
            return default;
        }

        var (ward, plot, room) = LocationShare.CurrentHousing();
        return new LocationKey(true, clientState.TerritoryType, LocationShare.CurrentHouseDistrict(), ward, plot, room,
            LocationShare.CurrentWorldId());
    }

    private MapLocation ResolveLocation(in LocationKey key)
    {
        if (!key.LoggedIn)
        {
            return new MapLocation(Loc.T(L.Maps.Somewhere), Loc.T(L.Maps.OfflineHint), MapLocationKind.Offline, 0);
        }

        var world = LocationShare.WorldName(key.WorldId);
        if (key.DistrictId != 0 && key.Ward > 0 && TryTerritory(key.DistrictId, out var district))
        {
            var districtName = ZoneName(district);
            if (districtName.Length > 0)
            {
                return new MapLocation(Loc.T(L.Maps.WardLine, districtName, key.Ward),
                    Join(HousingDetail(key.Plot, key.Room), world), MapLocationKind.House, key.DistrictId);
            }
        }

        if (key.TerritoryId == 0 || !TryTerritory(key.TerritoryId, out var territory))
        {
            return Unknown(world, key.TerritoryId);
        }

        var zone = ZoneName(territory);
        var duty = DutyName(territory);
        if (duty.Length > 0)
        {
            var context = zone.Length > 0 && !string.Equals(zone, duty, StringComparison.OrdinalIgnoreCase)
                ? zone
                : RegionName(territory, duty);
            return new MapLocation(duty, Join(context, world), MapLocationKind.Duty, key.TerritoryId);
        }

        if (zone.Length == 0)
        {
            return Unknown(world, key.TerritoryId);
        }

        if (key.Ward > 0)
        {
            return new MapLocation(Loc.T(L.Maps.WardLine, zone, key.Ward),
                Join(HousingDetail(key.Plot, key.Room), world), MapLocationKind.Ward, key.TerritoryId);
        }

        return new MapLocation(zone, Join(RegionName(territory, zone), world), MapLocationKind.Zone, key.TerritoryId);
    }

    private static MapLocation Unknown(string world, uint territoryId) =>
        new(Loc.T(L.Maps.Somewhere), world.Length > 0 ? world : Loc.T(L.Maps.SomewhereHint), MapLocationKind.Unknown,
            territoryId);

    private static string HousingDetail(short plot, short room)
    {
        if (plot > 0 && room > 0)
        {
            return Join(Loc.T(L.Maps.PlotLine, plot), Loc.T(L.Maps.RoomLine, room));
        }

        if (plot > 0)
        {
            return Loc.T(L.Maps.PlotLine, plot);
        }

        return room > 0 ? Loc.T(L.Maps.RoomLine, room) : string.Empty;
    }

    private bool TryTerritory(uint territoryId, out TerritoryType territory) =>
        data.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out territory);

    private string ZoneName(in TerritoryType territory)
    {
        var name = PlaceName(territory.PlaceName.RowId);
        if (name.Length > 0)
        {
            return name;
        }

        if (territory.Map.RowId != 0 && data.GetExcelSheet<Map>().TryGetRow(territory.Map.RowId, out var map))
        {
            name = PlaceName(map.PlaceName.RowId);
            if (name.Length > 0)
            {
                return name;
            }

            name = PlaceName(map.PlaceNameSub.RowId);
            if (name.Length > 0)
            {
                return name;
            }
        }

        return PlaceName(territory.PlaceNameZone.RowId);
    }

    private string RegionName(in TerritoryType territory, string zone)
    {
        var region = PlaceName(territory.PlaceNameRegion.RowId);
        if (region.Length == 0)
        {
            region = PlaceName(territory.PlaceNameZone.RowId);
        }

        return string.Equals(region, zone, StringComparison.OrdinalIgnoreCase) ? string.Empty : region;
    }

    private string DutyName(in TerritoryType territory)
    {
        var conditionId = territory.ContentFinderCondition.RowId;
        if (conditionId == 0 || !data.GetExcelSheet<ContentFinderCondition>().TryGetRow(conditionId, out var duty))
        {
            return string.Empty;
        }

        var name = duty.Name.ExtractText();
        if (name.Length == 0 || char.IsUpper(name[0]))
        {
            return name;
        }

        return string.Concat(char.ToUpper(name[0], Loc.Culture).ToString(), name.AsSpan(1));
    }

    private static string Join(string first, string second)
    {
        if (first.Length == 0)
        {
            return second;
        }

        return second.Length == 0 ? first : string.Concat(first, LineSeparator, second);
    }
}
