using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace Aetherphone.Core.Market;

internal readonly struct MarketItemRef
{
    public readonly uint Id;
    public readonly string Name;
    public readonly uint IconId;
    public readonly uint VendorPrice;

    public MarketItemRef(uint id, string name, uint iconId, uint vendorPrice)
    {
        Id = id;
        Name = name;
        IconId = iconId;
        VendorPrice = vendorPrice;
    }

    public bool IsValid => Id != 0;
}

internal sealed class MarketItemIndex
{
    private readonly IDataManager data;
    private readonly object sync = new();
    private uint[] ids = Array.Empty<uint>();
    private string[] names = Array.Empty<string>();
    private string[] lowerNames = Array.Empty<string>();
    private uint[] icons = Array.Empty<uint>();
    private uint[] vendorPrices = Array.Empty<uint>();
    private Dictionary<uint, int> indexById = new();
    private volatile bool building;
    private volatile bool ready;

    public MarketItemIndex(IDataManager data)
    {
        this.data = data;
    }

    public bool Ready => ready;

    public void EnsureBuilt()
    {
        if (ready || building)
        {
            return;
        }

        lock (sync)
        {
            if (ready || building)
            {
                return;
            }

            building = true;
        }

        _ = Task.Run(Build);
    }

    public void Search(string query, List<MarketItemRef> results, int max)
    {
        results.Clear();
        if (!ready)
        {
            return;
        }

        var needle = query.Trim().ToLowerInvariant();
        if (needle.Length == 0)
        {
            return;
        }

        var localLower = lowerNames;
        for (var index = 0; index < localLower.Length && results.Count < max; index++)
        {
            if (localLower[index].StartsWith(needle, StringComparison.Ordinal))
            {
                results.Add(At(index));
            }
        }

        for (var index = 0; index < localLower.Length && results.Count < max; index++)
        {
            if (!localLower[index].StartsWith(needle, StringComparison.Ordinal) &&
                localLower[index].Contains(needle, StringComparison.Ordinal))
            {
                results.Add(At(index));
            }
        }
    }

    public bool TryGet(uint id, out MarketItemRef item)
    {
        item = default;
        if (!ready || !indexById.TryGetValue(id, out var index))
        {
            return false;
        }

        item = At(index);
        return true;
    }

    private MarketItemRef At(int index) => new(ids[index], names[index], icons[index], vendorPrices[index]);

    private void Build()
    {
        try
        {
            var vendorItems = BuildVendorSet();
            var sheet = data.GetExcelSheet<Item>();
            var bufferIds = new List<uint>(8192);
            var bufferNames = new List<string>(8192);
            var bufferIcons = new List<uint>(8192);
            var bufferVendor = new List<uint>(8192);
            foreach (var item in sheet)
            {
                if (item.ItemSearchCategory.RowId == 0)
                {
                    continue;
                }

                var name = item.Name.ExtractText();
                if (name.Length == 0)
                {
                    continue;
                }

                bufferIds.Add(item.RowId);
                bufferNames.Add(name);
                bufferIcons.Add(item.Icon);
                bufferVendor.Add(vendorItems.Contains(item.RowId) ? item.PriceMid : 0u);
            }

            var count = bufferIds.Count;
            var localIds = bufferIds.ToArray();
            var localNames = bufferNames.ToArray();
            var localIcons = bufferIcons.ToArray();
            var localVendor = bufferVendor.ToArray();
            var localLower = new string[count];
            var localIndex = new Dictionary<uint, int>(count);
            for (var index = 0; index < count; index++)
            {
                localLower[index] = localNames[index].ToLowerInvariant();
                localIndex[localIds[index]] = index;
            }

            ids = localIds;
            names = localNames;
            icons = localIcons;
            vendorPrices = localVendor;
            lowerNames = localLower;
            indexById = localIndex;
            ready = true;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Market item index build failed: {exception.Message}");
            building = false;
        }
    }

    private HashSet<uint> BuildVendorSet()
    {
        var vendorItems = new HashSet<uint>();
        try
        {
            var shops = data.GetSubrowExcelSheet<GilShopItem>();
            foreach (var shop in shops)
            {
                foreach (var entry in shop)
                {
                    var itemId = entry.Item.RowId;
                    if (itemId != 0)
                    {
                        vendorItems.Add(itemId);
                    }
                }
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Market vendor set build failed: {exception.Message}");
        }

        return vendorItems;
    }
}
