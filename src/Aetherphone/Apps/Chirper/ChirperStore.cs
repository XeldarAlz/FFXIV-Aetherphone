using System.Threading;
using System.Threading.Tasks;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Apps.Chirper;

internal enum ChirperFeedScope
{
    ForYou,
    Following,
}

internal sealed class ChirperStore : IDisposable
{
    private static readonly TimeSpan MeRetryCooldown = TimeSpan.FromSeconds(30);

    private readonly AethernetSession session;
    private readonly AethernetClient client;
    private readonly CancellationTokenSource cancellation = new();

    private DateTime lastMeAttemptUtc = DateTime.MinValue;

    private volatile PostDto[] forYou = Array.Empty<PostDto>();
    private volatile PostDto[] following = Array.Empty<PostDto>();
    private volatile bool loadingForYou;
    private volatile bool loadingFollowing;

    private volatile string? profileUserId;
    private volatile UserDto? profileUser;
    private volatile PostDto[] profilePosts = Array.Empty<PostDto>();
    private volatile bool profileLoading;
    private volatile bool profileFailed;

    private volatile UserDto[] discoverResults = Array.Empty<UserDto>();
    private volatile bool searching;
    private volatile bool posting;
    private volatile bool loadingMe;

    public ChirperStore(AethernetSession session, AethernetClient client)
    {
        this.session = session;
        this.client = client;
    }

    public bool IsSignedIn => session.IsSignedIn;

    public UserDto? Me => session.CurrentUser;

    public PostDto[] Feed(ChirperFeedScope scope) => scope == ChirperFeedScope.ForYou ? forYou : following;

    public bool IsLoading(ChirperFeedScope scope) => scope == ChirperFeedScope.ForYou ? loadingForYou : loadingFollowing;

    public string? ProfileUserId => profileUserId;

    public UserDto? ProfileUser => profileUser;

    public PostDto[] ProfilePosts => profilePosts;

    public bool ProfileLoading => profileLoading;

    public bool ProfileFailed => profileFailed;

    public UserDto[] DiscoverResults => discoverResults;

    public bool Searching => searching;

    public bool Posting => posting;

    public void EnsureMe()
    {
        if (!session.IsSignedIn || session.CurrentUser is not null || loadingMe)
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
            var me = await client.MeAsync(token).ConfigureAwait(false);
            if (me is not null)
            {
                session.SetUser(me);
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
                    session.SetUser(updated);
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
    }

    private void UpdateUserEverywhere(string userId, bool follow)
    {
        discoverResults = MapUsers(discoverResults, userId, follow);
        if (profileUser is { } current && current.Id == userId)
        {
            profileUser = current with { IsFollowing = follow, Followers = Math.Max(0, current.Followers + (follow ? 1 : -1)) };
        }
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

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
