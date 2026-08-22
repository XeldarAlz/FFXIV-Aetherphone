using Aetherphone.Core.Localization;
using Aetherphone.Core.Venues;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace Aetherphone.Core.Maps;

internal enum TravelKind
{
    None,
    AlreadyThere,
    World,
    Aetheryte,
    Aethernet,
    HousingPlot,
}

internal readonly record struct TravelDestination(
    TravelKind Kind,
    string Name,
    string GatewayName,
    uint AetheryteRowId,
    uint MasterRowId,
    uint WorldId,
    uint DistrictId = 0,
    int Ward = 0,
    int Plot = 0);

internal readonly record struct HousingHome(
    uint DistrictId,
    uint CityAetheryteRowId,
    string CityName,
    string DistrictName);

internal static class TravelPlanner
{
    private const byte HousingWardUse = 13;
    private const byte HousingInteriorUse = 14;

    private static Dictionary<uint, TravelDestination>? destinationByTerritory;
    private static Dictionary<uint, HousingHome>? housingByTerritory;

    public static TravelDestination Resolve(in SharedLocation location)
    {
        var currentWorldId = LocationShare.CurrentWorldId();
        if (currentWorldId == 0)
        {
            return default;
        }

        var currentTerritoryId = Plugin.ClientState.TerritoryType;
        var worldId = location.WorldId != 0 ? location.WorldId : currentWorldId;
        var housing = housingByTerritory ??= BuildHousingLookup();
        if (!housing.TryGetValue(location.TerritoryId, out var home))
        {
            return Resolve(location.TerritoryId, worldId, currentWorldId, currentTerritoryId);
        }

        if (location.Ward <= 0 || location.Plot <= 0)
        {
            return Resolve(home.DistrictId, worldId, currentWorldId, currentTerritoryId);
        }

        if (currentWorldId == worldId && housing.TryGetValue(currentTerritoryId, out var currentHome)
            && currentHome.DistrictId == home.DistrictId && StandingOnPlot(in location))
        {
            return new TravelDestination(TravelKind.AlreadyThere, string.Empty, string.Empty, 0, 0, 0);
        }

        return new TravelDestination(TravelKind.HousingPlot, home.DistrictName, home.CityName,
            home.CityAetheryteRowId, 0, worldId, home.DistrictId, location.Ward, location.Plot);
    }

    public static TravelDestination Resolve(uint territoryId, uint worldId, uint currentWorldId,
        uint currentTerritoryId) =>
        ResolveCore(territoryId, worldId, currentWorldId, currentTerritoryId, null);

    public static TravelDestination ResolveNearestAetheryteTo(uint territoryId, uint worldId, uint currentWorldId,
        uint currentTerritoryId, (float X, float Y)? targetCoordinate) =>
        ResolveCore(territoryId, worldId, currentWorldId, currentTerritoryId, targetCoordinate);

    private static TravelDestination ResolveCore(uint territoryId, uint worldId, uint currentWorldId,
        uint currentTerritoryId, (float X, float Y)? targetCoordinate)
    {
        if (currentWorldId == 0)
        {
            return default;
        }

        if (worldId != 0 && currentWorldId != worldId)
        {
            var worldName = LocationShare.WorldName(worldId);
            return worldName.Length > 0
                ? new TravelDestination(TravelKind.World, worldName, worldName, 0, 0, worldId)
                : default;
        }

        if (territoryId == 0)
        {
            return default;
        }

        if (currentTerritoryId == territoryId)
        {
            return new TravelDestination(TravelKind.AlreadyThere, string.Empty, string.Empty, 0, 0, 0);
        }

        if (targetCoordinate is { } coordinate && ResolveNearestAetheryte(territoryId, coordinate) is { } nearest)
        {
            return nearest;
        }

        var lookup = destinationByTerritory ??= BuildDestinationLookup();
        return lookup.TryGetValue(territoryId, out var destination) ? destination : default;
    }

    private readonly record struct AetheryteCandidate(uint RowId, string Name, Vector2 MapCoordinate);

    private static Dictionary<uint, List<AetheryteCandidate>>? aetherytesByTerritory;

    private static TravelDestination? ResolveNearestAetheryte(uint territoryId, (float X, float Y) targetCoordinate)
    {
        var candidates = (aetherytesByTerritory ??= BuildAetheryteCandidates()).GetValueOrDefault(territoryId);
        if (candidates is not { Count: > 0 })
        {
            return null;
        }

        var target = new Vector2(targetCoordinate.X, targetCoordinate.Y);
        var nearest = candidates[0];
        var nearestDistanceSquared = Vector2.DistanceSquared(nearest.MapCoordinate, target);
        for (var index = 1; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            var distanceSquared = Vector2.DistanceSquared(candidate.MapCoordinate, target);
            if (distanceSquared >= nearestDistanceSquared)
            {
                continue;
            }

            nearest = candidate;
            nearestDistanceSquared = distanceSquared;
        }

        return new TravelDestination(TravelKind.Aetheryte, nearest.Name, nearest.Name, nearest.RowId, 0, 0);
    }

