using System.Security.Cryptography;
using System.Text;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Crypto;

internal enum KeyVaultState
{
    Unavailable = 0,
    Provisioning = 1,
    Unlocked = 2,
    Unsupported = 3,
}

internal sealed class KeyVault : IDisposable
{
    private readonly Configuration configuration;
    private readonly AethernetSession session;
    private readonly AethernetClient client;
    private readonly SemaphoreSlim gate = new(1, 1);
    private ECDiffieHellman? privateKey;
    private MyKeysDto? serverBundle;
    private volatile bool refreshing;

    public KeyVault(Configuration configuration, AethernetSession session, AethernetClient client)
    {
        this.configuration = configuration;
        this.session = session;
        this.client = client;
    }

    public KeyVaultState State { get; private set; } = KeyVaultState.Unavailable;

    public int KeyVersion => serverBundle?.KeyVersion ?? 0;

    public string? PublicKey => serverBundle?.PublicKey;

    public string? MyUserId => session.CurrentUser?.Id;

    public bool IsRefreshing => refreshing;

    public event Action? Changed;

    public async Task RefreshAsync(CancellationToken token)
    {
        if (!session.IsSignedIn)
        {
            await gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                ClearKey();
                serverBundle = null;
                SetState(KeyVaultState.Unavailable);
            }
            finally
            {
                gate.Release();
            }

            return;
        }

        if (State == KeyVaultState.Unsupported)
        {
            return;
        }

        refreshing = true;
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var (bundle, status) = await client.MyKeysAsync(token).ConfigureAwait(false);
            if (status == 404)
            {
                serverBundle = null;
                await ProvisionAsync(token).ConfigureAwait(false);
                return;
            }

            if (bundle is null)
            {
                return;
            }

            serverBundle = bundle;
            if (privateKey is not null
                && string.Equals(CryptoBox.ExportPublicKey(privateKey), bundle.PublicKey, StringComparison.Ordinal))
            {
                SetState(KeyVaultState.Unlocked);
                await StripEscrowAsync(bundle, token).ConfigureAwait(false);
                return;
            }

            ClearKey();
            if (TryLoadLocalCache(bundle))
            {
                SetState(KeyVaultState.Unlocked);
                await StripEscrowAsync(bundle, token).ConfigureAwait(false);
                return;
            }

            await ProvisionAsync(token).ConfigureAwait(false);
        }
        finally
        {
            refreshing = false;
            gate.Release();
        }
    }

    public async Task<bool> ResetAsync(CancellationToken token)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            return await ProvisionAsync(token).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public byte[]? UnwrapCek(string wrappedKey)
    {
        var key = privateKey;
        return key is null ? null : CryptoBox.UnwrapCek(wrappedKey, key);
    }

    public void Dispose()
    {
        ClearKey();
        gate.Dispose();
    }

    private async Task<bool> ProvisionAsync(CancellationToken token)
    {
        SetState(KeyVaultState.Provisioning);
        var identity = CryptoBox.TryGenerateIdentity();
        var publicKey = identity is null ? null : CryptoBox.TryExportPublicKey(identity);
        if (identity is null || publicKey is null)
        {
            identity?.Dispose();
            AepLog.Warning("[Encryption] identity unsupported: this system cannot create an encryption key.");
            SetState(KeyVaultState.Unsupported);
            return false;
        }

        var stored = await client.PutMyKeysAsync(new PutMyKeysRequest(publicKey), token).ConfigureAwait(false);
        if (stored is null)
        {
            identity.Dispose();
            return false;
        }

        serverBundle = stored;
        ClearKey();
        privateKey = identity;
        var pkcs8 = CryptoBox.TryExportPrivateKey(identity);
        if (pkcs8 is not null)
        {
            StoreLocalCache(pkcs8);
            CryptographicOperations.ZeroMemory(pkcs8);
        }

        SetState(KeyVaultState.Unlocked);
        return true;
    }

    private async Task StripEscrowAsync(MyKeysDto bundle, CancellationToken token)
    {
        if (bundle.PrivateKey is null)
        {
            return;
        }

        var stored = await client.PutMyKeysAsync(new PutMyKeysRequest(bundle.PublicKey), token).ConfigureAwait(false);
        if (stored is not null)
        {
            serverBundle = stored;
        }
    }

    private bool TryLoadLocalCache(MyKeysDto bundle)
    {
        var userId = MyUserId;
        if (userId is null
            || configuration.EncryptionKeyCache.Length == 0
            || !string.Equals(configuration.EncryptionKeyCacheUserId, userId, StringComparison.Ordinal))
        {
            return false;
        }

        var pkcs8 = LocalKeyProtector.Unprotect(configuration.EncryptionKeyCache, userId);
        if (pkcs8 is null)
        {
            return false;
        }

        var imported = CryptoBox.ImportPrivateKey(pkcs8);
        if (imported is null)
        {
            CryptographicOperations.ZeroMemory(pkcs8);
            return false;
        }

        if (!string.Equals(CryptoBox.ExportPublicKey(imported), bundle.PublicKey, StringComparison.Ordinal))
        {
            imported.Dispose();
            CryptographicOperations.ZeroMemory(pkcs8);
            return false;
        }

        CryptographicOperations.ZeroMemory(pkcs8);
        privateKey = imported;
        return true;
    }

    private void StoreLocalCache(byte[] pkcs8)
    {
        var userId = MyUserId;
        if (userId is null)
        {
            return;
        }

        var protectedBlob = LocalKeyProtector.Protect(pkcs8, userId);
        if (protectedBlob is null)
        {
            return;
        }

        configuration.EncryptionKeyCache = protectedBlob;
        configuration.EncryptionKeyCacheUserId = userId;
        configuration.Save();
    }

    private void ClearKey()
    {
        privateKey?.Dispose();
        privateKey = null;
    }

    private void SetState(KeyVaultState next)
    {
        if (State == next)
        {
            return;
        }

        State = next;
        Changed?.Invoke();
    }
}

internal static class LocalKeyProtector
{
    private const string RawPrefix = "raw.";

    public static string? Protect(byte[] secret, string userId)
    {
        try
        {
            var entropy = Encoding.UTF8.GetBytes(userId);
            var protectedBytes = System.Security.Cryptography.ProtectedData.Protect(secret, entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedBytes);
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Key protection unavailable ({exception.GetType().Name}); storing the encryption key unprotected.");
            return RawPrefix + Convert.ToBase64String(secret);
        }
    }

    public static byte[]? Unprotect(string stored, string userId)
    {
        if (stored.StartsWith(RawPrefix, StringComparison.Ordinal))
        {
            try
            {
                return Convert.FromBase64String(stored[RawPrefix.Length..]);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        try
        {
            var protectedBytes = Convert.FromBase64String(stored);
            var entropy = Encoding.UTF8.GetBytes(userId);
            return System.Security.Cryptography.ProtectedData.Unprotect(protectedBytes, entropy, DataProtectionScope.CurrentUser);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
