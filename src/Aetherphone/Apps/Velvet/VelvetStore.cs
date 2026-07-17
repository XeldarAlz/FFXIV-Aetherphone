using System.Collections.Concurrent;
using Aetherphone.Apps.DirectMessages;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Analytics;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Social;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows.Components;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Velvet;

internal sealed class VelvetStore : IDisposable
{
    private const int AvatarSize = 512;
    private const int PostSize = 1080;
    private const int DmImageMaxDimension = 1280;
    private static readonly TimeSpan ForegroundInboxPollInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan BackgroundInboxPollInterval = TimeSpan.FromSeconds(600);
    private static readonly TimeSpan ViewingGrace = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan VaultRetryInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan KeyStatusRetryInterval = TimeSpan.FromSeconds(15);
    private readonly AethernetSession session;
    private readonly AethernetClient client;
    private readonly NotificationService notifications;
    private readonly Configuration configuration;
    private readonly KeyVault vault;
    private readonly ConversationKeyStore keys;
    private readonly RealtimeSignalBus signals;
    private readonly PollCadence inboxCadence;
    private readonly StoreWork work = new StoreWork("Velvet");
    private readonly object messagesLock = new();
    private readonly Dictionary<string, long> inboxLastAt = new();
    private readonly ConcurrentDictionary<string, string> dmMediaUrls = new();
    private readonly ConcurrentDictionary<string, byte> dmMediaLoading = new();
    private readonly ConcurrentDictionary<string, DmDecryptedBody> decryptedBodies = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, (long AtUnix, string Text)> previewCache = new(StringComparer.Ordinal);
    private volatile bool velvetKeysHydrated;
    private volatile bool vaultRefreshRequested;
    private volatile bool vaultRefreshInFlight;
    private DateTime nextVaultRetryUtc = DateTime.MinValue;
    private volatile bool keyStatusRefreshing;
    private DateTime lastKeyStatusUtc = DateTime.MinValue;
    private volatile ChatKeyStatus currentKeyStatus = ChatKeyStatus.None;
    private volatile bool inboxPolling;
    private bool inboxPrimed;
    private volatile string? viewingThreadUserId;
    private DateTime lastViewingUtc = DateTime.MinValue;
    private readonly RetryGate meGate = new RetryGate(TimeSpan.FromSeconds(30));
    private volatile VelvetProfileDto? me;
    private volatile bool loadingMe;
    private volatile bool avatarBusy;
    private volatile VelvetProfileDto[] discoverResults = Array.Empty<VelvetProfileDto>();
    private volatile bool loadingDiscover;
    private volatile bool discoverLoaded;
    private volatile VelvetConnectionDto[] connections = Array.Empty<VelvetConnectionDto>();
    private volatile bool loadingConnections;
    private volatile bool connectionsLoaded;
    private volatile VelvetConnectionDto[] requests = Array.Empty<VelvetConnectionDto>();
    private volatile bool loadingRequests;
    private volatile bool requestsLoaded;
    private volatile VelvetConnectionDto[] sentRequests = Array.Empty<VelvetConnectionDto>();
    private volatile bool loadingSentRequests;
    private volatile bool sentRequestsLoaded;
    private volatile string? profileUserId;
    private volatile VelvetProfileDto? profileUser;
    private volatile bool profileLoading;
    private volatile bool profileFailed;
    private volatile VelvetThreadDto[] threads = Array.Empty<VelvetThreadDto>();
    private volatile bool loadingThreads;
    private volatile bool threadsLoaded;
    private volatile string? threadId;
    private volatile VelvetMessageDto[] messages = Array.Empty<VelvetMessageDto>();
    private volatile string? olderCursor;
    private volatile bool loadingOlder;
    private volatile bool hasMoreOlder;
    private volatile bool loadingThread;
    private volatile bool refreshingThread;
    private volatile bool refreshingTyping;
    private int pollFailureStreak;
    private DateTime pollBackoffUntilUtc = DateTime.MinValue;
    private volatile bool sending;
    private volatile bool otherTyping;
    private volatile VelvetPostDto[] feed = Array.Empty<VelvetPostDto>();
    private volatile bool loadingFeed;
    private volatile string? feedCursor;
    private volatile bool loadingMoreFeed;
    private readonly object feedLock = new();
    private volatile string? detailPostId;
    private volatile VelvetCommentDto[] detailComments = Array.Empty<VelvetCommentDto>();
    private volatile bool loadingComments;
    private volatile bool commenting;
    private volatile bool feedLoaded;
    private volatile bool posting;
    private volatile VelvetPostDto? fetchedPost;
    private volatile string? fetchingPostId;
    private volatile string? likersPostId;
    private volatile UserDto[] likers = Array.Empty<UserDto>();
    private volatile bool likersLoading;
    private volatile bool likersFailed;
    private volatile UserDto[] blocked = Array.Empty<UserDto>();
    private volatile bool loadingBlocked;
    private volatile bool blockedLoaded;

    public VelvetStore(AethernetSession session, AethernetClient client, NotificationService notifications,
        Configuration configuration, KeyVault vault, ConversationKeyStore keys, PhoneVisibility visibility, RealtimeSignalBus signals)
    {
        this.session = session;
        this.client = client;
        this.notifications = notifications;
        this.configuration = configuration;
        this.vault = vault;
        this.keys = keys;
        this.signals = signals;
        inboxCadence = new PollCadence(visibility, ForegroundInboxPollInterval, BackgroundInboxPollInterval);
        signals.VelvetPinged += inboxCadence.RequestImmediate;
        vault.Changed += OnVaultChanged;
        Plugin.Framework.Update += OnFrameworkTick;
    }

    public KeyVaultState VaultState => vault.State;
    public ChatKeyStatus CurrentKeyStatus => currentKeyStatus;
    public bool EncryptingCurrent => vault.State == KeyVaultState.Unlocked && currentKeyStatus.CanEncrypt;

    public DmDecryptedBody DecryptionState(string messageId)
    {
        return decryptedBodies.TryGetValue(messageId, out var state)
            ? state
            : new DmDecryptedBody(DmBodyState.Plain, string.Empty, null, false);
    }

    private void OnVaultChanged()
    {
        decryptedBodies.Clear();
        previewCache.Clear();
        velvetKeysHydrated = false;
        if (vault.State != KeyVaultState.Unlocked)
        {
            currentKeyStatus = ChatKeyStatus.None;
            return;
        }

        work.Run("vault unlocked", async token =>
        {
            await EnsureVelvetHydratedAsync(token).ConfigureAwait(false);
            var current = threadId;
            if (current is not null)
            {
                var status = await keys.EnsureVelvetKeysAsync(current, MyUserId, token).ConfigureAwait(false);
                if (threadId == current)
                {
                    currentKeyStatus = status;
                }
            }

            threadsLoaded = false;
        });
    }

