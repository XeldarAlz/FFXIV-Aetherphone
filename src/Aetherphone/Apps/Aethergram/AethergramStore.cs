using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Aetherphone.Core.Social;
using Aetherphone.Core.Wallpapers;

namespace Aetherphone.Apps.Aethergram;

internal sealed class AethergramStore : SocialFeedStore
{
    private const int LoveKind = 1;
    private const int GramSize = 1080;

    private readonly GramClient grams;

    public AethergramStore(AethernetSession session, AccountClient account, SocialClient client, GramClient grams,
        SafetyClient safety, MediaClient media, RealtimeSignalBus signals)
        : base(session, account, client, safety, media, signals, "Aethergram")
    {
        this.grams = grams;
    }

    protected override Task<FeedPage?> FetchFeedAsync(string feedKey, string? cursor, string? regions,
        CancellationToken token, Action<AepFailure>? onFailure = null) =>
        grams.FeedAsync(feedKey, cursor, regions, token, onFailure);

    protected override Task<FeedPage?> FetchProfilePostsAsync(string userId, string? cursor, CancellationToken token) =>
        grams.UserGramsAsync(userId, cursor, token);

    protected override Task<FeedPage?> FetchTaggedPostsAsync(string userId, string? cursor, CancellationToken token) =>
        grams.UserTaggedAsync(userId, cursor, token);

    protected override Task<FeedPage?> FetchHashtagPostsAsync(string tag, string? cursor, CancellationToken token) =>
        grams.TagPostsAsync(tag, cursor, token);

    // aspects holds one choice per photo. The post's MediaWidth/MediaHeight is the first photo's
    // real baked size (GIFs already send theirs), so the feed frame matches that photo's shape;
    // the other carousel photos are baked to their own boxes and cover-fit into the frame at
    // draw time.
    public void CreateGram(string[] sourcePaths, WallpaperCrop[] crops, PostAspect[] aspects, string caption,
        PhotoTagInput[]? photoTags, bool sensitive, Action<bool> onComplete)
    {
        if (posting || sourcePaths.Length == 0)
        {
            return;
        }

        posting = true;
        work.Run("create gram", async token =>
        {
            var keys = new string[sourcePaths.Length];
            var (containerWidth, containerHeight) = PostAspects.Size(aspects[0], GramSize);
            for (var index = 0; index < sourcePaths.Length; index++)
            {
                byte[] bytes;
                string contentType;
                if (GifMedia.IsGif(sourcePaths[index]))
                {
                    bytes = await File.ReadAllBytesAsync(sourcePaths[index], token).ConfigureAwait(false);
                    if (bytes.Length == 0 || bytes.Length > GifMedia.MaxBytes)
                    {
                        AepLog.Warning($"Gram upload rejected a GIF of {bytes.Length} bytes; the cap is {GifMedia.MaxBytes}");
                        return false;
                    }

                    var (gifWidth, gifHeight) = ImageProcessor.IdentifyDimensions(bytes);
                    contentType = "image/gif";
                    if (index == 0 && gifWidth > 0 && gifHeight > 0)
                    {
                        containerWidth = gifWidth;
                        containerHeight = gifHeight;
                    }
                }
                else
                {
                    var (bakedWidth, bakedHeight) = PostAspects.Size(aspects[index], GramSize);
                    var baked = ImageProcessor.BakeCroppedJpeg(sourcePaths[index], crops[index], bakedWidth,
                        bakedHeight, PostAspects.RevealsWholeImage(aspects[index]));
                    bytes = baked.Bytes;
                    contentType = "image/jpeg";
                    if (index == 0)
                    {
                        containerWidth = baked.Width;
                        containerHeight = baked.Height;
                    }
                }

                var upload = await media.UploadUrlAsync(contentType, "gram", token).ConfigureAwait(false);
                if (upload is null)
                {
                    return false;
                }

                var uploaded = await media.UploadImageAsync(upload.UploadUrl, bytes, contentType, token)
                    .ConfigureAwait(false);
                if (!uploaded)
                {
                    return false;
                }

                keys[index] = upload.Key;
            }

            var created = await grams.CreateAsync(caption.Trim(), keys, containerWidth, containerHeight, photoTags,
                sensitive, token).ConfigureAwait(false);
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
