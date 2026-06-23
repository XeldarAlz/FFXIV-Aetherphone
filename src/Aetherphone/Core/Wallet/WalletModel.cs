namespace Aetherphone.Core.Wallet;

internal enum CurrencyKind
{
    Generic,
    Gil,
    Tomestone,
}

internal sealed class WalletEntry
{
    public WalletEntry(uint itemId, uint iconId, string name, long cap, CurrencyKind kind)
    {
        ItemId = itemId;
        IconId = iconId;
        Name = name;
        Cap = cap;
        Kind = kind;
    }

    public uint ItemId { get; }

    public uint IconId { get; }

    public string Name { get; }

    public long Cap { get; }

    public CurrencyKind Kind { get; }

    public long Amount { get; set; }
}

internal sealed class WalletSection
{
    public WalletSection(string title, WalletEntry[] entries)
    {
        Title = title;
        Entries = entries;
    }

    public string Title { get; }

    public WalletEntry[] Entries { get; }
}
