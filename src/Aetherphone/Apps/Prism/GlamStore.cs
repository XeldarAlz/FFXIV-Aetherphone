using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Aetherphone.Core.Report;
using Aetherphone.Core.Social;
using Aetherphone.Core.Wallpapers;

namespace Aetherphone.Apps.Prism;

internal enum GlamScope : byte
{
    Today,
    New,
    Following,
}

internal readonly record struct GlamPhotoDraft(string Path, WallpaperCrop Crop);

internal sealed class GlamStore : IDisposable
{
    public const int MaxPhotos = 4;

    private const int PhotoBaseSize = 1080;
    private const PostAspect GlamAspect = PostAspect.Portrait;

    private static readonly string[] ScopeKeys = { GlamClient.ScopeToday, GlamClient.ScopeNew, GlamClient.ScopeFollowing };

    private readonly AethernetSession session;
    private readonly GlamClient client;
    private readonly MediaClient media;
    private readonly SafetyClient safety;
    private readonly SocialClient social;
    private readonly StoreWork work = new("Glams");
    private readonly FailureSlot postFailure = new();
    private readonly FeedLane<GlamDto>[] discover =
    {
        FeedLane<GlamDto>.ServerOrdered(), FeedLane<GlamDto>.ServerOrdered(), FeedLane<GlamDto>.ServerOrdered(),
    };
    private readonly FeedLane<GlamDto> list = FeedLane<GlamDto>.ServerOrdered();
    private readonly HashSet<string> triedOn = new(StringComparer.Ordinal);

    private Func<string?, CancellationToken, Task<GlamPage?>>? listFetch;
    private volatile string listKey = string.Empty;
    private volatile GlamDto? detail;
    private volatile GlamProfileDto? profile;
    private volatile GlamCollectionDto[] collections = Array.Empty<GlamCollectionDto>();
    private volatile GlamItemDto? item;
    private volatile GlamSeasonDto? season;
    private volatile bool posting;
    private volatile bool postFailed;
    private volatile int epoch;
    private bool? vanillaOnly;

    public GlamStore(AethernetSession session, AethernetApi api)
    {
        this.session = session;
        client = api.Glams;
        media = api.Media;
        safety = api.Safety;
        social = api.Social;
    }

    public static float PhotoRatio => PostAspects.Ratio(GlamAspect);

    public bool IsSignedIn => session.IsSignedIn;

    public string MyId => session.CurrentUser?.Id ?? string.Empty;

    public bool? VanillaOnly => vanillaOnly;

    public GlamDto? Detail => detail;

    public GlamProfileDto? Profile => profile;

    public GlamCollectionDto[] Collections => collections;

    public GlamItemDto? Item => item;

    public GlamSeasonDto? Season => season;

    public bool Posting => posting;

    public bool PostFailed => postFailed;

    public string PostFailureText => postFailure.Text();

    public FeedLane<GlamDto> Lane(GlamScope scope) => discover[(int)scope];

    public FeedLane<GlamDto> List => list;

    public string ListKey => listKey;

    public void SetVanillaOnly(bool? only)
    {
        if (vanillaOnly == only)
        {
            return;
        }

        vanillaOnly = only;
        epoch++;
        for (var index = 0; index < discover.Length; index++)
        {
            discover[index].Clear();
        }
    }

    public void Refresh(GlamScope scope)
    {
        var lane = discover[(int)scope];
        if (!session.IsSignedIn || lane.Loading)
        {
            return;
        }

        var key = ScopeKeys[(int)scope];
        var only = vanillaOnly;
        var started = epoch;
        lane.Loading = true;
        work.Run("discover", async token =>
        {
            var reported = AepFailure.None;
            var page = await client.FeedAsync(key, only, null, token, failure => reported = failure).ConfigureAwait(false);
            if (started != epoch)
            {
                return;
            }

            if (page is null)
            {
                lane.RecordFailure(reported.Failed ? reported : AepFailure.Transport(AepFailureKind.Offline));
                return;
            }

            lane.ApplyRefresh(page.Items ?? Array.Empty<GlamDto>(), page.NextCursor);
        }, () => lane.Loading = false);
    }

    public void LoadMore(GlamScope scope)
    {
        var lane = discover[(int)scope];
        var cursor = lane.Cursor;
        if (!session.IsSignedIn || cursor is null || lane.Loading || lane.LoadingMore)
        {
            return;
        }

        var key = ScopeKeys[(int)scope];
        var only = vanillaOnly;
        var started = epoch;
        lane.LoadingMore = true;
        work.Run("discover more", async token =>
        {
            var page = await client.FeedAsync(key, only, cursor, token).ConfigureAwait(false);
            if (page is not null && started == epoch)
            {
                lane.ApplyMore(page.Items ?? Array.Empty<GlamDto>(), page.NextCursor);
            }
        }, () => lane.LoadingMore = false);
    }

