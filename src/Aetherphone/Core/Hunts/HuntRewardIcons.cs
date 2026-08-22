using Lumina.Excel.Sheets;

namespace Aetherphone.Core.Hunts;

internal static class HuntRewardIcons
{
    private static readonly Dictionary<string, uint> IconIdByItemId = new();
    private static Dictionary<string, uint>? iconIdByItemName;

    public static uint ResolveIconId(string itemId, string? itemName)
    {
        if (IconIdByItemId.TryGetValue(itemId, out var cached))
        {
            return cached;
        }

        var iconId = ResolveIconIdByName(itemName);
        IconIdByItemId[itemId] = iconId;
        return iconId;
    }

    private static uint ResolveIconIdByName(string? itemName)
    {
        if (string.IsNullOrEmpty(itemName))
        {
            return 0u;
        }

        var lookup = iconIdByItemName ??= BuildIconIdByItemName();
        return lookup.TryGetValue(itemName, out var iconId) ? iconId : 0u;
    }

    private static Dictionary<string, uint> BuildIconIdByItemName()
    {
        var lookup = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in Plugin.DataManager.GetExcelSheet<Item>())
        {
            var name = item.Name.ExtractText();
            if (name.Length > 0 && !lookup.ContainsKey(name))
            {
                lookup[name] = item.Icon;
            }
        }

        return lookup;
    }
}
