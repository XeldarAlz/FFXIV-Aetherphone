using System.Security.Cryptography;
using System.Text;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Crypto;

internal enum KeyVaultState
{
    Unavailable = 0,
    Provisioning = 1,
    Unlocked = 2,
    Unsupported = 3,
    Locked = 4,
}

internal sealed class KeyVault : IDisposable
{
    private readonly Configuration configuration;
    private readonly AethernetSession session;
    private readonly KeysClient client;
    private readonly SemaphoreSlim gate = new(1, 1);
    private ECDiffieHellman? privateKey;
    private string? loadedUserId;
    private MyKeysDto? serverBundle;
    private volatile bool refreshing;

    public KeyVault(Configuration configuration, AethernetSession session, KeysClient client)
    {
        this.configuration = configuration;
        this.session = session;
        this.client = client;
        session.Changed += OnSessionChanged;
    }

    private void OnSessionChanged()
    {
        if (session.IsSignedIn && (MyUserId is null || string.Equals(MyUserId, loadedUserId, StringComparison.Ordinal)))
        {
            return;
        }

        _ = RefreshAsync(CancellationToken.None);
    }

    public KeyVaultState State { get; private set; } = KeyVaultState.Unavailable;

    public int KeyVersion => serverBundle?.KeyVersion ?? 0;

    public string? PublicKey => serverBundle?.PublicKey;

    public string? MyUserId => session.CurrentUser?.Id;

    public bool RecoveryConfigured => serverBundle?.PrivateKey is not null;

    public int EscrowKind => serverBundle?.PrivateKey?.Kind ?? EscrowKinds.RecoveryCode;

    public bool IsRefreshing => refreshing;

    public event Action? Changed;

