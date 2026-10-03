namespace Aetherphone.Core.Inventory;

internal readonly struct InventoryPlacement
{
    public readonly int ItemIndex;
    public readonly int SourceIndex;
    public readonly int Page;
    public readonly int Quantity;
    public readonly int Stacks;
    public readonly bool HighQuality;

    public InventoryPlacement(int itemIndex, int sourceIndex, int page, int quantity, int stacks, bool highQuality)
    {
        ItemIndex = itemIndex;
        SourceIndex = sourceIndex;
        Page = page;
        Quantity = quantity;
        Stacks = stacks;
        HighQuality = highQuality;
    }
}

internal sealed class InventoryItemEntry
{
    public InventoryItemEntry(uint itemId, in InventoryItemInfo info)
    {
        ItemId = itemId;
        Info = info;
    }

    public uint ItemId { get; }
    public InventoryItemInfo Info { get; }
    public long Quantity { get; set; }
    public long HighQualityQuantity { get; set; }
    public int[] Placements { get; set; } = Array.Empty<int>();
    public int SourceCount { get; set; }
}

internal sealed class InventorySourceEntry
{
    public InventorySourceEntry(InventorySourceKind kind, string ownerName, ulong ownerId, DateTime capturedUtc,
        bool isCached, bool browsable)
    {
        Kind = kind;
        OwnerName = ownerName;
        OwnerId = ownerId;
        CapturedUtc = capturedUtc;
        IsCached = isCached;
        Browsable = browsable;
    }

    public InventorySourceKind Kind { get; }
    public string OwnerName { get; }
    public ulong OwnerId { get; }
    public DateTime CapturedUtc { get; }
    public bool IsCached { get; }
    public bool Browsable { get; }
    public int Capacity { get; set; }
    public int Used { get; set; }
    public long Quantity { get; set; }
    public long Gil { get; set; } = -1;
    public int MarketCount { get; set; }
    public int[] Placements { get; set; } = Array.Empty<int>();

    public bool HasMeter => Capacity > 0 && Kind is not (InventorySourceKind.Equipped or InventorySourceKind.Crystals);

    public float Fill => Capacity <= 0 ? 0f : Math.Clamp((float)Used / Capacity, 0f, 1f);
}

internal readonly struct InventoryTidyEntry
{
    public readonly int ItemIndex;
    public readonly bool HighQuality;
    public readonly int Stacks;
    public readonly int Minimum;

    public InventoryTidyEntry(int itemIndex, bool highQuality, int stacks, int minimum)
    {
        ItemIndex = itemIndex;
        HighQuality = highQuality;
        Stacks = stacks;
        Minimum = minimum;
    }

    public int Saved => Stacks - Minimum;
}

internal sealed class InventoryCatalog
{
    private static readonly InventorySourceKind[] LocalOrder =
    {
        InventorySourceKind.Inventory, InventorySourceKind.Equipped, InventorySourceKind.Armoury,
        InventorySourceKind.Crystals, InventorySourceKind.Saddlebag,
    };

    private readonly List<InventorySourceEntry> sources = new();
    private readonly List<InventorySource?> sourceData = new();
    private readonly List<InventoryItemEntry> items = new();
    private readonly List<InventoryTidyEntry> tidy = new();
    private readonly Dictionary<uint, int> itemIndexById = new();
    private readonly Dictionary<long, int> placementIndexByKey = new();
    private readonly Dictionary<long, TidyAccumulator> tidyByKey = new();
    private readonly List<PlacementAccumulator> placementScratch = new();
    private InventoryPlacement[] placements = Array.Empty<InventoryPlacement>();

    public IReadOnlyList<InventorySourceEntry> Sources => sources;
    public IReadOnlyList<InventoryItemEntry> Items => items;
    public IReadOnlyList<InventoryTidyEntry> Tidy => tidy;
    public InventoryPlacement[] Placements => placements;
    public int TidySlotsSaved { get; private set; }
    public long Gil { get; private set; }
    public long RetainerGil { get; private set; }
    public int RetainersWithGil { get; private set; }
    public DateTime RetainersCapturedUtc { get; private set; }
    public bool HasLocal { get; private set; }

