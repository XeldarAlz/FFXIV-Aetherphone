using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Inventory;

internal readonly struct InventoryItemInfo
{
    public readonly string Name;
    public readonly string Lower;
    public readonly uint IconId;

    public InventoryItemInfo(string name, string lower, uint iconId)
    {
        Name = name;
        Lower = lower;
        IconId = iconId;
    }
}

internal sealed class InventorySearch
{
    private readonly GameData gameData;
    private readonly Dictionary<uint, InventoryItemInfo> itemCache = new();

    private readonly List<InventoryStack> bags = new();
    private readonly List<InventoryStack> armoury = new();
    private readonly List<InventoryStack> crystals = new();
    private readonly List<InventoryStack> saddlebag = new();
    private readonly List<InventoryStack> equipped = new();
    private readonly List<StoredSource> cached = new();

    private readonly Dictionary<long, InventoryResultRow> rowLookup = new();

    public InventorySearch(GameData gameData)
    {
        this.gameData = gameData;
    }

    public int LocalItemCount { get; private set; }

    public DateTime RetainerCapturedUtc { get; private set; }

    public DateTime FreeCompanyCapturedUtc { get; private set; }

    public bool HasRetainerCache { get; private set; }

    public bool HasFreeCompanyCache { get; private set; }

    public void Build(InventoryCaptureService capture, string query, List<InventoryResultGroup> groups)
    {
        groups.Clear();
        capture.SnapshotLocal(bags, armoury, crystals, saddlebag, equipped);
        capture.CopyCachedSources(cached);

        LocalItemCount = bags.Count + armoury.Count + crystals.Count + saddlebag.Count + equipped.Count;
        RefreshCacheTimestamps();

        var needle = query.Trim().ToLowerInvariant();

        AppendLocal(groups, InventorySourceKind.Inventory, Loc.T(L.Inventory.SourceInventory), bags, needle);
        AppendLocal(groups, InventorySourceKind.Armoury, Loc.T(L.Inventory.SourceArmoury), armoury, needle);
        AppendLocal(groups, InventorySourceKind.Crystals, Loc.T(L.Inventory.SourceCrystals), crystals, needle);
        AppendLocal(groups, InventorySourceKind.Saddlebag, Loc.T(L.Inventory.SourceSaddlebag), saddlebag, needle);
        AppendLocal(groups, InventorySourceKind.Equipped, Loc.T(L.Inventory.SourceEquipped), equipped, needle);

        for (var sourceIndex = 0; sourceIndex < cached.Count; sourceIndex++)
        {
            AppendCached(groups, cached[sourceIndex], needle);
        }
    }

    private void RefreshCacheTimestamps()
    {
        HasRetainerCache = false;
        HasFreeCompanyCache = false;
        RetainerCapturedUtc = default;
        FreeCompanyCapturedUtc = default;

        for (var index = 0; index < cached.Count; index++)
        {
            var source = cached[index];
            var captured = DateTimeOffset.FromUnixTimeSeconds(source.CapturedUnix).UtcDateTime;
            if (source.Kind == InventorySourceKind.Retainer)
            {
                HasRetainerCache = true;
                if (captured > RetainerCapturedUtc)
                {
                    RetainerCapturedUtc = captured;
                }
            }
            else if (source.Kind == InventorySourceKind.FreeCompany)
            {
                HasFreeCompanyCache = true;
                if (captured > FreeCompanyCapturedUtc)
                {
                    FreeCompanyCapturedUtc = captured;
                }
            }
        }
    }

    private void AppendLocal(List<InventoryResultGroup> groups, InventorySourceKind kind, string title, List<InventoryStack> stacks, string needle)
    {
        if (stacks.Count == 0)
        {
            return;
        }

        var group = new InventoryResultGroup(kind, title, false, default);
        rowLookup.Clear();
        for (var index = 0; index < stacks.Count; index++)
        {
            AccumulateStack(group, stacks[index].ItemId, stacks[index].Quantity, stacks[index].HighQuality, needle);
        }

        AppendIfMatched(groups, group);
    }

    private void AppendCached(List<InventoryResultGroup> groups, StoredSource source, string needle)
    {
        if (source.Stacks.Length == 0)
        {
            return;
        }

        var captured = DateTimeOffset.FromUnixTimeSeconds(source.CapturedUnix).UtcDateTime;
        var title = BuildCachedTitle(source);
        var group = new InventoryResultGroup(source.Kind, title, true, captured);
        rowLookup.Clear();
        for (var index = 0; index < source.Stacks.Length; index++)
        {
            var stack = source.Stacks[index];
            AccumulateStack(group, stack.ItemId, stack.Quantity, stack.HighQuality, needle);
        }

        AppendIfMatched(groups, group);
    }

    private static string BuildCachedTitle(StoredSource source)
    {
        if (source.OwnerName.Length == 0)
        {
            return source.Kind == InventorySourceKind.Retainer ? Loc.T(L.Inventory.SourceRetainer) : Loc.T(L.Inventory.SourceFreeCompany);
        }

        return source.Kind == InventorySourceKind.Retainer
            ? Loc.T(L.Inventory.RetainerNamed, source.OwnerName)
            : Loc.T(L.Inventory.FreeCompanyNamed, source.OwnerName);
    }

    private void AccumulateStack(InventoryResultGroup group, uint itemId, int quantity, bool highQuality, string needle)
    {
        var info = Resolve(itemId);
        if (info.Name.Length == 0)
        {
            return;
        }

        if (needle.Length > 0 && !info.Lower.Contains(needle))
        {
            return;
        }

        var key = ((long)itemId << 1) | (highQuality ? 1L : 0L);
        if (rowLookup.TryGetValue(key, out var existing))
        {
            existing.Quantity += quantity;
        }
        else
        {
            var row = new InventoryResultRow(itemId, info.Name, info.IconId, quantity, highQuality);
            rowLookup[key] = row;
            group.Rows.Add(row);
        }

        group.TotalQuantity += quantity;
    }

    private static void AppendIfMatched(List<InventoryResultGroup> groups, InventoryResultGroup group)
    {
        if (group.Rows.Count == 0)
        {
            return;
        }

        group.Rows.Sort(static (left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
        groups.Add(group);
    }

    private InventoryItemInfo Resolve(uint itemId)
    {
        if (itemCache.TryGetValue(itemId, out var cachedInfo))
        {
            return cachedInfo;
        }

        if (!gameData.TryGetItem(itemId, out var name, out var iconId, out _))
        {
            var empty = new InventoryItemInfo(string.Empty, string.Empty, 0);
            itemCache[itemId] = empty;
            return empty;
        }

        var info = new InventoryItemInfo(name, name.ToLowerInvariant(), iconId);
        itemCache[itemId] = info;
        return info;
    }
}
