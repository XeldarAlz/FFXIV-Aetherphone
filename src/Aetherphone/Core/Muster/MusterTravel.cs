using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Venues;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace Aetherphone.Core.Muster;

internal enum MusterTravelKind
{
    None,
    AlreadyThere,
    World,
    Aetheryte,
    Aethernet,
}

internal readonly record struct MusterDestination(
    MusterTravelKind Kind,
    string Name,
    string GatewayName,
    uint AetheryteRowId,
    uint MasterRowId,
    uint WorldId);

internal static class MusterTravel
{
    private static Dictionary<uint, MusterDestination>? destinationByTerritory;

    public static MusterDestination Resolve(MusterDto muster, int currentWorldId, uint currentTerritoryId)
    {
        if (muster.WorldId != 0 && currentWorldId != muster.WorldId)
        {
            var worldName = LocationShare.WorldName((uint)muster.WorldId);
            return worldName.Length > 0
                ? new MusterDestination(MusterTravelKind.World, worldName, worldName, 0, 0, (uint)muster.WorldId)
                : default;
        }

        if (muster.TerritoryId == 0)
        {
            return default;
        }

        if (currentTerritoryId == (uint)muster.TerritoryId)
        {
            return new MusterDestination(MusterTravelKind.AlreadyThere, string.Empty, string.Empty, 0, 0, 0);
        }

        var lookup = destinationByTerritory ??= BuildDestinationLookup();
        return lookup.TryGetValue((uint)muster.TerritoryId, out var destination) ? destination : default;
    }

    public static bool CanGo(in MusterDestination destination) =>
        destination.Kind is MusterTravelKind.World or MusterTravelKind.Aetheryte or MusterTravelKind.Aethernet;

    public static LifestreamOutcome Go(in MusterDestination destination) =>
        destination.Kind switch
        {
            MusterTravelKind.Aetheryte => LifestreamBridge.TeleportToAetheryte(destination.AetheryteRowId),
            MusterTravelKind.Aethernet => LifestreamBridge.TravelToAethernet(destination.MasterRowId,
                destination.Name),
            MusterTravelKind.World => LifestreamBridge.TravelToWorld(destination.WorldId, destination.Name),
            _ => LifestreamOutcome.CannotTeleportNow,
        };

    public static string Command(in MusterDestination destination) =>
        destination.Kind switch
        {
            MusterTravelKind.World or MusterTravelKind.Aethernet => LifestreamBridge.TravelCommand(destination.Name),
            MusterTravelKind.Aetheryte => LifestreamBridge.AetheryteCommand(destination.Name),
            _ => string.Empty,
        };

    private static Dictionary<uint, MusterDestination> BuildDestinationLookup()
    {
        var aetherytes = Plugin.DataManager.GetExcelSheet<Aetheryte>();
        var placeNames = Plugin.DataManager.GetExcelSheet<PlaceName>();
        var mastersByAethernetGroup = BuildMasterLookup(aetherytes, placeNames);
        var byTerritory = new Dictionary<uint, MusterDestination>();
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
            byTerritory[territoryId] = new MusterDestination(
                rank == 0 ? MusterTravelKind.Aetheryte : MusterTravelKind.Aethernet, name, master.Item2,
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