    public void OpenProfile(GlamAuthorDto author)
    {
        profile = new GlamProfileDto(User: author);
        var userId = author.Id;
        OpenList("user:" + userId, (cursor, token) => client.UserGlamsAsync(userId, cursor, token));
        work.Run("profile", async token =>
        {
            var fetched = await client.ProfileAsync(userId, token).ConfigureAwait(false);
            if (fetched is not null && profile?.User?.Id == userId)
            {
                profile = fetched with { User = fetched.User ?? author };
            }
        });
    }

    public void OpenCollection(string collectionId) =>
        OpenList("collection:" + collectionId, (cursor, token) => client.CollectionGlamsAsync(collectionId, cursor, token));

    public void OpenItem(uint itemId)
    {
        item = new GlamItemDto(itemId);
        OpenList("item:" + itemId, (cursor, token) => client.ItemGlamsAsync(itemId, cursor, token));
        work.Run("item", async token =>
        {
            var fetched = await client.ItemAsync(itemId, token).ConfigureAwait(false);
            if (fetched is not null && item?.ItemId == itemId)
            {
                item = fetched;
            }
        });
    }

    public void OpenMod(string name, string author) =>
        OpenList("mod:" + name + "|" + author, (cursor, token) => client.ModGlamsAsync(name, author, cursor, token));

    public void LoadMoreList()
    {
        var cursor = list.Cursor;
        var fetch = listFetch;
        if (fetch is null || cursor is null || list.Loading || list.LoadingMore)
        {
            return;
        }

        var key = listKey;
        list.LoadingMore = true;
        work.Run("list more", async token =>
        {
            var page = await fetch(cursor, token).ConfigureAwait(false);
            if (page is not null && key == listKey)
            {
                list.ApplyMore(page.Items ?? Array.Empty<GlamDto>(), page.NextCursor);
            }
        }, () => list.LoadingMore = false);
    }

    public void Open(GlamDto glam)
    {
        detail = glam;
        var glamId = glam.Id;
        work.Run("glam", async token =>
        {
            var fetched = await client.GlamAsync(glamId, token).ConfigureAwait(false);
            if (fetched is not null && detail?.Id == glamId)
            {
                Replace(fetched);
            }
        });
    }

    public void ToggleLike(GlamDto glam)
    {
        var liked = !glam.Liked;
        var mine = glam.Author?.Id == MyId;
        Replace(glam with { Liked = liked, Likes = mine ? Math.Max(0, glam.Likes + (liked ? 1 : -1)) : glam.Likes });
        work.Run("like", async token =>
        {
            if (!await client.LikeAsync(glam.Id, liked, token).ConfigureAwait(false))
            {
                Replace(glam);
            }
        });
    }

    public void ToggleSave(GlamDto glam)
    {
        var saved = !glam.Saved;
        Replace(glam with { Saved = saved });
        work.Run("save", async token =>
        {
            if (!await client.SetInCollectionAsync(GlamClient.SavedCollection, glam.Id, saved, token).ConfigureAwait(false))
            {
                Replace(glam);
            }
        });
    }

    public void TriedOn(GlamDto glam)
    {
        if (!triedOn.Add(glam.Id))
        {
            return;
        }

        var glamId = glam.Id;
        work.Run("tried on", token => client.TriedOnAsync(glamId, token));
    }

    public void SetFollow(GlamAuthorDto author, bool follow)
    {
        if (profile is { } current && current.User?.Id == author.Id)
        {
            profile = current with { Following = follow };
        }

        work.Run("follow", async token =>
        {
            var done = follow
                ? await social.FollowAsync(author.Id, token).ConfigureAwait(false) is not null
                : await social.UnfollowAsync(author.Id, token).ConfigureAwait(false);
            if (!done && profile is { } latest && latest.User?.Id == author.Id)
            {
                profile = latest with { Following = !follow };
            }
        });
    }