    public async Task RefreshAsync(CancellationToken token)
    {
        refreshing = true;
        try
        {
            await gate.WaitAsync(token).ConfigureAwait(false);
        }
        catch
        {
            refreshing = false;
            throw;
        }

        try
        {
            if (!session.IsSignedIn)
            {
                ClearKey();
                serverBundle = null;
                SetState(KeyVaultState.Unavailable);
                return;
            }

            if (State == KeyVaultState.Unsupported)
            {
                return;
            }

            var userId = MyUserId;
            if (userId is null)
            {
                ClearKey();
                serverBundle = null;
                SetState(KeyVaultState.Unavailable);
                return;
            }

            var (bundle, status) = await client.MyKeysAsync(token).ConfigureAwait(false);
            if (!string.Equals(MyUserId, userId, StringComparison.Ordinal))
            {
                ClearKey();
                serverBundle = null;
                SetState(KeyVaultState.Unavailable);
                return;
            }

            if (status == 404)
            {
                serverBundle = null;
                await ProvisionAsync(userId, rotate: false, token).ConfigureAwait(false);
                return;
            }

            if (bundle is null)
            {
                if (privateKey is null)
                {
                    SetState(KeyVaultState.Unavailable);
                }

                return;
            }

            serverBundle = bundle;
            if (privateKey is not null
                && string.Equals(loadedUserId, userId, StringComparison.Ordinal)
                && string.Equals(CryptoBox.ExportPublicKey(privateKey), bundle.PublicKey, StringComparison.Ordinal))
            {
                EnsureLocalKeyPersisted(userId);
                SetState(KeyVaultState.Unlocked);
                return;
            }

            ClearKey();
            if (TryLoadLocalKey(userId, bundle.PublicKey))
            {
                SetState(KeyVaultState.Unlocked);
                return;
            }

            AepLog.Warning(
                $"[Encryption] no usable local key for this account; locking this device (escrow available: {bundle.PrivateKey is not null}).");
            SetState(KeyVaultState.Locked);
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
            var userId = MyUserId;
            if (userId is null)
            {
                return false;
            }

            return await ProvisionAsync(userId, rotate: true, token).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<string?> CreateRecoveryCodeAsync(CancellationToken token)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var code = RecoveryKey.GenerateCode();
            var escrow = WrapCurrentKey(pkcs8 => RecoveryKey.Wrap(pkcs8, code));
            if (escrow is null)
            {
                return null;
            }

            return await StoreEscrowAsync(escrow, token).ConfigureAwait(false) ? code : null;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<bool> CreatePassphraseEscrowAsync(string passphrase, CancellationToken token)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var escrow = WrapCurrentKey(pkcs8 => RecoveryKey.WrapPassphrase(pkcs8, passphrase));
            if (escrow is null)
            {
                return false;
            }

            return await StoreEscrowAsync(escrow, token).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public Task<bool> RecoverWithCodeAsync(string code, CancellationToken token)
    {
        return RecoverAsync(escrow => RecoveryKey.Unwrap(escrow, code), token);
    }

    public Task<bool> UnlockWithPassphraseAsync(string passphrase, CancellationToken token)
    {
        return RecoverAsync(escrow => RecoveryKey.UnwrapPassphrase(escrow, passphrase), token);
    }

    public byte[]? UnwrapCek(string wrappedKey)
    {
        var key = privateKey;
        return key is null ? null : CryptoBox.UnwrapCek(wrappedKey, key);
    }

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        ClearKey();
        gate.Dispose();
    }

    private async Task<bool> RecoverAsync(Func<WrappedPrivateKeyDto, byte[]?> unwrap, CancellationToken token)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var userId = MyUserId;
            if (userId is null)
            {
                return false;
            }

            var (bundle, _) = await client.MyKeysAsync(token).ConfigureAwait(false);
            if (bundle is null || !string.Equals(MyUserId, userId, StringComparison.Ordinal))
            {
                return false;
            }

            serverBundle = bundle;
            if (bundle.PrivateKey is null)
            {
                return false;
            }

            var pkcs8 = unwrap(bundle.PrivateKey);
            if (pkcs8 is null)
            {
                return false;
            }

            var imported = CryptoBox.ImportPrivateKey(pkcs8);
            if (imported is null
                || !string.Equals(CryptoBox.ExportPublicKey(imported), bundle.PublicKey, StringComparison.Ordinal))
            {
                imported?.Dispose();
                CryptographicOperations.ZeroMemory(pkcs8);
                return false;
            }

            StoreLocalKey(userId, pkcs8);
            CryptographicOperations.ZeroMemory(pkcs8);
            ClearKey();
            privateKey = imported;
            loadedUserId = userId;
            SetState(KeyVaultState.Unlocked);
            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    private WrappedPrivateKeyDto? WrapCurrentKey(Func<byte[], WrappedPrivateKeyDto?> wrap)
    {
        var key = privateKey;
        if (State != KeyVaultState.Unlocked || key is null || serverBundle is null)
        {
            return null;
        }

        var pkcs8 = CryptoBox.TryExportPrivateKey(key);
        if (pkcs8 is null)
        {
            return null;
        }

        var escrow = wrap(pkcs8);
        CryptographicOperations.ZeroMemory(pkcs8);
        return escrow;
    }

    private async Task<bool> StoreEscrowAsync(WrappedPrivateKeyDto escrow, CancellationToken token)
    {
        var bundle = serverBundle;
        if (bundle is null)
        {
            return false;
        }

        var stored = await client.PutMyKeysAsync(new PutMyKeysRequest(bundle.PublicKey, escrow), token).ConfigureAwait(false);
        if (stored is null)
        {
            return false;
        }

        serverBundle = stored;
        Changed?.Invoke();
        return true;
    }

    private async Task<bool> ProvisionAsync(string userId, bool rotate, CancellationToken token)
    {
        SetState(KeyVaultState.Provisioning);
        var identity = CryptoBox.TryGenerateIdentity();
        var publicKey = identity is null ? null : CryptoBox.TryExportPublicKey(identity);
        var pkcs8 = identity is null ? null : CryptoBox.TryExportPrivateKey(identity);
        if (identity is null || publicKey is null || pkcs8 is null)
        {
            identity?.Dispose();
            AepLog.Warning("[Encryption] identity unsupported: this system cannot create an encryption key.");
            SetState(KeyVaultState.Unsupported);
            return false;
        }

        if (!StoreAndVerifyLocalKey(userId, pkcs8, publicKey))
        {
            identity.Dispose();
            CryptographicOperations.ZeroMemory(pkcs8);
            AepLog.Warning("[Encryption] refusing to create a key that cannot be stored on this device.");
            SetState(KeyVaultState.Unsupported);
            return false;
        }

        CryptographicOperations.ZeroMemory(pkcs8);
        var stored = await client.PutMyKeysAsync(new PutMyKeysRequest(publicKey, null, rotate), token).ConfigureAwait(false);
        if (stored is null)
        {
            RemoveLocalKey(userId);
            identity.Dispose();
            SetState(KeyVaultState.Unavailable);
            return false;
        }

        serverBundle = stored;
        ClearKey();
        privateKey = identity;
        loadedUserId = userId;
        SetState(KeyVaultState.Unlocked);
        return true;
    }

    private bool TryLoadLocalKey(string userId, string expectedPublicKey)
    {
        if (!configuration.EncryptionKeysByUserId.TryGetValue(userId, out var stored) || stored.Length == 0)
        {
            return false;
        }

        var pkcs8 = LocalKeyProtector.Unprotect(stored, userId);
        if (pkcs8 is null)
        {
            return false;
        }

        var imported = CryptoBox.ImportPrivateKey(pkcs8);
        CryptographicOperations.ZeroMemory(pkcs8);
        if (imported is null)
        {
            return false;
        }

        if (!string.Equals(CryptoBox.ExportPublicKey(imported), expectedPublicKey, StringComparison.Ordinal))
        {
            imported.Dispose();
            return false;
        }

        privateKey = imported;
        loadedUserId = userId;
        return true;
    }

    private bool StoreAndVerifyLocalKey(string userId, byte[] pkcs8, string expectedPublicKey)
    {
        StoreLocalKey(userId, pkcs8);
        if (!configuration.EncryptionKeysByUserId.TryGetValue(userId, out var stored) || stored.Length == 0)
        {
            return false;
        }

        var restored = LocalKeyProtector.Unprotect(stored, userId);
        if (restored is null)
        {
            RemoveLocalKey(userId);
            return false;
        }

        var imported = CryptoBox.ImportPrivateKey(restored);
        CryptographicOperations.ZeroMemory(restored);
        if (imported is null)
        {
            RemoveLocalKey(userId);
            return false;
        }

        var matches = string.Equals(CryptoBox.ExportPublicKey(imported), expectedPublicKey, StringComparison.Ordinal);
        imported.Dispose();
        if (!matches)
        {
            RemoveLocalKey(userId);
        }

        return matches;
    }

    private void StoreLocalKey(string userId, byte[] pkcs8)
    {
        var protectedBlob = LocalKeyProtector.Protect(pkcs8, userId);
        configuration.EncryptionKeysByUserId[userId] = protectedBlob;
        if (string.Equals(MyUserId, userId, StringComparison.Ordinal))
        {
            configuration.EncryptionKeyCache = protectedBlob;
            configuration.EncryptionKeyCacheUserId = userId;
        }

        configuration.Save();
        session.PersistActiveKeyCache();
    }

    private void RemoveLocalKey(string userId)
    {
        configuration.EncryptionKeysByUserId.Remove(userId);
        if (string.Equals(configuration.EncryptionKeyCacheUserId, userId, StringComparison.Ordinal))
        {
            configuration.EncryptionKeyCache = string.Empty;
            configuration.EncryptionKeyCacheUserId = string.Empty;
        }

        configuration.Save();
        session.PersistActiveKeyCache();
    }

    private void EnsureLocalKeyPersisted(string userId)
    {
        var key = privateKey;
        if (key is null || configuration.EncryptionKeysByUserId.ContainsKey(userId))
        {
            return;
        }

        var pkcs8 = CryptoBox.TryExportPrivateKey(key);
        if (pkcs8 is null)
        {
            return;
        }

        StoreLocalKey(userId, pkcs8);
        CryptographicOperations.ZeroMemory(pkcs8);
    }

    private void ClearKey()
    {
        privateKey?.Dispose();
        privateKey = null;
        loadedUserId = null;
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

    public static string Protect(byte[] secret, string userId)
    {
        try
        {
            var entropy = Encoding.UTF8.GetBytes(userId);
            var protectedBytes = System.Security.Cryptography.ProtectedData.Protect(secret, entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedBytes);
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Key protection unavailable ({exception.GetType().Name}); storing the key without OS protection so it survives restarts.");
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
