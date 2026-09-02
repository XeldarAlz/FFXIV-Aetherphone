using Aetherphone.Core.Game;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace Aetherphone.Core.Maps;

internal sealed partial class MapData
{
    private const string UnknownRegionName = "Eorzea";
    private const string SummarySeparator = ", ";
    private const byte AetheryteMarkerDataType = 3;
    private static readonly IReadOnlyList<MapAetheryte> NoAetherytes = Array.Empty<MapAetheryte>();
    private readonly IDataManager data;
    private readonly IClientState clientState;
    private readonly List<MapRegion> regions = new();
    private readonly List<MapExpansion> expansions = new();
    private readonly Dictionary<uint, MapAetheryte> aetherytesById = new();
    private readonly Dictionary<uint, List<MapAetheryte>> aetherytesByMap = new();
    private bool built;

    private readonly record struct AetheryteSource(uint RowId, string Name, byte Order, uint MapId);

    private readonly record struct MarkerSpot(uint MapId, float U, float V, float GameX, float GameY);

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

    public IReadOnlyList<MapAetheryte> AetherytesOnMap(uint mapRowId)
    {
        EnsureBuilt();
        return mapRowId != 0 && aetherytesByMap.TryGetValue(mapRowId, out var found) ? found : NoAetherytes;
    }

    public bool TryMapMetrics(uint mapRowId, out ushort sizeFactor)
    {
        sizeFactor = 100;
        if (!GameSheets.Available)
        {
            return false;
        }

        if (mapRowId == 0 || !data.GetExcelSheet<Map>().TryGetRow(mapRowId, out var map))
        {
            return false;
        }

        sizeFactor = map.SizeFactor == 0 ? (ushort)100 : map.SizeFactor;
        return true;
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
        if (!GameSheets.Available)
        {
            return;
        }

        var sourcesByTerritory = CollectAetherytes();
        var spots = CollectMarkerSpots();
        var territories = data.GetExcelSheet<TerritoryType>();
        var aetherytesByRegion = new Dictionary<string, List<MapAetheryte>>(StringComparer.Ordinal);
        var regionOrder = new Dictionary<string, byte>(StringComparer.Ordinal);
        foreach (var territory in territories)
        {
            if (!sourcesByTerritory.TryGetValue(territory.RowId, out var sources) || sources.Count == 0)
            {
                continue;
            }

            var regionName = PlaceName(territory.PlaceNameRegion.RowId);
            if (regionName.Length == 0)
            {
                regionName = UnknownRegionName;
            }

            var zoneName = PlaceName(territory.PlaceName.RowId);
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

            for (var index = 0; index < sources.Count; index++)
            {
                var source = sources[index];
                if (aetherytesById.ContainsKey(source.RowId))
                {
                    continue;
                }

                var aetheryte = CreateAetheryte(source, territory.RowId, zoneName, regionName, spots);
                aetherytesById[source.RowId] = aetheryte;
                bucket.Add(aetheryte);
                IndexByMap(aetheryte);
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

    private static MapAetheryte CreateAetheryte(in AetheryteSource source, uint territoryId, string zoneName,
        string regionName, Dictionary<uint, MarkerSpot> spots)
    {
        var hasSpot = spots.TryGetValue(source.RowId, out var spot);
        var zone = zoneName.Length > 0 ? zoneName : regionName;
        var subtitle = string.Equals(zone, source.Name, StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(zone, regionName, StringComparison.Ordinal)
            ? regionName
            : string.Concat(zone, SummarySeparator, regionName);
        return new MapAetheryte
        {
            RowId = source.RowId,
            Name = source.Name,
            Order = source.Order,
            TerritoryId = territoryId,
            MapId = hasSpot ? spot.MapId : source.MapId,
            ZoneName = zone,
            RegionName = regionName,
            Subtitle = subtitle,
            U = hasSpot ? spot.U : -1f,
            V = hasSpot ? spot.V : -1f,
            GameX = hasSpot ? spot.GameX : 0f,
            GameY = hasSpot ? spot.GameY : 0f,
        };
    }

    private void IndexByMap(MapAetheryte aetheryte)
    {
        if (aetheryte.MapId == 0 || !aetheryte.HasPosition)
        {
            return;
        }

        if (!aetherytesByMap.TryGetValue(aetheryte.MapId, out var bucket))
        {
            bucket = new List<MapAetheryte>();
            aetherytesByMap[aetheryte.MapId] = bucket;
        }

        bucket.Add(aetheryte);
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
            var expansionRegions = regionsByExpansion[order];
            var names = new string[expansionRegions.Count];
            for (var regionIndex = 0; regionIndex < expansionRegions.Count; regionIndex++)
            {
                names[regionIndex] = expansionRegions[regionIndex].Name;
            }

            expansions.Add(new MapExpansion
            {
                Name = ExpansionName(order),
                Order = order,
                Regions = expansionRegions,
                Summary = string.Join(SummarySeparator, names),
            });
        }
    }

    private Dictionary<uint, List<AetheryteSource>> CollectAetherytes()
    {
        if (!GameSheets.Available)
        {
            return new Dictionary<uint, List<AetheryteSource>>();
        }

        var result = new Dictionary<uint, List<AetheryteSource>>();
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
                bucket = new List<AetheryteSource>();
                result[territoryId] = bucket;
            }

            bucket.Add(new AetheryteSource(aetheryte.RowId, name, aetheryte.Order, aetheryte.Map.RowId));
        }

        return result;
    }

    private Dictionary<uint, MarkerSpot> CollectMarkerSpots()
    {
        var result = new Dictionary<uint, MarkerSpot>();
        var aetherytes = data.GetExcelSheet<Aetheryte>();
        var markers = data.GetSubrowExcelSheet<MapMarker>();
        foreach (var map in data.GetExcelSheet<Map>())
        {
            if (map.TerritoryType.RowId == 0 || !markers.TryGetRow(map.MapMarkerRange, out var markerGroup))
            {
                continue;
            }

            foreach (var marker in markerGroup)
            {
                if (marker.DataType != AetheryteMarkerDataType)
                {
                    continue;
                }

                var aetheryteId = marker.DataKey.RowId;
                var preferred = aetherytes.TryGetRow(aetheryteId, out var aetheryte) &&
                                aetheryte.Map.RowId == map.RowId;
                if (!preferred && result.ContainsKey(aetheryteId))
                {
                    continue;
                }

                var (gameX, gameY) = MapPixelMath.ToGameCoordinate(marker.X, marker.Y, map.SizeFactor);
                var (u, v) = MapPixelMath.NormalizeToFullCanvas(marker.X, marker.Y);
                result[aetheryteId] = new MarkerSpot(map.RowId, u, v, gameX, gameY);
            }
        }

        return result;
    }

    private string PlaceName(uint placeNameRowId)
    {
        if (!GameSheets.Available)
        {
            return string.Empty;
        }

        if (placeNameRowId != 0 && data.GetExcelSheet<PlaceName>().TryGetRow(placeNameRowId, out var placeName))
        {
            return placeName.Name.ExtractText();
        }

        return string.Empty;
    }

    private string ExpansionName(uint exVersionRowId)
    {
        if (!GameSheets.Available)
        {
            return string.Empty;
        }

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
