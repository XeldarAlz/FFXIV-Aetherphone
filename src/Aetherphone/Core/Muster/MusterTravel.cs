using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Venues;
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

internal readonly record struct MusterDestination(MusterTravelKind Kind, string Name);

internal static class MusterTravel
{
    private static readonly MusterDestination Nowhere = new(MusterTravelKind.None, string.Empty);
    private static Dictionary<uint, MusterDestination>? destinationByTerritory;

    public static MusterDestination Resolve(MusterDto muster, int currentWorldId, uint currentTerritoryId)
    {
        if (muster.WorldId != 0 && currentWorldId != muster.WorldId)
        {
            var worldName = LocationShare.WorldName((uint)muster.WorldId);
            return worldName.Length > 0 ? new MusterDestination(MusterTravelKind.World, worldName) : Nowhere;
        }

        if (muster.TerritoryId == 0)
        {
            return Nowhere;
        }

        if (currentTerritoryId == (uint)muster.TerritoryId)
        {
            return new MusterDestination(MusterTravelKind.AlreadyThere, string.Empty);
        }

        var lookup = destinationByTerritory ??= BuildDestinationLookup();
        return lookup.TryGetValue((uint)muster.TerritoryId, out var destination) ? destination : Nowhere;
    }

    public static bool CanGo(in MusterDestination destination) =>
        destination.Kind is MusterTravelKind.World or MusterTravelKind.Aetheryte or MusterTravelKind.Aethernet;

    public static void Go(in MusterDestination destination)
    {
        if (destination.Kind == MusterTravelKind.Aetheryte)
        {
            LifestreamBridge.TravelToAetheryte(destination.Name);
            return;
        }

        if (destination.Kind is MusterTravelKind.World or MusterTravelKind.Aethernet)
        {
            LifestreamBridge.Travel(destination.Name);
        }
    }

    public static string Command(in MusterDestination destination) =>
        destination.Kind switch
        {
            MusterTravelKind.World or MusterTravelKind.Aethernet => $"/li {destination.Name}",
            MusterTravelKind.Aetheryte => LifestreamBridge.AetheryteCommand(destination.Name),
            _ => string.Empty,
        };

    private static Dictionary<uint, MusterDestination> BuildDestinationLookup()
    {
        var byTerritory = new Dictionary<uint, MusterDestination>();
        var chosen = new Dictionary<uint, (int Rank, byte Order)>();
        var placeNames = Plugin.DataManager.GetExcelSheet<PlaceName>();
        foreach (var aetheryte in Plugin.DataManager.GetExcelSheet<Aetheryte>())
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

            chosen[territoryId] = (rank, aetheryte.Order);
            byTerritory[territoryId] = new MusterDestination(
                rank == 0 ? MusterTravelKind.Aetheryte : MusterTravelKind.Aethernet, name);
        }

        return byTerritory;
    }
}
