using System.Globalization;
using Aetherphone.Core.Localization;
using Lumina.Excel.Sheets;

namespace Aetherphone.Core.Geography;

internal sealed class GeoDataCenterInfo(string name, int id, int regionId, string[] worlds)
{
    public string Name { get; } = name;
    public int Id { get; } = id;
    public int RegionId { get; } = regionId;
    public string[] Worlds { get; } = worlds;
    public HashSet<string> Set { get; } = new(StringComparer.OrdinalIgnoreCase) { name };
}

internal sealed class GeoRegionInfo(int id, LocString label, GeoDataCenterInfo[] dataCenters)
{
    public int Id { get; } = id;
    public string Key { get; } = id.ToString(CultureInfo.InvariantCulture);
    public LocString Label { get; } = label;
    public GeoDataCenterInfo[] DataCenters { get; } = dataCenters;
    public HashSet<string> Set { get; } = BuildSet(dataCenters);

    private static HashSet<string> BuildSet(GeoDataCenterInfo[] dataCenters)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < dataCenters.Length; index++)
        {
            set.Add(dataCenters[index].Name);
        }

        return set;
    }
}

internal static class WorldGeography
{
    private const int JapanId = 1;
    private const int NorthAmericaId = 2;
    private const int EuropeId = 3;
    private const int OceaniaId = 4;

    private static readonly int[] RegionOrder = { NorthAmericaId, EuropeId, OceaniaId, JapanId };

    private static GeoRegionInfo[]? regions;
    private static Dictionary<string, GeoDataCenterInfo>? dataCenterByName;
    private static Dictionary<string, GeoDataCenterInfo>? dataCenterByWorld;

    public static GeoRegionInfo[] Regions => regions ??= Build();

    public static GeoRegionInfo? RegionById(int id)
    {
        var all = Regions;
        for (var index = 0; index < all.Length; index++)
        {
            if (all[index].Id == id)
            {
                return all[index];
            }
        }

        return null;
    }

    public static GeoDataCenterInfo? DataCenter(string name)
    {
        _ = Regions;
        return name.Length > 0 && dataCenterByName!.TryGetValue(name, out var info) ? info : null;
    }

    public static GeoDataCenterInfo? DataCenterOfWorld(string world)
    {
        _ = Regions;
        return world.Length > 0 && dataCenterByWorld!.TryGetValue(world, out var info) ? info : null;
    }

    public static GeoRegionInfo? RegionOfDataCenter(string name) =>
        DataCenter(name) is { } info ? RegionById(info.RegionId) : null;

    private static LocString LabelFor(int regionId) =>
        regionId switch
        {
            JapanId => L.Venues.RegionJapan,
            EuropeId => L.Venues.RegionEurope,
            OceaniaId => L.Venues.RegionOceania,
            _ => L.Venues.RegionNorthAmerica,
        };

    private static GeoRegionInfo[] Build()
    {
        var worldsByDataCenter = new Dictionary<uint, List<string>>();
        foreach (var world in Plugin.DataManager.GetExcelSheet<World>())
        {
            if (!world.IsPublic || world.DataCenter.RowId == 0)
            {
                continue;
            }

            var name = world.Name.ExtractText();
            if (name.Length == 0)
            {
                continue;
            }

            if (!worldsByDataCenter.TryGetValue(world.DataCenter.RowId, out var list))
            {
                list = new List<string>();
                worldsByDataCenter[world.DataCenter.RowId] = list;
            }

            list.Add(name);
        }

        var byRegion = new Dictionary<int, List<GeoDataCenterInfo>>();
        var byName = new Dictionary<string, GeoDataCenterInfo>(StringComparer.OrdinalIgnoreCase);
        var byWorld = new Dictionary<string, GeoDataCenterInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in Plugin.DataManager.GetExcelSheet<WorldDCGroupType>())
        {
            var regionId = (int)group.Region.RowId;
            if (group.RowId == 0 || Array.IndexOf(RegionOrder, regionId) < 0 ||
                !worldsByDataCenter.TryGetValue(group.RowId, out var worlds))
            {
                continue;
            }

            var name = group.Name.ExtractText();
            if (name.Length == 0)
            {
                continue;
            }

            worlds.Sort(StringComparer.OrdinalIgnoreCase);
            var info = new GeoDataCenterInfo(name, (int)group.RowId, regionId, worlds.ToArray());
            byName[name] = info;
            for (var index = 0; index < info.Worlds.Length; index++)
            {
                byWorld[info.Worlds[index]] = info;
            }

            if (!byRegion.TryGetValue(regionId, out var list))
            {
                list = new List<GeoDataCenterInfo>();
                byRegion[regionId] = list;
            }

            list.Add(info);
        }

        var result = new List<GeoRegionInfo>(RegionOrder.Length);
        for (var index = 0; index < RegionOrder.Length; index++)
        {
            var regionId = RegionOrder[index];
            if (byRegion.TryGetValue(regionId, out var list))
            {
                result.Add(new GeoRegionInfo(regionId, LabelFor(regionId), list.ToArray()));
            }
        }

        dataCenterByName = byName;
        dataCenterByWorld = byWorld;
        return result.ToArray();
    }
}
