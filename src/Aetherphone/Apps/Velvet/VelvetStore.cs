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

internal sealed partial class VelvetStore : ChatThreadStoreBase<VelvetMessageDto, VelvetThreadDto>
{
    private const int PostSize = 1080;
    private const int CardPhotoWidth = 864;
    private const int CardPhotoHeight = 1080;
    private readonly VelvetClient client;
    private readonly AccountClient account;
    private readonly Configuration configuration;
    private readonly RealtimeSignalBus signals;
    private readonly RetryGate meGate = new RetryGate(TimeSpan.FromSeconds(30));
    private readonly FeedLane<VelvetPostDto>[] feedLanes =
    {
        new FeedLane<VelvetPostDto>(ByNewestFirst, ByCreatedAtUnix),
        new FeedLane<VelvetPostDto>(ByNewestFirst, ByCreatedAtUnix),
    };
    private volatile bool velvetKeysHydrated;
    private volatile int accountEpoch;
    private volatile VelvetProfileDto? me;
    private volatile bool loadingMe;
    private volatile bool accessBlocked;
    private volatile bool regionBlocked;
    private volatile bool avatarBusy;
    private volatile bool cardPhotoBusy;
    private volatile AvatarUploadOutcome cardPhotoFailure = AvatarUploadOutcome.Unreachable;
    private volatile AvatarUploadOutcome avatarFailure = AvatarUploadOutcome.Unreachable;
    private volatile bool introBusy;
    private volatile VelvetProfileDto[] discoverResults = Array.Empty<VelvetProfileDto>();
    private volatile HashSet<string> notInterestedIds = EmptyIds;
    private volatile Dictionary<string, long> passedAt = EmptyPasses;
    private volatile bool loadingDiscover;
    private volatile bool discoverLoaded;
    private volatile AepFailureBox? discoverFailureBox;
    private volatile string? discoverCursor;
    private volatile bool loadingMoreDiscover;
    private volatile VelvetDiscoverFilter discoverFilter = VelvetDiscoverFilter.Empty;
    private volatile string discoverTags = string.Empty;
    private volatile string discoverRegion = string.Empty;
    private volatile int discoverEpoch;
    private volatile VelvetProfileDto[] searchResults = Array.Empty<VelvetProfileDto>();
    private volatile bool loadingSearch;
    private volatile bool searchLoaded;
    private volatile int searchEpoch;
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
    private volatile bool profileRevalidating;
    private volatile string? detailPostId;
    private volatile VelvetCommentDto[] detailComments = Array.Empty<VelvetCommentDto>();
    private volatile string? commentsCursor;
    private volatile bool commentsLoadingMore;
    private volatile bool loadingComments;
    private volatile bool commenting;
    private volatile bool feedLoadedAll;
    private volatile bool feedLoadedConnections;
    private volatile int feedScope = (int)VelvetFeedScope.All;
    private volatile VelvetDiscoverFilter feedFilter = VelvetDiscoverFilter.Empty;
    private volatile string feedRegion = string.Empty;
    private volatile string[] feedPostTags = Array.Empty<string>();
    private volatile int feedEpoch;
    private volatile bool posting;
    private volatile VelvetPostDto? fetchedPost;
    private volatile string? fetchingPostId;
    private volatile string? likersPostId;
    private volatile UserDto[] likers = Array.Empty<UserDto>();
    private volatile string? likersCursor;
    private volatile bool likersLoadingMore;
    private volatile bool likersLoading;
    private volatile bool likersFailed;
    private int likersGeneration;
    private volatile UserDto[] blocked = Array.Empty<UserDto>();
    private volatile bool loadingBlocked;
    private volatile bool blockedLoaded;
    private volatile VelvetProfileDto[] notInterested = Array.Empty<VelvetProfileDto>();
    private readonly VelvetNotInterestedArchive notInterestedArchive;
    private static readonly HashSet<string> EmptyIds = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, long> EmptyPasses = new(StringComparer.Ordinal);
    private const long PassLifetimeSeconds = 30L * 24L * 60L * 60L;
    private volatile bool notInterestedIdsLoaded;
    private volatile bool notInterestedLoaded;
    private volatile bool loadingNotInterested;
    private Task? notInterestedIdsLoadTask;
    private readonly object notInterestedIdsSync = new();
    private readonly FeedImpressions impressions = new();
    private readonly FeedSignalQueue feedSignals;

    public VelvetStore(AethernetSession session, VelvetClient client, AccountClient account, SafetyClient safety,
        MediaClient media, NotificationService notifications, Configuration configuration, KeyVault vault,
        ConversationKeyStore keys, DecryptedHistoryStore chatHistory, PhoneVisibility visibility,
        RealtimeSignalBus signals, AppInstaller installer,
        VelvetNotInterestedArchive notInterestedArchive)
        : base("Velvet", session, safety, media, notifications, vault, keys, chatHistory, visibility,
            installer.Gate("velvet"))
    {
        this.client = client;
        this.account = account;
        this.configuration = configuration;
        this.signals = signals;
        this.notInterestedArchive = notInterestedArchive;
        feedSignals = new FeedSignalQueue(client.ReportSeenAsync, client.ReportSignalAsync, work);
        signals.VelvetPinged += OnVelvetPinged;
        signals.SocialPinged += OnSocialPinged;
        signals.ConnectedChanged += OnRealtimeConnected;
        signals.ContentRemoved += OnContentRemoved;
    }

