using System.Security.Cryptography;
using System.Text;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Crypto;

internal enum KeyVaultState
{
    Unavailable = 0,
    NeedsSetup = 1,
    Locked = 2,
    Unlocked = 3,
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

        refreshing = true;
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var (bundle, status) = await client.MyKeysAsync(token).ConfigureAwait(false);
            if (status == 404)
            {
                ClearKey();
                serverBundle = null;
                SetState(KeyVaultState.NeedsSetup);
                return;
            }

            if (bundle is null)
            {
                return;
            }

            var previousPublicKey = serverBundle?.PublicKey;
            serverBundle = bundle;
            if (privateKey is not null)
            {
                if (previousPublicKey is not null && !string.Equals(previousPublicKey, bundle.PublicKey, StringComparison.Ordinal))
                {
                    ClearKey();
                    ClearLocalCache();
                    SetState(KeyVaultState.Locked);
                    return;
                }

                SetState(KeyVaultState.Unlocked);
                return;
            }

            if (!configuration.EncryptionRequirePassphraseEachSession && TryLoadLocalCache(bundle))
            {
                SetState(KeyVaultState.Unlocked);
                return;
            }

            SetState(KeyVaultState.Locked);
        }
        finally
        {
            refreshing = false;
            gate.Release();
        }
    }

    public async Task<bool> SetupAsync(string passphrase, CancellationToken token)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var identity = CryptoBox.GenerateIdentity();
            var stored = await PublishAsync(identity, passphrase, token).ConfigureAwait(false);
            if (!stored)
            {
                identity.Dispose();
                return false;
            }

            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<bool> UnlockAsync(string passphrase, CancellationToken token)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var bundle = serverBundle;
            if (bundle is null)
            {
                return false;
            }

            var wrapped = ToWrappedSecret(bundle.PrivateKey);
            if (wrapped is null)
            {
                return false;
            }

            var pkcs8 = await Task.Run(() => CryptoBox.UnwrapPrivateKey(wrapped.Value, passphrase), token).ConfigureAwait(false);
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

            ClearKey();
            privateKey = imported;
            StoreLocalCache(pkcs8);
            CryptographicOperations.ZeroMemory(pkcs8);
            SetState(KeyVaultState.Unlocked);
            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<bool> ChangePassphraseAsync(string passphrase, CancellationToken token)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (privateKey is null)
            {
                return false;
            }

            return await PublishAsync(privateKey, passphrase, token, keepExisting: true).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<bool> RekeyAsync(string passphrase, CancellationToken token)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var identity = CryptoBox.GenerateIdentity();
            var stored = await PublishAsync(identity, passphrase, token).ConfigureAwait(false);
            if (!stored)
            {
                identity.Dispose();
                return false;
            }

            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    public void Lock()
    {
        ClearKey();
        ClearLocalCache();
        SetState(serverBundle is null ? KeyVaultState.Unavailable : KeyVaultState.Locked);
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

    private async Task<bool> PublishAsync(ECDiffieHellman identity, string passphrase, CancellationToken token, bool keepExisting = false)
    {
        var pkcs8 = CryptoBox.ExportPrivateKey(identity);
        var wrapped = await Task.Run(() => CryptoBox.WrapPrivateKey(pkcs8, passphrase), token).ConfigureAwait(false);
        var request = new PutMyKeysRequest(
            CryptoBox.ExportPublicKey(identity),
            new WrappedPrivateKeyDto(
                Convert.ToBase64String(wrapped.Salt),
                wrapped.Iterations,
                Convert.ToBase64String(wrapped.Nonce),
                Convert.ToBase64String(wrapped.Ciphertext)));

        var stored = await client.PutMyKeysAsync(request, token).ConfigureAwait(false);
        if (stored is null)
        {
            CryptographicOperations.ZeroMemory(pkcs8);
            return false;
        }

        serverBundle = stored;
        if (!keepExisting)
        {
            ClearKey();
            privateKey = identity;
        }

        StoreLocalCache(pkcs8);
        CryptographicOperations.ZeroMemory(pkcs8);
        SetState(KeyVaultState.Unlocked);
        return true;
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
            ClearLocalCache();
            return false;
        }

        CryptographicOperations.ZeroMemory(pkcs8);
        privateKey = imported;
        return true;
    }

    private void StoreLocalCache(byte[] pkcs8)
    {
        var userId = MyUserId;
        if (userId is null || configuration.EncryptionRequirePassphraseEachSession)
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

    private void ClearLocalCache()
    {
        if (configuration.EncryptionKeyCache.Length == 0 && configuration.EncryptionKeyCacheUserId.Length == 0)
        {
            return;
        }

        configuration.EncryptionKeyCache = string.Empty;
        configuration.EncryptionKeyCacheUserId = string.Empty;
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

    private static WrappedSecret? ToWrappedSecret(WrappedPrivateKeyDto dto)
    {
        try
        {
            return new WrappedSecret(
                Convert.FromBase64String(dto.Salt),
                dto.Iterations,
                Convert.FromBase64String(dto.Nonce),
                Convert.FromBase64String(dto.Ciphertext));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}

internal static class LocalKeyProtector
{
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
            AepLog.Warning($"Encryption key cache unavailable ({exception.GetType().Name}); passphrase will be required each session.");
            return null;
        }
    }

    public static byte[]? Unprotect(string protectedBase64, string userId)
    {
        try
        {
            var protectedBytes = Convert.FromBase64String(protectedBase64);
            var entropy = Encoding.UTF8.GetBytes(userId);
            return System.Security.Cryptography.ProtectedData.Unprotect(protectedBytes, entropy, DataProtectionScope.CurrentUser);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
