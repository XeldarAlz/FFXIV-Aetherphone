using System.Collections.Concurrent;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Crypto;

internal sealed record ChatKeyStatus(
    bool VaultUnlocked,
    bool CanEncrypt,
    int CurrentGeneration,
    string[] MembersWithoutKeys,
    bool Known = true)
{
    public static readonly ChatKeyStatus None = new(false, false, 0, Array.Empty<string>(), false);
}

internal sealed class ConversationKeyStore
{
    private readonly KeysClient client;
    private readonly KeyVault vault;
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, byte[]>> keysByScope = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> currentGenerations = new(StringComparer.Ordinal);
    private readonly UnwrapFailureCache failedUnwraps = new();
    private readonly ConcurrentDictionary<(string ScopeId, int Generation), byte> scheduledSelfRepairs = new();
    private readonly ConcurrentDictionary<(string ScopeId, int Generation), DateTime> unreadableRekeys = new();
    private readonly ConcurrentDictionary<string, DateTime> previewHydrateRequests = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> hydratedScopes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<(string ScopeId, string UserId, int KeyVersion), byte> healedTargets = new();
    private readonly ConcurrentDictionary<string, byte> healsInFlight = new(StringComparer.Ordinal);
    private static readonly TimeSpan PreviewHydrateCooldown = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan UnreadableRekeyCooldown = TimeSpan.FromMinutes(2);

    private static readonly (string Name, bool RotatesGenerations)[] PairSurfaceSpecs =
    {
        ("velvet", true), ("gram", true), ("ads", false), ("chirper", true),
    };

    private readonly KeySurface chatSurface;
    private readonly Dictionary<string, KeySurface> pairSurfaces = new(StringComparer.Ordinal);

    private readonly record struct KeySurface(
        Func<string, CancellationToken, Task<ConversationKeysDto?>> ThreadKeys,
        Func<string, CreateGenerationRequest, CancellationToken, Task<(bool Ok, int Status)>> NewGeneration,
        Func<string, AddWrapsRequest, CancellationToken, Task<bool>> AddWraps,
        bool RotatesGenerations);

    public ConversationKeyStore(KeysClient client, KeyVault vault, RealtimeSignalBus signals)
    {
        this.client = client;
        this.vault = vault;
        chatSurface = new KeySurface(
            (id, token) => client.ConversationKeysAsync(id, token),
            (id, request, token) => client.CreateConversationGenerationAsync(id, request, token),
            (id, request, token) => client.AddConversationWrapsAsync(id, request, token),
            RotatesGenerations: true);
        for (var index = 0; index < PairSurfaceSpecs.Length; index++)
        {
            var name = PairSurfaceSpecs[index].Name;
            pairSurfaces[name] = new KeySurface(
                (id, token) => client.PairThreadKeysAsync(name, id, token),
                (id, request, token) => client.CreatePairGenerationAsync(name, id, request, token),
                (id, request, token) => client.AddPairWrapsAsync(name, id, request, token),
                PairSurfaceSpecs[index].RotatesGenerations);
        }

        vault.Changed += OnVaultChanged;
        vault.PreviousKeysRestored += OnPreviousKeysRestored;
        signals.KeysWentStale += OnKeysWentStale;
    }

    public static string ChatScope(string conversationId)
    {
        return "chat:" + conversationId;
    }

    public static string VelvetScope(string pairKey)
    {
        return "velvet:" + pairKey;
    }

    public static string GramScope(string pairKey)
    {
        return "gram:" + pairKey;
    }

    public static string AdScope(string pairKey)
    {
        return "ads:" + pairKey;
    }

    public static string PairScope(string surface, string pairKey)
    {
        return surface + ":" + pairKey;
    }

    public static string Pair(string firstUserId, string secondUserId)
    {
        return string.CompareOrdinal(firstUserId, secondUserId) <= 0
            ? $"{firstUserId}:{secondUserId}"
            : $"{secondUserId}:{firstUserId}";
    }