    public MentionSuggestions NewMentionSuggestions() => new(client, work);

    private string MyUserId => session.CurrentUser?.Id ?? string.Empty;

    private async Task EnsureVelvetHydratedAsync(CancellationToken token)
    {
        if (velvetKeysHydrated || vault.State != KeyVaultState.Unlocked)
        {
            return;
        }

        velvetKeysHydrated = true;
        await keys.HydrateVelvetAsync(token).ConfigureAwait(false);
    }

    public void NoteThreadViewed(string userId)
    {
        viewingThreadUserId = userId;
        lastViewingUtc = DateTime.UtcNow;
        notifications.RemoveGroup(userId);
    }

    private void OnFrameworkTick(IFramework framework)
    {
        if (!session.IsSignedIn || !configuration.IsVelvetOnboarded())
        {
            inboxPrimed = false;
            vaultRefreshRequested = false;
            return;
        }

        EnsureVaultRefreshed();
        var now = DateTime.UtcNow;
        EnsureThreadKeysFresh(now);
        if (!inboxCadence.Due(now))
        {
            return;
        }

        PollInbox();
    }

    private void EnsureVaultRefreshed()
    {
        if (session.CurrentUser is null || vaultRefreshInFlight || vault.State == KeyVaultState.Unsupported)
        {
            return;
        }

        if (vaultRefreshRequested
            && (vault.State == KeyVaultState.Unlocked || DateTime.UtcNow < nextVaultRetryUtc))
        {
            return;
        }

        vaultRefreshRequested = true;
        vaultRefreshInFlight = true;
        work.Run("vault refresh", async token =>
        {
            await vault.RefreshAsync(token).ConfigureAwait(false);
            if (vault.State == KeyVaultState.Unlocked)
            {
                await EnsureVelvetHydratedAsync(token).ConfigureAwait(false);
            }
        }, () =>
        {
            nextVaultRetryUtc = DateTime.UtcNow + VaultRetryInterval;
            vaultRefreshInFlight = false;
        });
    }

    private void EnsureThreadKeysFresh(DateTime now)
    {
        var id = threadId;
        if (id is null || keyStatusRefreshing || vault.State != KeyVaultState.Unlocked
            || currentKeyStatus.CanEncrypt || now - lastKeyStatusUtc < KeyStatusRetryInterval)
        {
            return;
        }

        keyStatusRefreshing = true;
        lastKeyStatusUtc = now;
        work.Run("key status refresh", async token =>
        {
            var status = await keys.EnsureVelvetKeysAsync(id, MyUserId, token).ConfigureAwait(false);
            if (threadId == id)
            {
                currentKeyStatus = status;
            }
        }, () => keyStatusRefreshing = false);
    }

    private void PollInbox()
    {
        if (inboxPolling)
        {
            return;
        }

        inboxPolling = true;
        work.Run("inbox poll", async token =>
        {
            await EnsureVelvetHydratedAsync(token).ConfigureAwait(false);
            var page = await client.VelvetThreadsAsync(null, token).ConfigureAwait(false);
            if (page is not null)
            {
                var decorated = DecorateThreads(page.Items);
                threads = decorated;
                RaiseInboxNotifications(decorated);
            }
        }, () => inboxPolling = false);
    }

    private void RaiseInboxNotifications(VelvetThreadDto[] items)
    {
        var primed = inboxPrimed;
        for (var index = 0; index < items.Length; index++)
        {
            var thread = items[index];
            var previous = inboxLastAt.GetValueOrDefault(thread.OtherUserId, 0L);
            inboxLastAt[thread.OtherUserId] = thread.LastMessageAtUnix;
            if (!primed || thread.LastMessageAtUnix <= previous || thread.UnreadCount <= 0)
            {
                continue;
            }

            if (viewingThreadUserId == thread.OtherUserId && DateTime.UtcNow - lastViewingUtc < ViewingGrace)
            {
                continue;
            }

            var name = string.IsNullOrEmpty(thread.OtherDisplayName) ? thread.OtherHandle : thread.OtherDisplayName;
            notifications.Notify(new PhoneNotification("velvet", name, thread.LastMessagePreview, DateTime.Now,
                AppPalettes.Velvet.Accent, thread.OtherUserId));
        }

        inboxPrimed = true;
    }

    public bool IsSignedIn => session.IsSignedIn;
    public VelvetProfileDto? Me => me;
    public bool HasProfile => me is not null;
    public bool AvatarBusy => avatarBusy;
    public VelvetProfileDto[] DiscoverResults => discoverResults;
    public bool LoadingDiscover => loadingDiscover;
    public bool DiscoverLoaded => discoverLoaded;
    public VelvetConnectionDto[] Connections => connections;
    public bool LoadingConnections => loadingConnections;
    public bool ConnectionsLoaded => connectionsLoaded;
    public VelvetConnectionDto[] Requests => requests;
    public bool LoadingRequests => loadingRequests;
    public bool RequestsLoaded => requestsLoaded;
    public int RequestCount => requests.Length;
    public VelvetConnectionDto[] SentRequests => sentRequests;
    public bool LoadingSentRequests => loadingSentRequests;
    public bool SentRequestsLoaded => sentRequestsLoaded;
    public string? ProfileUserId => profileUserId;
    public VelvetProfileDto? ProfileUser => profileUser;
    public bool ProfileLoading => profileLoading;
    public bool ProfileFailed => profileFailed;
    public VelvetThreadDto[] Threads => threads;
    public bool LoadingThreads => loadingThreads;
    public bool ThreadsLoaded => threadsLoaded;
    public string? ThreadId => threadId;
    public VelvetMessageDto[] Messages => messages;
    public bool LoadingOlder => loadingOlder;
    public bool HasMoreOlder => hasMoreOlder;
    public bool LoadingThread => loadingThread;
    public bool Sending => sending;
    public bool OtherTyping => otherTyping;
    public VelvetPostDto[] Feed => feed;
    public bool LoadingFeed => loadingFeed;
    public bool FeedLoaded => feedLoaded;
    public bool HasMoreFeed => feedCursor is not null;
    public bool LoadingMoreFeed => loadingMoreFeed;
    public bool Posting => posting;
    public VelvetPostDto? FetchedPost => fetchedPost;
    public UserDto[] Likers => likers;
    public bool LikersLoading => likersLoading;
    public bool LikersFailed => likersFailed;
    public UserDto[] Blocked => blocked;
    public bool LoadingBlocked => loadingBlocked;
    public bool BlockedLoaded => blockedLoaded;

