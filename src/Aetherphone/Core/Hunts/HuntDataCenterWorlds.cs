using Lumina.Excel.Sheets;

namespace Aetherphone.Core.Hunts;

internal static class HuntDataCenterWorlds
{
    private static Dictionary<string, string[]>? worldsByDataCenter;

    public static string[] WorldsFor(string dataCenter)
    {
        var lookup = worldsByDataCenter ??= BuildWorldsByDataCenter();
        return lookup.TryGetValue(dataCenter, out var worlds) ? worlds : Array.Empty<string>();
    }

    private static Dictionary<string, uint>? rowIdBySlug;

    public static uint WorldRowId(string worldSlug)
    {
        var lookup = rowIdBySlug ??= BuildRowIdLookup();
        return lookup.TryGetValue(worldSlug, out var rowId) ? rowId : 0u;
    }

    private static Dictionary<string, uint> BuildRowIdLookup()
    {
        var lookup = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        foreach (var world in Plugin.DataManager.GetExcelSheet<World>())
        {
            if (!world.IsPublic)
            {
                continue;
            }

            var name = world.Name.ExtractText();
            if (name.Length > 0)
            {
                lookup[name] = world.RowId;
            }
        }

        return lookup;
    }

    private static Dictionary<string, string[]> BuildWorldsByDataCenter()
    {
        var grouped = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var world in Plugin.DataManager.GetExcelSheet<World>())
        {
            if (world.DataCenter.RowId == 0 || !world.IsPublic)
            {
                continue;
            }

            var worldName = world.Name.ExtractText();
            var dataCenterName = world.DataCenter.Value.Name.ExtractText();
            if (worldName.Length == 0 || dataCenterName.Length == 0)
            {
                continue;
            }

            if (!grouped.TryGetValue(dataCenterName, out var worlds))
            {
                worlds = new List<string>();
                grouped[dataCenterName] = worlds;
            }

            worlds.Add(worldName.ToLowerInvariant());
        }

        var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in grouped)
        {
            entry.Value.Sort(StringComparer.Ordinal);
            result[entry.Key] = entry.Value.ToArray();
        }

        return result;
    }
}
