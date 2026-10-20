using Lumina.Excel.Sheets;

namespace Aetherphone.Core.Photos;

internal static class PhotoPlaces
{
    private static readonly Dictionary<uint, string> Names = new();

    public static uint TerritoryOf(Dictionary<string, uint> places, string path) =>
        places.TryGetValue(Path.GetFileName(path), out var territory) ? territory : 0u;

    public static bool Stamp(Dictionary<string, uint> places, string path, uint territory)
    {
        if (territory == 0 || path.Length == 0)
        {
            return false;
        }

        places[Path.GetFileName(path)] = territory;
        return true;
    }

    public static bool CopyStamp(Dictionary<string, uint> places, string source, string target) =>
        Stamp(places, target, TerritoryOf(places, source));

    public static bool Prune<TValue>(Dictionary<string, TValue> places, HashSet<string> keptNames)
    {
        if (places.Count == 0)
        {
            return false;
        }

        List<string>? stale = null;
        foreach (var name in places.Keys)
        {
            if (!keptNames.Contains(name))
            {
                (stale ??= new List<string>()).Add(name);
            }
        }

        if (stale is null)
        {
            return false;
        }

        for (var index = 0; index < stale.Count; index++)
        {
            places.Remove(stale[index]);
        }

        return true;
    }

    public static string Name(uint territory)
    {
        if (territory == 0)
        {
            return string.Empty;
        }

        if (Names.TryGetValue(territory, out var cached))
        {
            return cached;
        }

        var name = string.Empty;
        try
        {
            if (Plugin.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territory, out var row))
            {
                name = row.PlaceName.Value.Name.ExtractText();
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"[Photos] could not resolve the place of territory {territory}");
        }

        Names[territory] = name;
        return name;
    }
}
