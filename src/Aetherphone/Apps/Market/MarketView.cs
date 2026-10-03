namespace Aetherphone.Apps.Market;

internal enum MarketViewKind : byte
{
    Root,
    Item,
    Listings,
    Sales,
    Alerts,
}

internal readonly struct MarketView
{
    public readonly MarketViewKind Kind;
    public readonly uint ItemId;
    public readonly string Name;
    public readonly uint IconId;

    private MarketView(MarketViewKind kind, uint itemId, string name, uint iconId)
    {
        Kind = kind;
        ItemId = itemId;
        Name = name;
        IconId = iconId;
    }

    public static MarketView Root() => new(MarketViewKind.Root, 0, string.Empty, 0);

    public static MarketView Alerts() => new(MarketViewKind.Alerts, 0, string.Empty, 0);

    public static MarketView Item(uint itemId, string name, uint iconId) =>
        new(MarketViewKind.Item, itemId, name, iconId);

    public MarketView With(MarketViewKind kind) => new(kind, ItemId, Name, IconId);
}
