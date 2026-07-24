using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Media;
using Aetherphone.Core.Message;
using Aetherphone.Core.Net;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Social;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Velvet;

internal sealed class VelvetStore : ChatThreadStoreBase<VelvetMessageDto, VelvetThreadDto>
{
    private const int AvatarSize = 512;
    private const int PostSize = 1080;
    private readonly VelvetClient client;
    private readonly AccountClient account;
    private readonly Configuration configuration;
    private readonly RealtimeSignalBus signals;
    private readonly RetryGate meGate = new RetryGate(TimeSpan.FromSeconds(30));
    private readonly FeedLane<VelvetPostDto>[] feedLanes =
    {
        new FeedLane<VelvetPostDto>(ByNewestFirst),
        new FeedLane<VelvetPostDto>(ByNewestFirst),
    };
    private volatile bool velvetKeysHydrated;
    private volatile int accountEpoch;
    private volatile VelvetProfileDto? me;
    private volatile bool loadingMe;
    private volatile bool avatarBusy;
    private volatile bool introBusy;
    private volatile VelvetProfileDto[] discoverResults = Array.Empty<VelvetProfileDto>();
    private volatile bool loadingDiscover;
    private volatile bool discoverLoaded;
    private volatile string? discoverCursor;
    private volatile bool loadingMoreDiscover;
    private volatile VelvetDiscoverFilter discoverFilter = VelvetDiscoverFilter.Empty;
    private volatile string discoverTags = string.Empty;
    private volatile string discoverRegion = string.Empty;
    private volatile int discoverEpoch;
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
    private volatile string? userPostsUserId;
    private volatile VelvetPostDto[] userPosts = Array.Empty<VelvetPostDto>();
    private volatile int userPostsTotal;
    private volatile bool userPostsLoading;
    private volatile bool userPostsLoaded;
    private volatile bool userPostsFailed;
    private volatile string? detailPostId;
    private volatile VelvetCommentDto[] detailComments = Array.Empty<VelvetCommentDto>();
    private volatile bool loadingComments;
    private volatile bool commenting;
    private volatile bool feedLoadedAll;
    private volatile bool feedLoadedConnections;
    private volatile int feedScope = (int)VelvetFeedScope.All;
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

    public VelvetStore(AethernetSession session, VelvetClient client, AccountClient account, SafetyClient safety,
        MediaClient media, NotificationService notifications, Configuration configuration, KeyVault vault,
        ConversationKeyStore keys, PhoneVisibility visibility, RealtimeSignalBus signals)
        : base("Velvet", session, safety, media, notifications, vault, keys, visibility)
    {
        this.client = client;
        this.account = account;
        this.configuration = configuration;
        this.signals = signals;
        signals.VelvetPinged += OnVelvetPinged;
    }

    public override bool RealtimePushActive => signals.RealtimeActive;

    private void OnVelvetPinged()
    {
        InboxCadence.RequestImmediate();
        RefreshThread();
    }

    public MentionSuggestions NewMentionSuggestions() => new(account, work);

