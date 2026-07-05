using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace Aetherphone.Core.Maps;

internal sealed class MapData
{
    private const string UnknownRegionName = "Eorzea";
    private readonly IDataManager data;
    private readonly IClientState clientState;
    private readonly List<MapRegion> regions = new();
    private readonly List<MapExpansion> expansions = new();
    private readonly Dictionary<uint, MapAetheryte> aetherytesById = new();
    private bool built;

    public MapData(IDataManager data, IClientState clientState)
    {
        this.data = data;
        this.clientState = clientState;
    }

    public IReadOnlyList<MapRegion> Regions
    {
        get
        {
            EnsureBuilt();
            return regions;
        }
    }

    public IReadOnlyList<MapExpansion> Expansions
    {
        get
        {
            EnsureBuilt();
            return expansions;
        }
    }

    public MapLocation CurrentLocation()
    {
        var territoryId = clientState.TerritoryType;
        if (territoryId == 0 || !data.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territory))
        {
            return new MapLocation(string.Empty, string.Empty);
        }

        var zone = PlaceName(territory.PlaceName.RowId);
        var region = PlaceName(territory.PlaceNameRegion.RowId);
        return new MapLocation(zone, region);
    }

    public bool TryGetAetheryte(uint rowId, out MapAetheryte aetheryte)
    {
        EnsureBuilt();
        if (rowId != 0 && aetherytesById.TryGetValue(rowId, out var found))
        {
            aetheryte = found;
            return true;
        }

        aetheryte = null!;
        return false;
    }

    private void EnsureBuilt()
    {
        if (built)
        {
            return;
        }

        built = true;
        Build();
    }

    private void Build()
    {
        var aetherytesByTerritory = CollectAetherytes();
        var territories = data.GetExcelSheet<TerritoryType>();
        var aetherytesByRegion = new Dictionary<string, List<MapAetheryte>>(StringComparer.Ordinal);
        var regionOrder = new Dictionary<string, byte>(StringComparer.Ordinal);
        foreach (var territory in territories)
        {
            if (!aetherytesByTerritory.TryGetValue(territory.RowId, out var aetherytes) || aetherytes.Count == 0)
            {
                continue;
            }

            var regionName = PlaceName(territory.PlaceNameRegion.RowId);
            if (regionName.Length == 0)
            {
                regionName = UnknownRegionName;
            }

            var expansionOrder = (byte)territory.ExVersion.RowId;
            if (!aetherytesByRegion.TryGetValue(regionName, out var bucket))
            {
                bucket = new List<MapAetheryte>();
                aetherytesByRegion[regionName] = bucket;
                regionOrder[regionName] = expansionOrder;
            }
            else if (expansionOrder < regionOrder[regionName])
            {
                regionOrder[regionName] = expansionOrder;
            }

            for (var index = 0; index < aetherytes.Count; index++)
            {
                var aetheryte = aetherytes[index];
                if (!aetherytesById.TryAdd(aetheryte.RowId, aetheryte))
                {
                    continue;
                }

                bucket.Add(aetheryte);
            }
        }

        regions.Clear();
        foreach (var pair in aetherytesByRegion)
        {
            pair.Value.Sort(CompareAetherytes);
            regions.Add(new MapRegion { Name = pair.Key, Order = regionOrder[pair.Key], Aetherytes = pair.Value, });
        }

        regions.Sort(CompareRegions);
        BuildExpansions();
    }

    private void BuildExpansions()
    {
        expansions.Clear();
        var regionsByExpansion = new Dictionary<byte, List<MapRegion>>();
        var expansionSequence = new List<byte>();
        for (var index = 0; index < regions.Count; index++)
        {
            var region = regions[index];
            if (!regionsByExpansion.TryGetValue(region.Order, out var bucket))
            {
                bucket = new List<MapRegion>();
                regionsByExpansion[region.Order] = bucket;
                expansionSequence.Add(region.Order);
            }

            bucket.Add(region);
        }

        for (var index = 0; index < expansionSequence.Count; index++)
        {
            var order = expansionSequence[index];
            expansions.Add(new MapExpansion
            {
                Name = ExpansionName(order), Order = order, Regions = regionsByExpansion[order],
            });
        }
    }

    private Dictionary<uint, List<MapAetheryte>> CollectAetherytes()
    {
        var result = new Dictionary<uint, List<MapAetheryte>>();
        var seenNames = new Dictionary<uint, HashSet<string>>();
        foreach (var aetheryte in data.GetExcelSheet<Aetheryte>())
        {
            if (!aetheryte.IsAetheryte || aetheryte.Invisible)
            {
                continue;
            }

            var territoryId = aetheryte.Territory.RowId;
            if (territoryId == 0)
            {
                continue;
            }

            var name = PlaceName(aetheryte.PlaceName.RowId);
            if (name.Length == 0)
            {
                continue;
            }

            if (!seenNames.TryGetValue(territoryId, out var names))
            {
                names = new HashSet<string>(StringComparer.Ordinal);
                seenNames[territoryId] = names;
            }

            if (!names.Add(name))
            {
                continue;
            }

            if (!result.TryGetValue(territoryId, out var bucket))
            {
                bucket = new List<MapAetheryte>();
                result[territoryId] = bucket;
            }

            bucket.Add(new MapAetheryte { RowId = aetheryte.RowId, Name = name, Order = aetheryte.Order, });
        }

        return result;
    }

    private string PlaceName(uint placeNameRowId)
    {
        if (placeNameRowId != 0 && data.GetExcelSheet<PlaceName>().TryGetRow(placeNameRowId, out var placeName))
        {
            return placeName.Name.ExtractText();
        }

        return string.Empty;
    }

    private string ExpansionName(uint exVersionRowId)
    {
        if (data.GetExcelSheet<ExVersion>().TryGetRow(exVersionRowId, out var exVersion))
        {
            var name = exVersion.Name.ExtractText();
            if (name.Length > 0)
            {
                return name;
            }
        }

        return UnknownRegionName;
    }

    private static int CompareAetherytes(MapAetheryte left, MapAetheryte right)
    {
        var byOrder = left.Order.CompareTo(right.Order);
        if (byOrder != 0)
        {
            return byOrder;
        }

        return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareRegions(MapRegion left, MapRegion right)
    {
        var byOrder = left.Order.CompareTo(right.Order);
        if (byOrder != 0)
        {
            return byOrder;
        }

        return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
    }
}
