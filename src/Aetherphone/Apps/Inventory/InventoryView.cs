using Aetherphone.Core.Inventory;

namespace Aetherphone.Apps.Inventory;

internal enum InventoryViewKind : byte
{
    Root,
    Source,
}

internal readonly struct InventoryView
{
    public readonly InventoryViewKind Kind;
    public readonly InventorySourceKind Source;
    public readonly string Title;

    private InventoryView(InventoryViewKind kind, InventorySourceKind source, string title)
    {
        Kind = kind;
        Source = source;
        Title = title;
    }

    public static InventoryView Root() => new(InventoryViewKind.Root, InventorySourceKind.Inventory, string.Empty);

    public static InventoryView ForSource(InventorySourceKind source, string title) =>
        new(InventoryViewKind.Source, source, title);
}
