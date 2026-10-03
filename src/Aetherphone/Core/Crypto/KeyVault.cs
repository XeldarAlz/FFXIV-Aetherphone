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

internal enum LocalKeyStatus
{
    Missing = 0,
    Opened = 1,
    Unreadable = 2,
}

internal enum RecoveryCodeCreationStatus
{
    Failed = 0,
    Created = 1,
    KeyChangedElsewhere = 2,
}

internal enum RecoveryAttemptOutcome
{
    Failed = 0,
    Recovered = 1,
    WrongCode = 2,
    OlderCode = 3,
}

internal readonly record struct RecoveryCodeCreation(RecoveryCodeCreationStatus Status, string? Code)
{
    public static readonly RecoveryCodeCreation Failure = new(RecoveryCodeCreationStatus.Failed, null);

    public static readonly RecoveryCodeCreation KeyChangedElsewhere =
        new(RecoveryCodeCreationStatus.KeyChangedElsewhere, null);

    public static RecoveryCodeCreation Created(string code)
    {
        return new RecoveryCodeCreation(RecoveryCodeCreationStatus.Created, code);
    }
}

internal sealed class KeyVault : IDisposable
{
    private readonly Configuration configuration;
    private readonly AethernetSession session;
    private readonly KeysClient client;
    private readonly SemaphoreSlim gate = new(1, 1);
    private EcPrivateKey? privateKey;
    private EcPrivateKey[] recoveredPreviousKeys = Array.Empty<EcPrivateKey>();
    private MyKeysDto? serverBundle;
    private volatile bool refreshing;
    private int missingServerKeyStreak;
    private string? lastAccountId;
    private EcPrivateKey? linkEphemeral;

    private const int MissingServerKeyConfirmations = 2;

    private const int MaxRetiredKeys = 8;

    private const int MaxLockedRetryDoublings = 5;

    private static readonly TimeSpan TransientRetryDelay = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan LockedRetryCeiling = TimeSpan.FromMinutes(15);

    private readonly object refreshSync = new();
    private Task? refreshInFlight;
    private string? refreshInFlightAccountId;
    private int lockedRefreshStreak;

    public KeyVault(Configuration configuration, AethernetSession session, KeysClient client)
    {
        this.configuration = configuration;
        this.session = session;
        this.client = client;
        session.Changed += OnSessionChanged;
    }

    private void OnSessionChanged()
    {
        var accountId = session.CurrentUser?.Id;
        if (!string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            lastAccountId = accountId;
            missingServerKeyStreak = 0;
            lockedRefreshStreak = 0;
            LocalKeyUnreadable = false;
            ClearKey();
            serverBundle = null;
            SetState(KeyVaultState.Unavailable);
        }

        if (session.IsSignedIn)
        {
            return;
        }

        _ = RefreshAsync(CancellationToken.None);
    }

    public KeyVaultState State { get; private set; } = KeyVaultState.Unavailable;

    public bool LocalCacheUnavailable { get; private set; }

    public bool LocalKeyUnreadable { get; private set; }

    public int KeyVersion => serverBundle?.KeyVersion ?? 0;

    public string? PublicKey => serverBundle?.PublicKey;

    public string? MyUserId => session.CurrentUser?.Id;

    public bool RecoveryConfigured => serverBundle?.PrivateKey is not null;

    public int OlderKeysHeldHere => recoveredPreviousKeys.Length;

    public bool IsRefreshing => refreshing;

    public event Action? Changed;

    public event Action? PreviousKeysRestored;

    public TimeSpan BackgroundRetryDelay
    {
        get
        {
            if (State != KeyVaultState.Locked || lockedRefreshStreak <= 1)
            {
                return TransientRetryDelay;
            }

            var doublings = Math.Min(lockedRefreshStreak - 1, MaxLockedRetryDoublings);
            var delay = TransientRetryDelay * (1 << doublings);
            return delay < LockedRetryCeiling ? delay : LockedRetryCeiling;
        }
    }

    public Task RefreshAsync(CancellationToken token)
    {
        if (!session.IsSignedIn)
        {
            return RefreshCoreAsync(token);
        }

        var accountId = MyUserId;
        lock (refreshSync)
        {
            if (refreshInFlight is { IsCompleted: false } running
                && string.Equals(refreshInFlightAccountId, accountId, StringComparison.Ordinal))
            {
                return running;
            }

            refreshInFlightAccountId = accountId;
            refreshInFlight = Task.Run(() => RefreshCoreAsync(token), token);
            return refreshInFlight;
        }
    }

    private async Task RefreshCoreAsync(CancellationToken token)
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
            var accountId = MyUserId;
            var (bundle, status) = await client.MyKeysAsync(token).ConfigureAwait(false);
            if (!string.Equals(MyUserId, accountId, StringComparison.Ordinal))
            {
                AepLog.Debug("[Encryption] vault refresh abandoned: the account changed while the keys were being fetched.");
                return;
            }

            if (status == 404)
            {
                serverBundle = null;
                await HandleMissingServerKeyAsync(accountId, token).ConfigureAwait(false);
                return;
            }

            missingServerKeyStreak = 0;
            if (bundle is null)
            {
                AepLog.Debug("[Encryption] vault refresh skipped: the keys endpoint was unreachable; it will be retried.");
                return;
            }

