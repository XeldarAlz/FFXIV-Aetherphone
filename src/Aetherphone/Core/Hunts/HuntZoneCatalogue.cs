using Aetherphone.Core.Maps;
using Dalamud.Game;
using Lumina.Excel.Sheets;

namespace Aetherphone.Core.Hunts;

internal sealed class HuntZoneCatalog
{
    private readonly HuntJsonCatalogLoader<Dictionary<string, HuntZoneDefinition>> loader;
    private Dictionary<string, HuntZoneDefinition> byId = new();
    private Dictionary<int, (string ZoneId, HuntPoiEntry Poi)> poisById = new();
    private Dictionary<string, uint>? territoryIdByPlaceName;

    public HuntZoneCatalog(FileInfo source)
    {
        loader = new HuntJsonCatalogLoader<Dictionary<string, HuntZoneDefinition>>(source,
            HuntZoneJsonContext.Default.DictionaryStringHuntZoneDefinition, "zone catalog", OnLoaded);
        loader.Preload();
    }

    public HuntZoneDefinition? FindZone(string zoneId)
    {
        loader.EnsureLoaded();
        return byId.GetValueOrDefault(zoneId);
    }

    public (string ZoneId, HuntPoiEntry Poi)? FindPoi(int poiId)
    {
        loader.EnsureLoaded();
        return poisById.TryGetValue(poiId, out var entry) ? entry : null;
    }

    public (float X, float Y)? ResolveCoordinate(int poiId)
    {
        var found = FindPoi(poiId);
        if (found is not { } resolved)
        {
            return null;
        }

        if (!byId.TryGetValue(resolved.ZoneId, out var zone))
        {
            return null;
        }

        return ToGameCoordinate(resolved.Poi, zone.Map);
    }

    public uint ResolveTerritoryId(string zoneId)
    {
        var zone = FindZone(zoneId);
        var name = zone?.Name;
        if (string.IsNullOrEmpty(name))
        {
            return 0u;
        }

        var lookup = territoryIdByPlaceName ??= BuildTerritoryIdLookup();
        return lookup.TryGetValue(name, out var territoryId) ? territoryId : 0u;
    }

    private void OnLoaded(Dictionary<string, HuntZoneDefinition> parsed)
    {
        byId = parsed;
        var index = new Dictionary<int, (string, HuntPoiEntry)>();
        foreach (var zone in parsed.Values)
        {
            foreach (var poi in zone.Pois)
            {
                index[poi.Id] = (zone.Id, poi);
            }
        }

        poisById = index;
    }

    private static Dictionary<string, uint> BuildTerritoryIdLookup()
    {
        var lookup = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        foreach (var territory in Plugin.DataManager.GetExcelSheet<TerritoryType>(ClientLanguage.English))
        {
            if (territory.PlaceName.RowId == 0)
            {
                continue;
            }

            var name = territory.PlaceName.Value.Name.ExtractText();
            if (name.Length > 0 && !lookup.ContainsKey(name))
            {
                lookup[name] = territory.RowId;
            }
        }

        return lookup;
    }

    private static (float X, float Y) ToGameCoordinate(HuntPoiEntry poi, HuntZoneMap map)
    {
        var (rawX, rawY) = poi.ParsedLocation();
        return MapPixelMath.ToGameCoordinate(rawX, rawY, map.SizeFactor);
    }
}