    private const byte AetheryteMarkerDataType = 3;

    private static Dictionary<uint, List<AetheryteCandidate>> BuildAetheryteCandidates()
    {
        var maps = Plugin.DataManager.GetExcelSheet<Map>();
        var markers = Plugin.DataManager.GetSubrowExcelSheet<MapMarker>();
        var aetherytes = Plugin.DataManager.GetExcelSheet<Aetheryte>();
        var placeNames = Plugin.DataManager.GetExcelSheet<PlaceName>();
        var byTerritory = new Dictionary<uint, List<AetheryteCandidate>>();

        foreach (var map in maps)
        {
            var territoryId = map.TerritoryType.RowId;
            if (territoryId == 0 || !markers.TryGetRow(map.MapMarkerRange, out var markerGroup))
            {
                continue;
            }

            foreach (var marker in markerGroup)
            {
                if (marker.DataType != AetheryteMarkerDataType
                    || !aetherytes.TryGetRow(marker.DataKey.RowId, out var aetheryte)
                    || !aetheryte.IsAetheryte || aetheryte.Invisible
                    || aetheryte.PlaceName.RowId == 0
                    || !placeNames.TryGetRow(aetheryte.PlaceName.RowId, out var placeName))
                {
                    continue;
                }

                var name = placeName.Name.ExtractText();
                if (name.Length == 0)
                {
                    continue;
                }

                var (mapX, mapY) = MapPixelMath.ToGameCoordinate(marker.X, marker.Y, map.SizeFactor);
                if (!byTerritory.TryGetValue(territoryId, out var candidates))
                {
                    candidates = new List<AetheryteCandidate>();
                    byTerritory[territoryId] = candidates;
                }

                candidates.Add(new AetheryteCandidate(aetheryte.RowId, name, new Vector2(mapX, mapY)));
            }
        }

        return byTerritory;
    }

    public static bool CanGo(in TravelDestination destination) =>
        destination.Kind is TravelKind.World or TravelKind.Aetheryte or TravelKind.Aethernet
            or TravelKind.HousingPlot;

    public static LifestreamOutcome Go(in TravelDestination destination) =>
        destination.Kind switch
        {
            TravelKind.Aetheryte => LifestreamBridge.TeleportToAetheryte(destination.AetheryteRowId),
            TravelKind.Aethernet => LifestreamBridge.TravelToAethernet(destination.MasterRowId, destination.Name),
            TravelKind.World => LifestreamBridge.TravelToWorld(destination.WorldId, destination.Name),
            TravelKind.HousingPlot => LifestreamBridge.TravelToHousingPlot(destination.WorldId,
                destination.AetheryteRowId, destination.Ward, destination.Plot),
            _ => LifestreamOutcome.CannotTeleportNow,
        };

    public static string Command(in TravelDestination destination) =>
        destination.Kind switch
        {
            TravelKind.World or TravelKind.Aethernet => LifestreamBridge.TravelCommand(destination.Name),
            TravelKind.Aetheryte => LifestreamBridge.AetheryteCommand(destination.Name),
            TravelKind.HousingPlot => LifestreamBridge.HousingCommand(LocationShare.WorldName(destination.WorldId),
                destination.Name, destination.Ward, destination.Plot),
            _ => string.Empty,
        };

    public static string Label(in TravelDestination destination) =>
        destination.Kind switch
        {
            TravelKind.HousingPlot => Loc.T(L.Travel.TeleportToPlot, destination.Name, destination.Ward,
                destination.Plot),
            TravelKind.World => Loc.T(L.Travel.TravelTo, destination.Name),
            _ => Loc.T(L.Travel.TeleportTo, destination.Name),
        };

    public static uint CityAetheryteOf(uint districtId) =>
        (housingByTerritory ??= BuildHousingLookup()).TryGetValue(districtId, out var home)
            ? home.CityAetheryteRowId
            : 0u;

    public static string Notice(LifestreamOutcome outcome, in TravelDestination destination) =>
        outcome switch
        {
            LifestreamOutcome.Busy => Loc.T(L.Travel.Busy),
            LifestreamOutcome.NotAttuned => Loc.T(L.Travel.NotAttuned, destination.GatewayName),
            LifestreamOutcome.WorldUnreachable => Loc.T(L.Travel.NoWorld, destination.Name),
            _ => Loc.T(L.Travel.Blocked),
        };

