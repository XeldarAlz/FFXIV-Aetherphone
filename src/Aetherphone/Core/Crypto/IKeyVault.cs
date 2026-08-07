namespace Aetherphone.Core.Crypto;

internal interface IKeyVault
{
    event Action? Changed;

    KeyVaultState State { get; }

    byte[]? UnwrapCek(string wrappedKey);
}