    public bool TryGetCek(string scopeId, int generation, out byte[] cek)
    {
        if (keysByScope.TryGetValue(scopeId, out var generations) && generations.TryGetValue(generation, out var stored))
        {
            cek = stored;
            return true;
        }

        cek = Array.Empty<byte>();
        return false;
    }

    public int CurrentGeneration(string scopeId)
    {
        return currentGenerations.GetValueOrDefault(scopeId);
    }

    public void Clear()
    {
        keysByScope.Clear();
        currentGenerations.Clear();
        failedUnwraps.Clear();
        scheduledSelfRepairs.Clear();
        unreadableRekeys.Clear();
        hydratedScopes.Clear();
        healedTargets.Clear();
    }

    public bool IsScopeHydrated(string scopeId)
    {
        return hydratedScopes.ContainsKey(scopeId);
    }

    public async Task HydrateAsync(CancellationToken token)
    {
        if (vault.State != KeyVaultState.Unlocked)
        {
            return;
        }

        var bulk = await client.MyConversationKeysAsync(token).ConfigureAwait(false);
        if (bulk is null)
        {
            return;
        }

        for (var index = 0; index < bulk.Items.Length; index++)
        {
            var item = bulk.Items[index];
            var scope = ChatScope(item.ConversationId);
            CacheWraps(scope, item.CurrentGeneration, item.Wraps);
            ScheduleHeal(chatSurface, scope, item.ConversationId, item.HealTargets);
        }
    }

    public async Task HydratePairsAsync(string surface, CancellationToken token)
    {
        if (vault.State != KeyVaultState.Unlocked)
        {
            return;
        }

        var bulk = await client.PairKeysAsync(surface, token).ConfigureAwait(false);
        if (bulk is null)
        {
            return;
        }

        var keySurface = pairSurfaces[surface];
        for (var index = 0; index < bulk.Items.Length; index++)
        {
            var item = bulk.Items[index];
            var scope = PairScope(surface, item.ConversationId);
            CacheWraps(scope, item.CurrentGeneration, item.Wraps);
            if (vault.MyUserId is { } myUserId
                && OtherIdFromPairKey(item.ConversationId, myUserId) is { } otherId)
            {
                ScheduleHeal(keySurface, scope, otherId, item.HealTargets);
            }
        }
    }

    public Task<ChatKeyStatus> EnsurePairKeysAsync(string surface, string otherId, string myUserId,
        CancellationToken token) =>
        EnsureScopeKeysAsync(pairSurfaces[surface], otherId, PairScope(surface, Pair(myUserId, otherId)), token);

    private async Task<ChatKeyStatus> EnsureScopeKeysAsync(KeySurface surface, string remoteId, string scope,
        CancellationToken token)
    {
        if (vault.State != KeyVaultState.Unlocked)
        {
            return await ServerStatusAsync(surface, remoteId, token).ConfigureAwait(false);
        }

        ConversationKeysDto? keys = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            keys = await surface.ThreadKeys(remoteId, token).ConfigureAwait(false);
            if (keys is null)
            {
                break;
            }

            CacheWraps(scope, keys.CurrentGeneration, keys.MyWraps);

            if (keys.CurrentGeneration == 0)
            {
                if (keys.MembersWithoutKeys.Length > 0 || keys.MemberKeys.Length == 0)
                {
                    break;
                }

                if (await CreateGenerationAsync(surface, remoteId, scope, 1, keys.MemberKeys, token)
                        .ConfigureAwait(false))
                {
                    keys = keys with { CurrentGeneration = 1 };
                    break;
                }

                continue;
            }

            if (surface.RotatesGenerations && keys.MemberKeys.Length > 0
                && (keys.NeedsNewGeneration || ShouldRekeyUnreadable(scope, keys.CurrentGeneration)))
            {
                var nextGeneration = keys.CurrentGeneration + 1;
                if (await CreateGenerationAsync(surface, remoteId, scope, nextGeneration, keys.MemberKeys, token)
                        .ConfigureAwait(false))
                {
                    keys = keys with { CurrentGeneration = nextGeneration };
                    break;
                }

                continue;
            }

            await FixWrapsAsync(surface, remoteId, scope, keys, token).ConfigureAwait(false);
            break;
        }

