using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Social;
using Aetherphone.Core.Wallpapers;

namespace Aetherphone.Apps.Chirper;

internal sealed class ChirperStore : SocialFeedStore
{
    private volatile bool avatarBusy;

    public ChirperStore(AethernetSession session, AccountClient account, SocialClient client, SafetyClient safety,
        MediaClient media)
        : base(session, account, client, safety, media, "Chirper")
    {
    }

    public bool AvatarBusy => avatarBusy;

    protected override Task<FeedPage?> FetchFeedAsync(string feedKey, string? cursor, CancellationToken token) =>
        client.FeedAsync(feedKey, cursor, token);

    protected override Task<FeedPage?> FetchProfilePostsAsync(string userId, CancellationToken token) =>
        client.UserPostsAsync(userId, token);

    public void Compose(string text, Action<bool> onComplete)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0 || posting)
        {
            return;
        }

        posting = true;
        work.Run("compose", async token =>
        {
            var created = await client.CreatePostAsync(trimmed, token).ConfigureAwait(false);
            if (created is null)
            {
                return false;
            }

            AcceptCreatedPost(created);
            return true;
        }, onComplete, () => posting = false);
    }

    public void ToggleReaction(PostDto post, int kind)
    {
        var target = post.MyReaction == kind ? -1 : kind;
        ReplacePost(ApplyReaction(post, target));
        work.Run("reaction", async token =>
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

    public void ToggleRepost(PostDto post)
    {
        var original = post.RepostOfId is not null && post.ReferencedPost is not null ? post.ReferencedPost : post;
        var next = !original.MyReposted;
        var updated = original with
        {
            MyReposted = next,
            RepostCount = Math.Max(0, original.RepostCount + (next ? 1 : -1)),
        };
        ReplacePost(updated);
        if (next)
        {
            InsertLocalRepost(updated);
        }
        else
        {
            RemoveLocalReposts(updated.Id);
        }

        work.Run("repost", async token =>
        {
            var result = next
                ? await client.RepostAsync(original.Id, token).ConfigureAwait(false)
                : await client.UnrepostAsync(original.Id, token).ConfigureAwait(false);
            if (result is not null)
            {
                ReplacePost(result);
            }
        });
    }

    private void InsertLocalRepost(PostDto original)
    {
        if (Me is not { } me)
        {
            return;
        }

        RemoveLocalReposts(original.Id);
        var row = original with
        {
            Id = "pending-repost-" + original.Id,
            AuthorId = me.Id,
            AuthorName = me.Name,
            AuthorWorld = me.World,
            AuthorDisplayName = me.DisplayName,
            AuthorHandle = me.Handle,
            AuthorAvatarUrl = me.AvatarUrl,
            Text = string.Empty,
            CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            RepostOfId = original.Id,
            ReferencedPost = original,
        };
        AcceptCreatedPost(row);
    }

    private void RemoveLocalReposts(string originalId)
    {
        if (Me is not { } me)
        {
            return;
        }

        forYouLane.Items = CopyOnWrite.RemoveWhere(forYouLane.Items,
            post => post.RepostOfId == originalId && post.AuthorId == me.Id);
        followingLane.Items = CopyOnWrite.RemoveWhere(followingLane.Items,
            post => post.RepostOfId == originalId && post.AuthorId == me.Id);
        profilePosts = CopyOnWrite.RemoveWhere(profilePosts,
            post => post.RepostOfId == originalId && post.AuthorId == me.Id);
    }

    public void Quote(string text, string quotedPostId, Action<bool> onComplete)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0 || posting)
        {
            return;
        }

        posting = true;
        work.Run("quote", async token =>
        {
            var created = await client.QuotePostAsync(trimmed, quotedPostId, token).ConfigureAwait(false);
            if (created is null)
            {
                return false;
            }

            AcceptCreatedPost(created);
            return true;
        }, onComplete, () => posting = false);
    }

    public void UpdateAvatar(string sourcePath, WallpaperCrop crop, Action<bool> onComplete)
    {
        if (avatarBusy)
        {
            return;
        }

        avatarBusy = true;
        work.Run("avatar update", token => UploadAvatarAsync(sourcePath, crop, token), onComplete,
            () => avatarBusy = false);
    }

    protected override void ApplyFollowEverywhere(string userId, bool follow)
    {
        base.ApplyFollowEverywhere(userId, follow);
        forYouLane.Items = MapFollowByAuthor(forYouLane.Items, userId, follow);
        followingLane.Items = MapFollowByAuthor(followingLane.Items, userId, follow);
        profilePosts = MapFollowByAuthor(profilePosts, userId, follow);
        if (detailPost is { } post && post.AuthorId == userId)
        {
            detailPost = post with { IsFollowing = follow };
        }
    }

    private static PostDto[] MapFollowByAuthor(PostDto[] source, string userId, bool follow) =>
        CopyOnWrite.Map(source,
            post => post.AuthorId == userId && post.IsFollowing != follow,
            post => post with { IsFollowing = follow });

    private static PostDto ApplyReaction(PostDto post, int newKind)
    {
        var (counts, total) = ReactionTally.Shift(post.ReactionCounts, post.MyReaction, newKind);
        return post with { ReactionCounts = counts, TotalReactions = total, MyReaction = newKind };
    }
}
