using Aetherphone.Core.Aethernet;
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
    protected readonly AethernetClient client;
    protected readonly StoreWork work;
    private readonly RetryGate meGate = new(TimeSpan.FromSeconds(30));
    private readonly string analyticsChannel;
    private volatile UserDto? me;
    protected volatile PostDto[] forYou = Array.Empty<PostDto>();
    protected volatile PostDto[] following = Array.Empty<PostDto>();
    protected volatile PostDto[] profilePosts = Array.Empty<PostDto>();
    protected volatile PostDto? detailPost;
    protected volatile bool posting;
    private volatile bool loadingForYou;
    private volatile bool loadingFollowing;
    private volatile string? forYouCursor;
    private volatile string? followingCursor;
    private volatile bool loadingMoreForYou;
    private volatile bool loadingMoreFollowing;
    private readonly object feedLock = new();
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

    protected SocialFeedStore(
        AethernetSession session,
        AethernetClient client,
        string logTag,
        string analyticsChannel)
    {
        this.session = session;
        this.client = client;
        this.analyticsChannel = analyticsChannel;
        work = new StoreWork(logTag);
        Mentions = new MentionSuggestions(client, work);
    }

    public MentionSuggestions Mentions { get; }

    public bool IsSignedIn => session.IsSignedIn;
    public UserDto? Me => me;
    public PostDto[] Feed(SocialFeedScope scope) => scope == SocialFeedScope.ForYou ? forYou : following;

    public bool IsLoading(SocialFeedScope scope) =>
        scope == SocialFeedScope.ForYou ? loadingForYou : loadingFollowing;

    public bool HasMoreFeed(SocialFeedScope scope) =>
        (scope == SocialFeedScope.ForYou ? forYouCursor : followingCursor) is not null;

    public bool LoadingMore(SocialFeedScope scope) =>
        scope == SocialFeedScope.ForYou ? loadingMoreForYou : loadingMoreFollowing;

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
            var profile = await client.MeAsync(token).ConfigureAwait(false);
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

        if (scope == SocialFeedScope.ForYou)
        {
            loadingForYou = true;
        }
        else
        {
            loadingFollowing = true;
        }

        work.Run("feed refresh", async token =>
        {
            var page = await FetchFeedAsync(scope == SocialFeedScope.ForYou ? "explore" : "following", null, token)
                .ConfigureAwait(false);
            if (page is not null)
            {
                ApplyFeedRefresh(scope, page);
            }
        }, () =>
        {
            if (scope == SocialFeedScope.ForYou)
            {
                loadingForYou = false;
            }
            else
            {
                loadingFollowing = false;
            }
        });
    }

    public void LoadMoreFeed(SocialFeedScope scope)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var cursor = scope == SocialFeedScope.ForYou ? forYouCursor : followingCursor;
        if (cursor is null || LoadingMore(scope) || IsLoading(scope))
        {
            return;
        }

        if (scope == SocialFeedScope.ForYou)
        {
            loadingMoreForYou = true;
        }
        else
        {
            loadingMoreFollowing = true;
        }

        work.Run("feed more", async token =>
        {
            var page = await FetchFeedAsync(scope == SocialFeedScope.ForYou ? "explore" : "following", cursor, token)
                .ConfigureAwait(false);
            if (page is not null)
            {
                ApplyFeedMore(scope, page);
            }
        }, () =>
        {
            if (scope == SocialFeedScope.ForYou)
            {
                loadingMoreForYou = false;
            }
            else
            {
                loadingMoreFollowing = false;
            }
        });
    }

    private void ApplyFeedRefresh(SocialFeedScope scope, FeedPage page)
    {
        lock (feedLock)
        {
            if (scope == SocialFeedScope.ForYou)
            {
                if (forYou.Length == 0)
                {
                    forYou = page.Items;
                    forYouCursor = page.NextCursor;
                }
                else
                {
                    forYou = MergeFeed(forYou, page.Items);
                }
            }
            else
            {
                if (following.Length == 0)
                {
                    following = page.Items;
                    followingCursor = page.NextCursor;
                }
                else
                {
                    following = MergeFeed(following, page.Items);
                }
            }
        }
    }

    private void ApplyFeedMore(SocialFeedScope scope, FeedPage page)
    {
        lock (feedLock)
        {
            if (scope == SocialFeedScope.ForYou)
            {
                forYou = MergeFeed(forYou, page.Items);
                forYouCursor = page.NextCursor;
            }
            else
            {
                following = MergeFeed(following, page.Items);
                followingCursor = page.NextCursor;
            }
        }
    }

    private static PostDto[] MergeFeed(PostDto[] current, PostDto[] incoming)
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

        var merged = new List<PostDto>(current.Length + incoming.Length);
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

    private static int ByNewestFirst(PostDto left, PostDto right)
    {
        var byTime = right.CreatedAtUnix.CompareTo(left.CreatedAtUnix);
        return byTime != 0 ? byTime : string.CompareOrdinal(right.Id, left.Id);
    }

    public void OpenDetail(PostDto post)
    {
        detailPostId = post.Id;
        detailPost = post;
        detailComments = Array.Empty<CommentDto>();
        detailLoading = true;
        work.Run("comments load", async token =>
        {
            var page = await client.CommentsAsync(post.Id, null, token).ConfigureAwait(false);
            if (detailPostId != post.Id)
            {
                return;
            }

            if (page is not null)
            {
                detailComments = CopyOnWrite.Reversed(page.Items);
            }
        }, () =>
        {
            if (detailPostId == post.Id)
            {
                detailLoading = false;
            }
        });
    }

    public void OpenDetailById(string postId)
    {
        detailPostId = postId;
        detailPost = null;
        detailComments = Array.Empty<CommentDto>();
        detailLoading = true;
        work.Run("detail by id", async token =>
        {
            var post = await client.PostAsync(postId, token).ConfigureAwait(false);
            if (detailPostId != postId)
            {
                return;
            }

            if (post is not null)
            {
                detailPost = post;
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
            Plugin.Analytics.Track(AnalyticsEvents.Comment(analyticsChannel));
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
            Plugin.Analytics.Track(AnalyticsEvents.Follow(analyticsChannel));
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
        work.Run("report", token => client.ReportAsync(targetType, targetId, reason, token), onComplete);
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
        work.Run("profile open", async token =>
        {
            var user = await client.UserAsync(userId, token).ConfigureAwait(false);
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
            var updated = await client.UpdateProfileAsync(new UpdateProfileRequest(displayName, handle, bio), token)
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
            var result = await client.SearchAsync(trimmed, token).ConfigureAwait(false);
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
        forYou = CopyOnWrite.Prepend(forYou, created);
        following = CopyOnWrite.Prepend(following, created);
        if (profileUserId is not null && profileUserId == created.AuthorId)
        {
            profilePosts = CopyOnWrite.Prepend(profilePosts, created);
        }

        Plugin.Analytics.Track(AnalyticsEvents.PostCreated(analyticsChannel));
    }

    protected void ReplacePost(PostDto updated)
    {
        forYou = CopyOnWrite.Replace(forYou, updated);
        following = CopyOnWrite.Replace(following, updated);
        profilePosts = CopyOnWrite.Replace(profilePosts, updated);
        if (detailPost is { } current && current.Id == updated.Id)
        {
            detailPost = updated;
        }
    }

    protected void RemovePost(string postId)
    {
        forYou = CopyOnWrite.RemoveById(forYou, postId);
        following = CopyOnWrite.RemoveById(following, postId);
        profilePosts = CopyOnWrite.RemoveById(profilePosts, postId);
        if (detailPost is { } current && current.Id == postId)
        {
            detailPost = null;
            detailPostId = null;
        }
    }

    protected void BumpCommentCount(string postId, int delta)
    {
        forYou = MapCommentCount(forYou, postId, delta);
        following = MapCommentCount(following, postId, delta);
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
