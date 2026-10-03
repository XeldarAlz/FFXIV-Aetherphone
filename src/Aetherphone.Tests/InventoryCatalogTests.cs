using Aetherphone.Core.Inventory;
using Xunit;

namespace Aetherphone.Tests;

public sealed class InventoryCatalogTests
{
    private const uint Potion = 4551;
    private const uint Ore = 5111;
    private const uint Sword = 1601;
    private const uint FireShard = 2;
    private const ulong CharacterId = 77;

    private sealed class FakeItems : IInventoryItemSource
    {
        private readonly Dictionary<uint, InventoryItemInfo> items = new()
        {
            [Potion] = new InventoryItemInfo("Potion", 1, 99, 10, true),
            [Ore] = new InventoryItemInfo("Iron Ore", 2, 999, 3, true),
            [Sword] = new InventoryItemInfo("Bronze Sword", 3, 1, 50, false),
            [FireShard] = new InventoryItemInfo("Fire Shard", 4, 9999, 1, true),
        };

        public bool TryGet(uint itemId, out InventoryItemInfo info) => items.TryGetValue(itemId, out info);
    }

    private sealed class FakePrices : IInventoryPriceSource
    {
        public readonly Dictionary<uint, long> Known = new();
        public int Requests;

        public void Request(List<uint> itemIds) => Requests++;

        public bool TryGet(uint itemId, out long unitPrice) => Known.TryGetValue(itemId, out unitPrice);
    }

    private static InventoryStack Stack(uint itemId, int quantity, int page, int slot, bool highQuality = false) =>
        new(itemId, quantity, highQuality, slot, page);

    private static InventorySource Local(InventorySourceKind kind, int capacity, params InventoryStack[] stacks) =>
        new(kind, string.Empty, CharacterId, stacks, DateTime.UtcNow, capacity, false);

    private static InventorySource Retainer(ulong id, string name, params InventoryStack[] stacks) =>
        new(InventorySourceKind.Retainer, name, id, stacks, DateTime.UtcNow.AddHours(-3), 175, true);

    private static InventoryCatalog Build(InventorySnapshot snapshot)
    {
        snapshot.HasLocal = true;
        var catalog = new InventoryCatalog();
        catalog.Build(snapshot, new FakeItems());
        return catalog;
    }

    [Fact]
    public void ItemsAggregateAcrossSourcesAndSortByName()
    {
        var snapshot = new InventorySnapshot();
        snapshot.Sources.Add(Local(InventorySourceKind.Inventory, 140, Stack(Potion, 20, 0, 0), Stack(Ore, 5, 1, 3)));
        snapshot.Sources.Add(Retainer(9, "Kupo", Stack(Potion, 30, 2, 4, true)));
        var catalog = Build(snapshot);

        Assert.Equal(2, catalog.Items.Count);
        Assert.Equal("Iron Ore", catalog.Items[0].Info.Name);
        var potion = catalog.Items[catalog.IndexOfItem(Potion)];
        Assert.Equal(50, potion.Quantity);
        Assert.Equal(30, potion.HighQualityQuantity);
        Assert.Equal(2, potion.SourceCount);
        Assert.Equal(2, potion.Placements.Length);
        Assert.Equal(InventorySourceKind.Inventory, catalog.Sources[catalog.Placements[potion.Placements[0]].SourceIndex].Kind);
        Assert.Equal(2, catalog.Placements[potion.Placements[1]].Page);
    }

    [Fact]
    public void UnknownItemsAreSkippedButStillTakeSlots()
    {
        var snapshot = new InventorySnapshot();
        snapshot.Sources.Add(Local(InventorySourceKind.Inventory, 140, Stack(999999, 1, 0, 0), Stack(Ore, 1, 0, 1)));
        var catalog = Build(snapshot);

        Assert.Single(catalog.Items);
        Assert.Equal(2, catalog.Sources[0].Used);
    }

