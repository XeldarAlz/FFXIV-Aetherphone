using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Analytics;
using Aetherphone.Core.Social;
using Aetherphone.Core.Wallpapers;

namespace Aetherphone.Apps.Chirper;

internal sealed class ChirperStore : SocialFeedStore
{
    private volatile bool avatarBusy;

    public ChirperStore(AethernetSession session, AethernetClient client)
        : base(session, client, "Chirper", "chirper")
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
        if (target >= 0)
        {
            Plugin.Analytics.Track(AnalyticsEvents.Reaction("chirper"));
        }

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
        forYou = MapFollowByAuthor(forYou, userId, follow);
        following = MapFollowByAuthor(following, userId, follow);
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
}
