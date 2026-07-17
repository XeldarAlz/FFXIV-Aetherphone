using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Analytics;
using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Aetherphone.Core.Wallpapers;

namespace Aetherphone.Core.Social;

internal enum SocialFeedScope
{
    ForYou,
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
    protected readonly IAnalyticsService analytics;
    private readonly RetryGate meGate = new(TimeSpan.FromSeconds(30));
    private readonly string analyticsChannel;
    private volatile UserDto? me;
    protected readonly FeedLane<PostDto> forYouLane = new(ByNewestFirst);
    protected readonly FeedLane<PostDto> followingLane = new(ByNewestFirst);
    protected volatile PostDto[] profilePosts = Array.Empty<PostDto>();
    protected volatile PostDto? detailPost;
    protected volatile bool posting;
    private volatile string? profileUserId;
    private volatile UserDto? profileUser;
    private volatile bool profileLoading;
    private volatile bool profileFailed;
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

    protected SocialFeedStore(
        AethernetSession session,
        AccountClient account,
        SocialClient client,
        SafetyClient safety,
        MediaClient media,
        IAnalyticsService analytics,
        string logTag,
        string analyticsChannel)
    {
        this.session = session;
        this.account = account;
        this.client = client;
        this.safety = safety;
        this.media = media;
        this.analytics = analytics;
        this.analyticsChannel = analyticsChannel;
        work = new StoreWork(logTag);
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
            analytics.Track(AnalyticsEvents.Comment(analyticsChannel));
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

    public void SetFollow(string userId, bool follow)
    {
        ApplyFollowEverywhere(userId, follow);
        if (follow)
        {
            analytics.Track(AnalyticsEvents.Follow(analyticsChannel));
        }

        work.Run("follow", async token =>
        {
            if (follow)
            {
                await client.FollowAsync(userId, token).ConfigureAwait(false);
            }
            else
            {
                await client.UnfollowAsync(userId, token).ConfigureAwait(false);
            }
        });
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
            }
        }, () =>
        {
            if (profileUserId == userId)
            {
                profileLoading = false;
            }
        });
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

        analytics.Track(AnalyticsEvents.PostCreated(analyticsChannel));
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

    protected virtual void ApplyFollowEverywhere(string userId, bool follow)
    {
        discoverResults = MapFollow(discoverResults, userId, follow);
        userListResults = MapFollow(userListResults, userId, follow);
        if (profileUser is { } current && current.Id == userId)
        {
            profileUser = current with
            {
                IsFollowing = follow, Followers = Math.Max(0, current.Followers + (follow ? 1 : -1))
            };
        }
    }

    private static PostDto[] MapCommentCount(PostDto[] source, string postId, int delta) =>
        CopyOnWrite.MapById(source, postId,
            post => post with { CommentCount = Math.Max(0, post.CommentCount + delta) });

    private static UserDto[] MapFollow(UserDto[] source, string userId, bool follow) =>
        CopyOnWrite.Map(source,
            user => user.Id == userId && user.IsFollowing != follow,
            user => user with
            {
                IsFollowing = follow, Followers = Math.Max(0, user.Followers + (follow ? 1 : -1))
            });

    public void Dispose() => work.Dispose();
}