    [Fact]
    public void LocalSourcesComeFirstInFixedOrder()
    {
        var snapshot = new InventorySnapshot();
        snapshot.Sources.Add(Local(InventorySourceKind.Saddlebag, 70));
        snapshot.Sources.Add(Retainer(9, "Kupo"));
        snapshot.Sources.Add(Local(InventorySourceKind.Armoury, 400));
        snapshot.Sources.Add(Local(InventorySourceKind.Inventory, 140));
        var catalog = Build(snapshot);

        Assert.Equal(InventorySourceKind.Inventory, catalog.Sources[0].Kind);
        Assert.Equal(InventorySourceKind.Armoury, catalog.Sources[1].Kind);
        Assert.Equal(InventorySourceKind.Saddlebag, catalog.Sources[2].Kind);
        Assert.Equal(InventorySourceKind.Retainer, catalog.Sources[3].Kind);
    }

    [Fact]
    public void RosterOrdersRetainersAndHidesDismissedOnes()
    {
        var snapshot = new InventorySnapshot();
        snapshot.Sources.Add(Local(InventorySourceKind.Inventory, 140));
        snapshot.Sources.Add(Retainer(1, "Gone", Stack(Ore, 10, 0, 0)));
        snapshot.Sources.Add(Retainer(2, "Kupo", Stack(Ore, 10, 0, 0)));
        snapshot.Retainers.Add(new RetainerSummary(3, "Mog", 500, 40, 2));
        snapshot.Retainers.Add(new RetainerSummary(2, "Kupo", 1200, 1, 0));
        var catalog = Build(snapshot);

        Assert.Equal(3, catalog.Sources.Count);
        var mog = catalog.Sources[1];
        Assert.Equal("Mog", mog.OwnerName);
        Assert.False(mog.Browsable);
        Assert.Equal(40, mog.Used);
        Assert.Equal(InventoryPages.RetainerCapacity, mog.Capacity);
        Assert.Equal(2, mog.MarketCount);
        Assert.True(catalog.Sources[2].Browsable);
        Assert.Equal(10, catalog.Items[catalog.IndexOfItem(Ore)].Quantity);
        Assert.Equal(1700, catalog.RetainerGil);
        Assert.Equal(2, catalog.RetainersWithGil);
    }

    [Fact]
    public void CrystalPagesDoNotTakeSlots()
    {
        var snapshot = new InventorySnapshot();
        snapshot.Sources.Add(Retainer(2, "Kupo", Stack(FireShard, 500, InventoryPages.RetainerCrystalPage, 0),
            Stack(Ore, 1, 0, 0)));
        var catalog = Build(snapshot);

        Assert.Equal(1, catalog.Sources[0].Used);
    }

    [Fact]
    public void TidyCountsSlotsFreedByMerging()
    {
        var snapshot = new InventorySnapshot();
        snapshot.Sources.Add(Local(InventorySourceKind.Inventory, 140, Stack(Potion, 50, 0, 0), Stack(Potion, 40, 1, 0),
            Stack(Sword, 1, 0, 2), Stack(Sword, 1, 0, 3)));
        snapshot.Sources.Add(Retainer(2, "Kupo", Stack(Potion, 9, 0, 0), Stack(Potion, 5, 0, 1, true)));
        var catalog = Build(snapshot);

        Assert.Single(catalog.Tidy);
        var entry = catalog.Tidy[0];
        Assert.False(entry.HighQuality);
        Assert.Equal(3, entry.Stacks);
        Assert.Equal(1, entry.Minimum);
        Assert.Equal(2, catalog.TidySlotsSaved);
    }

    [Fact]
    public void TidyIgnoresCrystalsAndArmoury()
    {
        var snapshot = new InventorySnapshot();
        snapshot.Sources.Add(Local(InventorySourceKind.Crystals, 18, Stack(FireShard, 10, 0, 0)));
        snapshot.Sources.Add(Local(InventorySourceKind.Armoury, 400, Stack(Potion, 1, 0, 0), Stack(Potion, 1, 0, 1)));
        snapshot.Sources.Add(Retainer(2, "Kupo", Stack(FireShard, 10, InventoryPages.RetainerCrystalPage, 0)));
        var catalog = Build(snapshot);

        Assert.Empty(catalog.Tidy);
    }

