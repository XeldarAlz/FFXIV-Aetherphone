using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Analytics;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Core.Wallpapers;

namespace Aetherphone.Apps.Aethergram;

internal sealed class AethergramStore : SocialFeedStore
{
    private const int LoveKind = 1;
    private const int GramSize = 1080;

    public AethergramStore(AethernetSession session, AethernetClient client)
        : base(session, client, "Aethergram", "aethergram")
    {
    }

    protected override Task<FeedPage?> FetchFeedAsync(string feedKey, string? cursor, CancellationToken token) =>
        client.GramFeedAsync(feedKey, cursor, token);

    protected override Task<FeedPage?> FetchProfilePostsAsync(string userId, CancellationToken token) =>
        client.UserGramsAsync(userId, token);

    public void CreateGram(string sourcePath, WallpaperCrop crop, string caption, Action<bool> onComplete)
    {
        if (posting)
        {
            return;
        }

        posting = true;
        work.Run("create gram", async token =>
        {
            var baked = ImageProcessor.BakeSquareJpeg(sourcePath, crop, GramSize);
            var upload = await client.UploadUrlAsync("image/jpeg", "gram", token).ConfigureAwait(false);
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

            var created = await client.CreateGramAsync(caption.Trim(), upload.Key, baked.Width, baked.Height, token)
                .ConfigureAwait(false);
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
        if (posting)
        {
            return;
        }

        posting = true;
        work.Run("avatar update", token => UploadAvatarAsync(sourcePath, crop, token), onComplete,
            () => posting = false);
    }

    public void ToggleLike(PostDto post)
    {
        var liked = post.MyReaction < 0;
        ReplacePost(ApplyLike(post, liked));
        if (liked)
        {
            Plugin.Analytics.Track(AnalyticsEvents.Reaction("aethergram"));
        }

        work.Run("like", async token =>
        {
            var result = liked
                ? await client.LikeAsync(post.Id, token).ConfigureAwait(false)
                : await client.UnlikeAsync(post.Id, token).ConfigureAwait(false);
            if (result is not null)
            {
                ReplacePost(result);
            }
        });
    }

    private static PostDto ApplyLike(PostDto post, bool liked)
    {
        var counts = (int[])post.ReactionCounts.Clone();
        var alreadyLiked = post.MyReaction >= 0;
        var total = post.TotalReactions;
        if (liked && !alreadyLiked)
        {
            if (LoveKind < counts.Length)
            {
                counts[LoveKind]++;
            }

            total++;
        }
        else if (!liked && alreadyLiked)
        {
            if (post.MyReaction >= 0 && post.MyReaction < counts.Length && counts[post.MyReaction] > 0)
            {
                counts[post.MyReaction]--;
            }

            total = Math.Max(0, total - 1);
        }

        return post with { ReactionCounts = counts, TotalReactions = total, MyReaction = liked ? LoveKind : -1 };
    }
}