    private void OnContentRemoved(ContentRemovalSignal removal)
    {
        if (string.Equals(removal.Kind, ContentRemovalKinds.Post, StringComparison.Ordinal))
        {
            RemovePost(removal.ContentId);
            return;
        }

        if (string.Equals(removal.Kind, ContentRemovalKinds.Comment, StringComparison.Ordinal))
        {
            detailComments = CopyOnWrite.RemoveById(detailComments, removal.ContentId);
        }
    }

    private void OnSocialPinged()
    {
        if (TickActive)
        {
            RefreshRequests();
        }
    }

    private void OnRealtimeConnected(bool active)
    {
        if (active)
        {
            InboxCadence.RequestImmediate();
        }
    }

    public override bool RealtimePushActive => signals.RealtimeActive;

    private void OnVelvetPinged()
    {
        InboxCadence.RequestImmediate();
        RequestThreadRefresh();
    }

    public MentionSuggestions NewMentionSuggestions() => new(account, work);

    public void BeginImpressions(float windowTop, float windowBottom, float deltaSeconds)
    {
        impressions.BeginFrame(windowTop, windowBottom, deltaSeconds);
        feedSignals.Tick(DateTime.UtcNow);
    }

    public void ObserveImpression(string postId, float rowTop, float rowBottom)
    {
        if (impressions.Observe(postId, rowTop, rowBottom))
        {
            feedSignals.MarkSeen(postId);
        }
    }

    public void ReportFeedSignal(string postId, int kind)
    {
        feedSignals.Signal(postId, kind);
    }

    public void FlushFeedSignals()
    {
        feedSignals.Flush(DateTime.UtcNow);
    }

    public VelvetProfileDto? Me => me;
    public bool AccessBlocked => accessBlocked;
    public bool RegionBlocked => regionBlocked;
    public bool HasProfile => me is not null;
    public bool AvatarBusy => avatarBusy;

    public AvatarUploadOutcome AvatarFailure => avatarFailure;
    public bool CardPhotoBusy => cardPhotoBusy;
    public AvatarUploadOutcome CardPhotoFailure => cardPhotoFailure;
    public bool IntroBusy => introBusy;
    public VelvetProfileDto[] DiscoverResults => discoverResults;
    public bool LoadingDiscover => loadingDiscover;
    public bool DiscoverLoaded => discoverLoaded;

    public bool DiscoverFailed => discoverFailureBox is not null;

    public AepFailure DiscoverFailure => discoverFailureBox?.Failure ?? AepFailure.None;
    public bool HasMoreDiscover => discoverCursor is not null;
    public bool LoadingMoreDiscover => loadingMoreDiscover;
    public int PassCount => passedAt.Count;
    public VelvetProfileDto[] SearchResults => searchResults;
    public bool LoadingSearch => loadingSearch;
    public bool SearchLoaded => searchLoaded;
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
    public ITrimmable FeedSource => ActiveFeedLane;
    private FeedLane<VelvetPostDto> ActiveFeedLane => feedLanes[feedScope];
    public bool Posting => posting;
    public VelvetPostDto? FetchedPost => fetchedPost;
    public UserDto[] Likers => likers;
    public bool LikersLoading => likersLoading;
    public bool LikersLoadingMore => likersLoadingMore;
    public bool HasMoreLikers => likersCursor is not null;
    public bool LikersFailed => likersFailed;
    public UserDto[] Blocked => blocked;
    public bool LoadingBlocked => loadingBlocked;
    public bool BlockedLoaded => blockedLoaded;

    public VelvetProfileDto[] NotInterested => notInterested;
    public bool NotInterestedLoaded => notInterestedLoaded;
    public bool LoadingNotInterested => loadingNotInterested;

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
        accessBlocked = false;
        regionBlocked = false;
        meGate.Reset();
        discoverResults = Array.Empty<VelvetProfileDto>();
        ResetUserPosts();
        ResetTagPosts();

        notInterestedIds = EmptyIds;
        passedAt = EmptyPasses;
        notInterestedLoaded = false;
        searchEpoch++;
        searchResults = Array.Empty<VelvetProfileDto>();
        loadingSearch = false;
        searchLoaded = false;
        notInterestedIdsLoaded = false;
        notInterested = Array.Empty<VelvetProfileDto>();
        discoverCursor = null;
        discoverLoaded = false;
        discoverFailureBox = null;
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
        detailPostId = null;
        detailComments = Array.Empty<VelvetCommentDto>();
        commentsCursor = null;
        for (var laneIndex = 0; laneIndex < feedLanes.Length; laneIndex++)
        {
            feedLanes[laneIndex].Clear();
        }