            serverBundle = bundle;
            if (privateKey is not null
                && string.Equals(CryptoBox.ExportPublicKey(privateKey), bundle.PublicKey, StringComparison.Ordinal))
            {
                LocalKeyUnreadable = false;
                EnsureLocalCachePersisted();
                SetState(KeyVaultState.Unlocked);
                return;
            }

            if (await TryAdoptPendingKeyAsync(bundle, accountId).ConfigureAwait(false))
            {
                LocalKeyUnreadable = false;
                SetState(KeyVaultState.Unlocked);
                return;
            }

            ClearKey();
            if (TryLoadLocalCache(bundle))
            {
                LocalKeyUnreadable = false;
                SetState(KeyVaultState.Unlocked);
                return;
            }

            if (accountId is null)
            {
                AepLog.Debug("[Encryption] vault refresh stopped: the account has not resolved yet.");
                SetState(KeyVaultState.Unavailable);
                return;
            }

            ImportStoredKey(accountId, out var localStatus);
            LocalKeyUnreadable = localStatus == LocalKeyStatus.Unreadable;
            LoadRetiredKeys();
            lockedRefreshStreak++;
            AepLog.Warning(
                $"[Encryption] no usable local key for this account; locking this device instead of creating a new key (stored key: {localStatus}, recovery available: {bundle.PrivateKey is not null}).");
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
            AepLog.Warning(
                $"[Encryption] a key reset was requested on this device (recovery configured: {RecoveryConfigured}, current key version: {KeyVersion}); the previous key is being replaced.");
            return await ProvisionAsync(serverBundle?.KeyVersion ?? 0, token).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<RecoveryCodeCreation> CreateRecoveryCodeAsync(CancellationToken token)
    {
        var rotatedElsewhere = false;
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var key = privateKey;
            var bundle = serverBundle;
            if (State != KeyVaultState.Unlocked || key is null || bundle is null)
            {
                return RecoveryCodeCreation.Failure;
            }

            var (fetched, _) = await client.MyKeysAsync(token).ConfigureAwait(false);
            if (fetched is null)
            {
                AepLog.Warning("[Encryption] creating a recovery code failed: the keys endpoint was unreachable.");
                return RecoveryCodeCreation.Failure;
            }

            if (!string.Equals(fetched.PublicKey, CryptoBox.ExportPublicKey(key), StringComparison.Ordinal))
            {
                AepLog.Warning(
                    "[Encryption] the account key was rotated on another device; refusing to overwrite it with this device's key.");
                rotatedElsewhere = true;
                return RecoveryCodeCreation.KeyChangedElsewhere;
            }

            serverBundle = fetched;
            var pkcs8 = CryptoBox.TryExportPrivateKey(key);
            if (pkcs8 is null)
            {
                AepLog.Warning("[Encryption] creating a recovery code failed: the private key could not be exported.");
                return RecoveryCodeCreation.Failure;
            }

            var code = RecoveryKey.GenerateCode();
            var escrow = RecoveryKey.Wrap(pkcs8, code);
            CryptographicOperations.ZeroMemory(pkcs8);
            if (escrow is null)
            {
                AepLog.Warning("[Encryption] creating a recovery code failed: wrapping the private key failed.");
                return RecoveryCodeCreation.Failure;
            }

            var (stored, status) = await client.PutMyKeysAsync(
                new PutMyKeysRequest(fetched.PublicKey, escrow, fetched.KeyVersion), token).ConfigureAwait(false);
            if (status == 409)
            {
                AepLog.Warning(
                    "[Encryption] the account key was rotated on another device while saving the recovery code; keeping the newer key.");
                rotatedElsewhere = true;
                return RecoveryCodeCreation.KeyChangedElsewhere;
            }

            if (stored is null)
            {
                AepLog.Warning("[Encryption] creating a recovery code failed: the server did not accept the escrow.");
                return RecoveryCodeCreation.Failure;
            }

            serverBundle = stored;
            return RecoveryCodeCreation.Created(code);
        }
        finally
        {
            gate.Release();
            if (rotatedElsewhere)
            {
                _ = RefreshAsync(CancellationToken.None);
            }
        }
    }

    public async Task<RecoveryAttemptOutcome> RecoverWithCodeAsync(string code, CancellationToken token)
    {
        var olderKeysRestored = 0;
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var accountId = MyUserId;
            var (bundle, _) = await client.MyKeysAsync(token).ConfigureAwait(false);
            if (bundle is null)
            {
                AepLog.Warning("[Encryption] recovery failed: the keys endpoint was unreachable.");
                return RecoveryAttemptOutcome.Failed;
            }

            if (accountId is null || !string.Equals(MyUserId, accountId, StringComparison.Ordinal))
            {
                AepLog.Warning("[Encryption] recovery stopped: the account changed while the escrow was being fetched.");
                return RecoveryAttemptOutcome.Failed;
            }

            serverBundle = bundle;
            if (bundle.PrivateKey is null)
            {
                AepLog.Warning("[Encryption] recovery found no escrow for the current key; checking archived escrows.");
                olderKeysRestored = await RestoreArchivedKeysAsync(code, token).ConfigureAwait(false);
                return olderKeysRestored > 0 ? RecoveryAttemptOutcome.OlderCode : RecoveryAttemptOutcome.Failed;
            }

            var pkcs8 = RecoveryKey.Unwrap(bundle.PrivateKey, code);
            if (pkcs8 is null)
            {
                AepLog.Info("[Encryption] the entered code did not open the current escrow; checking archived escrows.");
                olderKeysRestored = await RestoreArchivedKeysAsync(code, token).ConfigureAwait(false);
                return olderKeysRestored > 0 ? RecoveryAttemptOutcome.OlderCode : RecoveryAttemptOutcome.WrongCode;
            }

            var imported = CryptoBox.ImportPrivateKey(pkcs8);
            if (imported is null)
            {
                CryptographicOperations.ZeroMemory(pkcs8);
                AepLog.Warning("[Encryption] recovery failed: the escrow opened but its private key could not be imported.");
                return RecoveryAttemptOutcome.Failed;
            }

            if (!string.Equals(CryptoBox.ExportPublicKey(imported), bundle.PublicKey, StringComparison.Ordinal))
            {
                CryptographicOperations.ZeroMemory(pkcs8);
                AepLog.Warning(
                    "[Encryption] the entered code opened an escrow for a key that is no longer current; keeping that key so older chats open.");
                var stale = new List<EcPrivateKey>();
                var stalePublicKey = CryptoBox.TryExportPublicKey(imported);
                if (stalePublicKey is not null && !CollectKnownPublicKeys().Contains(stalePublicKey))
                {
                    stale.Add(imported);
                }

                olderKeysRestored = AdoptOlderKeys(stale);
                olderKeysRestored += await RestoreArchivedKeysAsync(code, token).ConfigureAwait(false);
                return RecoveryAttemptOutcome.OlderCode;
            }

            ClearKey();
            StoreLocalCache(pkcs8, accountId);
            AdoptPrivateKey(imported);
            CryptographicOperations.ZeroMemory(pkcs8);
            LocalKeyUnreadable = false;
            AepLog.Info("[Encryption] a recovery code restored this account's key on this device.");
            SetState(KeyVaultState.Unlocked);
            return RecoveryAttemptOutcome.Recovered;
        }
        finally
        {
            gate.Release();
            if (olderKeysRestored > 0)
            {
                PreviousKeysRestored?.Invoke();
            }
        }
    }

    public async Task<DeviceLinkTicketDto?> StartDeviceLinkAsync(CancellationToken token)
    {
        var ephemeral = CryptoBox.TryGenerateIdentity();
        var ephemeralPublicKey = ephemeral is null ? null : CryptoBox.TryExportPublicKey(ephemeral);
        if (ephemeral is null || ephemeralPublicKey is null)
        {
            AepLog.Warning("[Encryption] this device cannot create the key needed to link with another PC.");
            return null;
        }

        var ticket = await client.StartDeviceLinkAsync(ephemeralPublicKey, token).ConfigureAwait(false);
        if (ticket is null)
        {
            return null;
        }

        linkEphemeral = ephemeral;
        AepLog.Info("[Encryption] waiting for another PC to approve this device.");
        return ticket;
    }

    public async Task<bool> TryCompleteDeviceLinkAsync(string requestId, CancellationToken token)
    {
        var ephemeral = linkEphemeral;
        if (ephemeral is null)
        {
            return false;
        }

        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var accountId = MyUserId;
            var status = await client.DeviceLinkStatusAsync(requestId, token).ConfigureAwait(false);
            if (status is null || status.WrappedIdentityKey is null || accountId is null)
            {
                return false;
            }

            if (!string.Equals(MyUserId, accountId, StringComparison.Ordinal))
            {
                return false;
            }

            var pkcs8 = CryptoBox.UnwrapSecret(status.WrappedIdentityKey, ephemeral);
            if (pkcs8 is null)
            {
                AepLog.Warning("[Encryption] the approving PC sent a key this device could not open.");
                return false;
            }

            var imported = CryptoBox.ImportPrivateKey(pkcs8);
            var bundle = serverBundle;
            if (imported is null || bundle is null
                || !string.Equals(CryptoBox.TryExportPublicKey(imported), bundle.PublicKey, StringComparison.Ordinal))
            {
                CryptographicOperations.ZeroMemory(pkcs8);
                AepLog.Warning("[Encryption] the linked key does not match the account's current key.");
                return false;
            }

            ClearKey();
            StoreLocalCache(pkcs8, accountId);
            AdoptPrivateKey(imported);
            CryptographicOperations.ZeroMemory(pkcs8);
            linkEphemeral = null;
            LocalKeyUnreadable = false;
            AepLog.Info("[Encryption] another PC approved this device and its chats are unlocked.");
            SetState(KeyVaultState.Unlocked);
            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    public void CancelDeviceLink(string requestId)
    {
        linkEphemeral = null;
        _ = client.CancelDeviceLinkAsync(requestId, CancellationToken.None);
    }

    public Task<PendingDeviceLinksDto?> PendingDeviceLinksAsync(CancellationToken token)
    {
        return client.PendingDeviceLinksAsync(token);
    }

    public async Task<bool> ApproveDeviceLinkAsync(string requestId, string ephemeralPublicKey,
        CancellationToken token)
    {
        var key = privateKey;
        if (key is null || State != KeyVaultState.Unlocked)
        {
            return false;
        }

        var pkcs8 = CryptoBox.TryExportPrivateKey(key);
        if (pkcs8 is null)
        {
            AepLog.Warning("[Encryption] approving the new PC failed: the key could not be exported.");
            return false;
        }

        var wrapped = CryptoBox.WrapSecret(pkcs8, ephemeralPublicKey);
        CryptographicOperations.ZeroMemory(pkcs8);
        if (wrapped is null)
        {
            AepLog.Warning("[Encryption] approving the new PC failed: the key could not be wrapped for it.");
            return false;
        }

        var approved = await client.ApproveDeviceLinkAsync(requestId, wrapped, token).ConfigureAwait(false);
        AepLog.Info($"[Encryption] approving another PC returned {approved}.");
        return approved;
    }

    public byte[]? UnwrapCek(string wrappedKey)
    {
        return UnwrapCek(wrappedKey, out _);
    }

    public byte[]? UnwrapCek(string wrappedKey, out bool privateKeyWasLoaded)
    {
        var key = privateKey;
        privateKeyWasLoaded = key is not null;
        if (key is not null)
        {
            var unwrapped = CryptoBox.UnwrapCek(wrappedKey, key);
            if (unwrapped is not null)
            {
                return unwrapped;
            }
        }

        var recovered = recoveredPreviousKeys;
        for (var index = 0; index < recovered.Length; index++)
        {
            var unwrapped = CryptoBox.UnwrapCek(wrappedKey, recovered[index]);
            if (unwrapped is not null)
            {
                return unwrapped;
            }
        }

        return null;
    }

    public async Task<bool> HasArchivedEscrowsAsync(CancellationToken token)
    {
        if (!session.IsSignedIn)
        {
            return false;
        }

        var escrows = await client.MyKeyEscrowsAsync(token).ConfigureAwait(false);
        if (escrows is null)
        {
            AepLog.Debug("[Encryption] archived escrow lookup failed: the escrows endpoint was unreachable.");
            return false;
        }

        return escrows.Items.Length > 0;
    }

    public async Task<int> RestorePreviousKeysAsync(string code, CancellationToken token)
    {
        var restoredCount = 0;
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (State != KeyVaultState.Unlocked)
            {
                return 0;
            }

            var canonical = RecoveryKey.Canonicalize(code);
            if (canonical.Length == 0)
            {
                return 0;
            }

            var escrows = await client.MyKeyEscrowsAsync(token).ConfigureAwait(false);
            if (escrows is null)
            {
                AepLog.Warning("[Encryption] restoring older keys failed: the escrows endpoint was unreachable.");
                return -1;
            }

            restoredCount = RestoreFromEscrows(escrows.Items, canonical);
            return restoredCount;
        }
        finally
        {
            gate.Release();
            if (restoredCount > 0)
            {
                PreviousKeysRestored?.Invoke();
            }
        }
    }

    private async Task<int> RestoreArchivedKeysAsync(string code, CancellationToken token)
    {
        var canonical = RecoveryKey.Canonicalize(code);
        if (canonical.Length == 0)
        {
            return 0;
        }

        var escrows = await client.MyKeyEscrowsAsync(token).ConfigureAwait(false);
        if (escrows is null)
        {
            AepLog.Warning("[Encryption] archived escrows could not be checked: the escrows endpoint was unreachable.");
            return 0;
        }

        return RestoreFromEscrows(escrows.Items, canonical);
    }

    private int RestoreFromEscrows(ArchivedKeyEscrowDto[] items, string canonical)
    {
        if (items.Length == 0)
        {
            return 0;
        }

        var knownPublicKeys = CollectKnownPublicKeys();
        var restored = new List<EcPrivateKey>();
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            if (!knownPublicKeys.Add(item.PublicKey))
            {
                continue;
            }

            var imported = TryRecoverArchivedKey(item, canonical);
            if (imported is null)
            {
                knownPublicKeys.Remove(item.PublicKey);
                continue;
            }

            restored.Add(imported);
        }

        if (restored.Count == 0)
        {
            AepLog.Info($"[Encryption] the entered code did not open any of the {items.Length} archived keys.");
            return 0;
        }

        AepLog.Info($"[Encryption] restored {restored.Count} of {items.Length} archived keys.");
        return AdoptOlderKeys(restored);
    }

    private int AdoptOlderKeys(List<EcPrivateKey> restored)
    {
        if (restored.Count == 0)
        {
            return 0;
        }

        var merged = new EcPrivateKey[recoveredPreviousKeys.Length + restored.Count];
        recoveredPreviousKeys.CopyTo(merged, 0);
        for (var index = 0; index < restored.Count; index++)
        {
            merged[recoveredPreviousKeys.Length + index] = restored[index];
        }

        recoveredPreviousKeys = merged;
        PersistRestoredKeys(restored);
        return restored.Count;
    }

    private void PersistRestoredKeys(List<EcPrivateKey> restored)
    {
        var userId = MyUserId;
        if (userId is null)
        {
            return;
        }

        var persisted = false;
        for (var index = 0; index < restored.Count; index++)
        {
            var pkcs8 = CryptoBox.TryExportPrivateKey(restored[index]);
            if (pkcs8 is null)
            {
                continue;
            }

            RetireStoredBlob(LocalKeyProtector.Protect(pkcs8, userId), userId);
            CryptographicOperations.ZeroMemory(pkcs8);
            persisted = true;
        }

        if (persisted)
        {
            configuration.Save();
        }
    }

    private HashSet<string> CollectKnownPublicKeys()
    {
        var knownPublicKeys = new HashSet<string>(StringComparer.Ordinal);
        var currentKey = privateKey;
        if (currentKey is not null)
        {
            var currentPublicKey = CryptoBox.TryExportPublicKey(currentKey);
            if (currentPublicKey is not null)
            {
                knownPublicKeys.Add(currentPublicKey);
            }
        }

        for (var index = 0; index < recoveredPreviousKeys.Length; index++)
        {
            var publicKey = CryptoBox.TryExportPublicKey(recoveredPreviousKeys[index]);
            if (publicKey is not null)
            {
                knownPublicKeys.Add(publicKey);
            }
        }

        return knownPublicKeys;
    }

    private static EcPrivateKey? TryRecoverArchivedKey(ArchivedKeyEscrowDto item, string canonicalCode)
    {
        var pkcs8 = RecoveryKey.Unwrap(item.Escrow, canonicalCode);
        if (pkcs8 is null)
        {
            return null;
        }

        var imported = CryptoBox.ImportPrivateKey(pkcs8);
        CryptographicOperations.ZeroMemory(pkcs8);
        if (imported is null)
        {
            AepLog.Warning($"[Encryption] archived key version {item.KeyVersion} opened but could not be imported.");
            return null;
        }

        if (!string.Equals(CryptoBox.TryExportPublicKey(imported), item.PublicKey, StringComparison.Ordinal))
        {
            AepLog.Warning($"[Encryption] archived key version {item.KeyVersion} opened but does not match its recorded public key.");
            return null;
        }

        return imported;
    }

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        ClearKey();
        gate.Dispose();
    }

    private async Task<bool> ProvisionAsync(int expectedKeyVersion, CancellationToken token)
    {
        var accountId = MyUserId;
        if (accountId is null)
        {
            AepLog.Warning("[Encryption] skipped creating a key because the account has not resolved yet.");
            return false;
        }

        var previousState = State;
        SetState(KeyVaultState.Provisioning);
        var identity = CryptoBox.TryGenerateIdentity();
        var publicKey = identity is null ? null : CryptoBox.TryExportPublicKey(identity);
        if (identity is null || publicKey is null)
        {
            AepLog.Warning("[Encryption] identity unsupported: this system cannot create an encryption key.");
            SetState(KeyVaultState.Unsupported);
            return false;
        }

        var pkcs8 = CryptoBox.TryExportPrivateKey(identity);
        if (pkcs8 is null)
        {
            AepLog.Error("[Encryption] the new key could not be exported for storage, so it was not published.");
            SetState(previousState);
            return false;
        }

        var staged = await StoreAndVerifyAsync(pkcs8, accountId, publicKey).ConfigureAwait(false);
        if (!staged)
        {
            CryptographicOperations.ZeroMemory(pkcs8);
            SetState(previousState);
            return false;
        }

        var code = RecoveryKey.GenerateCode();
        var escrow = RecoveryKey.Wrap(pkcs8, code);
        CryptographicOperations.ZeroMemory(pkcs8);
        if (escrow is null)
        {
            AepLog.Warning("[Encryption] the recovery escrow could not be prepared; this key is published without one.");
        }

        var (stored, status) = await client.PutMyKeysAsync(
            new PutMyKeysRequest(publicKey, escrow, expectedKeyVersion), token).ConfigureAwait(false);
        if (status == 409)
        {
            AepLog.Warning("[Encryption] the account key changed on another device while creating a new key; keeping the newer key.");
            ClearPendingKey();
            await configuration.SaveNowAsync().ConfigureAwait(false);
            SetState(previousState);
            return false;
        }

        if (stored is null)
        {
            AepLog.Warning("[Encryption] creating a key failed: the server did not accept the upload; it will be retried.");
            ClearPendingKey();
            await configuration.SaveNowAsync().ConfigureAwait(false);
            SetState(previousState);
            return false;
        }

        if (!string.Equals(MyUserId, accountId, StringComparison.Ordinal))
        {
            AepLog.Warning(
                "[Encryption] the account changed while the new key was being published; it stays staged until that account refreshes.");
            SetState(KeyVaultState.Unavailable);
            return false;
        }

        await PromotePendingKeyAsync(accountId).ConfigureAwait(false);
        serverBundle = stored;
        ClearKey();
        AdoptPrivateKey(identity);
        if (escrow is not null)
        {
            HoldRecoveryCode(accountId, code);
        }

        AepLog.Info($"[Encryption] a new key is active for this account at version {stored.KeyVersion}.");
        SetState(KeyVaultState.Unlocked);
        return true;
    }

    private bool TryLoadLocalCache(MyKeysDto bundle)
    {
        var imported = ImportStoredKeyForCurrentUser();
        if (imported is null)
        {
            return false;
        }

        if (!string.Equals(CryptoBox.ExportPublicKey(imported), bundle.PublicKey, StringComparison.Ordinal))
        {
            return false;
        }

        AdoptPrivateKey(imported);
        return true;
    }

    private async Task HandleMissingServerKeyAsync(string? accountId, CancellationToken token)
    {
        var existingKey = privateKey ?? ImportStoredKey(accountId, out _);
        if (existingKey is not null)
        {
            AepLog.Warning(
                "[Encryption] server reported no key for this account but this device already has one; re-uploading it instead of creating a new key.");
            await ReuploadExistingIdentityAsync(existingKey, accountId, token).ConfigureAwait(false);
            return;
        }

        if (accountId is not null)
        {
            ImportStoredKey(accountId, out var localStatus);
            if (localStatus == LocalKeyStatus.Unreadable)
            {
                LocalKeyUnreadable = true;
                lockedRefreshStreak++;
                AepLog.Warning(
                    "[Encryption] this device holds a key for the account that could not be opened; it is left in place instead of being replaced by a new one.");
                SetState(KeyVaultState.Locked);
                return;
            }
        }

        missingServerKeyStreak++;
        if (missingServerKeyStreak < MissingServerKeyConfirmations)
        {
            AepLog.Warning(
                "[Encryption] the server reported no key for this account; confirming that on the next refresh before creating one.");
            return;
        }

        await ProvisionAsync(0, token).ConfigureAwait(false);
    }

    private async Task<bool> TryAdoptPendingKeyAsync(MyKeysDto bundle, string? accountId)
    {
        if (accountId is null
            || configuration.EncryptionKeyCachePending.Length == 0
            || !string.Equals(configuration.EncryptionKeyCachePendingUserId, accountId, StringComparison.Ordinal))
        {
            return false;
        }

        var pending = ImportBlob(configuration.EncryptionKeyCachePending, accountId, out _);
        if (pending is null
            || !string.Equals(CryptoBox.TryExportPublicKey(pending), bundle.PublicKey, StringComparison.Ordinal))
        {
            return false;
        }

        AepLog.Warning(
            "[Encryption] a key that reached the server but had not finished saving locally was recovered on this device.");
        await PromotePendingKeyAsync(accountId).ConfigureAwait(false);
        ClearKey();
        AdoptPrivateKey(pending);
        return true;
    }

    private EcPrivateKey? ImportStoredKeyForCurrentUser()
    {
        return ImportStoredKey(MyUserId, out _);
    }

    private EcPrivateKey? ImportStoredKey(string? userId, out LocalKeyStatus status)
    {
        status = LocalKeyStatus.Missing;
        if (userId is null)
        {
            return null;
        }

        return ImportBlob(ReadStoredBlob(userId), userId, out status);
    }

    private static EcPrivateKey? ImportBlob(string blob, string userId, out LocalKeyStatus status)
    {
        status = LocalKeyProtector.TryUnprotect(blob, userId, out var pkcs8);
        if (pkcs8 is null)
        {
            return null;
        }

        var imported = CryptoBox.ImportPrivateKey(pkcs8);
        CryptographicOperations.ZeroMemory(pkcs8);
        if (imported is null)
        {
            status = LocalKeyStatus.Unreadable;
        }

        return imported;
    }

    private string ReadStoredBlob(string userId)
    {
        if (configuration.EncryptionKeysByUserId.TryGetValue(userId, out var stored) && stored.Length > 0)
        {
            return stored;
        }

        if (string.Equals(configuration.EncryptionKeyCacheUserId, userId, StringComparison.Ordinal))
        {
            return configuration.EncryptionKeyCache;
        }

        return string.Empty;
    }

    private async Task ReuploadExistingIdentityAsync(EcPrivateKey existingKey, string? accountId, CancellationToken token)
    {
        var publicKey = CryptoBox.TryExportPublicKey(existingKey);
        if (publicKey is null)
        {
            AepLog.Warning("[Encryption] re-uploading the existing key failed: its public key could not be exported.");
            return;
        }

        var pkcs8 = CryptoBox.TryExportPrivateKey(existingKey);
        var code = RecoveryKey.GenerateCode();
        var escrow = pkcs8 is null ? null : RecoveryKey.Wrap(pkcs8, code);
        var (stored, status) = await client.PutMyKeysAsync(
            new PutMyKeysRequest(publicKey, escrow, 0), token).ConfigureAwait(false);
        if (status == 409)
        {
            AepLog.Warning("[Encryption] the server reported a missing key but one exists after all; keeping the server's key.");
            ZeroIfPresent(pkcs8);
            return;
        }

        if (stored is null)
        {
            AepLog.Warning("[Encryption] re-uploading the existing key failed: the server did not accept it; it will be retried.");
            ZeroIfPresent(pkcs8);
            return;
        }

        if (!string.Equals(MyUserId, accountId, StringComparison.Ordinal))
        {
            AepLog.Warning("[Encryption] the account changed while the existing key was being re-uploaded; stopping here.");
            ZeroIfPresent(pkcs8);
            SetState(KeyVaultState.Unavailable);
            return;
        }

        serverBundle = stored;
        if (pkcs8 is not null)
        {
            if (accountId is not null)
            {
                StoreLocalCache(pkcs8, accountId);
            }

            CryptographicOperations.ZeroMemory(pkcs8);
        }

        AdoptPrivateKey(existingKey);

        if (escrow is not null && accountId is not null)
        {
            HoldRecoveryCode(accountId, code);
        }

        SetState(KeyVaultState.Unlocked);
    }

    private static void ZeroIfPresent(byte[]? buffer)
    {
        if (buffer is not null)
        {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }

    private void StoreLocalCache(byte[] pkcs8)
    {
        var userId = MyUserId;
        if (userId is null)
        {
            AepLog.Warning("[Encryption] the key could not be stored because the account is no longer resolved.");
            return;
        }

        StoreLocalCache(pkcs8, userId);
    }

    private void StoreLocalCache(byte[] pkcs8, string userId)
    {
        var protectedBlob = LocalKeyProtector.Protect(pkcs8, userId);
        LocalCacheUnavailable = LocalKeyProtector.IsUnprotected(protectedBlob);
        WriteStoredBlob(protectedBlob, userId);
        configuration.Save();
        session.PersistActiveKeyCache();
    }

    private async Task<bool> StoreAndVerifyAsync(byte[] pkcs8, string userId, string expectedPublicKey)
    {
        var protectedBlob = LocalKeyProtector.Protect(pkcs8, userId);
        LocalCacheUnavailable = LocalKeyProtector.IsUnprotected(protectedBlob);
        configuration.EncryptionKeyCachePending = protectedBlob;
        configuration.EncryptionKeyCachePendingUserId = userId;
        await configuration.SaveNowAsync().ConfigureAwait(false);

        var readBack = ImportBlob(configuration.EncryptionKeyCachePending, userId, out _);
        if (readBack is null
            || !string.Equals(CryptoBox.TryExportPublicKey(readBack), expectedPublicKey, StringComparison.Ordinal))
        {
            AepLog.Error(
                "[Encryption] the new key did not survive a write and read back, so it was not published; the account keeps its current key.");
            ClearPendingKey();
            await configuration.SaveNowAsync().ConfigureAwait(false);
            return false;
        }

        return true;
    }

    private async Task PromotePendingKeyAsync(string userId)
    {
        var pending = configuration.EncryptionKeyCachePending;
        if (pending.Length == 0)
        {
            return;
        }

        WriteStoredBlob(pending, userId);
        ClearPendingKey();
        await configuration.SaveNowAsync().ConfigureAwait(false);
        await session.PersistActiveKeyCacheAsync().ConfigureAwait(false);
    }

    private void WriteStoredBlob(string protectedBlob, string userId)
    {
        RetireStoredBlob(ReadStoredBlob(userId), userId);
        configuration.EncryptionKeysByUserId[userId] = protectedBlob;
        configuration.EncryptionKeyCache = protectedBlob;
        configuration.EncryptionKeyCacheUserId = userId;
    }

    private void RetireStoredBlob(string displaced, string userId)
    {
        if (displaced.Length == 0)
        {
            return;
        }

        if (!configuration.EncryptionRetiredKeysByUserId.TryGetValue(userId, out var retired))
        {
            retired = new List<string>();
            configuration.EncryptionRetiredKeysByUserId[userId] = retired;
        }

        if (IsAlreadyRetired(retired, displaced, userId))
        {
            return;
        }

        retired.Add(displaced);
        while (retired.Count > MaxRetiredKeys)
        {
            retired.RemoveAt(0);
        }

        AepLog.Info(
            $"[Encryption] the key this device is replacing was archived here, so chats sealed to it can still be opened ({retired.Count} kept).");
    }

    private static bool IsAlreadyRetired(List<string> retired, string displaced, string userId)
    {
        var imported = ImportBlob(displaced, userId, out _);
        var displacedPublicKey = imported is null ? null : CryptoBox.TryExportPublicKey(imported);
        for (var index = 0; index < retired.Count; index++)
        {
            if (string.Equals(retired[index], displaced, StringComparison.Ordinal))
            {
                return true;
            }

            if (displacedPublicKey is null)
            {
                continue;
            }

            var archived = ImportBlob(retired[index], userId, out _);
            if (archived is not null
                && string.Equals(CryptoBox.TryExportPublicKey(archived), displacedPublicKey, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void AdoptPrivateKey(EcPrivateKey key)
    {
        privateKey = key;
        LoadRetiredKeys();
    }

    private void LoadRetiredKeys()
    {
        var userId = MyUserId;
        if (userId is null)
        {
            return;
        }

        AdoptLegacyRetiredKey(userId);
        if (!configuration.EncryptionRetiredKeysByUserId.TryGetValue(userId, out var retired) || retired.Count == 0)
        {
            return;
        }

        var knownPublicKeys = CollectKnownPublicKeys();
        var loaded = new List<EcPrivateKey>();
        for (var index = 0; index < retired.Count; index++)
        {
            var imported = ImportBlob(retired[index], userId, out _);
            var publicKey = imported is null ? null : CryptoBox.TryExportPublicKey(imported);
            if (imported is null || publicKey is null || !knownPublicKeys.Add(publicKey))
            {
                continue;
            }

            loaded.Add(imported);
        }

        if (loaded.Count == 0)
        {
            return;
        }

        var merged = new EcPrivateKey[recoveredPreviousKeys.Length + loaded.Count];
        recoveredPreviousKeys.CopyTo(merged, 0);
        for (var index = 0; index < loaded.Count; index++)
        {
            merged[recoveredPreviousKeys.Length + index] = loaded[index];
        }

        recoveredPreviousKeys = merged;
        AepLog.Info(
            $"[Encryption] {loaded.Count} archived key(s) on this device were loaded, so chats sealed to them still open.");
    }

    private void AdoptLegacyRetiredKey(string userId)
    {
        if (configuration.EncryptionKeyCachePrevious.Length == 0
            || !string.Equals(configuration.EncryptionKeyCachePreviousUserId, userId, StringComparison.Ordinal))
        {
            return;
        }

        RetireStoredBlob(configuration.EncryptionKeyCachePrevious, userId);
        configuration.EncryptionKeyCachePrevious = string.Empty;
        configuration.EncryptionKeyCachePreviousUserId = string.Empty;
        configuration.Save();
    }

    private void ClearPendingKey()
    {
        configuration.EncryptionKeyCachePending = string.Empty;
        configuration.EncryptionKeyCachePendingUserId = string.Empty;
    }

    private void HoldRecoveryCode(string userId, string code)
    {
        configuration.PendingRecoveryCode = code;
        configuration.PendingRecoveryCodeUserId = userId;
        configuration.EncryptionRecoveryNudgeDismissed = false;
        configuration.EncryptionRecoveryNudgeSnoozedUntilUnix = 0;
        configuration.Save();
    }

    public string? UnsavedRecoveryCode
    {
        get
        {
            var userId = MyUserId;
            if (userId is null
                || configuration.PendingRecoveryCode.Length == 0
                || !string.Equals(configuration.PendingRecoveryCodeUserId, userId, StringComparison.Ordinal))
            {
                return null;
            }

            return configuration.PendingRecoveryCode;
        }
    }

    public void AcknowledgeRecoveryCode()
    {
        if (configuration.PendingRecoveryCode.Length == 0)
        {
            return;
        }

        configuration.PendingRecoveryCode = string.Empty;
        configuration.PendingRecoveryCodeUserId = string.Empty;
        configuration.Save();
        Changed?.Invoke();
    }

    private void EnsureLocalCachePersisted()
    {
        var userId = MyUserId;
        var key = privateKey;
        if (userId is null || key is null)
        {
            return;
        }

        var currentPublicKey = CryptoBox.TryExportPublicKey(key);
        if (currentPublicKey is null)
        {
            AepLog.Warning("[Encryption] verifying the stored key failed: the current public key could not be exported.");
            return;
        }

        var stored = ImportStoredKeyForCurrentUser();
        if (stored is not null
            && string.Equals(CryptoBox.ExportPublicKey(stored), currentPublicKey, StringComparison.Ordinal))
        {
            return;
        }

        var pkcs8 = CryptoBox.TryExportPrivateKey(key);
        if (pkcs8 is null)
        {
            AepLog.Warning("[Encryption] rewriting the stored key failed: the private key could not be exported.");
            return;
        }

        StoreLocalCache(pkcs8);
        CryptographicOperations.ZeroMemory(pkcs8);
    }

    private void ClearKey()
    {
        privateKey = null;
        recoveredPreviousKeys = Array.Empty<EcPrivateKey>();
    }

    private void SetState(KeyVaultState next)
    {
        if (State == next)
        {
            return;
        }

        AepLog.Info($"[Encryption] vault state {State} to {next}.");
        if (next != KeyVaultState.Locked)
        {
            lockedRefreshStreak = 0;
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
            AepLog.Warning($"Key protection unavailable ({exception.GetType().Name}); storing the key with basic protection instead.");
            return RawPrefix + Convert.ToBase64String(secret);
        }
    }

    public static bool IsUnprotected(string stored)
    {
        return stored.StartsWith(RawPrefix, StringComparison.Ordinal);
    }

    public static LocalKeyStatus TryUnprotect(string stored, string userId, out byte[]? secret)
    {
        if (stored.Length == 0)
        {
            secret = null;
            return LocalKeyStatus.Missing;
        }

        secret = Unprotect(stored, userId);
        return secret is null ? LocalKeyStatus.Unreadable : LocalKeyStatus.Opened;
    }

    public static byte[]? Unprotect(string stored, string userId)
    {
        if (stored.StartsWith(RawPrefix, StringComparison.Ordinal))
        {
            try
            {
                return Convert.FromBase64String(stored[RawPrefix.Length..]);
            }
            catch (FormatException exception)
            {
                AepLog.Error(exception, "[Crypto] the unprotected vault blob is not valid base64");
                return null;
            }
        }

        try
        {
            var protectedBytes = Convert.FromBase64String(stored);
            var entropy = Encoding.UTF8.GetBytes(userId);
            return System.Security.Cryptography.ProtectedData.Unprotect(protectedBytes, entropy, DataProtectionScope.CurrentUser);
        }
        catch (Exception exception)
        {
            AepLog.Error(exception,
                "[Crypto] DPAPI could not unprotect the vault; the key was stored by a different Windows user or prefix");
            return null;
        }
    }
}
