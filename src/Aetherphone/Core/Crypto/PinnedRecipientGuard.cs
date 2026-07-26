using System.Collections.Concurrent;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Crypto;

internal sealed class PinnedRecipientGuard : IWrapRecipientGuard
{
    private readonly ConcurrentDictionary<string, byte> authorizedMembers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, (int Version, string PublicKey)> pins = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<(string UserId, string Reason)> rejections = new();
    private volatile bool rosterConfigured;

    public IReadOnlyCollection<(string UserId, string Reason)> Rejections => rejections.ToArray();

    public void SetAuthorizedMembers(params string[] userIds)
    {
        authorizedMembers.Clear();
        for (var index = 0; index < userIds.Length; index++)
        {
            authorizedMembers[userIds[index]] = 1;
        }

        rosterConfigured = true;
    }

    public void Pin(string userId, int keyVersion, string publicKey)
    {
        pins[userId] = (keyVersion, publicKey);
    }

    public bool IsAuthorized(UserPublicKeyDto recipient)
    {
        if (rosterConfigured && !authorizedMembers.ContainsKey(recipient.UserId))
        {
            rejections.Enqueue((recipient.UserId, "not an authorized member"));
            return false;
        }

        if (pins.TryGetValue(recipient.UserId, out var pinned))
        {
            if (recipient.KeyVersion > pinned.Version)
            {
                pins[recipient.UserId] = (recipient.KeyVersion, recipient.PublicKey);
                return true;
            }

            if (!string.Equals(pinned.PublicKey, recipient.PublicKey, StringComparison.Ordinal))
            {
                rejections.Enqueue((recipient.UserId, "key material changed"));
                return false;
            }

            return true;
        }

        pins[recipient.UserId] = (recipient.KeyVersion, recipient.PublicKey);
        return true;
    }
}