    public void Post(GlamPhotoDraft[] photos, GlamGearDto[] gear, string title, string[] tags, int race, int gender,
        bool vanilla, Action<bool> onComplete)
    {
        if (posting || photos.Length == 0 || !session.IsSignedIn)
        {
            return;
        }

        posting = true;
        postFailed = false;
        postFailure.Clear();
        work.Run("post", async token =>
        {
            var (width, height) = PostAspects.Size(GlamAspect, PhotoBaseSize);
            var count = Math.Min(photos.Length, MaxPhotos);
            var uploaded = new GlamPhotoRequest[count];
            var failure = AepFailure.None;
            for (var index = 0; index < count; index++)
            {
                var baked = ImageProcessor.BakeCroppedUpload(photos[index].Path, photos[index].Crop, width, height,
                    PostAspects.RevealsWholeImage(GlamAspect), PhotoEdit.None);
                var upload = await media.UploadUrlAsync(baked.ContentType, PrismClient.MediaScope, token,
                    received => failure = received).ConfigureAwait(false);
                if (upload is null || !await media.UploadImageAsync(upload.UploadUrl, baked.Bytes, baked.ContentType,
                        token, received => failure = received).ConfigureAwait(false))
                {
                    return FailPost(failure);
                }

                uploaded[index] = new GlamPhotoRequest(upload.Key, baked.Width, baked.Height);
            }

            var created = await client.CreateAsync(new CreateGlamRequest(title, uploaded, gear, tags, race, gender, vanilla,
                string.Empty), token, received => failure = received).ConfigureAwait(false);
            if (created is null)
            {
                return FailPost(failure);
            }

            var newest = discover[(int)GlamScope.New];
            newest.Items = CopyOnWrite.Prepend(newest.Items, created);
            detail = created;
            return true;
        }, onComplete, () => posting = false);
    }

    public void Delete(GlamDto glam)
    {
        Remove(glam.Id);
        work.Run("delete", token => client.DeleteAsync(glam.Id, token));
    }

    public void Report(string glamId, ReportReason reason, Action<bool> done) =>
        work.Run("report", token => safety.ReportAsync(GlamClient.ReportTarget, glamId, reason, token), done);

    public void RefreshCollections()
    {
        work.Run("collections", async token =>
        {
            var fetched = await client.CollectionsAsync(token).ConfigureAwait(false);
            if (fetched is not null)
            {
                collections = fetched;
            }
        });
    }

    public void CreateCollection(string name)
    {
        work.Run("new collection", async token =>
        {
            var created = await client.CreateCollectionAsync(name, token).ConfigureAwait(false);
            if (created is not null)
            {
                collections = CopyOnWrite.Append(collections, created);
            }
        });
    }

    public void AddToCollection(string collectionId, GlamDto glam)
    {
        var glamId = glam.Id;
        work.Run("collect", token => client.SetInCollectionAsync(collectionId, glamId, true, token));
    }

    public void RefreshSeason()
    {
        work.Run("season", async token =>
        {
            var fetched = await client.SeasonAsync(token).ConfigureAwait(false);
            if (fetched is not null)
            {
                season = fetched;
            }
        });
    }

    public void Dispose()
    {
        work.Dispose();
    }

    private void OpenList(string key, Func<string?, CancellationToken, Task<GlamPage?>> fetch)
    {
        if (key == listKey && !list.Failed && (list.Loading || !list.NeverLoaded))
        {
            return;
        }

        listKey = key;
        listFetch = fetch;
        list.Clear();
        list.Loading = true;
        work.Run("list", async token =>
        {
            var reported = AepFailure.None;
            var page = await fetch(null, token).ConfigureAwait(false);
            if (key != listKey)
            {
                return;
            }

            if (page is null)
            {
                list.RecordFailure(reported.Failed ? reported : AepFailure.Transport(AepFailureKind.Offline));
                return;
            }

            list.ApplyRefresh(page.Items ?? Array.Empty<GlamDto>(), page.NextCursor);
        }, () => list.Loading = false);
    }

    private void Replace(GlamDto glam)
    {
        for (var index = 0; index < discover.Length; index++)
        {
            discover[index].Items = CopyOnWrite.Replace(discover[index].Items, glam);
        }

        list.Items = CopyOnWrite.Replace(list.Items, glam);
        if (detail?.Id == glam.Id)
        {
            detail = glam;
        }
    }

    private void Remove(string glamId)
    {
        for (var index = 0; index < discover.Length; index++)
        {
            discover[index].Items = CopyOnWrite.RemoveById(discover[index].Items, glamId);
        }

        list.Items = CopyOnWrite.RemoveById(list.Items, glamId);
        if (detail?.Id == glamId)
        {
            detail = null;
        }
    }

    private bool FailPost(AepFailure failure)
    {
        postFailure.Set(failure.Failed ? failure : AepFailure.Transport(AepFailureKind.Offline));
        postFailed = true;
        return false;
    }
}
