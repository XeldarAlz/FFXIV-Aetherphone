using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
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

    private readonly GramClient grams;

    public AethergramStore(AethernetSession session, AccountClient account, SocialClient client, GramClient grams,
        SafetyClient safety, MediaClient media)
        : base(session, account, client, safety, media, "Aethergram", "aethergram")
    {
        this.grams = grams;
    }

    protected override Task<FeedPage?> FetchFeedAsync(string feedKey, string? cursor, CancellationToken token) =>
        grams.FeedAsync(feedKey, cursor, token);

    protected override Task<FeedPage?> FetchProfilePostsAsync(string userId, CancellationToken token) =>
        grams.UserGramsAsync(userId, token);

    protected override Task<FeedPage?> FetchTaggedPostsAsync(string userId, CancellationToken token) =>
        grams.UserTaggedAsync(userId, token);

    public void CreateGram(string[] sourcePaths, WallpaperCrop[] crops, string caption, PhotoTagInput[]? photoTags,
        Action<bool> onComplete)
    {
        if (posting || sourcePaths.Length == 0)
        {
            return;
        }

        posting = true;
        work.Run("create gram", async token =>
        {
            var keys = new string[sourcePaths.Length];
            for (var index = 0; index < sourcePaths.Length; index++)
            {
                var baked = ImageProcessor.BakeSquareJpeg(sourcePaths[index], crops[index], GramSize);
                var upload = await media.UploadUrlAsync("image/jpeg", "gram", token).ConfigureAwait(false);
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

                keys[index] = upload.Key;
            }

            var created = await grams.CreateAsync(caption.Trim(), keys, GramSize, GramSize, photoTags, token)
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
