using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Message;
using Aetherphone.Core.Net;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Runtime;
using Aetherphone.Core.Social;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetStore
{
    public void OpenProfile(string userId)
    {
        if (profileUserId == userId && (profileUser is not null || profileLoading))
        {
            if (profileUser is not null && !profileLoading && !profileRevalidating)
            {
                RevalidateProfile(userId);
            }

            return;
        }

        profileUserId = userId;
        profileUser = null;
        profileFailed = false;
        profileLoading = true;
        work.Run("profile open", async token =>
        {
            var user = await client.UserAsync(userId, token).ConfigureAwait(false);
            if (profileUserId != userId)
            {
                return;
            }

            if (user is null)
            {
                profileFailed = true;
            }
            else
            {
                profileUser = user;
            }
        }, () =>
        {
            if (profileUserId == userId)
            {
                profileLoading = false;
            }
        });
    }

    private void RevalidateProfile(string userId)
    {
        profileRevalidating = true;
        work.Run("profile revalidate", async token =>
        {
            var user = await client.UserAsync(userId, token).ConfigureAwait(false);
            if (profileUserId != userId)
            {
                return;
            }

            if (user is not null)
            {
                profileUser = user;
            }
        }, () => profileRevalidating = false);
    }

    public void Connect(string userId)
    {
        sentRequestsLoaded = false;
        SetConnectionStateEverywhere(userId, VelvetConnectionState.OutgoingRequest);
        work.Run("connect", async token => await client.ConnectAsync(userId, string.Empty, token).ConfigureAwait(false));
    }

    public void Connect(string userId, string intro)
    {
        sentRequestsLoaded = false;
        SetConnectionStateEverywhere(userId, VelvetConnectionState.OutgoingRequest);
        work.Run("connect", async token => await client.ConnectAsync(userId, intro, token).ConfigureAwait(false));
    }

    public void SendIntro(string userId, string intro, Action<bool> onComplete)
    {
        var trimmed = intro.Trim();
        if (trimmed.Length == 0 || introBusy)
        {
            return;
        }

        introBusy = true;
        sentRequestsLoaded = false;
        SetConnectionStateEverywhere(userId, VelvetConnectionState.OutgoingRequest);
        work.Run("intro", async token =>
        {
            await client.ConnectAsync(userId, trimmed, token).ConfigureAwait(false);
            var status = await keys.EnsureVelvetKeysAsync(userId, MyUserId, token).ConfigureAwait(false);
            var scope = ScopeFor(userId);
            var encoded = default(EncryptedOutbound);
            var encrypted = status.CanEncrypt
                && cipher.TryEncrypt(scope, status.CurrentGeneration, trimmed, MyUserId, out encoded);
            if (DowngradeBlocked(userId, "intro", encrypted, status))
            {
                return false;
            }

            if (encrypted)
            {
                var sent = await SendMessageRequestAsync(userId, encoded.Envelope, 0, token, null, 0, 0,
                    EnvelopeCodec.VersionEnvelope, encoded.CommitmentTag, null, 0).ConfigureAwait(false);
                if (sent is not null)
                {
                    cipher.RecordDecrypted(sent.Id, trimmed, encoded.FrankingKeyBase64);
                }
            }
            else
            {
                await SendMessageRequestAsync(userId, trimmed, 0, token, null, 0, 0, 0, null, null, 0)
                    .ConfigureAwait(false);
            }

            return true;
        }, onComplete, () => introBusy = false);
    }

    public void Disconnect(string userId)
    {
        ForgetConnection(userId, VelvetConnectionState.None);
        work.Run("disconnect", async token => await client.DisconnectAsync(userId, token).ConfigureAwait(false));
    }

    public void Block(string userId, Action<bool> onComplete, Action<AepFailure>? onFailure = null)
    {
        blockedLoaded = false;
        ForgetConnection(userId, VelvetConnectionState.Blocked);
        HideFromDiscover(userId);
        work.Run("block", async token =>
        {
            var blocked = await safety.BlockAsync(userId, token, onFailure).ConfigureAwait(false);
            if (!blocked)
            {
                AepLog.Warning($"Velvet block of {userId} failed; the local hide is being undone");
                RefreshConnections();
                RefreshDiscover(discoverFilter, discoverTags, discoverRegion);
            }

            return blocked;
        }, onComplete);
    }

    public void HideFromDiscover(string userId)
    {
        notInterestedLoaded = false;
        discoverResults = RemoveDiscover(discoverResults, userId);

        var accountId = MyUserId;
        var epoch = accountEpoch;
        work.Run("discover not interested save", async token =>
        {
            await EnsureNotInterestedLoadedAsync(token).ConfigureAwait(false);
            if (epoch != accountEpoch)
            {
                return;
            }

            var current = notInterestedIds;
            if (!current.Contains(userId))
            {
                notInterestedIds = new HashSet<string>(current, StringComparer.Ordinal) { userId };
            }

            await Task.Run(() => notInterestedArchive.Save(accountId, notInterestedIds, passedAt), token)
                .ConfigureAwait(false);
        });
    }

    public void PassFromDiscover(string userId)
    {
        discoverResults = RemoveDiscover(discoverResults, userId);

        var accountId = MyUserId;
        var epoch = accountEpoch;
        var stamp = UnixNow();
        work.Run("discover pass save", async token =>
        {
            await EnsureNotInterestedLoadedAsync(token).ConfigureAwait(false);
            if (epoch != accountEpoch)
            {
                return;
            }

            passedAt = new Dictionary<string, long>(passedAt, StringComparer.Ordinal) { [userId] = stamp };
            await Task.Run(() => notInterestedArchive.Save(accountId, notInterestedIds, passedAt), token)
                .ConfigureAwait(false);
        });
    }

    public void CopyPassedIds(HashSet<string> into)
    {
        var passes = passedAt;
        into.Clear();
        foreach (var userId in passes.Keys)
        {
            into.Add(userId);
        }
    }

    public void ClearPasses()
    {
        if (passedAt.Count == 0)
        {
            return;
        }

        passedAt = EmptyPasses;
        discoverLoaded = false;

        var accountId = MyUserId;
        var epoch = accountEpoch;
        work.Run("discover passes clear", async token =>
        {
            await EnsureNotInterestedLoadedAsync(token).ConfigureAwait(false);
            if (epoch != accountEpoch)
            {
                return;
            }

            passedAt = EmptyPasses;
            await Task.Run(() => notInterestedArchive.Save(accountId, notInterestedIds, passedAt), token)
                .ConfigureAwait(false);
        });
    }

    public void RemoveFromNotInterested(string userId)
    {
        notInterested = RemoveProfile(notInterested, userId);
        discoverLoaded = false;
        ForgetNotInterested(userId);
    }

    private void ForgetNotInterested(string userId)
    {
        var accountId = MyUserId;
        var epoch = accountEpoch;
        work.Run("discover not interested remove", async token =>
        {
            await EnsureNotInterestedLoadedAsync(token).ConfigureAwait(false);
            if (epoch != accountEpoch)
            {
                return;
            }

            var current = notInterestedIds;
            if (current.Contains(userId))
            {
                var trimmed = new HashSet<string>(current, StringComparer.Ordinal);
                trimmed.Remove(userId);
                notInterestedIds = trimmed;
            }

            var passes = passedAt;
            if (passes.ContainsKey(userId))
            {
                var trimmedPasses = new Dictionary<string, long>(passes, StringComparer.Ordinal);
                trimmedPasses.Remove(userId);
                passedAt = trimmedPasses;
            }

            await Task.Run(() => notInterestedArchive.Save(accountId, notInterestedIds, passedAt), token)
                .ConfigureAwait(false);
        });
    }

    private static VelvetProfileDto[] RemoveDiscover(VelvetProfileDto[] source, string userId)
    {
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index].UserId != userId)
            {
                continue;
            }

            var trimmed = new VelvetProfileDto[source.Length - 1];
            Array.Copy(source, trimmed, index);
            Array.Copy(source, index + 1, trimmed, index, source.Length - index - 1);
            return trimmed;
        }

        return source;
    }

    public void Unblock(string userId)
    {
        blocked = CopyOnWrite.RemoveById(blocked, userId);
        SetConnectionStateEverywhere(userId, VelvetConnectionState.None);
        work.Run("unblock", async token => await safety.UnblockAsync(userId, token).ConfigureAwait(false));
    }

    public void RefreshBlocked()
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        loadingBlocked = true;
        work.Run("blocked", async token =>
        {
            var page = await safety.BlockedUsersAsync(token).ConfigureAwait(false);
            if (page is not null)
            {
                blocked = page.Users;
            }
        }, () =>
        {
            loadingBlocked = false;
            blockedLoaded = true;
        });
    }

    public void RefreshNotInterested()
    {
        if (!session.IsSignedIn || loadingNotInterested)
        {
            return;
        }

        loadingNotInterested = true;
        var epoch = accountEpoch;
        work.Run("notInterested", async token =>
        {
            try
            {
                await EnsureNotInterestedLoadedAsync(token).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Velvet not-interested ids load failed");
                return false;
            }

            if (epoch != accountEpoch)
            {
                return false;
            }

            var ids = notInterestedIds;
            var list = new List<VelvetProfileDto>(ids.Count);
            foreach (var id in ids)
            {
                var user = await client.UserAsync(id, token).ConfigureAwait(false);
                if (epoch != accountEpoch)
                {
                    return false;
                }

                if (user is not null)
                {
                    list.Add(user);
                }
            }

            notInterested = list.ToArray();
            return true;
        }, succeeded =>
        {
            if (epoch == accountEpoch && succeeded)
            {
                notInterestedLoaded = true;
            }

            loadingNotInterested = false;
        });
    }
}
