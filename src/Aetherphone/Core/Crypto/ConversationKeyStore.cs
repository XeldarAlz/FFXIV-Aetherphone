using System.Collections.Concurrent;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Crypto;

internal sealed record ChatKeyStatus(
    bool VaultUnlocked,
    bool CanEncrypt,
    int CurrentGeneration,
    string[] MembersWithoutKeys)
{
    public static readonly ChatKeyStatus None = new(false, false, 0, Array.Empty<string>());
}

internal sealed class ConversationKeyStore
{
    private readonly KeysClient client;
    private readonly KeyVault vault;
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, byte[]>> keysByScope = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> currentGenerations = new(StringComparer.Ordinal);

    public ConversationKeyStore(KeysClient client, KeyVault vault)
    {
        this.client = client;
        this.vault = vault;
        vault.Changed += OnVaultChanged;
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
            CacheWraps(ChatScope(item.ConversationId), item.CurrentGeneration, item.Wraps);
        }
    }

    public async Task HydrateVelvetAsync(CancellationToken token)
    {
        if (vault.State != KeyVaultState.Unlocked)
        {
            return;
        }

        var bulk = await client.VelvetKeysAsync(token).ConfigureAwait(false);
        if (bulk is null)
        {
            return;
        }

        for (var index = 0; index < bulk.Items.Length; index++)
        {
            var item = bulk.Items[index];
            CacheWraps(VelvetScope(item.ConversationId), item.CurrentGeneration, item.Wraps);
        }
    }

    public async Task<ChatKeyStatus> EnsureVelvetKeysAsync(string otherId, string myUserId, CancellationToken token)
    {
        var scope = VelvetScope(Pair(myUserId, otherId));
        if (vault.State != KeyVaultState.Unlocked)
        {
            return new ChatKeyStatus(false, false, CurrentGeneration(scope), Array.Empty<string>());
        }

        ConversationKeysDto? keys = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            keys = await client.VelvetThreadKeysAsync(otherId, token).ConfigureAwait(false);
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

                if (await CreateVelvetGenerationAsync(otherId, scope, 1, keys.MemberKeys, token).ConfigureAwait(false))
                {
                    keys = keys with { CurrentGeneration = 1 };
                    break;
                }

                continue;
            }

            await FixVelvetWrapsAsync(otherId, scope, keys, token).ConfigureAwait(false);
            break;
        }

        if (keys is null)
        {
            return new ChatKeyStatus(true, false, CurrentGeneration(scope), Array.Empty<string>());
        }

        var canEncrypt = keys.MembersWithoutKeys.Length == 0 && TryGetCek(scope, keys.CurrentGeneration, out _);
        return new ChatKeyStatus(true, canEncrypt, keys.CurrentGeneration, keys.MembersWithoutKeys);
    }

    private async Task<bool> CreateVelvetGenerationAsync(string otherId, string scope, int generation,
        UserPublicKeyDto[] memberKeys, CancellationToken token)
    {
        var cek = CryptoBox.GenerateCek();
        var wraps = BuildWraps(cek, memberKeys);
        if (wraps is null)
        {
            return false;
        }

        var (ok, _) = await client.CreateVelvetGenerationAsync(
            otherId, new CreateGenerationRequest(generation, wraps), token).ConfigureAwait(false);
        if (!ok)
        {
            return false;
        }

        Store(scope, generation, cek);
        return true;
    }

    private async Task FixVelvetWrapsAsync(string otherId, string scope, ConversationKeysDto keys, CancellationToken token)
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
            var recipients = new List<UserPublicKeyDto>();
            for (var index = 0; index < keys.StaleWrapUserIds.Length; index++)
            {
                if (memberKeys.TryGetValue(keys.StaleWrapUserIds[index], out var key))
                {
                    recipients.Add(key);
                }
            }

            if (generation == keys.CurrentGeneration)
            {
                for (var index = 0; index < keys.MissingWrapUserIds.Length; index++)
                {
                    if (memberKeys.TryGetValue(keys.MissingWrapUserIds[index], out var key))
                    {
                        recipients.Add(key);
                    }
                }
            }

            if (recipients.Count == 0)
            {
                continue;
            }

            var wraps = BuildWraps(cek, recipients);
            if (wraps is null)
            {
                continue;
            }

            await client.AddVelvetWrapsAsync(otherId, new AddWrapsRequest(generation, wraps), token).ConfigureAwait(false);
        }
    }

    public async Task HydrateGramAsync(CancellationToken token)
    {
        if (vault.State != KeyVaultState.Unlocked)
        {
            return;
        }

        var bulk = await client.GramKeysAsync(token).ConfigureAwait(false);
        if (bulk is null)
        {
            return;
        }

        for (var index = 0; index < bulk.Items.Length; index++)
        {
            var item = bulk.Items[index];
            CacheWraps(GramScope(item.ConversationId), item.CurrentGeneration, item.Wraps);
        }
    }

    public async Task<ChatKeyStatus> EnsureGramKeysAsync(string otherId, string myUserId, CancellationToken token)
    {
        var scope = GramScope(Pair(myUserId, otherId));
        if (vault.State != KeyVaultState.Unlocked)
        {
            return new ChatKeyStatus(false, false, CurrentGeneration(scope), Array.Empty<string>());
        }

        ConversationKeysDto? keys = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            keys = await client.GramThreadKeysAsync(otherId, token).ConfigureAwait(false);
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

                if (await CreateGramGenerationAsync(otherId, scope, 1, keys.MemberKeys, token).ConfigureAwait(false))
                {
                    keys = keys with { CurrentGeneration = 1 };
                    break;
                }

                continue;
            }

            await FixGramWrapsAsync(otherId, scope, keys, token).ConfigureAwait(false);
            break;
        }

        if (keys is null)
        {
            return new ChatKeyStatus(true, false, CurrentGeneration(scope), Array.Empty<string>());
        }

        var canEncrypt = keys.MembersWithoutKeys.Length == 0 && TryGetCek(scope, keys.CurrentGeneration, out _);
        return new ChatKeyStatus(true, canEncrypt, keys.CurrentGeneration, keys.MembersWithoutKeys);
    }

    private async Task<bool> CreateGramGenerationAsync(string otherId, string scope, int generation,
        UserPublicKeyDto[] memberKeys, CancellationToken token)
    {
        var cek = CryptoBox.GenerateCek();
        var wraps = BuildWraps(cek, memberKeys);
        if (wraps is null)
        {
            return false;
        }

        var (ok, _) = await client.CreateGramGenerationAsync(
            otherId, new CreateGenerationRequest(generation, wraps), token).ConfigureAwait(false);
        if (!ok)
        {
            return false;
        }

        Store(scope, generation, cek);
        return true;
    }

    private async Task FixGramWrapsAsync(string otherId, string scope, ConversationKeysDto keys, CancellationToken token)
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
            var recipients = new List<UserPublicKeyDto>();
            for (var index = 0; index < keys.StaleWrapUserIds.Length; index++)
            {
                if (memberKeys.TryGetValue(keys.StaleWrapUserIds[index], out var key))
                {
                    recipients.Add(key);
                }
            }

            if (generation == keys.CurrentGeneration)
            {
                for (var index = 0; index < keys.MissingWrapUserIds.Length; index++)
                {
                    if (memberKeys.TryGetValue(keys.MissingWrapUserIds[index], out var key))
                    {
                        recipients.Add(key);
                    }
                }
            }

            if (recipients.Count == 0)
            {
                continue;
            }

            var wraps = BuildWraps(cek, recipients);
            if (wraps is null)
            {
                continue;
            }

            await client.AddGramWrapsAsync(otherId, new AddWrapsRequest(generation, wraps), token).ConfigureAwait(false);
        }
    }

    public async Task<ChatKeyStatus> EnsureChatKeysAsync(string conversationId, CancellationToken token)
    {
        var scope = ChatScope(conversationId);
        if (vault.State != KeyVaultState.Unlocked)
        {
            return new ChatKeyStatus(false, false, CurrentGeneration(scope), Array.Empty<string>());
        }

        ConversationKeysDto? keys = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            keys = await client.ConversationKeysAsync(conversationId, token).ConfigureAwait(false);
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

                if (await CreateGenerationAsync(conversationId, scope, 1, keys.MemberKeys, token).ConfigureAwait(false))
                {
                    keys = keys with { CurrentGeneration = 1 };
                    break;
                }

                continue;
            }

            if (keys.NeedsNewGeneration && keys.MemberKeys.Length > 0)
            {
                var nextGeneration = keys.CurrentGeneration + 1;
                if (await CreateGenerationAsync(conversationId, scope, nextGeneration, keys.MemberKeys, token).ConfigureAwait(false))
                {
                    keys = keys with { CurrentGeneration = nextGeneration };
                    break;
                }

                continue;
            }

            await FixWrapsAsync(conversationId, scope, keys, token).ConfigureAwait(false);
            break;
        }

        if (keys is null)
        {
            return new ChatKeyStatus(true, false, CurrentGeneration(scope), Array.Empty<string>());
        }

        var canEncrypt = keys.MembersWithoutKeys.Length == 0 && TryGetCek(scope, keys.CurrentGeneration, out _);
        return new ChatKeyStatus(true, canEncrypt, keys.CurrentGeneration, keys.MembersWithoutKeys);
    }

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

    private async Task<bool> CreateGenerationAsync(string conversationId, string scope, int generation,
        UserPublicKeyDto[] memberKeys, CancellationToken token)
    {
        var cek = CryptoBox.GenerateCek();
        var wraps = BuildWraps(cek, memberKeys);
        if (wraps is null)
        {
            return false;
        }

        var (ok, _) = await client.CreateConversationGenerationAsync(
            conversationId, new CreateGenerationRequest(generation, wraps), token).ConfigureAwait(false);
        if (!ok)
        {
            return false;
        }

        Store(scope, generation, cek);
        return true;
    }

    private async Task FixWrapsAsync(string conversationId, string scope, ConversationKeysDto keys, CancellationToken token)
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
            var recipients = new List<UserPublicKeyDto>();
            for (var index = 0; index < keys.StaleWrapUserIds.Length; index++)
            {
                if (memberKeys.TryGetValue(keys.StaleWrapUserIds[index], out var key))
                {
                    recipients.Add(key);
                }
            }

            if (generation == keys.CurrentGeneration)
            {
                for (var index = 0; index < keys.MissingWrapUserIds.Length; index++)
                {
                    if (memberKeys.TryGetValue(keys.MissingWrapUserIds[index], out var key))
                    {
                        recipients.Add(key);
                    }
                }
            }

            if (recipients.Count == 0)
            {
                continue;
            }

            var wraps = BuildWraps(cek, recipients);
            if (wraps is null)
            {
                continue;
            }

            await client.AddConversationWrapsAsync(conversationId, new AddWrapsRequest(generation, wraps), token).ConfigureAwait(false);
        }
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

            var cek = vault.UnwrapCek(wrap.WrappedKey);
            if (cek is not null)
            {
                generations[wrap.Generation] = cek;
            }
        }
    }

    private void Store(string scopeId, int generation, byte[] cek)
    {
        var generations = keysByScope.GetOrAdd(scopeId, _ => new ConcurrentDictionary<int, byte[]>());
        generations[generation] = cek;
        currentGenerations[scopeId] = generation;
    }

    private void OnVaultChanged()
    {
        if (vault.State != KeyVaultState.Unlocked)
        {
            Clear();
        }
    }
}
