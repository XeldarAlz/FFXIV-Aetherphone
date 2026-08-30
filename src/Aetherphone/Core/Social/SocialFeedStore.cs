using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Aetherphone.Core.Wallpapers;

namespace Aetherphone.Core.Social;

internal enum SocialFeedScope
{
    ForYou,
    Following,
}

internal enum FollowState
{
    None,
    Requested,
    Following,
}

internal abstract class SocialFeedStore : IDisposable
{
    private const int CommentImageDimension = 1280;
    private const string CommentUploadScope = "comment";

    protected readonly AethernetSession session;
    protected readonly AccountClient account;
    protected readonly SocialClient client;
    protected readonly SafetyClient safety;
    protected readonly MediaClient media;
    protected readonly RealtimeSignalBus signals;
    protected readonly StoreWork work;
    private readonly RetryGate meGate = new(TimeSpan.FromSeconds(30));
    private volatile UserDto? me;
    private volatile AvatarUploadOutcome avatarFailure = AvatarUploadOutcome.Unreachable;
    protected readonly FeedLane<PostDto> forYouLane = new(ByNewestFirst, ByCreatedAtUnix);
    protected readonly FeedLane<PostDto> followingLane = new(ByNewestFirst, ByCreatedAtUnix);
    private readonly FeedLane<PostDto> savedLane = new(ByNewestFirst);
    private readonly FeedLane<PostDto> likedLane = new(ByNewestFirst);
    private volatile UserDto[] followRequests = Array.Empty<UserDto>();
    private volatile string? followRequestsCursor;
    private volatile bool followRequestsLoadingMore;
    private volatile bool followRequestsLoading;
    private volatile bool followRequestsLoaded;
    protected readonly FeedLane<PostDto> profileLane = new(ByNewestFirst);
    protected volatile PostDto? detailPost;
    protected volatile bool posting;
    private volatile string? profileUserId;
    private volatile UserDto? profileUser;
    private volatile bool profileLoading;
    private volatile bool profileFailed;
    private volatile bool profileRevalidating;
    private volatile string? detailPostId;
    private volatile CommentDto[] detailComments = Array.Empty<CommentDto>();
    private volatile string? commentsCursor;
    private volatile bool commentsLoadingMore;
    private volatile bool detailLoading;
    private volatile bool commenting;
    private volatile UserDto[] discoverResults = Array.Empty<UserDto>();
    private volatile TagSummaryDto[] discoverTags = Array.Empty<TagSummaryDto>();
    private volatile bool tagsLoading;
    private volatile bool searching;
    private volatile bool loadingMe;
    private volatile string? userListKey;
    private volatile UserDto[] userListResults = Array.Empty<UserDto>();
    private volatile string? userListCursor;
    private volatile bool userListLoadingMore;
    private volatile bool userListLoading;
    private volatile bool userListFailed;
    private UserListKind userListKind;
    private volatile string? userListSourceId;
    private volatile Dictionary<string, int>? userListReactionKinds;
    private volatile int[]? userListReactionCounts;
    private volatile int userListReactionFilter = -1;
    private volatile int userListTotal = -1;
    private int userListGeneration;
    private readonly FeedLane<PostDto> taggedLane = new(ByNewestFirst);
    private volatile string? taggedUserId;
    private readonly FeedLane<PostDto> hashtagLane = new(ByNewestFirst);
    private volatile string? hashtagTag;
    private volatile string? feedRegions;
    private string? lastAccountId;