    public void Build(InventorySnapshot snapshot, IInventoryItemSource itemSource)
    {
        sources.Clear();
        sourceData.Clear();
        items.Clear();
        tidy.Clear();
        itemIndexById.Clear();
        placementIndexByKey.Clear();
        tidyByKey.Clear();
        placementScratch.Clear();
        HasLocal = snapshot.HasLocal;
        Gil = snapshot.Gil;
        RetainersCapturedUtc = snapshot.RetainersCapturedUtc;
        OrderSources(snapshot);
        for (var sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
        {
            Accumulate(sourceIndex, itemSource);
        }

        SortItems();
        FreezePlacements();
        BuildTidy();
        SumRetainerGil();
    }

    public void Filter(string lowerNeedle, List<int> into)
    {
        into.Clear();
        if (lowerNeedle.Length == 0)
        {
            return;
        }

        for (var index = 0; index < items.Count; index++)
        {
            if (items[index].Info.Lower.StartsWith(lowerNeedle, StringComparison.Ordinal))
            {
                into.Add(index);
            }
        }

        for (var index = 0; index < items.Count; index++)
        {
            var lower = items[index].Info.Lower;
            if (!lower.StartsWith(lowerNeedle, StringComparison.Ordinal) &&
                lower.Contains(lowerNeedle, StringComparison.Ordinal))
            {
                into.Add(index);
            }
        }
    }

    public int IndexOfItem(uint itemId) => itemIndexById.TryGetValue(itemId, out var index) ? index : -1;

    public int IndexOfSource(InventorySourceKind kind, ulong ownerId)
    {
        for (var index = 0; index < sources.Count; index++)
        {
            var source = sources[index];
            if (source.Kind == kind && (source.OwnerId == ownerId || !IsOwned(kind)))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsOwned(InventorySourceKind kind) =>
        kind is InventorySourceKind.Retainer or InventorySourceKind.FreeCompany;

    private void OrderSources(InventorySnapshot snapshot)
    {
        for (var orderIndex = 0; orderIndex < LocalOrder.Length; orderIndex++)
        {
            var source = FindFirst(snapshot.Sources, LocalOrder[orderIndex]);
            if (source is not null)
            {
                AddSource(source);
            }
        }

        if (snapshot.Retainers.Count > 0)
        {
            for (var rosterIndex = 0; rosterIndex < snapshot.Retainers.Count; rosterIndex++)
            {
                AddRetainer(snapshot, snapshot.Retainers[rosterIndex]);
            }
        }
        else
        {
            for (var index = 0; index < snapshot.Sources.Count; index++)
            {
                if (snapshot.Sources[index].Kind == InventorySourceKind.Retainer)
                {
                    AddSource(snapshot.Sources[index]);
                }
            }
        }

        InventorySource? freeCompany = null;
        for (var index = 0; index < snapshot.Sources.Count; index++)
        {
            var source = snapshot.Sources[index];
            if (source.Kind == InventorySourceKind.FreeCompany &&
                (freeCompany is null || source.CapturedUtc > freeCompany.CapturedUtc))
            {
                freeCompany = source;
            }
        }

        if (freeCompany is not null)
        {
            AddSource(freeCompany);
        }
    }

    private void AddRetainer(InventorySnapshot snapshot, in RetainerSummary retainer)
    {
        InventorySource? captured = null;
        for (var index = 0; index < snapshot.Sources.Count; index++)
        {
            var source = snapshot.Sources[index];
            if (source.Kind == InventorySourceKind.Retainer && source.OwnerId == retainer.RetainerId)
            {
                captured = source;
                break;
            }
        }

        InventorySourceEntry entry;
        if (captured is not null)
        {
            entry = AddSource(captured, retainer.Name);
        }
        else
        {
            entry = new InventorySourceEntry(InventorySourceKind.Retainer, retainer.Name, retainer.RetainerId,
                snapshot.RetainersCapturedUtc, true, false)
            {
                Capacity = InventoryPages.RetainerCapacity,
                Used = retainer.ItemCount,
            };
            sources.Add(entry);
            sourceData.Add(null);
        }

        entry.Gil = retainer.Gil;
        entry.MarketCount = retainer.MarketCount;
    }

    private InventorySourceEntry AddSource(InventorySource source, string? nameOverride = null)
    {
        var name = nameOverride is { Length: > 0 } ? nameOverride : source.OwnerName;
        var entry = new InventorySourceEntry(source.Kind, name, source.OwnerId, source.CapturedUtc, source.IsCached,
            true)
        {
            Capacity = source.Kind == InventorySourceKind.Retainer && source.Capacity <= 0
                ? InventoryPages.RetainerCapacity
                : source.Capacity,
        };
        sources.Add(entry);
        sourceData.Add(source);
        return entry;
    }

    private static InventorySource? FindFirst(List<InventorySource> list, InventorySourceKind kind)
    {
        for (var index = 0; index < list.Count; index++)
        {
            if (list[index].Kind == kind)
            {
                return list[index];
            }
        }

        return null;
    }

    private void Accumulate(int sourceIndex, IInventoryItemSource itemSource)
    {
        var data = sourceData[sourceIndex];
        if (data is null)
        {
            return;
        }

        var entry = sources[sourceIndex];
        var used = 0;
        for (var stackIndex = 0; stackIndex < data.Stacks.Length; stackIndex++)
        {
            var stack = data.Stacks[stackIndex];
            if (InventoryPages.TakesSlots(entry.Kind, stack.Page))
            {
                used++;
            }

            if (!itemSource.TryGet(stack.ItemId, out var info))
            {
                continue;
            }

            if (!itemIndexById.TryGetValue(stack.ItemId, out var itemIndex))
            {
                itemIndex = items.Count;
                itemIndexById[stack.ItemId] = itemIndex;
                items.Add(new InventoryItemEntry(stack.ItemId, info));
            }

            var item = items[itemIndex];
            item.Quantity += stack.Quantity;
            if (stack.HighQuality)
            {
                item.HighQualityQuantity += stack.Quantity;
            }

            entry.Quantity += stack.Quantity;
            var key = PlacementKey(sourceIndex, stack.Page, stack.HighQuality, stack.ItemId);
            if (placementIndexByKey.TryGetValue(key, out var placementIndex))
            {
                var existing = placementScratch[placementIndex];
                existing.Quantity += stack.Quantity;
                existing.Stacks++;
            }
            else
            {
                placementIndexByKey[key] = placementScratch.Count;
                placementScratch.Add(new PlacementAccumulator(itemIndex, sourceIndex, stack.Page, stack.HighQuality,
                    stack.Quantity));
            }

            if (!InventoryPages.CanMerge(entry.Kind, stack.Page) || info.StackSize <= 1)
            {
                continue;
            }

            var tidyKey = ((long)stack.ItemId << 1) | (stack.HighQuality ? 1L : 0L);
            if (!tidyByKey.TryGetValue(tidyKey, out var accumulator))
            {
                accumulator = new TidyAccumulator(itemIndex, stack.HighQuality);
                tidyByKey[tidyKey] = accumulator;
            }

            accumulator.Stacks++;
            accumulator.Quantity += stack.Quantity;
        }

        entry.Used = used;
    }

    private void SortItems()
    {
        var order = new int[items.Count];
        for (var index = 0; index < order.Length; index++)
        {
            order[index] = index;
        }

        Array.Sort(order, (left, right) => CompareItems(items[left], items[right]));
        var remap = new int[items.Count];
        var sorted = new InventoryItemEntry[items.Count];
        for (var position = 0; position < order.Length; position++)
        {
            remap[order[position]] = position;
            sorted[position] = items[order[position]];
        }

        items.Clear();
        items.AddRange(sorted);
        itemIndexById.Clear();
        for (var index = 0; index < items.Count; index++)
        {
            itemIndexById[items[index].ItemId] = index;
        }

        for (var index = 0; index < placementScratch.Count; index++)
        {
            placementScratch[index].ItemIndex = remap[placementScratch[index].ItemIndex];
        }

        foreach (var accumulator in tidyByKey.Values)
        {
            accumulator.ItemIndex = remap[accumulator.ItemIndex];
        }
    }

    private static int CompareItems(InventoryItemEntry left, InventoryItemEntry right)
    {
        var byName = string.Compare(left.Info.Name, right.Info.Name, StringComparison.OrdinalIgnoreCase);
        return byName != 0 ? byName : left.ItemId.CompareTo(right.ItemId);
    }

    private void FreezePlacements()
    {
        placements = new InventoryPlacement[placementScratch.Count];
        for (var index = 0; index < placements.Length; index++)
        {
            var scratch = placementScratch[index];
            placements[index] = new InventoryPlacement(scratch.ItemIndex, scratch.SourceIndex, scratch.Page,
                scratch.Quantity, scratch.Stacks, scratch.HighQuality);
        }

        var perItem = new int[items.Count];
        var perSource = new int[sources.Count];
        for (var index = 0; index < placements.Length; index++)
        {
            perItem[placements[index].ItemIndex]++;
            perSource[placements[index].SourceIndex]++;
        }

        for (var itemIndex = 0; itemIndex < items.Count; itemIndex++)
        {
            items[itemIndex].Placements = new int[perItem[itemIndex]];
            perItem[itemIndex] = 0;
        }

        for (var sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
        {
            sources[sourceIndex].Placements = new int[perSource[sourceIndex]];
            perSource[sourceIndex] = 0;
        }

        for (var index = 0; index < placements.Length; index++)
        {
            var placement = placements[index];
            items[placement.ItemIndex].Placements[perItem[placement.ItemIndex]++] = index;
            sources[placement.SourceIndex].Placements[perSource[placement.SourceIndex]++] = index;
        }

        var local = placements;
        for (var itemIndex = 0; itemIndex < items.Count; itemIndex++)
        {
            var item = items[itemIndex];
            Array.Sort(item.Placements, (left, right) =>
            {
                var bySource = local[left].SourceIndex.CompareTo(local[right].SourceIndex);
                if (bySource != 0)
                {
                    return bySource;
                }

                var byPage = local[left].Page.CompareTo(local[right].Page);
                return byPage != 0 ? byPage : local[left].HighQuality.CompareTo(local[right].HighQuality);
            });
            item.SourceCount = CountDistinctSources(item.Placements);
        }

        for (var sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
        {
            Array.Sort(sources[sourceIndex].Placements, (left, right) =>
            {
                var byPage = local[left].Page.CompareTo(local[right].Page);
                if (byPage != 0)
                {
                    return byPage;
                }

                var byItem = local[left].ItemIndex.CompareTo(local[right].ItemIndex);
                return byItem != 0 ? byItem : local[left].HighQuality.CompareTo(local[right].HighQuality);
            });
        }
    }

    private int CountDistinctSources(int[] itemPlacements)
    {
        var count = 0;
        var previous = -1;
        for (var index = 0; index < itemPlacements.Length; index++)
        {
            var sourceIndex = placements[itemPlacements[index]].SourceIndex;
            if (sourceIndex != previous)
            {
                count++;
                previous = sourceIndex;
            }
        }

        return count;
    }

    private void BuildTidy()
    {
        var saved = 0;
        foreach (var accumulator in tidyByKey.Values)
        {
            var stackSize = items[accumulator.ItemIndex].Info.StackSize;
            var minimum = (int)((accumulator.Quantity + stackSize - 1) / stackSize);
            if (accumulator.Stacks <= minimum)
            {
                continue;
            }

            tidy.Add(new InventoryTidyEntry(accumulator.ItemIndex, accumulator.HighQuality, accumulator.Stacks,
                minimum));
            saved += accumulator.Stacks - minimum;
        }

        tidy.Sort((left, right) =>
        {
            var bySaved = right.Saved.CompareTo(left.Saved);
            if (bySaved != 0)
            {
                return bySaved;
            }

            var byItem = left.ItemIndex.CompareTo(right.ItemIndex);
            return byItem != 0 ? byItem : left.HighQuality.CompareTo(right.HighQuality);
        });
        TidySlotsSaved = saved;
    }

    private void SumRetainerGil()
    {
        long total = 0;
        var count = 0;
        for (var index = 0; index < sources.Count; index++)
        {
            var source = sources[index];
            if (source.Kind != InventorySourceKind.Retainer || source.Gil < 0)
            {
                continue;
            }

            total += source.Gil;
            count++;
        }

        RetainerGil = total;
        RetainersWithGil = count;
    }

    private static long PlacementKey(int sourceIndex, int page, bool highQuality, uint itemId) =>
        ((long)sourceIndex << 40) | ((long)(page & 0xFF) << 33) | (highQuality ? 1L << 32 : 0L) | itemId;

    private sealed class PlacementAccumulator
    {
        public PlacementAccumulator(int itemIndex, int sourceIndex, int page, bool highQuality, int quantity)
        {
            ItemIndex = itemIndex;
            SourceIndex = sourceIndex;
            Page = page;
            HighQuality = highQuality;
            Quantity = quantity;
            Stacks = 1;
        }

        public int ItemIndex;
        public readonly int SourceIndex;
        public readonly int Page;
        public readonly bool HighQuality;
        public int Quantity;
        public int Stacks;
    }

    private sealed class TidyAccumulator
    {
        public TidyAccumulator(int itemIndex, bool highQuality)
        {
            ItemIndex = itemIndex;
            HighQuality = highQuality;
        }

        public int ItemIndex;
        public readonly bool HighQuality;
        public int Stacks;
        public long Quantity;
    }
}