    public VelvetProfileDto? Me => me;
    public bool HasProfile => me is not null;
    public bool AvatarBusy => avatarBusy;
    public bool IntroBusy => introBusy;
    public VelvetProfileDto[] DiscoverResults => discoverResults;
    public bool LoadingDiscover => loadingDiscover;
    public bool DiscoverLoaded => discoverLoaded;
    public bool HasMoreDiscover => discoverCursor is not null;
    public bool LoadingMoreDiscover => loadingMoreDiscover;
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
    public string? UserPostsUserId => userPostsUserId;
    public VelvetPostDto[] UserPosts => userPosts;
    public int UserPostsTotal => userPostsTotal;
    public bool UserPostsLoaded => userPostsLoaded;
    public bool UserPostsFailed => userPostsFailed;
    public VelvetThreadDto[] Threads => ThreadListItems;
    public bool LoadingThreads => LoadingThreadList;
    public bool ThreadsLoaded => ThreadListLoaded;
    public string? ThreadId => CurrentThreadId;
    public VelvetFeedScope FeedScope => (VelvetFeedScope)feedScope;
    public VelvetPostDto[] Feed => ActiveFeedLane.Items;
    public bool LoadingFeed => ActiveFeedLane.Loading;
    public bool FeedLoaded => FeedScope == VelvetFeedScope.All ? feedLoadedAll : feedLoadedConnections;
    public bool HasMoreFeed => ActiveFeedLane.HasMore;
    public bool LoadingMoreFeed => ActiveFeedLane.LoadingMore;
    private FeedLane<VelvetPostDto> ActiveFeedLane => feedLanes[feedScope];
    public bool Posting => posting;
    public VelvetPostDto? FetchedPost => fetchedPost;
    public UserDto[] Likers => likers;
    public bool LikersLoading => likersLoading;
    public bool LikersFailed => likersFailed;
    public UserDto[] Blocked => blocked;
    public bool LoadingBlocked => loadingBlocked;
    public bool BlockedLoaded => blockedLoaded;
    public int UnreadCount => ComputeUnread();

    public void RefreshThreads() => RefreshThreadListCore();

    protected override string ImageUploadScope => "velvet-dm";
    protected override string VoiceUploadScope => "velvet-voice";
    protected override string ReportTargetType => "velvet_message";

    protected override bool TickActive => base.TickActive && configuration.IsVelvetOnboarded();

    protected override string ScopeFor(string threadId) =>
        ConversationKeyStore.VelvetScope(ConversationKeyStore.Pair(MyUserId, threadId));

    protected override Task HydrateKeysAsync(CancellationToken token) => EnsureVelvetHydratedAsync(token);

    protected override Task<ChatKeyStatus> EnsureThreadKeysAsync(string threadId, CancellationToken token) =>
        keys.EnsureVelvetKeysAsync(threadId, MyUserId, token);

    protected override void OnCipherCleared()
    {
        velvetKeysHydrated = false;
    }

    protected override void OnAccountSwitched()
    {
        accountEpoch++;
        discoverEpoch++;
        me = null;
        meGate.Reset();
        discoverResults = Array.Empty<VelvetProfileDto>();
        discoverCursor = null;
        discoverLoaded = false;
        discoverFilter = VelvetDiscoverFilter.Empty;
        discoverTags = string.Empty;
        discoverRegion = string.Empty;
        connections = Array.Empty<VelvetConnectionDto>();
        connectionsLoaded = false;
        requests = Array.Empty<VelvetConnectionDto>();
        requestsLoaded = false;
        sentRequests = Array.Empty<VelvetConnectionDto>();
        sentRequestsLoaded = false;
        profileUserId = null;
        profileUser = null;
        profileFailed = false;
        userPostsUserId = null;
        userPosts = Array.Empty<VelvetPostDto>();
        userPostsTotal = 0;
        userPostsLoaded = false;
        userPostsFailed = false;
        detailPostId = null;
        detailComments = Array.Empty<VelvetCommentDto>();
        for (var laneIndex = 0; laneIndex < feedLanes.Length; laneIndex++)
        {
            feedLanes[laneIndex].Clear();
        }

        feedLoadedAll = false;
        feedLoadedConnections = false;
        fetchedPost = null;
        fetchingPostId = null;
        likersPostId = null;
        likers = Array.Empty<UserDto>();
        likersFailed = false;
        blocked = Array.Empty<UserDto>();
        blockedLoaded = false;
    }

    private async Task EnsureVelvetHydratedAsync(CancellationToken token)
    {
        if (velvetKeysHydrated || vault.State != KeyVaultState.Unlocked)
        {
            return;
        }

        velvetKeysHydrated = true;
        await keys.HydrateVelvetAsync(token).ConfigureAwait(false);
    }

