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
    private readonly RetryGate userPostsGate = new RetryGate(TimeSpan.FromSeconds(15));
    private volatile string? userPostsUserId;
    private volatile VelvetPostDto[] userPosts = Array.Empty<VelvetPostDto>();
    private volatile int userPostsTotal;
    private volatile string? userPostsCursor;
    private volatile bool userPostsLoadingMore;
    private volatile bool userPostsLoading;
    private volatile bool userPostsLoaded;
    private volatile bool userPostsFailed;

    private readonly RetryGate tagGate = new RetryGate(TimeSpan.FromSeconds(15));
    private readonly FeedLane<VelvetPostDto> tagLane = new(ByNewestFirst, ByCreatedAtUnix);
    private volatile VelvetDiscoverFilter tagFilter = VelvetDiscoverFilter.Empty;
    private volatile string[] tagQuery = Array.Empty<string>();
    private volatile string tagToken = string.Empty;
    private volatile bool tagPostsLoaded;
    private volatile int tagEpoch;

    public string? UserPostsUserId => userPostsUserId;
    public VelvetPostDto[] UserPosts => userPosts;
    public int UserPostsTotal => userPostsTotal;
    public bool UserPostsLoaded => userPostsLoaded;
    public bool UserPostsFailed => userPostsFailed;
    public bool UserPostsLoadingMore => userPostsLoadingMore;
    public bool HasMoreUserPosts => userPostsCursor is not null;

    public string TagToken => tagToken;
    public VelvetPostDto[] TagPosts => tagLane.Items;
    public bool TagPostsLoaded => tagPostsLoaded;
    public bool TagPostsLoadingMore => tagLane.LoadingMore;
    public bool HasMoreTagPosts => tagLane.HasMore;
    public ITrimmable TagPostsSource => tagLane;

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

        if (userPostsUserId != userId)
        {
            userPostsGate.Reset();
        }

        if (!userPostsGate.TryPass())
        {
            return;
        }

        userPostsUserId = userId;
        userPosts = Array.Empty<VelvetPostDto>();
        userPostsTotal = 0;
        userPostsCursor = null;
        userPostsLoaded = false;
        userPostsFailed = false;
        userPostsLoading = true;
        work.Run("user posts", async token =>
        {
            var page = await client.UserPostsAsync(userId, null, token).ConfigureAwait(false);
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
            userPostsCursor = page.NextCursor;
            userPostsLoaded = true;
        }, () => userPostsLoading = false);
    }

    public void LoadMoreUserPosts()
    {
        var userId = userPostsUserId;
        var cursor = userPostsCursor;
        if (!session.IsSignedIn || userId is null || cursor is null || userPostsLoadingMore || userPostsLoading)
        {
            return;
        }

        userPostsLoadingMore = true;
        work.Run("user posts more", async token =>
        {
            var page = await client.UserPostsAsync(userId, cursor, token).ConfigureAwait(false);
            if (page is null || userPostsUserId != userId)
            {
                return;
            }

            userPosts = CopyOnWrite.AppendPageById(userPosts, page.Items);
            userPostsTotal = page.TotalCount;
            userPostsCursor = page.NextCursor;
        }, () => userPostsLoadingMore = false);
    }

    private void ResetTagPosts()
    {
        tagEpoch++;
        tagToken = string.Empty;
        tagQuery = Array.Empty<string>();
        tagFilter = VelvetDiscoverFilter.Empty;
        tagPostsLoaded = false;
        tagGate.Reset();
        tagLane.Clear();
    }

    private void ResetUserPosts()
    {
        userPostsUserId = null;
        userPosts = Array.Empty<VelvetPostDto>();
        userPostsTotal = 0;
        userPostsCursor = null;
        userPostsLoaded = false;
        userPostsFailed = false;
    }

    public void SetFeedScope(VelvetFeedScope scope)
    {
        feedScope = (int)scope;
    }

    public void SetFeedFilter(VelvetDiscoverFilter filter, string region, string[] postTags)
    {
        if (feedFilter.Matches(filter) && string.Equals(feedRegion, region, StringComparison.Ordinal)
            && VelvetDiscoverFilter.SameTokens(feedPostTags, postTags))
        {
            return;
        }

        feedEpoch++;
        feedFilter = filter;
        feedRegion = region;
        feedPostTags = postTags;
        for (var index = 0; index < feedLanes.Length; index++)
        {
            feedLanes[index].Clear();
        }

        feedLoadedAll = false;
        feedLoadedConnections = false;
        RefreshFeed();
    }

    public void RefreshFeed()
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var scope = FeedScope;
        var lane = feedLanes[(int)scope];
        var epoch = feedEpoch;
        var filter = feedFilter;
        var region = feedRegion;
        var postTags = feedPostTags;
        lane.Loading = true;
        work.Run("feed", async token =>
        {
            var page = await client.FeedAsync(ScopeKey(scope), filter, region, postTags, null, token)
                .ConfigureAwait(false);
            if (page is not null && epoch == feedEpoch)
            {
                lane.ApplyRefresh(page.Items, page.NextCursor);
            }
        }, () =>
        {
            lane.Loading = false;
            if (epoch != feedEpoch)
            {
                return;
            }

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

        var epoch = feedEpoch;
        var filter = feedFilter;
        var region = feedRegion;
        var postTags = feedPostTags;
        lane.LoadingMore = true;
        work.Run("feed more", async token =>
        {
            var page = await client.FeedAsync(ScopeKey(scope), filter, region, postTags, cursor, token)
                .ConfigureAwait(false);
            if (page is not null && epoch == feedEpoch)
            {
                lane.ApplyMore(page.Items, page.NextCursor);
            }
        }, () => lane.LoadingMore = false);
    }

    public void EnsureTagPosts(string token, VelvetDiscoverFilter filter)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var sameQuery = string.Equals(tagToken, token, StringComparison.Ordinal) && tagFilter.Matches(filter);
        if (sameQuery && (tagPostsLoaded || tagLane.Loading))
        {
            return;
        }

        if (!sameQuery)
        {
            tagGate.Reset();
        }

        if (!tagGate.TryPass())
        {
            return;
        }

        var epoch = ++tagEpoch;
        var query = new[] { token };
        tagToken = token;
        tagQuery = query;
        tagFilter = filter;
        tagPostsLoaded = false;
        tagLane.Clear();
        tagLane.Loading = true;
        work.Run("tag posts", async cancel =>
        {
            var page = await client.FeedAsync("all", filter, string.Empty, query, null, cancel).ConfigureAwait(false);
            if (page is null || epoch != tagEpoch)
            {
                return;
            }

            tagLane.ApplyRefresh(page.Items, page.NextCursor);
            tagPostsLoaded = true;
        }, () =>
        {
            if (epoch == tagEpoch)
            {
                tagLane.Loading = false;
            }
        });
    }

    public void LoadMoreTagPosts()
    {
        var cursor = tagLane.Cursor;
        if (!session.IsSignedIn || cursor is null || tagLane.LoadingMore || tagLane.Loading)
        {
            return;
        }

        var epoch = tagEpoch;
        var filter = tagFilter;
        var query = tagQuery;
        tagLane.LoadingMore = true;
        work.Run("tag posts more", async cancel =>
        {
            var page = await client.FeedAsync("all", filter, string.Empty, query, cursor, cancel).ConfigureAwait(false);
            if (page is not null && epoch == tagEpoch)
            {
                tagLane.ApplyMore(page.Items, page.NextCursor);
            }
        }, () => tagLane.LoadingMore = false);
    }

    private static string ScopeKey(VelvetFeedScope scope) =>
        scope == VelvetFeedScope.All ? "all" : "connections";

    private static int ByNewestFirst(VelvetPostDto left, VelvetPostDto right)
    {
        var byTime = right.CreatedAtUnix.CompareTo(left.CreatedAtUnix);
        return byTime != 0 ? byTime : string.CompareOrdinal(right.Id, left.Id);
    }

    private static long ByCreatedAtUnix(VelvetPostDto post) => post.CreatedAtUnix;

    // aspects holds one choice per photo, framed exactly as AethergramStore.CreateGram does.
    public void CreatePost(string[] sourcePaths, WallpaperCrop[] crops, PostAspect[] aspects, string caption,
        string[] tags, int audience, Action<bool> onComplete)
    {
        if (posting || sourcePaths.Length == 0)
        {
            return;
        }

        posting = true;
        work.Run("create post", async token =>
        {
            var mediaKeys = new string[sourcePaths.Length];
            var (containerWidth, containerHeight) = PostAspects.Size(aspects[0], PostSize);
            for (var index = 0; index < sourcePaths.Length; index++)
            {
                var (bakedWidth, bakedHeight) = PostAspects.Size(aspects[index], PostSize);
                var baked = ImageProcessor.BakeCroppedJpeg(sourcePaths[index], crops[index], bakedWidth, bakedHeight,
                    PostAspects.RevealsWholeImage(aspects[index]));
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

            var request = new CreateVelvetPostRequest(mediaKeys[0], containerWidth, containerHeight, caption, tags,
                mediaKeys, audience);
            var created = await client.CreatePostAsync(request, token).ConfigureAwait(false);
            if (created is null)
            {
                return false;
            }

            if (userPostsLoaded && userPostsUserId == MyUserId)
            {
                userPosts = CopyOnWrite.Prepend(userPosts, created);
                userPostsTotal++;
            }

            return true;
        }, onComplete, () => posting = false);
    }

    public VelvetCommentDto[] DetailComments => detailComments;
    public bool LoadingComments => loadingComments;
    public bool CommentsLoadingMore => commentsLoadingMore;
    public bool HasMoreComments => commentsCursor is not null;
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
        if (likersPostId == postId && likersLoading)
        {
            return;
        }

        var generation = Interlocked.Increment(ref likersGeneration);
        var keepStaleRows = likersPostId == postId && likers.Length > 0;
        var staleCursor = likersCursor;
        likersPostId = postId;
        if (!keepStaleRows)
        {
            likers = Array.Empty<UserDto>();
        }

        likersCursor = null;
        likersFailed = false;
        likersLoading = !keepStaleRows;
        work.Run("likers", async token =>
        {
            var page = await client.PostLikersAsync(postId, null, token).ConfigureAwait(false);
            if (Volatile.Read(ref likersGeneration) != generation)
            {
                return;
            }

            if (page is null)
            {
                likersFailed = !keepStaleRows;
                likersCursor = keepStaleRows ? staleCursor : null;
            }
            else
            {
                likers = page.Items;
                likersCursor = page.NextCursor;
            }
        }, () =>
        {
            if (Volatile.Read(ref likersGeneration) == generation)
            {
                likersLoading = false;
            }
        });
    }

    public void LoadMoreLikers()
    {
        var postId = likersPostId;
        var cursor = likersCursor;
        if (!session.IsSignedIn || postId is null || cursor is null || likersLoadingMore || likersLoading)
        {
            return;
        }

        var generation = Volatile.Read(ref likersGeneration);
        likersLoadingMore = true;
        work.Run("likers more", async token =>
        {
            var page = await client.PostLikersAsync(postId, cursor, token).ConfigureAwait(false);
            if (page is null || Volatile.Read(ref likersGeneration) != generation)
            {
                return;
            }

            likers = CopyOnWrite.AppendPageById(likers, page.Items);
            likersCursor = page.NextCursor;
        }, () => likersLoadingMore = false);
    }

    public void OpenComments(string postId)
    {
        detailPostId = postId;
        detailComments = Array.Empty<VelvetCommentDto>();
        commentsCursor = null;
        loadingComments = true;
        work.Run("comments", async token =>
        {
            var page = await client.CommentsAsync(postId, null, token).ConfigureAwait(false);
            if (detailPostId == postId && page is not null)
            {
                detailComments = page.Items;
                commentsCursor = page.NextCursor;
            }
        }, () =>
        {
            if (detailPostId == postId)
            {
                loadingComments = false;
            }
        });
    }

    public void LoadMoreComments()
    {
        var postId = detailPostId;
        var cursor = commentsCursor;
        if (!session.IsSignedIn || postId is null || cursor is null || commentsLoadingMore || loadingComments)
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

            detailComments = CopyOnWrite.AppendPageById(detailComments, page.Items);
            commentsCursor = page.NextCursor;
        }, () => commentsLoadingMore = false);
    }

    public void AddComment(string postId, string text, Action<bool> onComplete,
        Action<AepFailure>? onFailure = null)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0 || commenting)
        {
            return;
        }

        commenting = true;
        work.Run("comment", async token =>
        {
            var created = await client.AddCommentAsync(postId, trimmed, token, onFailure).ConfigureAwait(false);
            if (created is null)
            {
                AepLog.Warning($"Velvet comment on {postId} was not accepted");
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

    public void EditCaption(string postId, string caption, Action<bool> onComplete)
    {
        work.Run("edit caption", async token =>
        {
            var result = await client.EditCaptionAsync(postId, caption, token).ConfigureAwait(false);
            if (result is null)
            {
                return false;
            }

            AcceptPostEverywhere(result);
            return true;
        }, onComplete);
    }

    public void SetPostAudience(VelvetPostDto post, int audience)
    {
        AcceptPostEverywhere(post with { Audience = audience });
        work.Run("post audience", async token =>
        {
            var result = await client.SetPostAudienceAsync(post.Id, audience, token).ConfigureAwait(false);
            if (result is not null)
            {
                AcceptPostEverywhere(result);
            }
        });
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

        userPosts = CopyOnWrite.Replace(userPosts, post);
        tagLane.Items = CopyOnWrite.Replace(tagLane.Items, post);
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
}
