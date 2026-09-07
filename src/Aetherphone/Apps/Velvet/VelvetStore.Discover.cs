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
    public void RefreshDiscover(VelvetDiscoverFilter filter, string tags, string region)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var epoch = ++discoverEpoch;
        discoverFilter = filter;
        discoverTags = tags;
        discoverRegion = region;
        discoverCursor = null;
        loadingDiscover = true;
        EnsureNotInterestedLoaded();
        work.Run("discover", async token =>
        {
            var reported = AepFailure.None;
            var page = await client.DiscoverAsync(filter, tags, region, null, token, failure => reported = failure)
                .ConfigureAwait(false);
            if (epoch != discoverEpoch)
            {
                return;
            }

            if (page is null)
            {
                discoverFailureBox = new AepFailureBox(reported.Failed
                    ? reported
                    : AepFailure.Transport(AepFailureKind.Offline));
                AepLog.Warning($"Velvet discover failed: {discoverFailureBox.Failure.Describe()}");
                return;
            }

            discoverFailureBox = null;
            discoverResults = WithoutNotInterested(page.Users);
            discoverCursor = page.NextCursor;
            AepLog.Info($"Velvet discover returned {page.Users.Length} profiles, kept {discoverResults.Length} "
                + $"after {passedAt.Count} local passes");
        }, () =>
        {
            loadingDiscover = false;
            discoverLoaded = true;
        });
    }

    public void LoadMoreDiscover()
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var cursor = discoverCursor;
        if (cursor is null || loadingMoreDiscover || loadingDiscover)
        {
            return;
        }

        var epoch = discoverEpoch;
        loadingMoreDiscover = true;
        work.Run("discover more", async token =>
        {
            var page = await client.DiscoverAsync(discoverFilter, discoverTags, discoverRegion, cursor, token)
                .ConfigureAwait(false);
            if (page is not null && epoch == discoverEpoch)
            {
                discoverResults = AppendUniqueDiscover(discoverResults, WithoutNotInterested(page.Users));
                discoverCursor = page.Users.Length > 0 ? page.NextCursor : null;
                AepLog.Info($"Velvet discover page returned {page.Users.Length} profiles, "
                    + $"deck pool now {discoverResults.Length}");
            }
        }, () => loadingMoreDiscover = false);
    }

    public void SearchPeople(string needle, VelvetDiscoverFilter filter)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var epoch = ++searchEpoch;
        var trimmed = needle.Trim();
        if (trimmed.Length == 0)
        {
            searchResults = Array.Empty<VelvetProfileDto>();
            loadingSearch = false;
            searchLoaded = false;
            return;
        }

        loadingSearch = true;
        work.Run("people search", async token =>
        {
            var page = await client.DiscoverAsync(filter, trimmed, string.Empty, null, token).ConfigureAwait(false);
            if (page is not null && epoch == searchEpoch)
            {
                searchResults = page.Users;
            }
        }, () =>
        {
            if (epoch != searchEpoch)
            {
                return;
            }

            loadingSearch = false;
            searchLoaded = true;
        });
    }

    public void ClearSearch()
    {
        searchEpoch++;
        searchResults = Array.Empty<VelvetProfileDto>();
        loadingSearch = false;
        searchLoaded = false;
    }

    public void RestoreToDiscover(VelvetProfileDto profile)
    {
        discoverResults = PrependDiscover(discoverResults, profile);
        ForgetNotInterested(profile.UserId);
    }

    private static VelvetProfileDto[] PrependDiscover(VelvetProfileDto[] existing, VelvetProfileDto profile)
    {
        var merged = new VelvetProfileDto[existing.Length + 1];
        merged[0] = profile;
        var cursor = 1;
        for (var index = 0; index < existing.Length; index++)
        {
            if (existing[index].UserId != profile.UserId)
            {
                merged[cursor] = existing[index];
                cursor++;
            }
        }

        if (cursor == merged.Length)
        {
            return merged;
        }

        var trimmed = new VelvetProfileDto[cursor];
        Array.Copy(merged, trimmed, cursor);
        return trimmed;
    }

    private VelvetProfileDto[] WithoutNotInterested(VelvetProfileDto[] incoming)
    {
        var notInterested = notInterestedIds;
        var passes = passedAt;
        if (notInterested.Count == 0 && passes.Count == 0)
        {
            return incoming;
        }

        var now = UnixNow();
        var kept = new VelvetProfileDto[incoming.Length];
        var count = 0;
        for (var index = 0; index < incoming.Length; index++)
        {
            var userId = incoming[index].UserId;
            if (!notInterested.Contains(userId) && !PassActive(passes, userId, now))
            {
                kept[count] = incoming[index];
                count++;
            }
        }

        if (count == incoming.Length)
        {
            return incoming;
        }

        var trimmed = new VelvetProfileDto[count];
        Array.Copy(kept, trimmed, count);
        return trimmed;
    }

    private static VelvetProfileDto[] AppendUniqueDiscover(VelvetProfileDto[] existing, VelvetProfileDto[] incoming)
    {
        if (incoming.Length == 0)
        {
            return existing;
        }

        var seen = new HashSet<string>(existing.Length + incoming.Length);
        for (var index = 0; index < existing.Length; index++)
        {
            seen.Add(existing[index].UserId);
        }

        var picked = new VelvetProfileDto[incoming.Length];
        var count = 0;
        for (var index = 0; index < incoming.Length; index++)
        {
            if (seen.Add(incoming[index].UserId))
            {
                picked[count] = incoming[index];
                count++;
            }
        }

        if (count == 0)
        {
            return existing;
        }

        var merged = new VelvetProfileDto[existing.Length + count];
        Array.Copy(existing, merged, existing.Length);
        Array.Copy(picked, 0, merged, existing.Length, count);
        return merged;
    }

    public void RefreshConnections()
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        loadingConnections = true;
        work.Run("connections", async token =>
        {
            var page = await client.ConnectionsAsync(null, token).ConfigureAwait(false);
            if (page is not null)
            {
                connections = page.Items;
            }
        }, () =>
        {
            loadingConnections = false;
            connectionsLoaded = true;
        });
    }

    public void Heartbeat(bool? isLalafell, int raceId)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var offset = SocialTimeZone.EffectiveOffsetMinutes(configuration);
        work.Run("heartbeat", async token =>
            await client.HeartbeatAsync(offset, isLalafell, raceId, token).ConfigureAwait(false));
    }

    public void RefreshRequests()
    {
        if (!session.IsSignedIn || loadingRequests)
        {
            return;
        }

        loadingRequests = true;
        work.Run("requests", async token =>
        {
            var page = await client.RequestsAsync(token).ConfigureAwait(false);
            if (page is not null)
            {
                requests = page.Items;
            }
        }, () =>
        {
            loadingRequests = false;
            requestsLoaded = true;
        });
    }

    public void RefreshSentRequests()
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        loadingSentRequests = true;
        work.Run("sent requests", async token =>
        {
            var page = await client.SentRequestsAsync(token).ConfigureAwait(false);
            if (page is not null)
            {
                sentRequests = page.Items;
            }
        }, () =>
        {
            loadingSentRequests = false;
            sentRequestsLoaded = true;
        });
    }

    public void AcceptRequest(string userId)
    {
        var index = Array.FindIndex(requests, item => item.UserId == userId);
        var accepted = index >= 0 ? requests[index] : null;
        requests = RemoveConnection(requests, userId);
        if (accepted is not null)
        {
            connections = CopyOnWrite.Append(RemoveConnection(connections, userId),
                accepted with { State = VelvetConnectionState.Connected });
        }

        connectionsLoaded = false;
        SetConnectionStateEverywhere(userId, VelvetConnectionState.Connected);
        work.Run("accept", async token => await client.ConnectAsync(userId, string.Empty, token).ConfigureAwait(false));
    }

    public void DeclineRequest(string userId)
    {
        requests = RemoveConnection(requests, userId);
        SetConnectionStateEverywhere(userId, VelvetConnectionState.None);
        work.Run("decline", async token => await client.DeclineRequestAsync(userId, token).ConfigureAwait(false));
    }

    public void CancelRequest(string userId)
    {
        sentRequests = RemoveConnection(sentRequests, userId);
        SetConnectionStateEverywhere(userId, VelvetConnectionState.None);
        work.Run("cancel request",
            async token => await client.DisconnectAsync(userId, token).ConfigureAwait(false));
    }
}
