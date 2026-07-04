using System.Threading;
using System.Threading.Tasks;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Media;
using Aetherphone.Core.Wallpapers;

namespace Aetherphone.Apps.Chirper;

internal enum ChirperFeedScope
{
    ForYou,
    Following,
}

internal sealed class ChirperStore : IDisposable
{
    private const int AvatarSize = 512;

    private static readonly TimeSpan MeRetryCooldown = TimeSpan.FromSeconds(30);

    private readonly AethernetSession session;
    private readonly AethernetClient client;
    private readonly CancellationTokenSource cancellation = new();

    private DateTime lastMeAttemptUtc = DateTime.MinValue;

    private volatile UserDto? me;

    private volatile PostDto[] forYou = Array.Empty<PostDto>();
    private volatile PostDto[] following = Array.Empty<PostDto>();
    private volatile bool loadingForYou;
    private volatile bool loadingFollowing;

    private volatile string? profileUserId;
    private volatile UserDto? profileUser;
    private volatile PostDto[] profilePosts = Array.Empty<PostDto>();
    private volatile bool profileLoading;
    private volatile bool profileFailed;

    private volatile string? detailPostId;
    private volatile PostDto? detailPost;
    private volatile CommentDto[] detailComments = Array.Empty<CommentDto>();
    private volatile bool detailLoading;
    private volatile bool commenting;

    private volatile UserDto[] discoverResults = Array.Empty<UserDto>();
    private volatile bool searching;
    private volatile bool posting;
    private volatile bool loadingMe;
    private volatile bool avatarBusy;

    public ChirperStore(AethernetSession session, AethernetClient client)
    {
        this.session = session;
        this.client = client;
    }

    public bool IsSignedIn => session.IsSignedIn;

    public UserDto? Me => me;

    public PostDto[] Feed(ChirperFeedScope scope) => scope == ChirperFeedScope.ForYou ? forYou : following;

    public bool IsLoading(ChirperFeedScope scope) => scope == ChirperFeedScope.ForYou ? loadingForYou : loadingFollowing;

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

    public bool AvatarBusy => avatarBusy;

    public void EnsureMe()
    {
        if (!session.IsSignedIn || me is not null || loadingMe)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (now - lastMeAttemptUtc < MeRetryCooldown)
        {
            return;
        }

        lastMeAttemptUtc = now;
        loadingMe = true;
        RunGuarded("profile load", async token =>
        {
            var profile = await client.MeAsync(token).ConfigureAwait(false);
            if (profile is not null)
            {
                me = profile;
            }
        }, () => loadingMe = false);
    }

    public void RefreshFeed(ChirperFeedScope scope)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        if (scope == ChirperFeedScope.ForYou)
        {
            loadingForYou = true;
        }
        else
        {
            loadingFollowing = true;
        }

