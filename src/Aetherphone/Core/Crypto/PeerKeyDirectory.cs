using System.Collections.Concurrent;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Crypto;

internal sealed class PeerKeyDirectory
{
    private readonly Configuration configuration;
    private readonly KeysClient client;
    private readonly ConcurrentDictionary<string, UserPublicKeyDto> cache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> rotationNotices = new(StringComparer.Ordinal);

    public PeerKeyDirectory(Configuration configuration, KeysClient client)
    {
        this.configuration = configuration;
        this.client = client;
    }

    public UserPublicKeyDto? Cached(string userId)
    {
        return cache.TryGetValue(userId, out var key) ? key : null;
    }

    public bool HasRotationNotice(string userId)
    {
        return rotationNotices.ContainsKey(userId);
    }

    public void ClearRotationNotice(string userId)
    {
        rotationNotices.TryRemove(userId, out _);
    }

    public async Task<IReadOnlyDictionary<string, UserPublicKeyDto>> ResolveAsync(
        IReadOnlyList<string> userIds, CancellationToken token, bool forceRefresh = false)
    {
        var result = new Dictionary<string, UserPublicKeyDto>(StringComparer.Ordinal);
        List<string>? missing = null;
        for (var index = 0; index < userIds.Count; index++)
        {
            var userId = userIds[index];
            if (!forceRefresh && cache.TryGetValue(userId, out var cached))
            {
                result[userId] = cached;
            }
            else
            {
                missing ??= new List<string>();
                missing.Add(userId);
            }
        }

        if (missing is null)
        {
            return result;
        }

        var fetched = await client.PublicKeysAsync(missing.ToArray(), token).ConfigureAwait(false);
        if (fetched is null)
        {
            return result;
        }

        var configDirty = false;
        for (var index = 0; index < fetched.Items.Length; index++)
        {
            var item = fetched.Items[index];
            cache[item.UserId] = item;
            result[item.UserId] = item;
            if (configuration.KnownPeerKeyVersions.TryGetValue(item.UserId, out var knownVersion))
            {
                if (item.KeyVersion > knownVersion)
                {
                    rotationNotices[item.UserId] = 1;
                    configuration.KnownPeerKeyVersions[item.UserId] = item.KeyVersion;
                    configDirty = true;
                }
            }
            else
            {
                configuration.KnownPeerKeyVersions[item.UserId] = item.KeyVersion;
                configDirty = true;
            }
        }

        if (configDirty)
        {
            configuration.Save();
        }

        return result;
    }

    public void Invalidate(string userId)
    {
        cache.TryRemove(userId, out _);
    }
}