    protected SocialFeedStore(
        AethernetSession session,
        AccountClient account,
        SocialClient client,
        SafetyClient safety,
        MediaClient media,
        RealtimeSignalBus signals,
        string logTag)
    {
        this.session = session;
        this.account = account;
        this.client = client;
        this.safety = safety;
        this.media = media;
        this.signals = signals;
        work = new StoreWork(logTag);
        session.Changed += OnSessionChanged;
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

    private void OnSessionChanged()
    {
        var accountId = session.CurrentUser?.Id;
        if (string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            return;
        }

        lastAccountId = accountId;
        me = null;
        meGate.Reset();
        forYouLane.Clear();
        followingLane.Clear();
        profileLane.Clear();
        detailPost = null;
        profileUserId = null;
        profileUser = null;
        profileLoading = false;
        profileFailed = false;
        detailPostId = null;
        detailComments = Array.Empty<CommentDto>();
        commentsCursor = null;
        discoverResults = Array.Empty<UserDto>();
        Interlocked.Increment(ref userListGeneration);
        userListKey = null;
        userListResults = Array.Empty<UserDto>();
        userListCursor = null;
        userListLoading = false;
        userListFailed = false;
        userListReactionKinds = null;
        userListReactionCounts = null;
        userListReactionFilter = -1;
        userListTotal = -1;
        savedLane.Clear();
        likedLane.Clear();
        followRequests = Array.Empty<UserDto>();
        followRequestsCursor = null;
        followRequestsLoaded = false;
        ClearTagged();
        ClearHashtag();
    }

    public MentionSuggestions NewMentionSuggestions() => new(account, work);

    public bool IsSignedIn => session.IsSignedIn;
    public UserDto? Me => me;
    public PostDto[] Feed(SocialFeedScope scope) => Lane(scope).Items;

    public bool IsLoading(SocialFeedScope scope) => Lane(scope).Loading;

    public bool FeedFailed(SocialFeedScope scope) => Lane(scope).Failed;

    public AepFailure FeedFailure(SocialFeedScope scope) => Lane(scope).Failure;

    public bool HasMoreFeed(SocialFeedScope scope) => Lane(scope).HasMore;

    public bool LoadingMore(SocialFeedScope scope) => Lane(scope).LoadingMore;

    public ITrimmable FeedSource(SocialFeedScope scope) => Lane(scope);

    private FeedLane<PostDto> Lane(SocialFeedScope scope) =>
        scope == SocialFeedScope.ForYou ? forYouLane : followingLane;

    private static string FeedKey(SocialFeedScope scope) =>
        scope == SocialFeedScope.ForYou ? "explore" : "following";

    public string? ProfileUserId => profileUserId;
    public UserDto? ProfileUser => profileUser;
    public PostDto[] ProfilePosts => profileLane.Items;
    public bool ProfileLoading => profileLoading;
    public bool ProfileLoadingMore => profileLane.LoadingMore;
    public bool HasMoreProfilePosts => profileLane.HasMore;
    public bool ProfileFailed => profileFailed;
    public PostDto? DetailPost => detailPost;
    public CommentDto[] DetailComments => detailComments;
    public bool HasMoreComments => commentsCursor is not null;
    public bool CommentsLoadingMore => commentsLoadingMore;
    public bool DetailLoading => detailLoading;
    public bool Commenting => commenting;
    public UserDto[] DiscoverResults => discoverResults;
    public TagSummaryDto[] DiscoverTags => discoverTags;
    public bool TagsLoading => tagsLoading;
    public bool Searching => searching;
    public bool Posting => posting;

    public AvatarUploadOutcome AvatarFailure => avatarFailure;
    public UserDto[] UserListResults => userListResults;
    public bool UserListLoading => userListLoading;
    public bool UserListLoadingMore => userListLoadingMore;
    public bool HasMoreUserList => userListCursor is not null;
    public bool UserListFailed => userListFailed;
    public int[]? UserListReactionCounts => userListReactionCounts;
    public int UserListReactionFilter => userListReactionFilter;
    public int UserListTotal => userListTotal;

    public int ReactionKindOf(string userId)
    {
        var kinds = userListReactionKinds;
        return kinds is not null && kinds.TryGetValue(userId, out var kind) ? kind : -1;
    }
    public UserDto[] FollowRequests => followRequests;
    public bool FollowRequestsLoading => followRequestsLoading;
    public bool FollowRequestsLoadingMore => followRequestsLoadingMore;
    public bool HasMoreFollowRequests => followRequestsCursor is not null;

    public int PendingFollowRequestCount =>
        followRequestsLoaded && followRequestsCursor is null
            ? followRequests.Length
            : Math.Max(followRequests.Length, Me?.PendingFollowRequests ?? 0);

    public PostDto[] SavedPosts => savedLane.Items;
    public bool SavedLoading => savedLane.Loading;
    public bool SavedLoadingMore => savedLane.LoadingMore;
    public bool HasMoreSaved => savedLane.HasMore;
    public PostDto[] LikedPosts => likedLane.Items;
    public bool LikedLoading => likedLane.Loading;
    public bool LikedLoadingMore => likedLane.LoadingMore;
    public bool HasMoreLiked => likedLane.HasMore;

    public static FollowState FollowStateOf(UserDto user) =>
        user.IsFollowing ? FollowState.Following
        : user.FollowRequested ? FollowState.Requested
        : FollowState.None;

    protected abstract Task<FeedPage?> FetchFeedAsync(string feedKey, string? cursor, string? regions,
        CancellationToken token, Action<AepFailure>? onFailure = null);

    protected abstract Task<FeedPage?> FetchProfilePostsAsync(string userId, string? cursor, CancellationToken token);

    protected virtual Task<FeedPage?> FetchTaggedPostsAsync(string userId, string? cursor, CancellationToken token) =>
        Task.FromResult<FeedPage?>(null);

    public PostDto[] TaggedPosts => taggedLane.Items;

    public bool TaggedLoading => taggedLane.Loading;
    public bool TaggedLoadingMore => taggedLane.LoadingMore;
    public bool HasMoreTagged => taggedLane.HasMore;

    public void EnsureTaggedPosts(string userId)
    {
        if (!session.IsSignedIn || taggedLane.Loading || string.Equals(taggedUserId, userId, StringComparison.Ordinal))
        {
            return;
        }

        taggedUserId = userId;
        taggedLane.Clear();
        taggedLane.Loading = true;
        work.Run("tagged load", async token =>
        {
            var page = await FetchTaggedPostsAsync(userId, null, token).ConfigureAwait(false);
            if (page is not null && string.Equals(taggedUserId, userId, StringComparison.Ordinal))
            {
                taggedLane.ApplyRefresh(page.Items, page.NextCursor);
            }
        }, () => taggedLane.Loading = false);
    }

    public void LoadMoreTaggedPosts()
    {
        var userId = taggedUserId;
        var cursor = taggedLane.Cursor;
        if (!session.IsSignedIn || userId is null || cursor is null || taggedLane.LoadingMore || taggedLane.Loading)
        {
            return;
        }

        taggedLane.LoadingMore = true;
        work.Run("tagged more", async token =>
        {
            var page = await FetchTaggedPostsAsync(userId, cursor, token).ConfigureAwait(false);
            if (page is not null && string.Equals(taggedUserId, userId, StringComparison.Ordinal))
            {
                taggedLane.ApplyMore(page.Items, page.NextCursor);
            }
        }, () => taggedLane.LoadingMore = false);
    }

    protected void ClearTagged()
    {
        taggedUserId = null;
        taggedLane.Clear();
    }

    protected virtual Task<FeedPage?> FetchHashtagPostsAsync(string tag, string? cursor, CancellationToken token) =>
        Task.FromResult<FeedPage?>(null);

    public PostDto[] HashtagPosts => hashtagLane.Items;
    public string? HashtagTag => hashtagTag;

    public bool HashtagLoading => hashtagLane.Loading;
    public bool HashtagLoadingMore => hashtagLane.LoadingMore;
    public bool HasMoreHashtagPosts => hashtagLane.HasMore;

    public void OpenHashtagPosts(string tag)
    {
        if (!session.IsSignedIn || hashtagLane.Loading)
        {
            return;
        }

        RefreshHashtagLane(tag);
    }

    public void EnsureHashtagPosts(string tag)
    {
        if (!session.IsSignedIn || hashtagLane.Loading || string.Equals(hashtagTag, tag, StringComparison.Ordinal))
        {
            return;
        }

        RefreshHashtagLane(tag);
    }

    private void RefreshHashtagLane(string tag)
    {
        hashtagTag = tag;
        hashtagLane.Clear();
        hashtagLane.Loading = true;
        work.Run("hashtag load", async token =>
        {
            var page = await FetchHashtagPostsAsync(tag, null, token).ConfigureAwait(false);
            if (page is not null && string.Equals(hashtagTag, tag, StringComparison.Ordinal))
            {
                hashtagLane.ApplyRefresh(page.Items, page.NextCursor);
            }
        }, () => hashtagLane.Loading = false);
    }

    public void LoadMoreHashtagPosts()
    {
        var tag = hashtagTag;
        var cursor = hashtagLane.Cursor;
        if (!session.IsSignedIn || tag is null || cursor is null || hashtagLane.LoadingMore || hashtagLane.Loading)
        {
            return;
        }

        hashtagLane.LoadingMore = true;
        work.Run("hashtag more", async token =>
        {
            var page = await FetchHashtagPostsAsync(tag, cursor, token).ConfigureAwait(false);
            if (page is not null && string.Equals(hashtagTag, tag, StringComparison.Ordinal))
            {
                hashtagLane.ApplyMore(page.Items, page.NextCursor);
            }
        }, () => hashtagLane.LoadingMore = false);
    }

    protected void ClearHashtag()
    {
        hashtagTag = null;
        hashtagLane.Clear();
    }

    public void EnsureMe()
    {
        ReconcileAccountBadges();
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
            var profile = await account.MeAsync(token).ConfigureAwait(false);
            if (profile is not null)
            {
                me = profile;
            }
        }, () => loadingMe = false);
    }

    private void ReconcileAccountBadges()
    {
        var current = me;
        var signedInUser = session.CurrentUser;
        if (current is null || signedInUser is null || current.Badges == signedInUser.Badges)
        {
            return;
        }

        me = current with { Badges = signedInUser.Badges };
    }

    public void SetFeedRegions(string? regionsCsv)
    {
        if (string.Equals(feedRegions, regionsCsv, StringComparison.Ordinal))
        {
            return;
        }

        feedRegions = regionsCsv;
        forYouLane.Clear();
        RefreshFeed(SocialFeedScope.ForYou);
    }

    private string? RegionsFor(SocialFeedScope scope) =>
        scope == SocialFeedScope.ForYou ? feedRegions : null;

    public void RefreshFeed(SocialFeedScope scope)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var lane = Lane(scope);
        lane.Loading = true;
        var regions = RegionsFor(scope);
        work.Run("feed refresh", async token =>
        {
            var reported = AepFailure.None;
            var page = await FetchFeedAsync(FeedKey(scope), null, regions, token, failure => reported = failure)
                .ConfigureAwait(false);
            if (page is not null)
            {
                lane.ApplyRefresh(page.Items, page.NextCursor);
                return;
            }

            lane.RecordFailure(reported.Failed ? reported : AepFailure.Transport(AepFailureKind.Offline));
            AepLog.Warning($"Feed '{FeedKey(scope)}' failed to refresh: {lane.Failure.Describe()}");
        }, () => lane.Loading = false);
    }

    public void LoadMoreFeed(SocialFeedScope scope)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var lane = Lane(scope);
        var cursor = lane.Cursor;
        if (cursor is null || lane.LoadingMore || lane.Loading)
        {
            return;
        }

        lane.LoadingMore = true;
        var regions = RegionsFor(scope);
        work.Run("feed more", async token =>
        {
            var reported = AepFailure.None;
            var page = await FetchFeedAsync(FeedKey(scope), cursor, regions, token, failure => reported = failure)
                .ConfigureAwait(false);
            if (page is not null)
            {
                lane.ApplyMore(page.Items, page.NextCursor);
                return;
            }

            lane.RecordFailure(reported.Failed ? reported : AepFailure.Transport(AepFailureKind.Offline));
            AepLog.Warning($"Feed '{FeedKey(scope)}' failed to load more: {lane.Failure.Describe()}");
        }, () => lane.LoadingMore = false);
    }

    private static int ByNewestFirst(PostDto left, PostDto right)
    {
        var byTime = right.CreatedAtUnix.CompareTo(left.CreatedAtUnix);
        return byTime != 0 ? byTime : string.CompareOrdinal(right.Id, left.Id);
    }

    private static long ByCreatedAtUnix(PostDto post) => post.CreatedAtUnix;

    public void OpenDetail(PostDto post) => LoadDetail(post.Id, post);

    public void OpenDetailById(string postId) => LoadDetail(postId, null);

    private void LoadDetail(string postId, PostDto? cached)
    {
        detailPostId = postId;
        detailPost = cached;
        detailComments = Array.Empty<CommentDto>();
        commentsCursor = null;
        detailLoading = true;
        work.Run("detail load", async token =>
        {
            var post = await client.PostAsync(postId, token).ConfigureAwait(false);
            if (detailPostId != postId)
            {
                return;
            }

            if (post is not null)
            {
                detailPost = post;
                ReplacePost(post);
            }

            var page = await client.CommentsAsync(postId, null, token).ConfigureAwait(false);
            if (detailPostId == postId && page is not null)
            {
                detailComments = CopyOnWrite.Reversed(page.Items);
                commentsCursor = page.NextCursor;
            }
        }, () =>
        {
            if (detailPostId == postId)
            {
                detailLoading = false;
            }
        });
    }

    public void LoadMoreComments()
    {
        var postId = detailPostId;
        var cursor = commentsCursor;
        if (!session.IsSignedIn || postId is null || cursor is null || commentsLoadingMore || detailLoading)
        {
            return;
        }

        commentsLoadingMore = true;
        work.Run("comments more", async token =>
        {
            var page = await client.CommentsAsync(postId, cursor, token).ConfigureAwait(false);
            if (page is null || detailPostId != postId)
            {
                return;
            }

            detailComments = CopyOnWrite.PrependOlderPage(detailComments, page.Items);
            commentsCursor = page.NextCursor;
        }, () => commentsLoadingMore = false);
    }

    public void AddComment(string postId, string text, string? attachmentPath, Action<bool> onComplete,
        Action<AepFailure>? onFailure = null)
    {
        var trimmed = text.Trim();
        if ((trimmed.Length == 0 && attachmentPath is null) || commenting)
        {
            return;
        }

        commenting = true;
        work.Run("comment", async token =>
        {
            string? mediaKey = null;
            var mediaWidth = 0;
            var mediaHeight = 0;
            if (attachmentPath is not null)
            {
                var uploaded = await UploadImagesAsync(new[] { attachmentPath }, 1, CommentImageDimension,
                    CommentUploadScope, token, onFailure).ConfigureAwait(false);
                if (uploaded is null || uploaded.Value.Keys.Length == 0)
                {
                    return false;
                }

                mediaKey = uploaded.Value.Keys[0];
                mediaWidth = uploaded.Value.Width;
                mediaHeight = uploaded.Value.Height;
            }

            var created = await client.AddCommentAsync(postId, trimmed, mediaKey, mediaWidth, mediaHeight, token,
                onFailure).ConfigureAwait(false);
            if (created is null)
            {
                AepLog.Warning($"Comment on {postId} was not accepted");
                return false;
            }

            if (detailPostId == postId)
            {
                detailComments = CopyOnWrite.Append(detailComments, created);
            }

            BumpCommentCount(postId, 1);
            return true;
        }, onComplete, () => commenting = false);
    }

    protected async Task<(string[] Keys, int Width, int Height)?> UploadImagesAsync(
        IReadOnlyList<string> imagePaths, int maxImages, int maxDimension, string uploadScope,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        if (imagePaths.Count == 0)
        {
            return (Array.Empty<string>(), 0, 0);
        }

        var keys = new string[Math.Min(imagePaths.Count, maxImages)];
        var firstWidth = 0;
        var firstHeight = 0;
        for (var index = 0; index < keys.Length; index++)
        {
            byte[] bytes;
            string contentType;
            int width;
            int height;
            if (GifMedia.IsGif(imagePaths[index]))
            {
                bytes = await File.ReadAllBytesAsync(imagePaths[index], token).ConfigureAwait(false);
                if (bytes.Length == 0 || bytes.Length > GifMedia.MaxBytes)
                {
                    AepLog.Warning($"Upload to {uploadScope} rejected a GIF of {bytes.Length} bytes; the cap is {GifMedia.MaxBytes}");
                    onFailure?.Invoke(new AepFailure(AepFailureKind.Server, 0, FailureCodes.MediaInvalidImage, null,
                        null, null));
                    return null;
                }

                (width, height) = ImageProcessor.IdentifyDimensions(bytes);
                contentType = "image/gif";
            }
            else
            {
                var baked = ImageProcessor.BakeJpeg(imagePaths[index], maxDimension);
                bytes = baked.Bytes;
                width = baked.Width;
                height = baked.Height;
                contentType = "image/jpeg";
            }

            var upload = await media.UploadUrlAsync(contentType, uploadScope, token, onFailure).ConfigureAwait(false);
            if (upload is null)
            {
                return null;
            }

            var sent = await media.UploadImageAsync(upload.UploadUrl, bytes, contentType, token)
                .ConfigureAwait(false);
            if (!sent)
            {
                AepLog.Warning($"Upload to {uploadScope} could not store image {index + 1} of {keys.Length}");
                onFailure?.Invoke(AepFailure.Transport(AepFailureKind.Offline));
                return null;
            }

            keys[index] = upload.Key;
            if (index == 0)
            {
                firstWidth = width;
                firstHeight = height;
            }
        }

        return (keys, firstWidth, firstHeight);
    }

    public void DeleteComment(string postId, string commentId)
    {
        if (detailPostId == postId)
        {
            detailComments = CopyOnWrite.RemoveById(detailComments, commentId);
        }

        BumpCommentCount(postId, -1);
        work.Run("comment delete",
            async token => await client.DeleteCommentAsync(postId, commentId, token).ConfigureAwait(false));
    }

    public void DeleteComment(string postId, string commentId, Action<bool> onComplete)
    {
        work.Run("comment delete", async token =>
        {
            var succeeded = await client.DeleteCommentAsync(postId, commentId, token).ConfigureAwait(false);
            if (!succeeded)
            {
                return false;
            }

            if (detailPostId == postId)
            {
                detailComments = CopyOnWrite.RemoveById(detailComments, commentId);
            }

            BumpCommentCount(postId, -1);
            return true;
        }, onComplete);
    }

    public void ToggleCommentLike(CommentDto comment)
    {
        var liked = !comment.Liked;
        detailComments = CopyOnWrite.MapById(detailComments, comment.Id, ApplyCommentLike(liked));
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

    private static Func<CommentDto, CommentDto> ApplyCommentLike(bool liked) =>
        comment => comment.Liked == liked
            ? comment
            : comment with { Liked = liked, LikeCount = Math.Max(0, comment.LikeCount + (liked ? 1 : -1)) };

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

    public void ToggleFollow(UserDto user)
    {
        switch (FollowStateOf(user))
        {
            case FollowState.Following:
                SetFollow(user.Id, false);
                break;
            case FollowState.Requested:
                CancelFollowRequest(user.Id);
                break;
            default:
                RequestFollow(user.Id, user.IsPrivate);
                break;
        }
    }

    public void SetFollow(string userId, bool follow)
    {
        if (follow)
        {
            RequestFollow(userId, false);
            return;
        }

        ApplyFollowEverywhere(userId, false, false);
        work.Run("unfollow",
            async token => await client.UnfollowAsync(userId, token).ConfigureAwait(false));
    }

    private void RequestFollow(string userId, bool targetIsPrivate)
    {
        ApplyFollowEverywhere(userId, !targetIsPrivate, targetIsPrivate);
        work.Run("follow", async token =>
        {
            var result = await client.FollowAsync(userId, token).ConfigureAwait(false);
            if (result is null)
            {
                ApplyFollowEverywhere(userId, false, false);
            }
            else
            {
                ApplyFollowEverywhere(userId, result.Following, result.Requested);
            }
        });
    }

    private void CancelFollowRequest(string userId)
    {
        ApplyFollowEverywhere(userId, false, false);
        work.Run("follow cancel",
            async token => await client.UnfollowAsync(userId, token).ConfigureAwait(false));
    }

    public void EnsureFollowRequests()
    {
        if (!session.IsSignedIn || followRequestsLoaded || followRequestsLoading)
        {
            return;
        }

        FetchFollowRequests();
    }

    public void RefreshFollowRequests()
    {
        if (!session.IsSignedIn || followRequestsLoading)
        {
            return;
        }

        FetchFollowRequests();
    }

    private void FetchFollowRequests()
    {
        followRequestsLoading = true;
        work.Run("follow requests", async token =>
        {
            var page = await client.RequestsAsync(null, token).ConfigureAwait(false);
            if (page is not null)
            {
                followRequests = page.Items;
                followRequestsCursor = page.NextCursor;
                followRequestsLoaded = true;
                if (page.NextCursor is null)
                {
                    SyncPendingFollowRequests(page.Items.Length);
                }
            }
        }, () => followRequestsLoading = false);
    }

    public void LoadMoreFollowRequests()
    {
        var cursor = followRequestsCursor;
        if (!session.IsSignedIn || cursor is null || followRequestsLoadingMore || followRequestsLoading)
        {
            return;
        }

        followRequestsLoadingMore = true;
        work.Run("follow requests more", async token =>
        {
            var page = await client.RequestsAsync(cursor, token).ConfigureAwait(false);
            if (page is null)
            {
                return;
            }

            followRequests = CopyOnWrite.AppendPageById(followRequests, page.Items);
            followRequestsCursor = page.NextCursor;
            if (page.NextCursor is null)
            {
                SyncPendingFollowRequests(followRequests.Length);
            }
        }, () => followRequestsLoadingMore = false);
    }

    public void AcceptFollowRequest(UserDto requester)
    {
        RemoveFollowRequest(requester.Id, true);
        work.Run("follow accept",
            async token => await client.AcceptFollowRequestAsync(requester.Id, token).ConfigureAwait(false));
    }

    public void DeclineFollowRequest(UserDto requester)
    {
        RemoveFollowRequest(requester.Id, false);
        work.Run("follow decline",
            async token => await client.DeclineFollowRequestAsync(requester.Id, token).ConfigureAwait(false));
    }

    private void RemoveFollowRequest(string requesterId, bool accepted)
    {
        followRequests = CopyOnWrite.RemoveWhere(followRequests, user => user.Id == requesterId);
        if (me is { } current)
        {
            me = current with
            {
                PendingFollowRequests = Math.Max(0, current.PendingFollowRequests - 1),
                Followers = accepted ? current.Followers + 1 : current.Followers,
            };
        }
    }

    private void SyncPendingFollowRequests(int count)
    {
        if (me is { } current && current.PendingFollowRequests != count)
        {
            me = current with { PendingFollowRequests = count };
        }
    }

    public void RefreshSaved()
    {
        if (!session.IsSignedIn || savedLane.Loading)
        {
            return;
        }

        savedLane.Loading = true;
        work.Run("saved refresh", async token =>
        {
            var page = await client.SavedAsync(null, token).ConfigureAwait(false);
            if (page is not null)
            {
                savedLane.ApplyRefresh(page.Items, page.NextCursor);
            }
        }, () => savedLane.Loading = false);
    }

    public void RefreshLiked()
    {
        if (!session.IsSignedIn || likedLane.Loading)
        {
            return;
        }

        likedLane.Loading = true;
        work.Run("liked refresh", async token =>
        {
            var page = await client.LikedAsync(null, token).ConfigureAwait(false);
            if (page is not null)
            {
                likedLane.ApplyRefresh(page.Items, page.NextCursor);
            }
        }, () => likedLane.Loading = false);
    }

    public void LoadMoreLiked()
    {
        var cursor = likedLane.Cursor;
        if (!session.IsSignedIn || cursor is null || likedLane.LoadingMore || likedLane.Loading)
        {
            return;
        }

        likedLane.LoadingMore = true;
        work.Run("liked more", async token =>
        {
            var page = await client.LikedAsync(cursor, token).ConfigureAwait(false);
            if (page is not null)
            {
                likedLane.ApplyMore(page.Items, page.NextCursor);
            }
        }, () => likedLane.LoadingMore = false);
    }

    public void LoadMoreSaved()
    {
        var cursor = savedLane.Cursor;
        if (!session.IsSignedIn || cursor is null || savedLane.LoadingMore || savedLane.Loading)
        {
            return;
        }

        savedLane.LoadingMore = true;
        work.Run("saved more", async token =>
        {
            var page = await client.SavedAsync(cursor, token).ConfigureAwait(false);
            if (page is not null)
            {
                savedLane.ApplyMore(page.Items, page.NextCursor);
            }
        }, () => savedLane.LoadingMore = false);
    }

    public void SetSaved(string postId, bool saved)
    {
        ApplySavedEverywhere(postId, saved);
        work.Run("save toggle", async token =>
        {
            if (saved)
            {
                await client.SavePostAsync(postId, token).ConfigureAwait(false);
            }
            else
            {
                await client.UnsavePostAsync(postId, token).ConfigureAwait(false);
            }
        });
    }

    public void SetSensitive(string postId, bool sensitive, Action<AepFailure>? onFailure = null)
    {
        ApplySensitiveEverywhere(postId, sensitive);
        work.Run("sensitive toggle", async token =>
        {
            var updated = await client.SetSensitiveAsync(postId, sensitive, token, onFailure).ConfigureAwait(false);
            if (updated is null)
            {
                ApplySensitiveEverywhere(postId, !sensitive);
            }
        });
    }

    private void ApplySensitiveEverywhere(string postId, bool sensitive)
    {
        forYouLane.Items = MapSensitive(forYouLane.Items, postId, sensitive);
        followingLane.Items = MapSensitive(followingLane.Items, postId, sensitive);
        profileLane.Items = MapSensitive(profileLane.Items, postId, sensitive);
        taggedLane.Items = MapSensitive(taggedLane.Items, postId, sensitive);
        savedLane.Items = MapSensitive(savedLane.Items, postId, sensitive);
        if (detailPost is { } current && current.Id == postId)
        {
            detailPost = current with { Sensitive = sensitive };
        }
    }

    private static PostDto[] MapSensitive(PostDto[] source, string postId, bool sensitive) =>
        CopyOnWrite.Map(source,
            post => post.Id == postId && post.Sensitive != sensitive,
            post => post with { Sensitive = sensitive });

    private void ApplySavedEverywhere(string postId, bool saved)
    {
        forYouLane.Items = MapSaved(forYouLane.Items, postId, saved);
        followingLane.Items = MapSaved(followingLane.Items, postId, saved);
        profileLane.Items = MapSaved(profileLane.Items, postId, saved);
        taggedLane.Items = MapSaved(taggedLane.Items, postId, saved);
        savedLane.Items = saved
            ? MapSaved(savedLane.Items, postId, true)
            : CopyOnWrite.RemoveById(savedLane.Items, postId);
        if (detailPost is { } current && current.Id == postId)
        {
            detailPost = current with { Saved = saved };
        }
    }

    private static PostDto[] MapSaved(PostDto[] source, string postId, bool saved) =>
        CopyOnWrite.Map(source,
            post => post.Id == postId && post.Saved != saved,
            post => post with { Saved = saved });

    public void UpdateAccountPrivacy(bool isPrivate, Action<bool> onComplete)
    {
        work.Run("account privacy", async token =>
        {
            var updated = await account.UpdateAccountPrivacyAsync(isPrivate, token).ConfigureAwait(false);
            if (updated is null)
            {
                return false;
            }

            AcceptMe(updated);
            return true;
        }, onComplete);
    }

    public void Report(string targetType, string targetId, string? reason, Action<bool> onComplete)
    {
        work.Run("report", token => safety.ReportAsync(targetType, targetId, reason, token), onComplete);
    }

    public void Block(string userId, Action<bool> onComplete, Action<AepFailure>? onFailure = null)
    {
        RemoveAuthorEverywhere(userId);
        work.Run("block", async token =>
        {
            var blocked = await safety.BlockAsync(userId, token, onFailure).ConfigureAwait(false);
            if (!blocked)
            {
                AepLog.Warning($"Block of {userId} failed; restoring the feeds that were cleared optimistically");
                RefreshFeed(SocialFeedScope.ForYou);
                RefreshFeed(SocialFeedScope.Following);
            }

            return blocked;
        }, onComplete);
    }

    private void RemoveAuthorEverywhere(string userId)
    {
        forYouLane.Items = BlockedContent.Purge(forYouLane.Items, userId);
        followingLane.Items = BlockedContent.Purge(followingLane.Items, userId);
        profileLane.Items = BlockedContent.Purge(profileLane.Items, userId);
        taggedLane.Items = BlockedContent.Purge(taggedLane.Items, userId);
        detailComments = CopyOnWrite.RemoveWhere(detailComments, comment => comment.AuthorId == userId);
        discoverResults = CopyOnWrite.RemoveWhere(discoverResults, user => user.Id == userId);
        if (detailPost is not { } current)
        {
            return;
        }

        if (BlockedContent.Hides(current, userId))
        {
            detailPost = null;
            detailPostId = null;
        }
        else if (current.ReferencedPost?.AuthorId == userId)
        {
            detailPost = current with { ReferencedPost = null };
        }
    }

    public void OpenProfile(string userId)
    {
        if (profileUserId == userId && (profileUser is not null || profileLoading))
        {
            RevalidateProfile(userId);
            return;
        }

        profileUserId = userId;
        profileUser = null;
        profileLane.Clear();
        profileFailed = false;
        profileLoading = true;
        ClearTagged();
        work.Run("profile open", async token =>
        {
            var user = await account.UserAsync(userId, token).ConfigureAwait(false);
            var posts = await FetchProfilePostsAsync(userId, null, token).ConfigureAwait(false);
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
                if (posts is not null)
                {
                    profileLane.ApplyRefresh(posts.Items, posts.NextCursor);
                }
            }
        }, () =>
        {
            if (profileUserId == userId)
            {
                profileLoading = false;
            }
        });
    }

    public void RevalidateProfile(string userId)
    {
        if (!session.IsSignedIn || profileUserId != userId || profileUser is null || profileLoading
            || profileRevalidating)
        {
            return;
        }

        profileRevalidating = true;
        work.Run("profile revalidate", async token =>
        {
            var user = await account.UserAsync(userId, token).ConfigureAwait(false);
            var posts = await FetchProfilePostsAsync(userId, null, token).ConfigureAwait(false);
            if (profileUserId != userId)
            {
                return;
            }

            if (user is not null)
            {
                profileUser = user;
            }

            if (posts is not null)
            {
                profileLane.ApplyRefresh(posts.Items, posts.NextCursor);
            }
        }, () => profileRevalidating = false);
    }

    public void LoadMoreProfilePosts()
    {
        var userId = profileUserId;
        var cursor = profileLane.Cursor;
        if (!session.IsSignedIn || userId is null || cursor is null || profileLane.LoadingMore || profileLoading)
        {
            return;
        }

        profileLane.LoadingMore = true;
        work.Run("profile more", async token =>
        {
            var page = await FetchProfilePostsAsync(userId, cursor, token).ConfigureAwait(false);
            if (page is not null && profileUserId == userId)
            {
                profileLane.ApplyMore(page.Items, page.NextCursor);
            }
        }, () => profileLane.LoadingMore = false);
    }

    public void ReloadProfile()
    {
        var current = profileUserId;
        if (current is null)
        {
            return;
        }

        profileUserId = null;
        OpenProfile(current);
    }

    public void EnsureUserList(string sourceId, UserListKind kind)
    {
        if (userListKey == UserListKeyFor(sourceId, kind, userListReactionFilter))
        {
            return;
        }

        OpenUserList(sourceId, kind);
    }

    public void FilterUserListByReaction(int reactionKind)
    {
        var sourceId = userListSourceId;
        if (sourceId is null || userListReactionFilter == reactionKind)
        {
            return;
        }

        userListReactionFilter = reactionKind;
        OpenUserList(sourceId, userListKind);
    }

    public void OpenUserList(string sourceId, UserListKind kind, int reactionFilter = -1)
    {
        if (userListSourceId != sourceId || userListKind != kind)
        {
            userListReactionFilter = reactionFilter;
        }

        var key = UserListKeyFor(sourceId, kind, userListReactionFilter);
        if (userListKey == key && userListLoading)
        {
            return;
        }

        var generation = Interlocked.Increment(ref userListGeneration);
        var keepStaleRows = userListKey == key && userListResults.Length > 0;
        var staleCursor = userListCursor;
        userListKind = kind;
        userListSourceId = sourceId;
        userListKey = key;
        userListTotal = UserListTotalFor(sourceId, kind);
        if (!keepStaleRows)
        {
            userListResults = Array.Empty<UserDto>();
        }

        userListCursor = null;
        userListFailed = false;
        userListLoading = true;
        work.Run("user list", async token =>
        {
            var page = await FetchUserListPageAsync(kind, sourceId, null, token).ConfigureAwait(false);
            if (Volatile.Read(ref userListGeneration) != generation)
            {
                return;
            }

            if (page is null)
            {
                userListFailed = !keepStaleRows;
                userListCursor = keepStaleRows ? staleCursor : null;
            }
            else
            {
                userListResults = page.Items;
                userListCursor = page.NextCursor;
                userListReactionKinds = page.ReactionKinds;
                if (page.ReactionCounts is not null)
                {
                    userListReactionCounts = page.ReactionCounts;
                }
            }
        }, () =>
        {
            if (Volatile.Read(ref userListGeneration) == generation)
            {
                userListLoading = false;
            }
        });
    }

    public void LoadMoreUserList()
    {
        var sourceId = userListSourceId;
        var cursor = userListCursor;
        if (!session.IsSignedIn || userListKey is null || sourceId is null || cursor is null
            || userListLoadingMore || userListLoading)
        {
            return;
        }

        var kind = userListKind;
        var generation = Volatile.Read(ref userListGeneration);
        userListLoadingMore = true;
        work.Run("user list more", async token =>
        {
            var page = await FetchUserListPageAsync(kind, sourceId, cursor, token).ConfigureAwait(false);
            if (page is null || Volatile.Read(ref userListGeneration) != generation)
            {
                return;
            }

            userListResults = CopyOnWrite.AppendPageById(userListResults, page.Items);
            userListCursor = page.NextCursor;
            userListReactionKinds = MergeReactionKinds(userListReactionKinds, page.ReactionKinds);
        }, () => userListLoadingMore = false);
    }

    private static Dictionary<string, int>? MergeReactionKinds(Dictionary<string, int>? existing,
        Dictionary<string, int>? incoming)
    {
        if (incoming is null || incoming.Count == 0)
        {
            return existing;
        }

        var merged = existing is null ? new Dictionary<string, int>(incoming.Count) : new Dictionary<string, int>(existing);
        foreach (var pair in incoming)
        {
            merged[pair.Key] = pair.Value;
        }

        return merged;
    }

    private static string UserListKeyFor(string sourceId, UserListKind kind, int reactionFilter) =>
        $"{(int)kind}:{sourceId}:{reactionFilter}";

    private int UserListTotalFor(string sourceId, UserListKind kind)
    {
        if (profileUser is not { } user || !string.Equals(user.Id, sourceId, StringComparison.Ordinal))
        {
            return -1;
        }

        return kind switch
        {
            UserListKind.Followers => user.Followers,
            UserListKind.Following => user.Following,
            UserListKind.Mutuals => user.FollowedByCount,
            _ => -1,
        };
    }

    private async Task<UserListPage?> FetchUserListPageAsync(
        UserListKind kind, string sourceId, string? cursor, CancellationToken token) =>
        kind switch
        {
            UserListKind.Followers => await client.FollowersAsync(sourceId, cursor, token).ConfigureAwait(false),
            UserListKind.Following => await client.FollowingAsync(sourceId, cursor, token).ConfigureAwait(false),
            UserListKind.Mutuals => await client.MutualFollowersAsync(sourceId, cursor, token).ConfigureAwait(false),
            _ => await client.PostLikersAsync(sourceId, cursor, token, userListReactionFilter)
                .ConfigureAwait(false),
        };

    public void UpdateProfile(string? displayName, string? handle, string? bio, Action<bool, string> onResult)
    {
        work.Run("profile update", async token =>
        {
            var updated = await account.UpdateProfileAsync(new UpdateProfileRequest(displayName, handle, bio), token)
                .ConfigureAwait(false);
            if (updated is null)
            {
                return false;
            }

            AcceptMe(updated);
            return true;
        }, succeeded => onResult(succeeded, string.Empty));
    }

    public void SearchTags(string query)
    {
        if (!session.IsSignedIn || tagsLoading)
        {
            return;
        }

        var trimmed = query.Trim().TrimStart('#');
        tagsLoading = true;
        work.Run("tag search", async token =>
        {
            var result = await client.TagSearchAsync(trimmed, token).ConfigureAwait(false);
            if (result is not null)
            {
                discoverTags = result.Tags;
            }
        }, () => tagsLoading = false);
    }

    public void Search(string query)
    {
        var trimmed = query.Trim();
        if (trimmed.Length == 0)
        {
            discoverResults = Array.Empty<UserDto>();
            return;
        }

        searching = true;
        work.Run("search", async token =>
        {
            var result = await account.SearchAsync(trimmed, token).ConfigureAwait(false);
            if (result is not null)
            {
                discoverResults = result.Users;
            }
        }, () => searching = false);
    }

    public void ClearDiscover()
    {
        discoverResults = Array.Empty<UserDto>();
        discoverTags = Array.Empty<TagSummaryDto>();
    }

    protected async Task<bool> UploadBannerAsync(string sourcePath, WallpaperCrop crop, CancellationToken token)
    {
        var result = await BannerUpload.RunAsync(account, media, sourcePath, crop, token).ConfigureAwait(false);
        avatarFailure = result.Outcome;
        if (result.User is not { } updated)
        {
            return false;
        }

        AcceptMe(updated);
        return true;
    }

    protected async Task<bool> UploadAvatarAsync(string sourcePath, WallpaperCrop crop, CancellationToken token)
    {
        var result = await AvatarUpload.RunAsync(account, media, sourcePath, crop, token).ConfigureAwait(false);
        avatarFailure = result.Outcome;
        if (result.User is not { } updated)
        {
            return false;
        }

        AcceptMe(updated);
        return true;
    }

    protected void AcceptMe(UserDto updated)
    {
        me = updated;
        if (profileUserId == updated.Id)
        {
            profileUser = updated;
        }
    }

    protected void AcceptCreatedPost(PostDto created)
    {
        forYouLane.Items = CopyOnWrite.Prepend(forYouLane.Items, created);
        followingLane.Items = CopyOnWrite.Prepend(followingLane.Items, created);
        if (profileUserId is not null && profileUserId == created.AuthorId)
        {
            profileLane.Items = CopyOnWrite.Prepend(profileLane.Items, created);
        }
    }

    protected void ReplacePost(PostDto updated)
    {
        forYouLane.Items = CopyOnWrite.Replace(forYouLane.Items, updated);
        followingLane.Items = CopyOnWrite.Replace(followingLane.Items, updated);
        profileLane.Items = CopyOnWrite.Replace(profileLane.Items, updated);
        savedLane.Items = CopyOnWrite.Replace(savedLane.Items, updated);
        taggedLane.Items = CopyOnWrite.Replace(taggedLane.Items, updated);
        hashtagLane.Items = CopyOnWrite.Replace(hashtagLane.Items, updated);
        if (detailPost is { } current && current.Id == updated.Id)
        {
            detailPost = updated;
        }
    }

    protected void RemovePost(string postId)
    {
        forYouLane.Items = CopyOnWrite.RemoveById(forYouLane.Items, postId);
        followingLane.Items = CopyOnWrite.RemoveById(followingLane.Items, postId);
        profileLane.Items = CopyOnWrite.RemoveById(profileLane.Items, postId);
        savedLane.Items = CopyOnWrite.RemoveById(savedLane.Items, postId);
        taggedLane.Items = CopyOnWrite.RemoveById(taggedLane.Items, postId);
        hashtagLane.Items = CopyOnWrite.RemoveById(hashtagLane.Items, postId);
        if (detailPost is { } current && current.Id == postId)
        {
            detailPost = null;
            detailPostId = null;
            detailComments = Array.Empty<CommentDto>();
            commentsCursor = null;
        }
    }

    protected void BumpCommentCount(string postId, int delta)
    {
        forYouLane.Items = MapCommentCount(forYouLane.Items, postId, delta);
        followingLane.Items = MapCommentCount(followingLane.Items, postId, delta);
        profileLane.Items = MapCommentCount(profileLane.Items, postId, delta);
        savedLane.Items = MapCommentCount(savedLane.Items, postId, delta);
        taggedLane.Items = MapCommentCount(taggedLane.Items, postId, delta);
        hashtagLane.Items = MapCommentCount(hashtagLane.Items, postId, delta);
        if (detailPost is { } current && current.Id == postId)
        {
            detailPost = current with { CommentCount = Math.Max(0, current.CommentCount + delta) };
        }
    }

    protected virtual void ApplyFollowEverywhere(string userId, bool following, bool requested)
    {
        discoverResults = MapFollow(discoverResults, userId, following, requested);
        userListResults = MapFollow(userListResults, userId, following, requested);
        followRequests = MapFollow(followRequests, userId, following, requested);
        forYouLane.Items = MapFollowByAuthor(forYouLane.Items, userId, following);
        followingLane.Items = MapFollowByAuthor(followingLane.Items, userId, following);
        profileLane.Items = MapFollowByAuthor(profileLane.Items, userId, following);
        taggedLane.Items = MapFollowByAuthor(taggedLane.Items, userId, following);
        savedLane.Items = MapFollowByAuthor(savedLane.Items, userId, following);
        if (detailPost is { } post && post.AuthorId == userId && post.IsFollowing != following)
        {
            detailPost = post with { IsFollowing = following };
        }

        if (profileUser is { } current && current.Id == userId)
        {
            profileUser = current with
            {
                IsFollowing = following,
                FollowRequested = requested,
                Followers = Math.Max(0, current.Followers + FollowerDelta(current.IsFollowing, following)),
            };
        }
    }

    private static int FollowerDelta(bool wasFollowing, bool following) =>
        wasFollowing == following ? 0 : following ? 1 : -1;

    private static PostDto[] MapCommentCount(PostDto[] source, string postId, int delta) =>
        CopyOnWrite.MapById(source, postId,
            post => post with { CommentCount = Math.Max(0, post.CommentCount + delta) });

    private static UserDto[] MapFollow(UserDto[] source, string userId, bool following, bool requested) =>
        CopyOnWrite.Map(source,
            user => user.Id == userId && (user.IsFollowing != following || user.FollowRequested != requested),
            user => user with
            {
                IsFollowing = following,
                FollowRequested = requested,
                Followers = Math.Max(0, user.Followers + FollowerDelta(user.IsFollowing, following)),
            });

    private static PostDto[] MapFollowByAuthor(PostDto[] source, string userId, bool following) =>
        CopyOnWrite.Map(source,
            post => post.AuthorId == userId && post.IsFollowing != following,
            post => post with { IsFollowing = following });

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        signals.ContentRemoved -= OnContentRemoved;
        work.Dispose();
    }
}
