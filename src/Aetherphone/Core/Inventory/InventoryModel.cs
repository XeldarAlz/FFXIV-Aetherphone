namespace Aetherphone.Core.Inventory;

internal enum InventorySourceKind
{
    Inventory,
    Armoury,
    Crystals,
    Saddlebag,
    Equipped,
    Retainer,
    FreeCompany,
}

internal readonly struct InventoryStack : IEquatable<InventoryStack>
{
    public readonly uint ItemId;
    public readonly int Quantity;
    public readonly short Slot;
    public readonly byte Page;
    public readonly bool HighQuality;

    public InventoryStack(uint itemId, int quantity, bool highQuality, int slot, int page)
    {
        ItemId = itemId;
        Quantity = quantity;
        HighQuality = highQuality;
        Slot = (short)slot;
        Page = (byte)page;
    }

    public bool Equals(InventoryStack other) =>
        ItemId == other.ItemId && Quantity == other.Quantity && Slot == other.Slot && Page == other.Page &&
        HighQuality == other.HighQuality;

    public override bool Equals(object? obj) => obj is InventoryStack other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(ItemId, Quantity, Slot, Page, HighQuality);
}

internal sealed class InventorySource
{
    public InventorySource(InventorySourceKind kind, string ownerName, ulong ownerId, InventoryStack[] stacks,
        DateTime capturedUtc, int capacity, bool isCached)
    {
        Kind = kind;
        OwnerName = ownerName;
        OwnerId = ownerId;
        Stacks = stacks;
        CapturedUtc = capturedUtc;
        Capacity = capacity;
        IsCached = isCached;
    }

    public InventorySourceKind Kind { get; }
    public string OwnerName { get; }
    public ulong OwnerId { get; }
    public InventoryStack[] Stacks { get; }
    public DateTime CapturedUtc { get; }
    public int Capacity { get; }
    public bool IsCached { get; }
}

internal readonly struct RetainerSummary
{
    public readonly ulong RetainerId;
    public readonly string Name;
    public readonly long Gil;
    public readonly int ItemCount;
    public readonly int MarketCount;

    public RetainerSummary(ulong retainerId, string name, long gil, int itemCount, int marketCount)
    {
        RetainerId = retainerId;
        Name = name;
        Gil = gil;
        ItemCount = itemCount;
        MarketCount = marketCount;
    }
}

internal static class InventoryPages
{
    public const int RetainerPageSize = 25;
    public const int RetainerPages = 7;
    public const int RetainerCapacity = RetainerPageSize * RetainerPages;
    public const int RetainerCrystalPage = RetainerPages;
    public const int FreeCompanyCrystalPage = 5;
    public const int SaddlebagPremiumFirstPage = 2;

    public static bool IsCrystalPage(InventorySourceKind kind, int page) =>
        kind switch
        {
            InventorySourceKind.Crystals => true,
            InventorySourceKind.Retainer => page == RetainerCrystalPage,
            InventorySourceKind.FreeCompany => page == FreeCompanyCrystalPage,
            _ => false,
        };

    public static bool TakesSlots(InventorySourceKind kind, int page) =>
        kind != InventorySourceKind.Equipped && !IsCrystalPage(kind, page);

    public static bool CanMerge(InventorySourceKind kind, int page) =>
        (kind is InventorySourceKind.Inventory or InventorySourceKind.Saddlebag or InventorySourceKind.Retainer
            or InventorySourceKind.FreeCompany) && !IsCrystalPage(kind, page);
}