    protected override async Task<VelvetThreadDto[]?> FetchThreadListAsync(CancellationToken token)
    {
        await EnsureVelvetHydratedAsync(token).ConfigureAwait(false);
        var page = await client.ThreadsAsync(null, token).ConfigureAwait(false);
        return page?.Items;
    }

    protected override async Task<MessagePage?> FetchMessagesPageAsync(string threadId, string? cursor,
        CancellationToken token)
    {
        var page = await client.MessagesAsync(threadId, cursor, token).ConfigureAwait(false);
        return page is null ? null : new MessagePage(page.Items, page.NextCursor);
    }

    protected override Task<VelvetMessageDto?> SendMessageRequestAsync(string threadId, string body, int kind,
        CancellationToken token, string? mediaKey, int mediaWidth, int mediaHeight, int encVersion,
        string? commitmentTag, string? replyToId, int durationSecs)
    {
        return client.SendMessageAsync(threadId, body, kind, null, token, mediaKey, mediaWidth, mediaHeight,
            encVersion, commitmentTag, replyToId, durationSecs);
    }

    protected override Task<VelvetMessageDto?> EditMessageRequestAsync(string messageId, string body,
        CancellationToken token, int encVersion, string? commitmentTag)
    {
        return client.EditMessageAsync(messageId, body, token, encVersion, commitmentTag);
    }

    protected override Task<bool> DeleteMessageRequestAsync(string messageId, CancellationToken token) =>
        client.DeleteMessageAsync(messageId, token);

    protected override Task SetReactionRequestAsync(string messageId, string reactionToken, CancellationToken token) =>
        client.SetReactionAsync(messageId, reactionToken, token);

    protected override Task<ReactionListDto?> FetchReactionsAsync(string messageId, CancellationToken token) =>
        client.ReactionsAsync(messageId, token);

    protected override Task SendTypingRequestAsync(string threadId, CancellationToken token) =>
        client.SendTypingAsync(threadId, token);

    protected override async Task<bool?> FetchOtherTypingAsync(string threadId, CancellationToken token)
    {
        var result = await client.TypingAsync(threadId, token).ConfigureAwait(false);
        return result?.OtherTyping;
    }

    protected override async Task<string?> FetchMediaUrlRequestAsync(string messageId, CancellationToken token)
    {
        var result = await client.DmMediaUrlAsync(messageId, token).ConfigureAwait(false);
        return result?.Url;
    }

    protected override long MessageTimeOf(VelvetMessageDto message) => message.CreatedAtUnix;

    protected override int MessageEncVersionOf(VelvetMessageDto message) => message.EncVersion;

    protected override string MessageBodyOf(VelvetMessageDto message) => message.Body;

    protected override ReactionSummaryDto[]? ReactionsOf(VelvetMessageDto message) => message.Reactions;

    protected override VelvetMessageDto WithReactions(VelvetMessageDto message, ReactionSummaryDto[]? reactions) =>
        message with { Reactions = reactions };

    protected override VelvetMessageDto WithBody(VelvetMessageDto message, string body) =>
        message with { Body = body };

    protected override VelvetMessageDto PreserveLocalFields(VelvetMessageDto updated, VelvetMessageDto existing) =>
        updated with { Reactions = existing.Reactions, ReadAtUnix = existing.ReadAtUnix };

    protected override VelvetMessageDto Tombstone(VelvetMessageDto message)
    {
        return message with
        {
            Deleted = true,
            Body = string.Empty,
            EncVersion = 0,
            CommitmentTag = null,
            DurationSecs = 0,
            Reactions = null,
        };
    }

    protected override VelvetMessageDto ResolveOutgoingReply(string scope, VelvetMessageDto message)
    {
        if (message.ReplyEncVersion != EnvelopeCodec.VersionEnvelope)
        {
            return message;
        }

        return message with
        {
            ReplyBody = cipher.ResolveQuotedBody(scope, message.ReplyToId, message.ReplyBody, message.ReplySenderId),
        };
    }

    protected override string ThreadKeyOf(VelvetThreadDto thread) => thread.OtherUserId;

