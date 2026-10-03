namespace Aetherphone.Apps.Wallet;

internal enum WalletViewKind : byte
{
    Root,
    Currency,
    Activity,
}

internal readonly struct WalletView
{
    public readonly WalletViewKind Kind;
    public readonly uint ItemId;

    private WalletView(WalletViewKind kind, uint itemId)
    {
        Kind = kind;
        ItemId = itemId;
    }

    public static WalletView Root() => new(WalletViewKind.Root, 0);

    public static WalletView ForCurrency(uint itemId) => new(WalletViewKind.Currency, itemId);

    public static WalletView ForActivity() => new(WalletViewKind.Activity, 0);
}