    [Fact]
    public void FilterPutsPrefixMatchesFirst()
    {
        var snapshot = new InventorySnapshot();
        snapshot.Sources.Add(Local(InventorySourceKind.Inventory, 140, Stack(Potion, 1, 0, 0), Stack(Ore, 1, 0, 1),
            Stack(Sword, 1, 0, 2)));
        var catalog = Build(snapshot);
        var results = new List<int>();

        catalog.Filter("o", results);
        Assert.Equal(new[] { "Bronze Sword", "Iron Ore", "Potion" }.Length, results.Count);
        catalog.Filter("ore", results);
        Assert.Single(results);
        Assert.Equal("Iron Ore", catalog.Items[results[0]].Info.Name);
        catalog.Filter("i", results);
        Assert.Equal("Iron Ore", catalog.Items[results[0]].Info.Name);
    }

    [Fact]
    public void ValuationPricesMarketableItemsOnly()
    {
        var snapshot = new InventorySnapshot();
        snapshot.Sources.Add(Local(InventorySourceKind.Inventory, 140, Stack(Potion, 10, 0, 0), Stack(Ore, 100, 0, 1),
            Stack(Sword, 1, 0, 2)));
        var catalog = Build(snapshot);
        var valuation = new InventoryValuation();
        var prices = new FakePrices();

        valuation.Reset(catalog.Items);
        Assert.Equal(2, valuation.MarketableCount);
        Assert.False(valuation.Poll(catalog.Items, prices));
        Assert.True(valuation.Pending);
        Assert.Equal(1, prices.Requests);

        prices.Known[Potion] = 300;
        prices.Known[Ore] = 2;
        Assert.True(valuation.Poll(catalog.Items, prices));
        Assert.False(valuation.Pending);
        Assert.Equal(3200, valuation.Total);
        Assert.Equal(catalog.IndexOfItem(Potion), valuation.Ranked[0]);
        Assert.Equal(2, valuation.Ranked.Length);
    }

    [Fact]
    public void MergeKeepsPagesThatWereNotLoaded()
    {
        var existing = StoredFreeCompany(0b11, 100, (Ore, 0, 0), (Potion, 1, 4));
        var incoming = StoredFreeCompany(0b01, 50, (Sword, 0, 2));

        var merged = InventorySourceMerge.Merge(existing, incoming);

        Assert.Equal(0b11, merged.LoadedPages);
        Assert.Equal(100, merged.Capacity);
        Assert.Equal(2, merged.Stacks.Length);
        Assert.Equal(Sword, merged.Stacks[0].ItemId);
        Assert.Equal(Potion, merged.Stacks[1].ItemId);
    }

    [Fact]
    public void MergeReplacesLegacySources()
    {
        var existing = StoredFreeCompany(0, 0, (Ore, 0, 0));
        var incoming = StoredFreeCompany(0b01, 50, (Sword, 0, 2));

        Assert.Same(incoming, InventorySourceMerge.Merge(existing, incoming));
        Assert.False(InventorySourceMerge.SameContent(existing, incoming));
        Assert.True(InventorySourceMerge.SameContent(incoming, StoredFreeCompany(0b01, 50, (Sword, 0, 2))));
    }

    private static StoredSource StoredFreeCompany(int pages, int capacity, params (uint Item, int Page, int Slot)[] stacks)
    {
        var stored = new StoredStack[stacks.Length];
        for (var index = 0; index < stacks.Length; index++)
        {
            stored[index] = new StoredStack
            {
                ItemId = stacks[index].Item,
                Quantity = 1,
                Page = stacks[index].Page,
                Slot = stacks[index].Slot,
            };
        }

        return new StoredSource
        {
            Kind = InventorySourceKind.FreeCompany,
            OwnerName = "Moogles",
            OwnerId = 5,
            CapturedUnix = 1,
            Capacity = capacity,
            LoadedPages = pages,
            Stacks = stored,
        };
    }
}
