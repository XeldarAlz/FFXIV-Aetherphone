using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
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
    protected const int AvatarSize = 512;
    protected readonly AethernetSession session;
    protected readonly AccountClient account;
    protected readonly SocialClient client;
    protected readonly SafetyClient safety;
    protected readonly MediaClient media;
    protected readonly StoreWork work;
    private readonly RetryGate meGate = new(TimeSpan.FromSeconds(30));
    private volatile UserDto? me;
    protected readonly FeedLane<PostDto> forYouLane = new(ByNewestFirst);
    protected readonly FeedLane<PostDto> followingLane = new(ByNewestFirst);
    private readonly FeedLane<PostDto> savedLane = new(ByNewestFirst);
    private volatile UserDto[] followRequests = Array.Empty<UserDto>();
    private volatile bool followRequestsLoading;
    private volatile bool followRequestsLoaded;
    protected volatile PostDto[] profilePosts = Array.Empty<PostDto>();
    protected volatile PostDto? detailPost;
    protected volatile bool posting;
    private volatile string? profileUserId;
    private volatile UserDto? profileUser;
    private volatile bool profileLoading;
    private volatile bool profileFailed;
    private volatile bool profileRevalidating;
    private DateTime profileFetchedAt;
    private static readonly TimeSpan ProfileRevalidateAfter = TimeSpan.FromSeconds(20);
    private volatile string? detailPostId;
    private volatile CommentDto[] detailComments = Array.Empty<CommentDto>();
    private volatile bool detailLoading;
    private volatile bool commenting;
    private volatile UserDto[] discoverResults = Array.Empty<UserDto>();
    private volatile bool searching;
    private volatile bool loadingMe;
    private volatile string? userListKey;
    private volatile UserDto[] userListResults = Array.Empty<UserDto>();
    private volatile bool userListLoading;
    private volatile bool userListFailed;
    private volatile PostDto[] taggedPosts = Array.Empty<PostDto>();
    private volatile string? taggedUserId;
    private volatile bool taggedLoading;
    private string? lastAccountId;

    protected SocialFeedStore(
        AethernetSession session,
        AccountClient account,
        SocialClient client,
        SafetyClient safety,
        MediaClient media,
        string logTag)
    {
        this.session = session;
        this.account = account;
        this.client = client;
        this.safety = safety;
        this.media = media;
        work = new StoreWork(logTag);
        session.Changed += OnSessionChanged;
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
        profilePosts = Array.Empty<PostDto>();
        detailPost = null;
        profileUserId = null;
        profileUser = null;
        profileLoading = false;
        profileFailed = false;
        detailPostId = null;
        detailComments = Array.Empty<CommentDto>();
        discoverResults = Array.Empty<UserDto>();
        userListKey = null;
        userListResults = Array.Empty<UserDto>();
        savedLane.Clear();
        followRequests = Array.Empty<UserDto>();
        followRequestsLoaded = false;
        ClearTagged();
    }

    public MentionSuggestions NewMentionSuggestions() => new(account, work);

    public bool IsSignedIn => session.IsSignedIn;
    public UserDto? Me => me;
    public PostDto[] Feed(SocialFeedScope scope) => Lane(scope).Items;

    public bool IsLoading(SocialFeedScope scope) => Lane(scope).Loading;

    public bool HasMoreFeed(SocialFeedScope scope) => Lane(scope).HasMore;

    public bool LoadingMore(SocialFeedScope scope) => Lane(scope).LoadingMore;

    private FeedLane<PostDto> Lane(SocialFeedScope scope) =>
        scope == SocialFeedScope.ForYou ? forYouLane : followingLane;

    private static string FeedKey(SocialFeedScope scope) =>
        scope == SocialFeedScope.ForYou ? "explore" : "following";

    public string? ProfileUserId => profileUserId;
    public UserDto? ProfileUser => profileUser;
    public PostDto[] ProfilePosts => profilePosts;
    public bool ProfileLoading => profileLoading;
    public bool ProfileFailed => profileFailed;
    public PostDto? DetailPost => detailPost;
    public CommentDto[] DetailComments => detailComments;
    public bool DetailLoading => detailLoading;
    public bool Commenting => commenting;
    public UserDto[] DiscoverResults => discoverResults;
    public bool Searching => searching;
    public bool Posting => posting;
    public UserDto[] UserListResults => userListResults;
    public bool UserListLoading => userListLoading;
    public bool UserListFailed => userListFailed;
    public UserDto[] FollowRequests => followRequests;
    public bool FollowRequestsLoading => followRequestsLoading;

    public int PendingFollowRequestCount =>
        followRequestsLoaded ? followRequests.Length : Me?.PendingFollowRequests ?? 0;

    public PostDto[] SavedPosts => savedLane.Items;
    public bool SavedLoading => savedLane.Loading;
    public bool SavedLoadingMore => savedLane.LoadingMore;
    public bool HasMoreSaved => savedLane.HasMore;

    public static FollowState FollowStateOf(UserDto user) =>
        user.IsFollowing ? FollowState.Following
        : user.FollowRequested ? FollowState.Requested
        : FollowState.None;

    protected abstract Task<FeedPage?> FetchFeedAsync(string feedKey, string? cursor, CancellationToken token);

    protected abstract Task<FeedPage?> FetchProfilePostsAsync(string userId, CancellationToken token);

    protected virtual Task<FeedPage?> FetchTaggedPostsAsync(string userId, CancellationToken token) =>
        Task.FromResult<FeedPage?>(null);

    public PostDto[] TaggedPosts => taggedPosts;

    public bool TaggedLoading => taggedLoading;

    public void EnsureTaggedPosts(string userId)
    {
        if (!session.IsSignedIn || taggedLoading || string.Equals(taggedUserId, userId, StringComparison.Ordinal))
        {
            return;
        }

        taggedUserId = userId;
        taggedPosts = Array.Empty<PostDto>();
        taggedLoading = true;
        work.Run("tagged load", async token =>
        {
            var page = await FetchTaggedPostsAsync(userId, token).ConfigureAwait(false);
            if (page is not null && string.Equals(taggedUserId, userId, StringComparison.Ordinal))
            {
                taggedPosts = page.Items;
            }
        }, () => taggedLoading = false);
    }

    protected void ClearTagged()
    {
        taggedUserId = null;
        taggedPosts = Array.Empty<PostDto>();
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
            var profile = await account.MeAsync(token).ConfigureAwait(false);
            if (profile is not null)
            {
                me = profile;
            }
        }, () => loadingMe = false);
    }

    public void RefreshFeed(SocialFeedScope scope)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var lane = Lane(scope);
        lane.Loading = true;
        work.Run("feed refresh", async token =>
        {
            var page = await FetchFeedAsync(FeedKey(scope), null, token).ConfigureAwait(false);
            if (page is not null)
            {
                lane.ApplyRefresh(page.Items, page.NextCursor);
            }
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
        work.Run("feed more", async token =>
        {
            var page = await FetchFeedAsync(FeedKey(scope), cursor, token).ConfigureAwait(false);
            if (page is not null)
            {
                lane.ApplyMore(page.Items, page.NextCursor);
            }
        }, () => lane.LoadingMore = false);
    }

    private static int ByNewestFirst(PostDto left, PostDto right)
    {
        var byTime = right.CreatedAtUnix.CompareTo(left.CreatedAtUnix);
        return byTime != 0 ? byTime : string.CompareOrdinal(right.Id, left.Id);
    }

    public void OpenDetail(PostDto post) => LoadDetail(post.Id, post);

    public void OpenDetailById(string postId) => LoadDetail(postId, null);

    private void LoadDetail(string postId, PostDto? cached)
    {
        detailPostId = postId;
        detailPost = cached;
        detailComments = Array.Empty<CommentDto>();
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
            }
        }, () =>
        {
            if (detailPostId == postId)
            {
                detailLoading = false;
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

            BumpCommentCount(postId, 1);
            return true;
        }, onComplete, () => commenting = false);
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
                followRequestsLoaded = true;
                SyncPendingFollowRequests(page.Items.Length);
            }
        }, () => followRequestsLoading = false);
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

    private void ApplySavedEverywhere(string postId, bool saved)
    {
        forYouLane.Items = MapSaved(forYouLane.Items, postId, saved);
        followingLane.Items = MapSaved(followingLane.Items, postId, saved);
        profilePosts = MapSaved(profilePosts, postId, saved);
        taggedPosts = MapSaved(taggedPosts, postId, saved);
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

    public void Block(string userId, Action<bool> onComplete)
    {
        RemoveAuthorEverywhere(userId);
        work.Run("block", token => safety.BlockAsync(userId, token), onComplete);
    }

    private void RemoveAuthorEverywhere(string userId)
    {
        forYouLane.Items = CopyOnWrite.RemoveWhere(forYouLane.Items, post => post.AuthorId == userId);
        followingLane.Items = CopyOnWrite.RemoveWhere(followingLane.Items, post => post.AuthorId == userId);
        profilePosts = CopyOnWrite.RemoveWhere(profilePosts, post => post.AuthorId == userId);
        taggedPosts = CopyOnWrite.RemoveWhere(taggedPosts, post => post.AuthorId == userId);
        detailComments = CopyOnWrite.RemoveWhere(detailComments, comment => comment.AuthorId == userId);
        discoverResults = CopyOnWrite.RemoveWhere(discoverResults, user => user.Id == userId);
        if (detailPost is { } current && current.AuthorId == userId)
        {
            detailPost = null;
            detailPostId = null;
        }
    }

    public void OpenProfile(string userId)
    {
        if (profileUserId == userId && (profileUser is not null || profileLoading))
        {
            if (profileUser is not null && !profileLoading && !profileRevalidating
                && DateTime.UtcNow - profileFetchedAt > ProfileRevalidateAfter)
            {
                RevalidateProfile(userId);
            }

            return;
        }

        profileUserId = userId;
        profileUser = null;
        profilePosts = Array.Empty<PostDto>();
        profileFailed = false;
        profileLoading = true;
        ClearTagged();
        work.Run("profile open", async token =>
        {
            var user = await account.UserAsync(userId, token).ConfigureAwait(false);
            var posts = await FetchProfilePostsAsync(userId, token).ConfigureAwait(false);
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
                profilePosts = posts?.Items ?? Array.Empty<PostDto>();
                profileFetchedAt = DateTime.UtcNow;
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
            var user = await account.UserAsync(userId, token).ConfigureAwait(false);
            var posts = await FetchProfilePostsAsync(userId, token).ConfigureAwait(false);
            if (profileUserId != userId)
            {
                return;
            }

            if (user is not null)
            {
                profileUser = user;
                profileFetchedAt = DateTime.UtcNow;
            }

            if (posts is not null)
            {
                profilePosts = posts.Items;
            }
        }, () => profileRevalidating = false);
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

    public void OpenUserList(string sourceId, UserListKind kind)
    {
        var key = $"{(int)kind}:{sourceId}";
        if (userListKey == key && (userListResults.Length > 0 || userListLoading))
        {
            return;
        }

        userListKey = key;
        userListResults = Array.Empty<UserDto>();
        userListFailed = false;
        userListLoading = true;
        work.Run("user list", async token =>
        {
            var page = kind switch
            {
                UserListKind.Followers => await client.FollowersAsync(sourceId, null, token).ConfigureAwait(false),
                UserListKind.Following => await client.FollowingAsync(sourceId, null, token).ConfigureAwait(false),
                UserListKind.Mutuals => await client.MutualFollowersAsync(sourceId, null, token).ConfigureAwait(false),
                _ => await client.PostLikersAsync(sourceId, null, token).ConfigureAwait(false),
            };
            if (userListKey != key)
            {
                return;
            }

            if (page is null)
            {
                userListFailed = true;
            }
            else
            {
                userListResults = page.Items;
            }
        }, () =>
        {
            if (userListKey == key)
            {
                userListLoading = false;
            }
        });
    }

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

    public void ClearDiscover() => discoverResults = Array.Empty<UserDto>();

    protected async Task<bool> UploadAvatarAsync(string sourcePath, WallpaperCrop crop, CancellationToken token)
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
            profilePosts = CopyOnWrite.Prepend(profilePosts, created);
        }
    }

    protected void ReplacePost(PostDto updated)
    {
        forYouLane.Items = CopyOnWrite.Replace(forYouLane.Items, updated);
        followingLane.Items = CopyOnWrite.Replace(followingLane.Items, updated);
        profilePosts = CopyOnWrite.Replace(profilePosts, updated);
        if (detailPost is { } current && current.Id == updated.Id)
        {
            detailPost = updated;
        }
    }

    protected void RemovePost(string postId)
    {
        forYouLane.Items = CopyOnWrite.RemoveById(forYouLane.Items, postId);
        followingLane.Items = CopyOnWrite.RemoveById(followingLane.Items, postId);
        profilePosts = CopyOnWrite.RemoveById(profilePosts, postId);
        if (detailPost is { } current && current.Id == postId)
        {
            detailPost = null;
            detailPostId = null;
        }
    }

    protected void BumpCommentCount(string postId, int delta)
    {
        forYouLane.Items = MapCommentCount(forYouLane.Items, postId, delta);
        followingLane.Items = MapCommentCount(followingLane.Items, postId, delta);
        profilePosts = MapCommentCount(profilePosts, postId, delta);
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
        profilePosts = MapFollowByAuthor(profilePosts, userId, following);
        taggedPosts = MapFollowByAuthor(taggedPosts, userId, following);
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
        work.Dispose();
    }
}