    public int UnreadCount
    {
        get
        {
            var snapshot = threads;
            var total = 0;
            for (var index = 0; index < snapshot.Length; index++)
            {
                total += snapshot[index].UnreadCount;
            }

            return total;
        }
    }

    public void EnsureMe()
    {
        if (!session.IsSignedIn || me is not null || loadingMe)
        {
            return;
        }

        if (!meGate.TryPass())
        {
            return;
        }

        loadingMe = true;
        work.Run("profile load", async token =>
        {
            var profile = await client.VelvetMeAsync(token).ConfigureAwait(false);
            if (profile is not null)
            {
                me = profile;
            }
        }, () => loadingMe = false);
    }

    public void AcceptGate(int gateVersion, Action<bool> onComplete)
    {
        work.Run("gate accept", async token =>
        {
            var profile = await client.AcceptGateAsync(gateVersion, token).ConfigureAwait(false);
            if (profile is null)
            {
                return false;
            }

            me = profile;
            return true;
        }, onComplete);
    }

    public void UpdateAvatar(string sourcePath, WallpaperCrop crop, Action<bool> onComplete)
    {
        if (avatarBusy)
        {
            return;
        }

        avatarBusy = true;
        work.Run("avatar update", async token =>
        {
            var baked = ImageProcessor.BakeSquareJpeg(sourcePath, crop, AvatarSize);
            var upload = await client.UploadUrlAsync("image/jpeg", "avatar", token).ConfigureAwait(false);
            if (upload is null)
            {
                return false;
            }

            var uploaded = await client.UploadImageAsync(upload.UploadUrl, baked.Bytes, "image/jpeg", token)
                .ConfigureAwait(false);
            if (!uploaded)
            {
                return false;
            }

            var updated = await client
                .UpdateProfileAsync(new UpdateProfileRequest(null, null, null, upload.PublicUrl), token)
                .ConfigureAwait(false);
            if (updated is null)
            {
                return false;
            }

            var current = me;
            if (current is not null)
            {
                me = current with { AvatarUrl = upload.PublicUrl };
            }

            return true;
        }, onComplete, () => avatarBusy = false);
    }

    public void UpdateIdentity(string displayName, string handle, Action<bool> onComplete)
    {
        work.Run("identity update", async token =>
        {
            var request = new UpdateProfileRequest(displayName.Length > 0 ? displayName : null,
                handle.Length > 0 ? handle : null, null);
            var updated = await client.UpdateProfileAsync(request, token).ConfigureAwait(false);
            if (updated is null)
            {
                return false;
            }

            var current = me;
            if (current is not null)
            {
                me = current with { DisplayName = updated.DisplayName, Handle = updated.Handle };
            }

            return true;
        }, onComplete);
    }

    public void UpdateProfile(UpdateVelvetProfileRequest request, Action<bool> onComplete)
    {
        work.Run("profile update", async token =>
        {
            var updated = await client.UpdateVelvetProfileAsync(request, token).ConfigureAwait(false);
            if (updated is null)
            {
                return false;
            }

            me = updated;
            return true;
        }, onComplete);
    }

    public void RefreshDiscover(int lookingFor, string tags)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        loadingDiscover = true;
        work.Run("discover", async token =>
        {
            var page = await client.VelvetDiscoverAsync(lookingFor, tags, null, token).ConfigureAwait(false);
            if (page is not null)
            {
                discoverResults = page.Users;
            }
        }, () =>
        {
            loadingDiscover = false;
            discoverLoaded = true;
        });
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
            var page = await client.VelvetConnectionsAsync(null, token).ConfigureAwait(false);
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

