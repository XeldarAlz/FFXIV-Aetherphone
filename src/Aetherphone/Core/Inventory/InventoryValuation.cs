namespace Aetherphone.Core.Inventory;

internal interface IInventoryPriceSource
{
    void Request(List<uint> itemIds);
    bool TryGet(uint itemId, out long unitPrice);
}

internal sealed class InventoryValuation
{
    public const int MaxRanked = 25;
    private const int MaxPolls = 40;
    private readonly List<uint> pendingIds = new();
    private readonly List<int> pendingItems = new();
    private readonly List<int> rankScratch = new();
    private long[] unitPrices = Array.Empty<long>();
    private long[] values = Array.Empty<long>();
    private int polls;

    public long Total { get; private set; }
    public int PricedCount { get; private set; }
    public int MarketableCount { get; private set; }
    public bool Pending => pendingItems.Count > 0 && polls < MaxPolls;
    public int[] Ranked { get; private set; } = Array.Empty<int>();

    public long UnitPrice(int itemIndex) =>
        itemIndex >= 0 && itemIndex < unitPrices.Length ? unitPrices[itemIndex] : 0;

    public long Value(int itemIndex) => itemIndex >= 0 && itemIndex < values.Length ? values[itemIndex] : 0;

    public void Reset(IReadOnlyList<InventoryItemEntry> items)
    {
        unitPrices = new long[items.Count];
        values = new long[items.Count];
        pendingItems.Clear();
        polls = 0;
        MarketableCount = 0;
        for (var index = 0; index < items.Count; index++)
        {
            if (!items[index].Info.Marketable)
            {
                continue;
            }

            MarketableCount++;
            pendingItems.Add(index);
        }

        Total = 0;
        PricedCount = 0;
        Ranked = Array.Empty<int>();
    }

    public bool Poll(IReadOnlyList<InventoryItemEntry> items, IInventoryPriceSource prices)
    {
        if (!Pending || items.Count != unitPrices.Length)
        {
            return false;
        }

        polls++;
        var resolved = false;
        for (var position = pendingItems.Count - 1; position >= 0; position--)
        {
            var itemIndex = pendingItems[position];
            if (!prices.TryGet(items[itemIndex].ItemId, out var unitPrice))
            {
                continue;
            }

            unitPrices[itemIndex] = Math.Max(0, unitPrice);
            pendingItems.RemoveAt(position);
            resolved = true;
        }

        if (pendingItems.Count > 0)
        {
            pendingIds.Clear();
            for (var position = 0; position < pendingItems.Count; position++)
            {
                pendingIds.Add(items[pendingItems[position]].ItemId);
            }

            prices.Request(pendingIds);
        }

        if (resolved)
        {
            Recompute(items);
        }

        return resolved;
    }

    private void Recompute(IReadOnlyList<InventoryItemEntry> items)
    {
        long total = 0;
        var priced = 0;
        rankScratch.Clear();
        for (var index = 0; index < items.Count; index++)
        {
            var unitPrice = unitPrices[index];
            values[index] = unitPrice > 0 ? unitPrice * items[index].Quantity : 0;
            if (values[index] <= 0)
            {
                continue;
            }

            total += values[index];
            priced++;
            rankScratch.Add(index);
        }

        var localValues = values;
        rankScratch.Sort((left, right) =>
        {
            var byValue = localValues[right].CompareTo(localValues[left]);
            return byValue != 0 ? byValue : left.CompareTo(right);
        });
        var count = Math.Min(MaxRanked, rankScratch.Count);
        var ranked = new int[count];
        for (var position = 0; position < count; position++)
        {
            ranked[position] = rankScratch[position];
        }

        Ranked = ranked;
        Total = total;
        PricedCount = priced;
    }
}