        feedLoadedAll = false;
        feedLoadedConnections = false;
        fetchedPost = null;
        fetchingPostId = null;
        Interlocked.Increment(ref likersGeneration);
        likersPostId = null;
        likers = Array.Empty<UserDto>();
        likersCursor = null;
        likersLoading = false;
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

    protected override async Task<ThreadListPage?> FetchThreadListAsync(string? cursor, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        await EnsureVelvetHydratedAsync(token).ConfigureAwait(false);
        var page = await client.ThreadsAsync(cursor, token, onFailure).ConfigureAwait(false);
        return page is null ? null : new ThreadListPage(page.Items, page.NextCursor);
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

    protected override Task<bool> DeleteThreadRequestAsync(string threadId, CancellationToken token) =>
        client.DeleteThreadAsync(threadId, token);

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

    protected override int MessageKindOf(VelvetMessageDto message) => message.Kind;

    protected override string MessageSenderIdOf(VelvetMessageDto message) => message.SenderId;

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

    protected override bool IsInboxPreviewReady(VelvetThreadDto thread)
    {
        return thread.LastMessageEncVersion != EnvelopeCodec.VersionEnvelope
            || cipher.IsPreviewResolved(thread.OtherUserId, thread.LastMessageAtUnix);
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

        tagLane.Items = CopyOnWrite.RemoveById(tagLane.Items, postId);
        var remainingUserPosts = CopyOnWrite.RemoveById(userPosts, postId);
        if (!ReferenceEquals(remainingUserPosts, userPosts))
        {
            userPosts = remainingUserPosts;
            userPostsTotal = Math.Max(0, userPostsTotal - 1);
        }

        if (fetchedPost is { } current && current.Id == postId)
        {
            fetchedPost = null;
            fetchingPostId = null;
        }

        if (detailPostId == postId)
        {
            detailPostId = null;
            detailComments = Array.Empty<VelvetCommentDto>();
            commentsCursor = null;
        }
    }

    private void EnsureNotInterestedLoaded()
    {
        if (!session.IsSignedIn || notInterestedIdsLoaded)
        {
            return;
        }

        work.Run("discover not interested load", token => EnsureNotInterestedLoadedAsync(token));
    }

    private Task EnsureNotInterestedLoadedAsync(CancellationToken token)
    {
        if (notInterestedIdsLoaded)
        {
            return Task.CompletedTask;
        }

        lock (notInterestedIdsSync)
        {
            if (notInterestedIdsLoaded)
            {
                return Task.CompletedTask;
            }

            notInterestedIdsLoadTask ??= LoadNotInterestedIdsAsync(token);
            return notInterestedIdsLoadTask;
        }
    }

    private async Task LoadNotInterestedIdsAsync(CancellationToken token)
    {
        var epoch = accountEpoch;
        var loaded = false;
        try
        {
            var accountId = MyUserId;
            if (accountId.Length == 0)
            {
                return;
            }

            var snapshot = await Task.Run(() => notInterestedArchive.Load(accountId), token).ConfigureAwait(false);
            if (epoch != accountEpoch)
            {
                return;
            }

            notInterestedIds = MergeNotInterested(notInterestedIds, snapshot.UserIds);
            passedAt = MergePasses(passedAt, snapshot.Passes, UnixNow());
            discoverResults = WithoutNotInterested(discoverResults);
            loaded = true;
        }
        finally
        {
            if (loaded && epoch == accountEpoch)
            {
                notInterestedIdsLoaded = true;
            }

            lock (notInterestedIdsSync)
            {
                notInterestedIdsLoadTask = null;
            }
        }
    }

    private static long UnixNow() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static bool PassActive(Dictionary<string, long> passes, string userId, long now) =>
        passes.TryGetValue(userId, out var stamp) && now - stamp < PassLifetimeSeconds;

    private static Dictionary<string, long> MergePasses(Dictionary<string, long> existing,
        Dictionary<string, long> incoming, long now)
    {
        var merged = new Dictionary<string, long>(existing, StringComparer.Ordinal);
        var changed = false;
        foreach (var (userId, stamp) in incoming)
        {
            if (now - stamp >= PassLifetimeSeconds || merged.ContainsKey(userId))
            {
                continue;
            }

            merged[userId] = stamp;
            changed = true;
        }

        return changed ? merged : existing;
    }

    private static HashSet<string> MergeNotInterested(HashSet<string> existing, string[] incoming)
    {
        var merged = new HashSet<string>(existing, StringComparer.Ordinal);
        var added = false;
        for (var index = 0; index < incoming.Length; index++)
        {
            added |= merged.Add(incoming[index]);
        }

        return added ? merged : existing;
    }

    private static VelvetProfileDto[] RemoveProfile(VelvetProfileDto[] source, string userId)
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

    protected override void DisposeCore()
    {
        signals.VelvetPinged -= OnVelvetPinged;
        signals.SocialPinged -= OnSocialPinged;
        signals.ConnectedChanged -= OnRealtimeConnected;
        signals.ContentRemoved -= OnContentRemoved;
    }
}