    public void Heartbeat()
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var offset = SocialTimeZone.EffectiveOffsetMinutes(configuration);
        work.Run("heartbeat", async token => await client.VelvetHeartbeatAsync(offset, token).ConfigureAwait(false));
    }

    public void RefreshRequests()
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        loadingRequests = true;
        work.Run("requests", async token =>
        {
            var page = await client.VelvetRequestsAsync(token).ConfigureAwait(false);
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
            var page = await client.VelvetSentRequestsAsync(token).ConfigureAwait(false);
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
        requests = RemoveConnection(requests, userId);
        connectionsLoaded = false;
        SetConnectionStateEverywhere(userId, VelvetConnectionState.Connected);
        work.Run("accept", async token => await client.ConnectAsync(userId, token).ConfigureAwait(false));
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

    public void RefreshFeed()
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        loadingFeed = true;
        work.Run("feed", async token =>
        {
            var page = await client.VelvetFeedAsync(null, token).ConfigureAwait(false);
            if (page is not null)
            {
                ApplyFeedRefresh(page);
            }
        }, () =>
        {
            loadingFeed = false;
            feedLoaded = true;
        });
    }

    public void LoadMoreFeed()
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var cursor = feedCursor;
        if (cursor is null || loadingMoreFeed || loadingFeed)
        {
            return;
        }

        loadingMoreFeed = true;
        work.Run("feed more", async token =>
        {
            var page = await client.VelvetFeedAsync(cursor, token).ConfigureAwait(false);
            if (page is not null)
            {
                lock (feedLock)
                {
                    feed = MergeFeed(feed, page.Items);
                    feedCursor = page.NextCursor;
                }
            }
        }, () => loadingMoreFeed = false);
    }

    private void ApplyFeedRefresh(VelvetFeedPage page)
    {
        lock (feedLock)
        {
            if (feed.Length == 0)
            {
                feed = page.Items;
                feedCursor = page.NextCursor;
            }
            else
            {
                feed = MergeFeed(feed, page.Items);
            }
        }
    }

    private static VelvetPostDto[] MergeFeed(VelvetPostDto[] current, VelvetPostDto[] incoming)
    {
        if (incoming.Length == 0)
        {
            return current;
        }

        if (current.Length == 0)
        {
            return incoming;
        }

        var incomingIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < incoming.Length; index++)
        {
            incomingIds.Add(incoming[index].Id);
        }

        var merged = new List<VelvetPostDto>(current.Length + incoming.Length);
        for (var index = 0; index < current.Length; index++)
        {
            if (!incomingIds.Contains(current[index].Id))
            {
                merged.Add(current[index]);
            }
        }

        for (var index = 0; index < incoming.Length; index++)
        {
            merged.Add(incoming[index]);
        }

        merged.Sort(ByNewestFirst);
        return merged.ToArray();
    }

    private static int ByNewestFirst(VelvetPostDto left, VelvetPostDto right)
    {
        var byTime = right.CreatedAtUnix.CompareTo(left.CreatedAtUnix);
        return byTime != 0 ? byTime : string.CompareOrdinal(right.Id, left.Id);
    }

    public void OpenProfile(string userId)
    {
        if (profileUserId == userId && (profileUser is not null || profileLoading))
        {
            return;
        }

        profileUserId = userId;
        profileUser = null;
        profileFailed = false;
        profileLoading = true;
        work.Run("profile open", async token =>
        {
            var user = await client.VelvetUserAsync(userId, token).ConfigureAwait(false);
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

    public void Connect(string userId)
    {
        sentRequestsLoaded = false;
        SetConnectionStateEverywhere(userId, VelvetConnectionState.OutgoingRequest);
        work.Run("connect", async token => await client.ConnectAsync(userId, token).ConfigureAwait(false));
    }

    public void Connect(string userId, string intro)
    {
        sentRequestsLoaded = false;
        SetConnectionStateEverywhere(userId, VelvetConnectionState.OutgoingRequest);
        work.Run("connect", async token => await client.ConnectAsync(userId, intro, token).ConfigureAwait(false));
    }

    public void Disconnect(string userId)
    {
        ForgetConnection(userId, VelvetConnectionState.None);
        work.Run("disconnect", async token => await client.DisconnectAsync(userId, token).ConfigureAwait(false));
    }

    public void Block(string userId, Action<bool> onComplete)
    {
        blockedLoaded = false;
        ForgetConnection(userId, VelvetConnectionState.Blocked);
        work.Run("block", async token => await client.BlockAsync(userId, token).ConfigureAwait(false), onComplete);
    }

    public void Unblock(string userId)
    {
        blocked = CopyOnWrite.RemoveById(blocked, userId);
        SetConnectionStateEverywhere(userId, VelvetConnectionState.None);
        work.Run("unblock", async token => await client.UnblockAsync(userId, token).ConfigureAwait(false));
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
            var page = await client.BlockedUsersAsync(token).ConfigureAwait(false);
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

    public void RefreshThreads()
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        loadingThreads = true;
        work.Run("threads", async token =>
        {
            await EnsureVelvetHydratedAsync(token).ConfigureAwait(false);
            var page = await client.VelvetThreadsAsync(null, token).ConfigureAwait(false);
            if (page is not null)
            {
                threads = DecorateThreads(page.Items);
            }
        }, () =>
        {
            loadingThreads = false;
            threadsLoaded = true;
        });
    }

    public void OpenThread(string id)
    {
        if (threadId == id && (messages.Length > 0 || loadingThread))
        {
            return;
        }

        threadId = id;
        messages = Array.Empty<VelvetMessageDto>();
        olderCursor = null;
        hasMoreOlder = false;
        loadingOlder = false;
        otherTyping = false;
        loadingThread = true;
        currentKeyStatus = ChatKeyStatus.None;
        lastKeyStatusUtc = DateTime.UtcNow;
        work.Run("thread open", async token =>
        {
            var status = await keys.EnsureVelvetKeysAsync(id, MyUserId, token).ConfigureAwait(false);
            if (threadId == id)
            {
                currentKeyStatus = status;
            }

            var page = await client.VelvetMessagesAsync(id, null, token).ConfigureAwait(false);
            if (threadId == id && page is not null)
            {
                messages = DecorateMessages(id, page.Items);
                olderCursor = page.NextCursor;
                hasMoreOlder = page.NextCursor is not null;
            }
        }, () =>
        {
            if (threadId == id)
            {
                loadingThread = false;
            }
        });
    }

    public void RefreshThread()
    {
        var current = threadId;
        if (current is null || loadingThread || refreshingThread || DateTime.UtcNow < pollBackoffUntilUtc)
        {
            return;
        }

        refreshingThread = true;
        work.Run("thread refresh", async token =>
        {
            var page = await client.VelvetMessagesAsync(current, null, token).ConfigureAwait(false);
            NotePollResult(page is not null);
            if (threadId == current && page is not null)
            {
                var decorated = DecorateMessages(current, page.Items);
                lock (messagesLock)
                {
                    messages = MergeById(messages, decorated);
                }
            }
        }, () => refreshingThread = false);
    }

    private void NotePollResult(bool succeeded)
    {
        if (succeeded)
        {
            pollFailureStreak = 0;
            pollBackoffUntilUtc = DateTime.MinValue;
            return;
        }

        var streak = Math.Min(pollFailureStreak + 1, 4);
        pollFailureStreak = streak;
        pollBackoffUntilUtc = DateTime.UtcNow.AddSeconds(Math.Pow(2, streak) * 2.5);
    }

    public void LoadOlder()
    {
        var current = threadId;
        if (current is null || loadingThread || loadingOlder || !hasMoreOlder)
        {
            return;
        }

        var cursor = olderCursor;
        if (cursor is null)
        {
            hasMoreOlder = false;
            return;
        }

        loadingOlder = true;
        work.Run("thread older", async token =>
        {
            var page = await client.VelvetMessagesAsync(current, cursor, token).ConfigureAwait(false);
            if (threadId == current && page is not null)
            {
                var decorated = DecorateMessages(current, page.Items);
                lock (messagesLock)
                {
                    messages = MergeById(messages, decorated);
                }

                olderCursor = page.NextCursor;
                hasMoreOlder = page.NextCursor is not null;
            }
        }, () => loadingOlder = false);
    }

    private static VelvetMessageDto[] MergeById(VelvetMessageDto[] existing, VelvetMessageDto[] incoming)
    {
        if (existing.Length == 0)
        {
            return incoming;
        }

        if (incoming.Length == 0)
        {
            return existing;
        }

        var incomingIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < incoming.Length; index++)
        {
            incomingIds.Add(incoming[index].Id);
        }

        var merged = new List<VelvetMessageDto>(existing.Length + incoming.Length);
        for (var index = 0; index < existing.Length; index++)
        {
            if (!incomingIds.Contains(existing[index].Id))
            {
                merged.Add(existing[index]);
            }
        }

        for (var index = 0; index < incoming.Length; index++)
        {
            merged.Add(incoming[index]);
        }

        merged.Sort(CompareByCreatedAt);
        return merged.ToArray();
    }

    private static int CompareByCreatedAt(VelvetMessageDto left, VelvetMessageDto right)
    {
        var byTime = left.CreatedAtUnix.CompareTo(right.CreatedAtUnix);
        return byTime != 0 ? byTime : string.CompareOrdinal(left.Id, right.Id);
    }

    public void SendTyping(string id)
    {
        work.Run("typing", async token => await client.SendVelvetTypingAsync(id, token).ConfigureAwait(false));
    }

    public void RefreshTyping(string id)
    {
        if (refreshingTyping || DateTime.UtcNow < pollBackoffUntilUtc)
        {
            return;
        }

        refreshingTyping = true;
        work.Run("typing state", async token =>
        {
            var result = await client.VelvetTypingAsync(id, token).ConfigureAwait(false);
            NotePollResult(result is not null);
            if (threadId == id && result is not null)
            {
                otherTyping = result.OtherTyping;
            }
        }, () => refreshingTyping = false);
    }

    public void SendMessage(string id, string body, Action<bool> onComplete, string? replyToId = null)
    {
        var trimmed = body.Trim();
        if (trimmed.Length == 0 || sending)
        {
            return;
        }

        sending = true;
        work.Run("send", async token =>
        {
            VelvetMessageDto? sent;
            var scope = ConversationKeyStore.VelvetScope(ConversationKeyStore.Pair(MyUserId, id));
            var generation = keys.CurrentGeneration(scope);
            if (EncryptingCurrent && threadId == id && generation > 0
                && keys.TryGetCek(scope, generation, out var cek))
            {
                var encoded = EnvelopeCodec.Encode(trimmed, cek, generation, scope, MyUserId);
                sent = await client.SendVelvetMessageAsync(id, encoded.Envelope, 0, null, token,
                    encVersion: EnvelopeCodec.VersionEnvelope, commitmentTag: encoded.CommitmentTag,
                    replyToId: replyToId)
                    .ConfigureAwait(false);
                if (sent is not null)
                {
                    decryptedBodies[sent.Id] = new DmDecryptedBody(DmBodyState.Decrypted, trimmed,
                        encoded.FrankingKeyBase64, true);
                    sent = sent with { Body = trimmed };
                }
            }
            else
            {
                sent = await client.SendVelvetMessageAsync(id, trimmed, 0, null, token, replyToId: replyToId)
                    .ConfigureAwait(false);
            }

            if (sent is null)
            {
                return false;
            }

            if (sent.ReplyEncVersion == EnvelopeCodec.VersionEnvelope)
            {
                sent = sent with { ReplyBody = ResolveQuotedBody(scope, sent) };
            }

            if (threadId == id)
            {
                messages = CopyOnWrite.Append(messages, sent);
            }

            threadsLoaded = false;
            Plugin.Analytics.Track(AnalyticsEvents.DmSent("velvet"));
            return true;
        }, onComplete, () => sending = false);
    }

    public void SendImageMessage(string id, string sourcePath, string caption, Action<bool> onComplete)
    {
        if (sending)
        {
            return;
        }

        sending = true;
        work.Run("send image", async token =>
        {
            var baked = ImageProcessor.BakeJpeg(sourcePath, DmImageMaxDimension);
            var upload = await client.UploadUrlAsync("image/jpeg", "velvet-dm", token).ConfigureAwait(false);
            if (upload is null)
            {
                return false;
            }

            var uploaded = await client.UploadImageAsync(upload.UploadUrl, baked.Bytes, "image/jpeg", token)
                .ConfigureAwait(false);
            if (!uploaded)
            {
                return false;
            }

            var sent = await client
                .SendVelvetMessageAsync(id, caption.Trim(), 1, null, token, upload.Key, baked.Width, baked.Height)
                .ConfigureAwait(false);
            if (sent is null)
            {
                return false;
            }

            if (threadId == id)
            {
                messages = CopyOnWrite.Append(messages, sent);
            }

            threadsLoaded = false;
            Plugin.Analytics.Track(AnalyticsEvents.DmSent("velvet"));
            return true;
        }, onComplete, () => sending = false);
    }

    public void SendVoiceMessage(string id, byte[] wavBytes, int durationSecs, Action<bool> onComplete)
    {
        if (sending)
        {
            return;
        }

        sending = true;
        work.Run("send voice", async token =>
        {
            var upload = await client.UploadUrlAsync("audio/wav", "velvet-voice", token).ConfigureAwait(false);
            if (upload is null)
            {
                return false;
            }

            var uploaded = await client.UploadImageAsync(upload.UploadUrl, wavBytes, "audio/wav", token)
                .ConfigureAwait(false);
            if (!uploaded)
            {
                return false;
            }

            var sent = await client.SendVelvetMessageAsync(id, string.Empty, 3, null, token, upload.Key,
                durationSecs: durationSecs).ConfigureAwait(false);
            if (sent is null)
            {
                return false;
            }

            if (threadId == id)
            {
                messages = CopyOnWrite.Append(messages, sent);
            }

            threadsLoaded = false;
            Plugin.Analytics.Track(AnalyticsEvents.DmSent("velvet"));
            return true;
        }, onComplete, () => sending = false);
    }

    public void SetReaction(string messageId, string reactionToken)
    {
        messages = ApplyLocalReaction(messages, messageId, reactionToken);
        work.Run("react", async token =>
            await client.SetVelvetReactionAsync(messageId, reactionToken, token).ConfigureAwait(false));
    }

    public string MyReactionTo(string messageId)
    {
        var snapshot = messages;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (snapshot[index].Id != messageId)
            {
                continue;
            }

            var reactions = snapshot[index].Reactions;
            if (reactions is null)
            {
                return string.Empty;
            }

            for (var reactionIndex = 0; reactionIndex < reactions.Length; reactionIndex++)
            {
                if (reactions[reactionIndex].Mine)
                {
                    return reactions[reactionIndex].Token;
                }
            }

            return string.Empty;
        }

        return string.Empty;
    }

    private static VelvetMessageDto[] ApplyLocalReaction(VelvetMessageDto[] items, string messageId,
        string reactionToken)
    {
        for (var index = 0; index < items.Length; index++)
        {
            if (items[index].Id != messageId)
            {
                continue;
            }

            var current = items[index].Reactions ?? Array.Empty<ReactionSummaryDto>();
            var next = new List<ReactionSummaryDto>(current.Length + 1);
            var added = false;
            for (var summaryIndex = 0; summaryIndex < current.Length; summaryIndex++)
            {
                var summary = current[summaryIndex];
                if (summary.Mine)
                {
                    summary = summary with { Count = summary.Count - 1, Mine = false };
                }

                if (summary.Token == reactionToken)
                {
                    summary = summary with { Count = summary.Count + 1, Mine = true };
                    added = true;
                }

                if (summary.Count > 0)
                {
                    next.Add(summary);
                }
            }

            if (!added && reactionToken.Length > 0)
            {
                next.Add(new ReactionSummaryDto(reactionToken, 1, true));
            }

            var updated = (VelvetMessageDto[])items.Clone();
            updated[index] = items[index] with { Reactions = next.Count > 0 ? next.ToArray() : null };
            return updated;
        }

        return items;
    }

    public void LoadReactions(string messageId, Action<ReactorDto[]?> onResult)
    {
        work.Run("reaction list", async token =>
        {
            var result = await client.VelvetReactionsAsync(messageId, token).ConfigureAwait(false);
            onResult(result?.Items);
        });
    }

    public void EditMessage(string id, string messageId, string body, Action<bool> onComplete)
    {
        var trimmed = body.Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        work.Run("edit message", async token =>
        {
            VelvetMessageDto? edited;
            var scope = ConversationKeyStore.VelvetScope(ConversationKeyStore.Pair(MyUserId, id));
            var generation = keys.CurrentGeneration(scope);
            if (EncryptingCurrent && threadId == id && generation > 0
                && keys.TryGetCek(scope, generation, out var cek))
            {
                var encoded = EnvelopeCodec.Encode(trimmed, cek, generation, scope, MyUserId);
                edited = await client.EditVelvetMessageAsync(messageId, encoded.Envelope, token,
                    EnvelopeCodec.VersionEnvelope, encoded.CommitmentTag).ConfigureAwait(false);
                if (edited is not null)
                {
                    decryptedBodies[edited.Id] = new DmDecryptedBody(DmBodyState.Decrypted, trimmed,
                        encoded.FrankingKeyBase64, true);
                    edited = edited with { Body = trimmed };
                }
            }
            else
            {
                edited = await client.EditVelvetMessageAsync(messageId, trimmed, token).ConfigureAwait(false);
                if (edited is not null)
                {
                    decryptedBodies.TryRemove(messageId, out _);
                }
            }

            if (edited is null)
            {
                return false;
            }

            if (edited.ReplyEncVersion == EnvelopeCodec.VersionEnvelope)
            {
                edited = edited with { ReplyBody = ResolveQuotedBody(scope, edited) };
            }

            if (threadId == id)
            {
                messages = ReplaceMessage(messages, edited);
            }

            threadsLoaded = false;
            return true;
        }, onComplete);
    }

    private static VelvetMessageDto[] ReplaceMessage(VelvetMessageDto[] items, VelvetMessageDto updated)
    {
        for (var index = 0; index < items.Length; index++)
        {
            if (items[index].Id != updated.Id)
            {
                continue;
            }

            var next = (VelvetMessageDto[])items.Clone();
            next[index] = updated with { Reactions = items[index].Reactions, ReadAtUnix = items[index].ReadAtUnix };
            return next;
        }

        return items;
    }

    public void DeleteMessage(string messageId, Action<bool> onComplete)
    {
        work.Run("delete message", async token =>
        {
            var ok = await client.DeleteVelvetMessageAsync(messageId, token).ConfigureAwait(false);
            if (!ok)
            {
                return false;
            }

            messages = TombstoneLocal(messages, messageId);
            threadsLoaded = false;
            return true;
        }, onComplete);
    }

    private static VelvetMessageDto[] TombstoneLocal(VelvetMessageDto[] items, string messageId)
    {
        for (var index = 0; index < items.Length; index++)
        {
            if (items[index].Id != messageId)
            {
                continue;
            }

            var updated = (VelvetMessageDto[])items.Clone();
            updated[index] = items[index] with
            {
                Deleted = true,
                Body = string.Empty,
                EncVersion = 0,
                CommitmentTag = null,
                DurationSecs = 0,
                Reactions = null,
            };
            return updated;
        }

        return items;
    }

    private string ResolveQuotedBody(string scope, VelvetMessageDto item)
    {
        if (item.ReplyToId is null || string.IsNullOrEmpty(item.ReplyBody))
        {
            return Loc.T(L.Encryption.NoKeyPlaceholder);
        }

        if (decryptedBodies.TryGetValue(item.ReplyToId, out var cached) && cached.State == DmBodyState.Decrypted)
        {
            return cached.Text;
        }

        if (!EnvelopeCodec.TryParseGeneration(item.ReplyBody, out var generation))
        {
            return Loc.T(L.Encryption.NoKeyPlaceholder);
        }

        if (!keys.TryGetCek(scope, generation, out var cek))
        {
            return vault.State == KeyVaultState.Unlocked
                ? Loc.T(L.Encryption.NoKeyPlaceholder)
                : Loc.T(L.Encryption.EncryptedPlaceholder);
        }

        var decoded = EnvelopeCodec.Decode(item.ReplyBody, cek, scope, item.ReplySenderId ?? string.Empty, null);
        return decoded.Status == EnvelopeDecodeStatus.Success ? decoded.Body : Loc.T(L.Encryption.NoKeyPlaceholder);
    }

    public string? DmMediaUrl(string messageId)
    {
        if (dmMediaUrls.TryGetValue(messageId, out var url))
        {
            return url;
        }

        if (!dmMediaLoading.TryAdd(messageId, 0))
        {
            return null;
        }

        work.Run("dm media url", async token =>
        {
            var result = await client.VelvetDmMediaUrlAsync(messageId, token).ConfigureAwait(false);
            if (result is not null)
            {
                dmMediaUrls[messageId] = result.Url;
            }
        }, () => dmMediaLoading.TryRemove(messageId, out _));
        return null;
    }

    public void CreatePost(string[] sourcePaths, WallpaperCrop[] crops, string caption, string[] tags,
        Action<bool> onComplete)
    {
        if (posting || sourcePaths.Length == 0)
        {
            return;
        }

        posting = true;
        work.Run("create post", async token =>
        {
            var keys = new string[sourcePaths.Length];
            for (var index = 0; index < sourcePaths.Length; index++)
            {
                var baked = ImageProcessor.BakeSquareJpeg(sourcePaths[index], crops[index], PostSize);
                var upload = await client.UploadUrlAsync("image/jpeg", "velvet", token).ConfigureAwait(false);
                if (upload is null)
                {
                    return false;
                }

                var uploaded = await client.UploadImageAsync(upload.UploadUrl, baked.Bytes, "image/jpeg", token)
                    .ConfigureAwait(false);
                if (!uploaded)
                {
                    return false;
                }

                keys[index] = upload.Key;
            }

            var request =
                new CreateVelvetPostRequest(keys[0], PostSize, PostSize, caption, tags, keys);
            var created = await client.CreateVelvetPostAsync(request, token).ConfigureAwait(false);
            if (created is null)
            {
                return false;
            }

            Plugin.Analytics.Track(AnalyticsEvents.PostCreated("velvet"));
            return true;
        }, onComplete, () => posting = false);
    }

    public VelvetCommentDto[] DetailComments => detailComments;
    public bool LoadingComments => loadingComments;
    public bool Commenting => commenting;

    public void EnsurePost(string postId)
    {
        if (fetchingPostId == postId || fetchedPost?.Id == postId)
        {
            return;
        }

        fetchingPostId = postId;
        fetchedPost = null;
        work.Run("post by id", async token =>
        {
            var post = await client.VelvetPostAsync(postId, token).ConfigureAwait(false);
            if (fetchingPostId == postId && post is not null)
            {
                fetchedPost = post;
            }
        });
    }

    public void OpenLikers(string postId)
    {
        if (likersPostId == postId && (likers.Length > 0 || likersLoading))
        {
            return;
        }

        likersPostId = postId;
        likers = Array.Empty<UserDto>();
        likersFailed = false;
        likersLoading = true;
        work.Run("likers", async token =>
        {
            var page = await client.VelvetPostLikersAsync(postId, null, token).ConfigureAwait(false);
            if (likersPostId != postId)
            {
                return;
            }

            if (page is null)
            {
                likersFailed = true;
            }
            else
            {
                likers = page.Items;
            }
        }, () =>
        {
            if (likersPostId == postId)
            {
                likersLoading = false;
            }
        });
    }

    public void OpenComments(string postId)
    {
        detailPostId = postId;
        detailComments = Array.Empty<VelvetCommentDto>();
        loadingComments = true;
        work.Run("comments", async token =>
        {
            var page = await client.VelvetCommentsAsync(postId, token).ConfigureAwait(false);
            if (detailPostId == postId && page is not null)
            {
                detailComments = page.Items;
            }
        }, () =>
        {
            if (detailPostId == postId)
            {
                loadingComments = false;
            }
        });
    }

    public void AddComment(string postId, string text, Action<bool> onComplete)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0 || commenting)
        {
            return;
        }

        commenting = true;
        work.Run("comment", async token =>
        {
            var created = await client.AddVelvetCommentAsync(postId, trimmed, token).ConfigureAwait(false);
            if (created is null)
            {
                return false;
            }

            if (detailPostId == postId)
            {
                detailComments = CopyOnWrite.Append(detailComments, created);
            }

            Plugin.Analytics.Track(AnalyticsEvents.Comment("velvet"));
            return true;
        }, onComplete, () => commenting = false);
    }

    public void ToggleCommentLike(VelvetCommentDto comment)
    {
        var liked = !comment.Liked;
        detailComments = CopyOnWrite.MapById(detailComments, comment.Id, stored => stored.Liked == liked
            ? stored
            : stored with { Liked = liked, LikeCount = Math.Max(0, stored.LikeCount + (liked ? 1 : -1)) });
        work.Run("comment like", async token =>
        {
            var updated = liked
                ? await client.LikeVelvetCommentAsync(comment.PostId, comment.Id, token).ConfigureAwait(false)
                : await client.UnlikeVelvetCommentAsync(comment.PostId, comment.Id, token).ConfigureAwait(false);
            if (updated is not null && detailPostId == comment.PostId)
            {
                detailComments = CopyOnWrite.Replace(detailComments, updated);
            }
        });
    }

    public void DeletePost(string postId)
    {
        RemovePost(postId);
        work.Run("delete post",
            async token => await client.DeleteVelvetPostAsync(postId, token).ConfigureAwait(false));
    }

    public void ToggleReaction(VelvetPostDto post, int kind)
    {
        var target = post.MyReaction == kind ? -1 : kind;
        if (target >= 0)
        {
            Plugin.Analytics.Track(AnalyticsEvents.Reaction("velvet"));
        }

        work.Run("reaction", async token =>
        {
            var result = target < 0
                ? await client.VelvetRemoveReactionAsync(post.Id, token).ConfigureAwait(false)
                : await client.VelvetReactAsync(post.Id, target, token).ConfigureAwait(false);
            if (result is not null)
            {
                feed = CopyOnWrite.Replace(feed, result);
                if (fetchedPost?.Id == result.Id)
                {
                    fetchedPost = result;
                }
            }
        });
    }

    public void Report(string targetType, string targetId, string? reason, Action<bool> onComplete)
    {
        work.Run("report",
            async token => await client.ReportAsync(targetType, targetId, reason, token).ConfigureAwait(false),
            onComplete);
    }

    public void ReportMessage(string messageId, string? reason, Action<bool> onComplete)
    {
        var snapshot = messages;
        var targetIndex = -1;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (snapshot[index].Id == messageId)
            {
                targetIndex = index;
                break;
            }
        }

        if (targetIndex < 0)
        {
            onComplete(false);
            return;
        }

        var reveals = new List<RevealedMessageDto>(6);
        AppendReveal(reveals, snapshot[targetIndex]);
        for (var index = targetIndex - 1; index >= 0 && reveals.Count < 6; index--)
        {
            AppendReveal(reveals, snapshot[index]);
        }

        var revealed = reveals.Count > 0 && reveals[0].MessageId == messageId ? reveals.ToArray() : null;
        work.Run("report message", async token =>
            await client.ReportAsync("velvet_message", messageId, reason, token, revealed).ConfigureAwait(false),
            onComplete);
    }

    private void AppendReveal(List<RevealedMessageDto> reveals, VelvetMessageDto message)
    {
        if (message.EncVersion == 0)
        {
            reveals.Add(new RevealedMessageDto(message.Id, message.Body, null));
            return;
        }

        var state = DecryptionState(message.Id);
        if (state.State == DmBodyState.Decrypted)
        {
            reveals.Add(new RevealedMessageDto(message.Id, state.Text, state.FrankingKey));
        }
    }

    public void DeletePost(string postId, Action<bool> onComplete)
    {
        work.Run("delete post", async token =>
        {
            var succeeded = await client.DeleteVelvetPostAsync(postId, token).ConfigureAwait(false);
            if (succeeded)
            {
                RemovePost(postId);
            }

            return succeeded;
        }, onComplete);
    }

    public void DeleteComment(string postId, string commentId)
    {
        if (detailPostId == postId)
        {
            detailComments = CopyOnWrite.RemoveById(detailComments, commentId);
        }

        work.Run("comment delete",
            async token => await client.DeleteVelvetCommentAsync(postId, commentId, token).ConfigureAwait(false));
    }

    public void DeleteComment(string postId, string commentId, Action<bool> onComplete)
    {
        work.Run("comment delete", async token =>
        {
            var succeeded = await client.DeleteVelvetCommentAsync(postId, commentId, token).ConfigureAwait(false);
            if (succeeded && detailPostId == postId)
            {
                detailComments = CopyOnWrite.RemoveById(detailComments, commentId);
            }

            return succeeded;
        }, onComplete);
    }

    public void ClearDiscover() => discoverResults = Array.Empty<VelvetProfileDto>();

    public void InvalidateLists()
    {
        discoverLoaded = false;
        connectionsLoaded = false;
        threadsLoaded = false;
        requestsLoaded = false;
        sentRequestsLoaded = false;
        feedLoaded = false;
        blockedLoaded = false;
    }

    private static VelvetConnectionDto[] RemoveConnection(VelvetConnectionDto[] source, string userId)
    {
        var index = Array.FindIndex(source, item => item.UserId == userId);
        if (index < 0)
        {
            return source;
        }

        var result = new VelvetConnectionDto[source.Length - 1];
        Array.Copy(source, 0, result, 0, index);
        Array.Copy(source, index + 1, result, index, source.Length - index - 1);
        return result;
    }

    private void ForgetConnection(string userId, int state)
    {
        connections = RemoveConnection(connections, userId);
        threads = CopyOnWrite.RemoveById(threads, userId);
        if (threadId == userId)
        {
            threadId = null;
            messages = Array.Empty<VelvetMessageDto>();
        }

        SetConnectionStateEverywhere(userId, state);
    }

    private void SetConnectionStateEverywhere(string userId, int state)
    {
        if (profileUser is { } current && current.UserId == userId)
        {
            profileUser = current with { ConnectionState = state };
        }

        var discover = discoverResults;
        for (var index = 0; index < discover.Length; index++)
        {
            if (discover[index].UserId == userId && discover[index].ConnectionState != state)
            {
                var updated = (VelvetProfileDto[])discover.Clone();
                updated[index] = discover[index] with { ConnectionState = state };
                discoverResults = updated;
                break;
            }
        }
    }

    private void RemovePost(string postId)
    {
        feed = CopyOnWrite.RemoveById(feed, postId);
    }

    private VelvetMessageDto[] DecorateMessages(string otherId, VelvetMessageDto[] items)
    {
        var scope = ConversationKeyStore.VelvetScope(ConversationKeyStore.Pair(MyUserId, otherId));
        VelvetMessageDto[]? decorated = null;
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var needsBody = item.EncVersion == EnvelopeCodec.VersionEnvelope;
            var needsReply = item.ReplyEncVersion == EnvelopeCodec.VersionEnvelope;
            if (!needsBody && !needsReply)
            {
                continue;
            }

            var updated = item;
            if (needsBody)
            {
                updated = updated with { Body = ResolveBody(scope, item).Text };
            }

            if (needsReply)
            {
                updated = updated with { ReplyBody = ResolveQuotedBody(scope, item) };
            }

            decorated ??= (VelvetMessageDto[])items.Clone();
            decorated[index] = updated;
        }

        return decorated ?? items;
    }

    private DmDecryptedBody ResolveBody(string scope, VelvetMessageDto item)
    {
        if (decryptedBodies.TryGetValue(item.Id, out var cached)
            && cached.State is DmBodyState.Decrypted or DmBodyState.Malformed)
        {
            return cached;
        }

        DmDecryptedBody resolved;
        if (!EnvelopeCodec.TryParseGeneration(item.Body, out var generation))
        {
            resolved = new DmDecryptedBody(DmBodyState.Malformed, Loc.T(L.Encryption.NoKeyPlaceholder), null, false);
        }
        else if (!keys.TryGetCek(scope, generation, out var cek))
        {
            resolved = vault.State == KeyVaultState.Unlocked
                ? new DmDecryptedBody(DmBodyState.NoKey, Loc.T(L.Encryption.NoKeyPlaceholder), null, false)
                : new DmDecryptedBody(DmBodyState.Pending, Loc.T(L.Encryption.EncryptedPlaceholder), null, false);
        }
        else
        {
            var decoded = EnvelopeCodec.Decode(item.Body, cek, scope, item.SenderId, item.CommitmentTag);
            resolved = decoded.Status switch
            {
                EnvelopeDecodeStatus.Success => new DmDecryptedBody(DmBodyState.Decrypted, decoded.Body,
                    decoded.FrankingKeyBase64, decoded.CommitmentVerified),
                EnvelopeDecodeStatus.WrongKey => new DmDecryptedBody(DmBodyState.NoKey,
                    Loc.T(L.Encryption.NoKeyPlaceholder), null, false),
                _ => new DmDecryptedBody(DmBodyState.Malformed, Loc.T(L.Encryption.NoKeyPlaceholder), null, false),
            };
        }

        decryptedBodies[item.Id] = resolved;
        return resolved;
    }

    private VelvetThreadDto[] DecorateThreads(VelvetThreadDto[] items)
    {
        VelvetThreadDto[]? decorated = null;
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            if (item.LastMessageEncVersion != EnvelopeCodec.VersionEnvelope)
            {
                continue;
            }

            decorated ??= (VelvetThreadDto[])items.Clone();
            decorated[index] = item with { LastMessagePreview = ResolvePreview(item) };
        }

        return decorated ?? items;
    }

    private string ResolvePreview(VelvetThreadDto item)
    {
        if (previewCache.TryGetValue(item.OtherUserId, out var cached) && cached.AtUnix == item.LastMessageAtUnix)
        {
            return cached.Text;
        }

        var scope = ConversationKeyStore.VelvetScope(ConversationKeyStore.Pair(MyUserId, item.OtherUserId));
        var text = Loc.T(L.Encryption.EncryptedPlaceholder);
        if (EnvelopeCodec.TryParseGeneration(item.LastMessagePreview, out var generation)
            && keys.TryGetCek(scope, generation, out var cek))
        {
            var decoded = EnvelopeCodec.Decode(item.LastMessagePreview, cek, scope, item.LastMessageSenderId, null);
            if (decoded.Status == EnvelopeDecodeStatus.Success)
            {
                text = decoded.Body;
            }

            previewCache[item.OtherUserId] = (item.LastMessageAtUnix, text);
        }

        return text;
    }

    public void Dispose()
    {
        signals.VelvetPinged -= inboxCadence.RequestImmediate;
        vault.Changed -= OnVaultChanged;
        Plugin.Framework.Update -= OnFrameworkTick;
        work.Dispose();
    }
}