    private static bool StandingOnPlot(in SharedLocation location)
    {
        var current = LocationShare.CurrentHousing();
        return current.Ward == location.Ward && current.Plot == location.Plot;
    }

    private static Dictionary<uint, HousingHome> BuildHousingLookup()
    {
        var territories = Plugin.DataManager.GetExcelSheet<TerritoryType>();
        var districtByCity = new Dictionary<uint, (uint DistrictId, string DistrictName)>();
        foreach (var territory in territories)
        {
            var cityRowId = territory.Aetheryte.RowId;
            if (territory.TerritoryIntendedUse.RowId != HousingWardUse || cityRowId == 0
                || districtByCity.ContainsKey(cityRowId))
            {
                continue;
            }

            districtByCity[cityRowId] = (territory.RowId, territory.PlaceName.Value.Name.ExtractText());
        }

        var byTerritory = new Dictionary<uint, HousingHome>();
        foreach (var territory in territories)
        {
            var use = territory.TerritoryIntendedUse.RowId;
            if (use != HousingWardUse && use != HousingInteriorUse)
            {
                continue;
            }

            if (!districtByCity.TryGetValue(territory.Aetheryte.RowId, out var district))
            {
                continue;
            }

            byTerritory[territory.RowId] = new HousingHome(district.DistrictId, territory.Aetheryte.RowId,
                CityName(territory.Aetheryte.RowId), district.DistrictName);
        }

        return byTerritory;
    }

    private static string CityName(uint cityAetheryteRowId)
    {
        if (!Plugin.DataManager.GetExcelSheet<Aetheryte>().TryGetRow(cityAetheryteRowId, out var aetheryte))
        {
            return string.Empty;
        }

        return Plugin.DataManager.GetExcelSheet<PlaceName>().TryGetRow(aetheryte.PlaceName.RowId, out var placeName)
            ? placeName.Name.ExtractText()
            : string.Empty;
    }

    private static Dictionary<uint, TravelDestination> BuildDestinationLookup()
    {
        var aetherytes = Plugin.DataManager.GetExcelSheet<Aetheryte>();
        var placeNames = Plugin.DataManager.GetExcelSheet<PlaceName>();
        var mastersByAethernetGroup = BuildMasterLookup(aetherytes, placeNames);
        var byTerritory = new Dictionary<uint, TravelDestination>();
        var chosen = new Dictionary<uint, (int Rank, byte Order)>();
        foreach (var aetheryte in aetherytes)
        {
            if (aetheryte.Invisible)
            {
                continue;
            }

            var territoryId = aetheryte.Territory.RowId;
            if (territoryId == 0)
            {
                continue;
            }

            var rank = aetheryte.IsAetheryte ? 0 : 1;
            var nameRowId = rank == 0 ? aetheryte.PlaceName.RowId : aetheryte.AethernetName.RowId;
            if (nameRowId == 0 || !placeNames.TryGetRow(nameRowId, out var placeName))
            {
                continue;
            }

            var name = placeName.Name.ExtractText();
            if (name.Length == 0)
            {
                continue;
            }

            if (chosen.TryGetValue(territoryId, out var current)
                && (current.Rank < rank || (current.Rank == rank && current.Order <= aetheryte.Order)))
            {
                continue;
            }

            var master = rank == 0
                ? (aetheryte.RowId, name)
                : mastersByAethernetGroup.GetValueOrDefault(aetheryte.AethernetGroup, (0u, name));
            chosen[territoryId] = (rank, aetheryte.Order);
            byTerritory[territoryId] = new TravelDestination(
                rank == 0 ? TravelKind.Aetheryte : TravelKind.Aethernet, name, master.Item2,
                aetheryte.RowId, master.Item1, 0);
        }

        return byTerritory;
    }

    private static Dictionary<byte, (uint RowId, string Name)> BuildMasterLookup(ExcelSheet<Aetheryte> aetherytes,
        ExcelSheet<PlaceName> placeNames)
    {
        var masters = new Dictionary<byte, (uint RowId, string Name)>();
        foreach (var aetheryte in aetherytes)
        {
            if (!aetheryte.IsAetheryte || aetheryte.AethernetGroup == 0 || aetheryte.Invisible
                || masters.ContainsKey(aetheryte.AethernetGroup))
            {
                continue;
            }

            var name = placeNames.TryGetRow(aetheryte.PlaceName.RowId, out var placeName)
                ? placeName.Name.ExtractText()
                : string.Empty;
            masters[aetheryte.AethernetGroup] = (aetheryte.RowId, name);
        }

        return masters;
    }
}