    protected override long ThreadLastMessageAtOf(VelvetThreadDto thread) => thread.LastMessageAtUnix;

    protected override int ThreadUnreadCountOf(VelvetThreadDto thread) => thread.UnreadCount;

    protected override PhoneNotification BuildInboxNotification(VelvetThreadDto thread)
    {
        var name = string.IsNullOrEmpty(thread.OtherDisplayName) ? thread.OtherHandle : thread.OtherDisplayName;
        return new PhoneNotification("velvet", name, ChatText.ListPreview(thread.LastMessagePreview), DateTime.Now,
            AppPalettes.Velvet.Accent, thread.OtherUserId);
    }

    protected override VelvetMessageDto[] DecorateMessages(string threadId, VelvetMessageDto[] items)
    {
        var scope = ScopeFor(threadId);
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
                updated = updated with
                {
                    Body = cipher.ResolveBody(scope, item.Id, item.Body, item.SenderId, item.CommitmentTag).Text,
                };
            }

            if (needsReply)
            {
                updated = updated with
                {
                    ReplyBody = cipher.ResolveQuotedBody(scope, item.ReplyToId, item.ReplyBody, item.ReplySenderId),
                };
            }

            decorated ??= (VelvetMessageDto[])items.Clone();
            decorated[index] = updated;
        }

        return decorated ?? items;
    }

    protected override VelvetThreadDto[] DecorateThreadList(VelvetThreadDto[] items)
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
            var scope = ScopeFor(item.OtherUserId);
            decorated[index] = item with
            {
                LastMessagePreview = cipher.ResolvePreview(item.OtherUserId, scope, item.LastMessageAtUnix,
                    item.LastMessagePreview, item.LastMessageSenderId),
            };
        }

        return decorated ?? items;
    }

    public byte[]? DecryptMedia(VelvetMessageDto message, byte[] sealedBytes, string threadPartnerId)
    {
        if (message.EncVersion != EnvelopeCodec.VersionEnvelope
            || !cipher.TryGetGeneration(message.Id, out var generation))
        {
            return null;
        }

        var scope = ScopeFor(threadPartnerId);
        return cipher.TryDecryptMedia(scope, generation, sealedBytes, message.SenderId, message.Kind);
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
        var epoch = accountEpoch;
        work.Run("profile load", async token =>
        {
            var profile = await client.MeAsync(token).ConfigureAwait(false);
            if (profile is not null && epoch == accountEpoch)
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
            var upload = await media.UploadUrlAsync("image/jpeg", "avatar", token).ConfigureAwait(false);
            if (upload is null)
            {
                return false;
            }

            var uploaded = await media.UploadImageAsync(upload.UploadUrl, baked.Bytes, "image/jpeg", token)
                .ConfigureAwait(false);
            if (!uploaded)
            {
                return false;
            }

            var updated = await account
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
            var updated = await account.UpdateProfileAsync(request, token).ConfigureAwait(false);
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
            var updated = await client.UpdateProfileAsync(request, token).ConfigureAwait(false);
            if (updated is null)
            {
                return false;
            }

            me = updated;
            return true;
        }, onComplete);
    }

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
        work.Run("discover", async token =>
        {
            var page = await client.DiscoverAsync(filter, tags, region, null, token).ConfigureAwait(false);
            if (page is not null && epoch == discoverEpoch)
            {
                discoverResults = page.Users;
                discoverCursor = page.NextCursor;
            }
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
                discoverResults = AppendUniqueDiscover(discoverResults, page.Users);
                discoverCursor = page.NextCursor;
            }
        }, () => loadingMoreDiscover = false);
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

    public void Heartbeat(string region, bool isLalafell)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var offset = SocialTimeZone.EffectiveOffsetMinutes(configuration);
        work.Run("heartbeat", async token =>
            await client.HeartbeatAsync(offset, region, isLalafell, token).ConfigureAwait(false));
    }

    public void EnsureUserPosts(string userId)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        if (userPostsUserId == userId && (userPostsLoaded || userPostsLoading))
        {
            return;
        }

        userPostsUserId = userId;
        userPosts = Array.Empty<VelvetPostDto>();
        userPostsTotal = 0;
        userPostsLoaded = false;
        userPostsFailed = false;
        userPostsLoading = true;
        work.Run("user posts", async token =>
        {
            var page = await client.UserPostsAsync(userId, token).ConfigureAwait(false);
            if (userPostsUserId != userId)
            {
                return;
            }

            if (page is null)
            {
                userPostsFailed = true;
                return;
            }

            userPosts = page.Items;
            userPostsTotal = page.TotalCount;
            userPostsLoaded = true;
        }, () => userPostsLoading = false);
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

    public void SetFeedScope(VelvetFeedScope scope)
    {
        feedScope = (int)scope;
    }

    public void RefreshFeed()
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var scope = FeedScope;
        var lane = feedLanes[(int)scope];
        lane.Loading = true;
        work.Run("feed", async token =>
        {
            var page = await client.FeedAsync(ScopeKey(scope), null, token).ConfigureAwait(false);
            if (page is not null)
            {
                lane.ApplyRefresh(page.Items, page.NextCursor);
            }
        }, () =>
        {
            lane.Loading = false;
            if (scope == VelvetFeedScope.All)
            {
                feedLoadedAll = true;
            }
            else
            {
                feedLoadedConnections = true;
            }
        });
    }

    public void LoadMoreFeed()
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var scope = FeedScope;
        var lane = feedLanes[(int)scope];
        var cursor = lane.Cursor;
        if (cursor is null || lane.LoadingMore || lane.Loading)
        {
            return;
        }

        lane.LoadingMore = true;
        work.Run("feed more", async token =>
        {
            var page = await client.FeedAsync(ScopeKey(scope), cursor, token).ConfigureAwait(false);
            if (page is not null)
            {
                lane.ApplyMore(page.Items, page.NextCursor);
            }
        }, () => lane.LoadingMore = false);
    }

    private static string ScopeKey(VelvetFeedScope scope) =>
        scope == VelvetFeedScope.All ? "all" : "connections";

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
            if (status.CanEncrypt
                && cipher.TryEncrypt(scope, status.CurrentGeneration, trimmed, MyUserId, out var encoded))
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

    public void Block(string userId, Action<bool> onComplete)
    {
        blockedLoaded = false;
        ForgetConnection(userId, VelvetConnectionState.Blocked);
        work.Run("block", async token => await safety.BlockAsync(userId, token).ConfigureAwait(false), onComplete);
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

    public void CreatePost(string[] sourcePaths, WallpaperCrop[] crops, string caption, string[] tags,
        int audience, Action<bool> onComplete)
    {
        if (posting || sourcePaths.Length == 0)
        {
            return;
        }

        posting = true;
        work.Run("create post", async token =>
        {
            var mediaKeys = new string[sourcePaths.Length];
            for (var index = 0; index < sourcePaths.Length; index++)
            {
                var baked = ImageProcessor.BakeSquareJpeg(sourcePaths[index], crops[index], PostSize);
                var upload = await media.UploadUrlAsync("image/jpeg", "velvet", token).ConfigureAwait(false);
                if (upload is null)
                {
                    return false;
                }

                var uploaded = await media.UploadImageAsync(upload.UploadUrl, baked.Bytes, "image/jpeg", token)
                    .ConfigureAwait(false);
                if (!uploaded)
                {
                    return false;
                }

                mediaKeys[index] = upload.Key;
            }

            var request =
                new CreateVelvetPostRequest(mediaKeys[0], PostSize, PostSize, caption, tags, mediaKeys, audience);
            var created = await client.CreatePostAsync(request, token).ConfigureAwait(false);
            if (created is null)
            {
                return false;
            }

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
            var post = await client.PostAsync(postId, token).ConfigureAwait(false);
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
            var page = await client.PostLikersAsync(postId, null, token).ConfigureAwait(false);
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
            var page = await client.CommentsAsync(postId, token).ConfigureAwait(false);
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
            var created = await client.AddCommentAsync(postId, trimmed, token).ConfigureAwait(false);
            if (created is null)
            {
                return false;
            }

            if (detailPostId == postId)
            {
                detailComments = CopyOnWrite.Append(detailComments, created);
            }

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
                ? await client.LikeCommentAsync(comment.PostId, comment.Id, token).ConfigureAwait(false)
                : await client.UnlikeCommentAsync(comment.PostId, comment.Id, token).ConfigureAwait(false);
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
            async token => await client.DeletePostAsync(postId, token).ConfigureAwait(false));
    }

    public void ToggleReaction(VelvetPostDto post, int kind)
    {
        var target = post.MyReaction == kind ? -1 : kind;
        AcceptPostEverywhere(ApplyReaction(post, target));
        work.Run("reaction", async token =>
        {
            var result = target < 0
                ? await client.RemoveReactionAsync(post.Id, token).ConfigureAwait(false)
                : await client.ReactAsync(post.Id, target, token).ConfigureAwait(false);
            if (result is not null)
            {
                AcceptPostEverywhere(result);
            }
        });
    }

    private void AcceptPostEverywhere(VelvetPostDto post)
    {
        for (var laneIndex = 0; laneIndex < feedLanes.Length; laneIndex++)
        {
            feedLanes[laneIndex].Items = CopyOnWrite.Replace(feedLanes[laneIndex].Items, post);
        }

        if (fetchedPost?.Id == post.Id)
        {
            fetchedPost = post;
        }
    }

    private static VelvetPostDto ApplyReaction(VelvetPostDto post, int nextKind)
    {
        var (counts, total) = ReactionTally.Shift(post.ReactionCounts, post.MyReaction, nextKind);
        return post with { ReactionCounts = counts, TotalReactions = total, MyReaction = nextKind };
    }

    public void Report(string targetType, string targetId, string? reason, Action<bool> onComplete)
    {
        work.Run("report",
            async token => await safety.ReportAsync(targetType, targetId, reason, token).ConfigureAwait(false),
            onComplete);
    }

    public void DeletePost(string postId, Action<bool> onComplete)
    {
        work.Run("delete post", async token =>
        {
            var succeeded = await client.DeletePostAsync(postId, token).ConfigureAwait(false);
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
            async token => await client.DeleteCommentAsync(postId, commentId, token).ConfigureAwait(false));
    }

    public void DeleteComment(string postId, string commentId, Action<bool> onComplete)
    {
        work.Run("comment delete", async token =>
        {
            var succeeded = await client.DeleteCommentAsync(postId, commentId, token).ConfigureAwait(false);
            if (succeeded && detailPostId == postId)
            {
                detailComments = CopyOnWrite.RemoveById(detailComments, commentId);
            }

            return succeeded;
        }, onComplete);
    }

    public void ClearDiscover()
    {
        discoverResults = Array.Empty<VelvetProfileDto>();
        discoverCursor = null;
    }

    public void InvalidateLists()
    {
        discoverLoaded = false;
        connectionsLoaded = false;
        requestsLoaded = false;
        sentRequestsLoaded = false;
        feedLoadedAll = false;
        feedLoadedConnections = false;
        blockedLoaded = false;
        InvalidateThreadList();
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
        ThreadListItems = CopyOnWrite.RemoveById(ThreadListItems, userId);
        CloseThreadIfCurrent(userId);
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
        for (var laneIndex = 0; laneIndex < feedLanes.Length; laneIndex++)
        {
            feedLanes[laneIndex].Items = CopyOnWrite.RemoveById(feedLanes[laneIndex].Items, postId);
        }
    }

    protected override void DisposeCore()
    {
        signals.VelvetPinged -= OnVelvetPinged;
    }
}