        RunGuarded("feed refresh", async token =>
        {
            var page = await client.FeedAsync(scope == ChirperFeedScope.ForYou ? "explore" : "following", null, token).ConfigureAwait(false);
            if (page is not null)
            {
                if (scope == ChirperFeedScope.ForYou)
                {
                    forYou = page.Items;
                }
                else
                {
                    following = page.Items;
                }
            }
        }, () =>
        {
            if (scope == ChirperFeedScope.ForYou)
            {
                loadingForYou = false;
            }
            else
            {
                loadingFollowing = false;
            }
        });
    }

    public void Compose(string text, Action<bool> onComplete)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0 || posting)
        {
            return;
        }

        posting = true;
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            var succeeded = false;
            try
            {
                var created = await client.CreatePostAsync(trimmed, token).ConfigureAwait(false);
                if (created is not null)
                {
                    forYou = Prepend(forYou, created);
                    following = Prepend(following, created);
                    if (profileUserId is not null && profileUserId == created.AuthorId)
                    {
                        profilePosts = Prepend(profilePosts, created);
                    }

                    succeeded = true;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Chirper] compose failed: {exception.Message}");
            }
            finally
            {
                posting = false;
                onComplete(succeeded);
            }
        });
    }

    public void ToggleReaction(PostDto post, int kind)
    {
        var target = post.MyReaction == kind ? -1 : kind;
        var optimistic = ApplyReaction(post, target);
        ReplacePost(optimistic);

        RunGuarded("reaction", async token =>
        {
            var result = target < 0
                ? await client.RemoveReactionAsync(post.Id, token).ConfigureAwait(false)
                : await client.ReactAsync(post.Id, target, token).ConfigureAwait(false);
            if (result is not null)
            {
                ReplacePost(result);
            }
        });
    }

    public void OpenDetail(PostDto post)
    {
        detailPostId = post.Id;
        detailPost = post;
        detailComments = Array.Empty<CommentDto>();
        detailLoading = true;

        RunGuarded("comments load", async token =>
        {
            var page = await client.CommentsAsync(post.Id, null, token).ConfigureAwait(false);
            if (detailPostId != post.Id)
            {
                return;
            }

            if (page is not null)
            {
                detailComments = Oldest(page.Items);
            }
        }, () =>
        {
            if (detailPostId == post.Id)
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
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            var succeeded = false;
            try
            {
                var created = await client.AddCommentAsync(postId, trimmed, token).ConfigureAwait(false);
                if (created is not null)
                {
                    if (detailPostId == postId)
                    {
                        detailComments = Append(detailComments, created);
                    }

                    BumpCommentCount(postId, 1);
                    succeeded = true;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Chirper] comment failed: {exception.Message}");
            }
            finally
            {
                commenting = false;
                onComplete(succeeded);
            }
        });
    }

    public void DeleteComment(string postId, string commentId)
    {
        if (detailPostId == postId)
        {
            detailComments = RemoveComment(detailComments, commentId);
        }

        BumpCommentCount(postId, -1);
        RunGuarded("comment delete", async token => await client.DeleteCommentAsync(postId, commentId, token).ConfigureAwait(false));
    }

    public void SetFollow(string userId, bool follow)
    {
        UpdateUserEverywhere(userId, follow);

        RunGuarded("follow", async token =>
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
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            var succeeded = false;
            try
            {
                succeeded = await client.ReportAsync(targetType, targetId, reason, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Chirper] report failed: {exception.Message}");
            }
            finally
            {
                onComplete(succeeded);
            }
        });
    }

    public void DeletePost(string postId, Action<bool> onComplete)
    {
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            var succeeded = false;
            try
            {
                succeeded = await client.DeletePostAsync(postId, token).ConfigureAwait(false);
                if (succeeded)
                {
                    RemovePost(postId);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Chirper] delete post failed: {exception.Message}");
            }
            finally
            {
                onComplete(succeeded);
            }
        });
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

        RunGuarded("profile open", async token =>
        {
            var user = await client.UserAsync(userId, token).ConfigureAwait(false);
            var posts = await client.UserPostsAsync(userId, token).ConfigureAwait(false);
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

    public void UpdateProfile(string? displayName, string? handle, string? bio, Action<bool, string> onResult)
    {
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            var succeeded = false;
            try
            {
                var updated = await client.UpdateProfileAsync(new UpdateProfileRequest(displayName, handle, bio), token).ConfigureAwait(false);
                if (updated is not null)
                {
                    me = updated;
                    if (profileUserId == updated.Id)
                    {
                        profileUser = updated;
                    }

                    succeeded = true;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Chirper] profile update failed: {exception.Message}");
            }
            finally
            {
                onResult(succeeded, string.Empty);
            }
        });
    }

    public void UpdateAvatar(string sourcePath, WallpaperCrop crop, Action<bool> onComplete)
    {
        if (avatarBusy)
        {
            return;
        }

        avatarBusy = true;
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            var succeeded = false;
            try
            {
                var baked = ImageProcessor.BakeSquareJpeg(sourcePath, crop, AvatarSize);
                var upload = await client.UploadUrlAsync("image/jpeg", "avatar", token).ConfigureAwait(false);
                if (upload is null)
                {
                    return;
                }

                var uploaded = await client.UploadImageAsync(upload.UploadUrl, baked.Bytes, "image/jpeg", token).ConfigureAwait(false);
                if (!uploaded)
                {
                    return;
                }

                var updated = await client.UpdateProfileAsync(new UpdateProfileRequest(null, null, null, upload.PublicUrl), token).ConfigureAwait(false);
                if (updated is null)
                {
                    return;
                }

                me = updated;
                if (profileUserId == updated.Id)
                {
                    profileUser = updated;
                }

                succeeded = true;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Chirper] avatar update failed: {exception.Message}");
            }
            finally
            {
                avatarBusy = false;
                onComplete(succeeded);
            }
        });
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
        RunGuarded("search", async token =>
        {
            var result = await client.SearchAsync(trimmed, token).ConfigureAwait(false);
            if (result is not null)
            {
                discoverResults = result.Users;
            }
        }, () => searching = false);
    }

    public void ClearDiscover() => discoverResults = Array.Empty<UserDto>();

    private void RunGuarded(string operation, Func<CancellationToken, Task> action, Action? cleanup = null)
    {
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await action(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Chirper] {operation} failed: {exception.Message}");
            }
            finally
            {
                cleanup?.Invoke();
            }
        });
    }

    private static PostDto ApplyReaction(PostDto post, int newKind)
    {
        var counts = (int[])post.ReactionCounts.Clone();
        if (post.MyReaction >= 0 && post.MyReaction < counts.Length && counts[post.MyReaction] > 0)
        {
            counts[post.MyReaction]--;
        }

        if (newKind >= 0 && newKind < counts.Length)
        {
            counts[newKind]++;
        }

        var total = 0;
        for (var index = 0; index < counts.Length; index++)
        {
            total += counts[index];
        }

        return post with { ReactionCounts = counts, TotalReactions = total, MyReaction = newKind };
    }

    private void ReplacePost(PostDto updated)
    {
        forYou = Replace(forYou, updated);
        following = Replace(following, updated);
        profilePosts = Replace(profilePosts, updated);
        if (detailPost is { } current && current.Id == updated.Id)
        {
            detailPost = updated;
        }
    }

    private void RemovePost(string postId)
    {
        forYou = RemoveById(forYou, postId);
        following = RemoveById(following, postId);
        profilePosts = RemoveById(profilePosts, postId);
        if (detailPost is { } current && current.Id == postId)
        {
            detailPost = null;
            detailPostId = null;
        }
    }

    private void BumpCommentCount(string postId, int delta)
    {
        forYou = MapCommentCount(forYou, postId, delta);
        following = MapCommentCount(following, postId, delta);
        profilePosts = MapCommentCount(profilePosts, postId, delta);
        if (detailPost is { } current && current.Id == postId)
        {
            detailPost = current with { CommentCount = Math.Max(0, current.CommentCount + delta) };
        }
    }

    private void UpdateUserEverywhere(string userId, bool follow)
    {
        discoverResults = MapUsers(discoverResults, userId, follow);
        forYou = MapFollowByAuthor(forYou, userId, follow);
        following = MapFollowByAuthor(following, userId, follow);
        profilePosts = MapFollowByAuthor(profilePosts, userId, follow);
        if (detailPost is { } post && post.AuthorId == userId)
        {
            detailPost = post with { IsFollowing = follow };
        }

        if (profileUser is { } current && current.Id == userId)
        {
            profileUser = current with { IsFollowing = follow, Followers = Math.Max(0, current.Followers + (follow ? 1 : -1)) };
        }
    }

    private static PostDto[] MapCommentCount(PostDto[] source, string postId, int delta)
    {
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index].Id != postId)
            {
                continue;
            }

            var result = (PostDto[])source.Clone();
            result[index] = source[index] with { CommentCount = Math.Max(0, source[index].CommentCount + delta) };
            return result;
        }

        return source;
    }

    private static PostDto[] MapFollowByAuthor(PostDto[] source, string userId, bool follow)
    {
        var changed = false;
        var result = new PostDto[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            var post = source[index];
            if (post.AuthorId == userId && post.IsFollowing != follow)
            {
                result[index] = post with { IsFollowing = follow };
                changed = true;
            }
            else
            {
                result[index] = post;
            }
        }

        return changed ? result : source;
    }

    private static UserDto[] MapUsers(UserDto[] source, string userId, bool follow)
    {
        var changed = false;
        var result = new UserDto[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            var user = source[index];
            if (user.Id == userId && user.IsFollowing != follow)
            {
                result[index] = user with { IsFollowing = follow, Followers = Math.Max(0, user.Followers + (follow ? 1 : -1)) };
                changed = true;
            }
            else
            {
                result[index] = user;
            }
        }

        return changed ? result : source;
    }

    private static PostDto[] Replace(PostDto[] source, PostDto updated)
    {
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index].Id != updated.Id)
            {
                continue;
            }

            var result = (PostDto[])source.Clone();
            result[index] = updated;
            return result;
        }

        return source;
    }

    private static PostDto[] RemoveById(PostDto[] source, string postId)
    {
        var count = 0;
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index].Id != postId)
            {
                count++;
            }
        }

        if (count == source.Length)
        {
            return source;
        }

        var result = new PostDto[count];
        var resultIndex = 0;
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index].Id != postId)
            {
                result[resultIndex++] = source[index];
            }
        }

        return result;
    }

    private static PostDto[] Prepend(PostDto[] source, PostDto post)
    {
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index].Id == post.Id)
            {
                return source;
            }
        }

        var result = new PostDto[source.Length + 1];
        result[0] = post;
        Array.Copy(source, 0, result, 1, source.Length);
        return result;
    }

    private static CommentDto[] Oldest(CommentDto[] newestFirst)
    {
        var result = new CommentDto[newestFirst.Length];
        for (var index = 0; index < newestFirst.Length; index++)
        {
            result[index] = newestFirst[newestFirst.Length - 1 - index];
        }

        return result;
    }

    private static CommentDto[] Append(CommentDto[] source, CommentDto comment)
    {
        var result = new CommentDto[source.Length + 1];
        Array.Copy(source, 0, result, 0, source.Length);
        result[source.Length] = comment;
        return result;
    }

    private static CommentDto[] RemoveComment(CommentDto[] source, string commentId)
    {
        var index = Array.FindIndex(source, comment => comment.Id == commentId);
        if (index < 0)
        {
            return source;
        }

        var result = new CommentDto[source.Length - 1];
        Array.Copy(source, 0, result, 0, index);
        Array.Copy(source, index + 1, result, index, source.Length - index - 1);
        return result;
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
