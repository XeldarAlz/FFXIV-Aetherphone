namespace Aetherphone.Core.Crypto;

internal enum KeyVaultState
{
    Unavailable = 0,
    Provisioning = 1,
    Unlocked = 2,
    Unsupported = 3,
    Locked = 4,
}
