using Aetherphone.Core.Inventory;

namespace Aetherphone.Apps.Inventory;

internal enum InventoryViewKind : byte
{
    Root,
    Source,
    Item,
    Tidy,
    Wealth,
}

internal readonly struct InventoryView
{
    public readonly InventoryViewKind Kind;
    public readonly InventorySourceKind Source;
    public readonly ulong OwnerId;
    public readonly uint ItemId;
    public readonly string BackTitle;

    private InventoryView(InventoryViewKind kind, InventorySourceKind source, ulong ownerId, uint itemId,
        string backTitle)
    {
        Kind = kind;
        Source = source;
        OwnerId = ownerId;
        ItemId = itemId;
        BackTitle = backTitle;
    }

    public static InventoryView Root() =>
        new(InventoryViewKind.Root, InventorySourceKind.Inventory, 0, 0, string.Empty);

    public static InventoryView ForSource(InventorySourceKind source, ulong ownerId, string backTitle) =>
        new(InventoryViewKind.Source, source, ownerId, 0, backTitle);

    public static InventoryView ForItem(uint itemId, string backTitle) =>
        new(InventoryViewKind.Item, InventorySourceKind.Inventory, 0, itemId, backTitle);

    public static InventoryView ForTidy(string backTitle) =>
        new(InventoryViewKind.Tidy, InventorySourceKind.Inventory, 0, 0, backTitle);

    public static InventoryView ForWealth(string backTitle) =>
        new(InventoryViewKind.Wealth, InventorySourceKind.Inventory, 0, 0, backTitle);
}