        if (keys is null)
        {
            return new ChatKeyStatus(true, false, CurrentGeneration(scope), Array.Empty<string>(), false);
        }

        var canEncrypt = keys.MembersWithoutKeys.Length == 0 && TryGetCek(scope, keys.CurrentGeneration, out _);
        return new ChatKeyStatus(true, canEncrypt, keys.CurrentGeneration, keys.MembersWithoutKeys);
    }

    private static async Task<ChatKeyStatus> ServerStatusAsync(KeySurface surface, string remoteId,
        CancellationToken token)
    {
        var keys = await surface.ThreadKeys(remoteId, token).ConfigureAwait(false);
        if (keys is null)
        {
            return ChatKeyStatus.None;
        }

        return new ChatKeyStatus(false, false, keys.CurrentGeneration, keys.MembersWithoutKeys);
    }

    private bool ShouldRekeyUnreadable(string scope, int generation)
    {
        if (TryGetCek(scope, generation, out _))
        {
            return false;
        }

        var now = DateTime.UtcNow;
        if (unreadableRekeys.TryGetValue((scope, generation), out var lastAttemptedAt)
            && now - lastAttemptedAt < UnreadableRekeyCooldown)
        {
            return false;
        }

        unreadableRekeys[(scope, generation)] = now;
        AepLog.Warning(
            $"[Crypto] no readable key for {scope} generation {generation}; rolling this conversation to a new generation so it can carry on.");
        return true;
    }

    private async Task<bool> CreateGenerationAsync(KeySurface surface, string remoteId, string scope, int generation,
        UserPublicKeyDto[] memberKeys, CancellationToken token)
    {
        var cek = CryptoBox.GenerateCek();
        var wraps = BuildWraps(cek, memberKeys);
        if (wraps is null)
        {
            return false;
        }

        var (ok, _) = await surface.NewGeneration(
            remoteId, new CreateGenerationRequest(generation, wraps), token).ConfigureAwait(false);
        if (!ok)
        {
            return false;
        }

        Store(scope, generation, cek);
        return true;
    }

    private async Task FixWrapsAsync(KeySurface surface, string remoteId, string scope, ConversationKeysDto keys,
        CancellationToken token)
    {
        if (keys.MissingWrapUserIds.Length == 0 && keys.StaleWrapUserIds.Length == 0)
        {
            return;
        }

        var memberKeys = new Dictionary<string, UserPublicKeyDto>(StringComparer.Ordinal);
        for (var index = 0; index < keys.MemberKeys.Length; index++)
        {
            memberKeys[keys.MemberKeys[index].UserId] = keys.MemberKeys[index];
        }

        if (!keysByScope.TryGetValue(scope, out var generations))
        {
            return;
        }

        foreach (var (generation, cek) in generations)
        {
            var recipients = CollectHealRecipients(keys, memberKeys, generation);

            if (recipients.Count == 0)
            {
                continue;
            }

            var wraps = BuildWraps(cek, recipients);
            if (wraps is null)
            {
                continue;
            }

            await surface.AddWraps(remoteId, new AddWrapsRequest(generation, wraps), token).ConfigureAwait(false);
        }
    }

    public Task<ChatKeyStatus> EnsureChatKeysAsync(string conversationId, CancellationToken token) =>
        EnsureScopeKeysAsync(chatSurface, conversationId, ChatScope(conversationId), token);

    public async Task WrapForMembersAsync(string conversationId, IReadOnlyList<UserPublicKeyDto> recipients, CancellationToken token)
    {
        var scope = ChatScope(conversationId);
        var generation = CurrentGeneration(scope);
        if (generation == 0 || recipients.Count == 0 || !TryGetCek(scope, generation, out var cek))
        {
            return;
        }

        var wraps = BuildWraps(cek, recipients);
        if (wraps is null)
        {
            return;
        }

        await client.AddConversationWrapsAsync(conversationId, new AddWrapsRequest(generation, wraps), token).ConfigureAwait(false);
    }

    private void ScheduleHeal(KeySurface surface, string scopeId, string remoteId, WrapHealTargetDto[]? targets)
    {
        if (targets is null || targets.Length == 0 || vault.State != KeyVaultState.Unlocked)
        {
            return;
        }

        var pending = new List<WrapHealTargetDto>(targets.Length);
        for (var index = 0; index < targets.Length; index++)
        {
            var target = targets[index];
            if (!healedTargets.ContainsKey((scopeId, target.UserId, target.KeyVersion)))
            {
                pending.Add(target);
            }
        }

        if (pending.Count == 0 || !healsInFlight.TryAdd(scopeId, 0))
        {
            return;
        }

        _ = Task.Run(() => HealScopeAsync(surface, scopeId, remoteId, pending));
    }

    private async Task HealScopeAsync(KeySurface surface, string scopeId, string remoteId, List<WrapHealTargetDto> targets)
    {
        try
        {
            var recipientsByGeneration = new Dictionary<int, List<UserPublicKeyDto>>();
            for (var index = 0; index < targets.Count; index++)
            {
                var target = targets[index];
                for (var generationIndex = 0; generationIndex < target.Generations.Length; generationIndex++)
                {
                    var generation = target.Generations[generationIndex];
                    if (!TryGetCek(scopeId, generation, out _))
                    {
                        continue;
                    }

                    if (!recipientsByGeneration.TryGetValue(generation, out var recipients))
                    {
                        recipients = new List<UserPublicKeyDto>();
                        recipientsByGeneration[generation] = recipients;
                    }

                    recipients.Add(new UserPublicKeyDto(target.UserId, target.PublicKey, target.KeyVersion));
                }
            }

            if (recipientsByGeneration.Count == 0)
            {
                return;
            }

            var accepted = 0;
            var attempted = 0;
            foreach (var (generation, recipients) in recipientsByGeneration)
            {
                if (!TryGetCek(scopeId, generation, out var cek))
                {
                    continue;
                }

                var wraps = BuildWraps(cek, recipients);
                if (wraps is null)
                {
                    continue;
                }

                attempted++;
                if (await surface.AddWraps(remoteId, new AddWrapsRequest(generation, wraps), CancellationToken.None)
                        .ConfigureAwait(false))
                {
                    accepted++;
                }
            }

            if (attempted == 0 || accepted != attempted)
            {
                AepLog.Warning(
                    $"[Crypto] handing keys back in {scopeId} covered {accepted} of {attempted} generation(s); the rest retry next hydrate.");
                return;
            }

            for (var index = 0; index < targets.Count; index++)
            {
                healedTargets[(scopeId, targets[index].UserId, targets[index].KeyVersion)] = 1;
            }

            AepLog.Info(
                $"[Crypto] handed the conversation keys for {scopeId} back to {targets.Count} member(s) across {accepted} generation(s).");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"[Crypto] handing conversation keys back in {scopeId} failed");
        }
        finally
        {
            healsInFlight.TryRemove(scopeId, out _);
        }
    }

    private static List<UserPublicKeyDto> CollectHealRecipients(
        ConversationKeysDto keys,
        Dictionary<string, UserPublicKeyDto> memberKeys,
        int generation)
    {
        var recipients = new List<UserPublicKeyDto>();
        var addedUserIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < keys.StaleWrapUserIds.Length; index++)
        {
            if (memberKeys.TryGetValue(keys.StaleWrapUserIds[index], out var key) && addedUserIds.Add(key.UserId))
            {
                recipients.Add(key);
            }
        }

        if (generation != keys.CurrentGeneration)
        {
            return recipients;
        }

        for (var index = 0; index < keys.MissingWrapUserIds.Length; index++)
        {
            if (memberKeys.TryGetValue(keys.MissingWrapUserIds[index], out var key) && addedUserIds.Add(key.UserId))
            {
                recipients.Add(key);
            }
        }

        return recipients;
    }

    private static NewWrapDto[]? BuildWraps(byte[] cek, IReadOnlyList<UserPublicKeyDto> recipients)
    {
        var wraps = new NewWrapDto[recipients.Count];
        for (var index = 0; index < recipients.Count; index++)
        {
            var recipient = recipients[index];
            var wrapped = CryptoBox.WrapCek(cek, recipient.PublicKey);
            if (wrapped is null)
            {
                return null;
            }

            wraps[index] = new NewWrapDto(recipient.UserId, recipient.KeyVersion, wrapped);
        }

        return wraps;
    }

    private void CacheWraps(string scopeId, int currentGeneration, KeyWrapDto[] wraps)
    {
        hydratedScopes[scopeId] = 1;
        if (currentGeneration > 0)
        {
            currentGenerations[scopeId] = currentGeneration;
        }

        for (var index = 0; index < wraps.Length; index++)
        {
            var wrap = wraps[index];
            var generations = keysByScope.GetOrAdd(scopeId, _ => new ConcurrentDictionary<int, byte[]>());
            if (generations.ContainsKey(wrap.Generation))
            {
                continue;
            }

            if (failedUnwraps.ShouldSkip(scopeId, wrap.Generation, wrap.WrappedKey))
            {
                continue;
            }

            var cek = vault.UnwrapCek(wrap.WrappedKey, out var privateKeyWasLoaded);
            if (cek is not null)
            {
                generations[wrap.Generation] = cek;
                failedUnwraps.RecordSuccess(scopeId, wrap.Generation);
                if (wrap.RecipientKeyVersion < vault.KeyVersion)
                {
                    ScheduleSelfRepair(scopeId, wrap.Generation, cek);
                }
            }
            else
            {
                failedUnwraps.RecordFailure(scopeId, wrap.Generation, wrap.WrappedKey, privateKeyWasLoaded);
                AepLog.Warning(
                    $"[Encryption] failed to unwrap key for {scopeId} generation {wrap.Generation} (private key loaded: {privateKeyWasLoaded}).");
            }
        }
    }

    private void Store(string scopeId, int generation, byte[] cek)
    {
        var generations = keysByScope.GetOrAdd(scopeId, _ => new ConcurrentDictionary<int, byte[]>());
        generations[generation] = cek;
        currentGenerations[scopeId] = generation;
    }

    public void RequestPreviewHydrate(string scopeId)
    {
        if (vault.State != KeyVaultState.Unlocked)
        {
            return;
        }

        var separatorIndex = scopeId.IndexOf(':');
        if (separatorIndex <= 0)
        {
            return;
        }

        var surface = scopeId[..separatorIndex];
        var now = DateTime.UtcNow;
        if (previewHydrateRequests.TryGetValue(surface, out var lastRequestedAt)
            && now - lastRequestedAt < PreviewHydrateCooldown)
        {
            return;
        }

        previewHydrateRequests[surface] = now;
        _ = Task.Run(() => HydrateForPreviewAsync(surface));
    }

    private async Task HydrateForPreviewAsync(string surface)
    {
        try
        {
            if (surface == "chat")
            {
                await HydrateAsync(CancellationToken.None).ConfigureAwait(false);
            }
            else if (pairSurfaces.ContainsKey(surface))
            {
                await HydratePairsAsync(surface, CancellationToken.None).ConfigureAwait(false);
            }
            else
            {
                AepLog.Debug($"[Crypto] preview hydrate skipped for unknown surface {surface}.");
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"[Crypto] preview hydrate for {surface} failed");
        }
    }

    private void ScheduleSelfRepair(string scopeId, int generation, byte[] cek)
    {
        if (!scheduledSelfRepairs.TryAdd((scopeId, generation), 0))
        {
            return;
        }

        _ = Task.Run(() => SelfRepairWrapAsync(scopeId, generation, cek));
    }

    private async Task SelfRepairWrapAsync(string scopeId, int generation, byte[] cek)
    {
        try
        {
            var myUserId = vault.MyUserId;
            var myPublicKey = vault.PublicKey;
            var myKeyVersion = vault.KeyVersion;
            if (myUserId is null || myPublicKey is null || myKeyVersion <= 0)
            {
                AepLog.Debug(
                    $"[Crypto] self repair skipped for {scopeId} generation {generation}; the vault is not ready (key version {myKeyVersion})");
                return;
            }

            var wrapped = CryptoBox.WrapCek(cek, myPublicKey);
            if (wrapped is null)
            {
                AepLog.Warning($"[Crypto] self repair failed for {scopeId} generation {generation}; wrapping the key failed");
                return;
            }

            var request = new AddWrapsRequest(generation, new[] { new NewWrapDto(myUserId, myKeyVersion, wrapped) });
            await PostSelfRepairAsync(scopeId, myUserId, request).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"[Crypto] self repair threw for {scopeId} generation {generation}");
        }
    }

    private async Task PostSelfRepairAsync(string scopeId, string myUserId, AddWrapsRequest request)
    {
        const string chatPrefix = "chat:";
        bool accepted;
        if (scopeId.StartsWith(chatPrefix, StringComparison.Ordinal))
        {
            accepted = await client.AddConversationWrapsAsync(scopeId[chatPrefix.Length..], request, CancellationToken.None)
                .ConfigureAwait(false);
        }
        else
        {
            var separatorIndex = scopeId.IndexOf(':');
            if (separatorIndex <= 0 || !pairSurfaces.TryGetValue(scopeId[..separatorIndex], out var surface))
            {
                AepLog.Debug($"[Crypto] self repair skipped for {scopeId}; the surface has no repair route.");
                return;
            }

            var otherId = OtherIdFromPairKey(scopeId[(separatorIndex + 1)..], myUserId);
            if (otherId is null)
            {
                AepLog.Warning($"[Crypto] self repair skipped for {scopeId}; the pair key does not contain this user.");
                return;
            }

            accepted = await surface.AddWraps(otherId, request, CancellationToken.None).ConfigureAwait(false);
        }

        if (!accepted)
        {
            AepLog.Warning($"[Crypto] self repair for {scopeId} generation {request.Generation} was not accepted by the server; it will be retried next session.");
        }
    }

    private static string? OtherIdFromPairKey(string pairKey, string myUserId)
    {
        var separatorIndex = pairKey.IndexOf(':');
        if (separatorIndex < 0)
        {
            return null;
        }

        var first = pairKey[..separatorIndex];
        var second = pairKey[(separatorIndex + 1)..];
        if (string.Equals(first, myUserId, StringComparison.Ordinal))
        {
            return second;
        }

        return string.Equals(second, myUserId, StringComparison.Ordinal) ? first : null;
    }

    private void OnKeysWentStale()
    {
        if (vault.State != KeyVaultState.Unlocked)
        {
            return;
        }

        _ = RetryUnreadableWrapsAsync();
    }

    private void OnPreviousKeysRestored()
    {
        failedUnwraps.Clear();
        _ = RetryUnreadableWrapsAsync();
    }

    private async Task RetryUnreadableWrapsAsync()
    {
        try
        {
            await HydrateAsync(CancellationToken.None).ConfigureAwait(false);
            for (var index = 0; index < PairSurfaceSpecs.Length; index++)
            {
                await HydratePairsAsync(PairSurfaceSpecs[index].Name, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Encryption] refreshing conversation keys after restoring older keys failed");
        }
    }

    private void OnVaultChanged()
    {
        if (vault.State != KeyVaultState.Unlocked)
        {
            Clear();
        }
    }
}
