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

internal readonly struct InventoryStack
{
    public readonly uint ItemId;
    public readonly int Quantity;
    public readonly bool HighQuality;
    public readonly int Slot;

    public InventoryStack(uint itemId, int quantity, bool highQuality, int slot)
    {
        ItemId = itemId;
        Quantity = quantity;
        HighQuality = highQuality;
        Slot = slot;
    }
}

internal sealed class InventorySource
{
    public InventorySource(InventorySourceKind kind, string ownerName, ulong ownerId, InventoryStack[] stacks, DateTime capturedUtc)
    {
        Kind = kind;
        OwnerName = ownerName;
        OwnerId = ownerId;
        Stacks = stacks;
        CapturedUtc = capturedUtc;
    }

    public InventorySourceKind Kind { get; }

    public string OwnerName { get; }

    public ulong OwnerId { get; }

    public InventoryStack[] Stacks { get; }

    public DateTime CapturedUtc { get; }

    public bool IsCached => Kind is InventorySourceKind.Retainer or InventorySourceKind.FreeCompany;
}

internal sealed class InventoryResultRow
{
    public InventoryResultRow(uint itemId, string name, uint iconId, int quantity, bool hasHighQuality)
    {
        ItemId = itemId;
        Name = name;
        IconId = iconId;
        Quantity = quantity;
        HasHighQuality = hasHighQuality;
    }

    public uint ItemId { get; }

    public string Name { get; }

    public uint IconId { get; }

    public int Quantity { get; set; }

    public bool HasHighQuality { get; set; }
}

internal sealed class InventoryResultGroup
{
    public InventoryResultGroup(InventorySourceKind kind, string title, bool isCached, DateTime capturedUtc)
    {
        Kind = kind;
        Title = title;
        IsCached = isCached;
        CapturedUtc = capturedUtc;
        Rows = new List<InventoryResultRow>();
    }

    public InventorySourceKind Kind { get; }

    public string Title { get; }

    public bool IsCached { get; }

    public DateTime CapturedUtc { get; }

    public List<InventoryResultRow> Rows { get; }

    public int TotalQuantity { get; set; }
}
