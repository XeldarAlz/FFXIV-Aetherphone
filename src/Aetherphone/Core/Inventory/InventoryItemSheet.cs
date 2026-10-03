using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace Aetherphone.Core.Inventory;

internal readonly struct InventoryItemInfo
{
    public readonly string Name;
    public readonly string Lower;
    public readonly uint IconId;
    public readonly int StackSize;
    public readonly uint VendorPrice;
    public readonly bool Marketable;

    public InventoryItemInfo(string name, uint iconId, int stackSize, uint vendorPrice, bool marketable)
    {
        Name = name;
        Lower = name.ToLowerInvariant();
        IconId = iconId;
        StackSize = stackSize;
        VendorPrice = vendorPrice;
        Marketable = marketable;
    }
}

internal interface IInventoryItemSource
{
    bool TryGet(uint itemId, out InventoryItemInfo info);
}

internal sealed class InventoryItemSheet : IInventoryItemSource
{
    private readonly IDataManager data;
    private readonly Dictionary<uint, InventoryItemInfo> cache = new();
    private readonly HashSet<uint> missing = new();

    public InventoryItemSheet(IDataManager data)
    {
        this.data = data;
    }

    public bool TryGet(uint itemId, out InventoryItemInfo info)
    {
        if (cache.TryGetValue(itemId, out info))
        {
            return true;
        }

        if (missing.Contains(itemId))
        {
            return false;
        }

        if (itemId == 0 || !data.GetExcelSheet<Item>().TryGetRow(itemId, out var item))
        {
            missing.Add(itemId);
            return false;
        }

        var name = item.Name.ExtractText();
        if (name.Length == 0)
        {
            missing.Add(itemId);
            return false;
        }

        var marketable = item.ItemSearchCategory.RowId != 0 && !item.IsUntradable;
        info = new InventoryItemInfo(name, item.Icon, (int)Math.Max(1u, item.StackSize), item.PriceLow, marketable);
        cache[itemId] = info;
        return true;
    }
}
