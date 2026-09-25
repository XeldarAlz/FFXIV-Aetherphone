using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
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
    private readonly FeedLane<PostDto> archivedLane = new(PostOrder.NewestFirst);

    public PostDto[] ArchivedPosts => archivedLane.Items;
    public bool ArchivedLoading => archivedLane.Loading;
    public bool ArchivedLoadingMore => archivedLane.LoadingMore;
    public bool HasMoreArchived => archivedLane.HasMore;

    public AethergramStore(AethernetSession session, AccountClient account, SocialClient client, GramClient grams,
        SafetyClient safety, MediaClient media, RealtimeSignalBus signals)
        : base(session, account, client, safety, media, signals, "Aethergram")
    {
        this.grams = grams;
    }

    protected override void OnAccountReset()
    {
        archivedLane.Clear();
    }

    protected override Task<FeedPage?> FetchFeedAsync(string feedKey, string? cursor, string? regions, bool includeSensitive,
        CancellationToken token, Action<AepFailure>? onFailure = null) =>
        grams.FeedAsync(feedKey, cursor, regions, includeSensitive, token, onFailure);

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
    public void CreateGram(string[] sourcePaths, WallpaperCrop[] crops, PostAspect[] aspects, PhotoEdit[] edits,
        string caption, PhotoTagInput[]? photoTags, bool sensitive, Action<bool> onComplete)
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
                        bakedHeight, PostAspects.RevealsWholeImage(aspects[index]), edits[index]);
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

    public void EditPost(string postId, string caption, PhotoTagInput[] photoTags, bool sensitive,
        Action<bool> onComplete)
    {
        work.Run("edit post", async token =>
        {
            var result = await grams.EditAsync(postId, caption, photoTags, sensitive, token).ConfigureAwait(false);
            if (result is null)
            {
                return false;
            }

            ReplacePost(result);
            return true;
        }, onComplete);
    }

    public void ApproveTag(string postId, string tagId, Action<bool> onComplete)
    {
        work.Run("tag approve", async token =>
        {
            if (!await grams.ApproveTagAsync(tagId, token).ConfigureAwait(false))
            {
                return false;
            }

            MapPostEverywhere(postId,
                post => post with { PhotoTags = PhotoTagStates.WithState(post.PhotoTags, tagId, PhotoTagStates.Approved) });
            ClearTagged();
            return true;
        }, onComplete);
    }

    public void RemoveTag(string postId, string tagId, Action<bool> onComplete)
    {
        work.Run("tag remove", async token =>
        {
            if (!await grams.RemoveTagAsync(tagId, token).ConfigureAwait(false))
            {
                return false;
            }

            MapPostEverywhere(postId, post => post with { PhotoTags = PhotoTagStates.Without(post.PhotoTags, tagId) });
            return true;
        }, onComplete);
    }

    public void PinPost(string postId, bool replace, Action<PinOutcome> onComplete)
    {
        var outcome = PinOutcome.Failed;
        work.Run("pin post", async token =>
        {
            var result = await grams.PinAsync(postId, replace, token, failure =>
            {
                if (failure.Code == FailureCodes.PostPinLimit)
                {
                    outcome = PinOutcome.LimitReached;
                }
            }).ConfigureAwait(false);
            if (result is null)
            {
                return false;
            }

            if (result.ReplacedPostId is { } replacedPostId)
            {
                ApplyPinnedEverywhere(replacedPostId, null);
            }

            ApplyPinnedEverywhere(postId, result.Post.PinnedAtUnix);
            outcome = PinOutcome.Pinned;
            return true;
        }, _ => onComplete(outcome));
    }

    public void UnpinPost(string postId, Action<bool> onComplete)
    {
        work.Run("unpin post", async token =>
        {
            var updated = await grams.UnpinAsync(postId, token).ConfigureAwait(false);
            if (updated is null)
            {
                return false;
            }

            ApplyPinnedEverywhere(postId, null);
            return true;
        }, onComplete);
    }

    public void RefreshArchived()
    {
        if (!IsSignedIn || archivedLane.Loading)
        {
            return;
        }

        archivedLane.Loading = true;
        work.Run("archive refresh", async token =>
        {
            var page = await grams.ArchivedAsync(null, token).ConfigureAwait(false);
            if (page is not null)
            {
                archivedLane.ApplyRefresh(page.Items, page.NextCursor);
            }
        }, () => archivedLane.Loading = false);
    }

    public void LoadMoreArchived()
    {
        var cursor = archivedLane.Cursor;
        if (!IsSignedIn || cursor is null || archivedLane.LoadingMore || archivedLane.Loading)
        {
            return;
        }

        archivedLane.LoadingMore = true;
        work.Run("archive more", async token =>
        {
            var page = await grams.ArchivedAsync(cursor, token).ConfigureAwait(false);
            if (page is not null)
            {
                archivedLane.ApplyMore(page.Items, page.NextCursor);
            }
        }, () => archivedLane.LoadingMore = false);
    }

    public void ArchivePost(string postId, Action<bool> onComplete)
    {
        work.Run("archive post", async token =>
        {
            var archived = await grams.ArchiveAsync(postId, token).ConfigureAwait(false);
            if (archived is null)
            {
                return false;
            }

            RemovePost(postId);
            var current = archivedLane.Items;
            var items = CopyOnWrite.Prepend(current, archived);
            if (!ReferenceEquals(items, current))
            {
                Array.Sort(items, PostOrder.NewestFirst);
            }

            archivedLane.Items = items;
            return true;
        }, onComplete);
    }

    public void UnarchivePost(string postId, Action<bool> onComplete)
    {
        work.Run("restore post", async token =>
        {
            var restored = await grams.UnarchiveAsync(postId, token).ConfigureAwait(false);
            if (restored is null)
            {
                return false;
            }

            archivedLane.Items = CopyOnWrite.RemoveById(archivedLane.Items, postId);
            AcceptProfilePost(restored);
            ReplacePost(restored);
            return true;
        }, onComplete);
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
