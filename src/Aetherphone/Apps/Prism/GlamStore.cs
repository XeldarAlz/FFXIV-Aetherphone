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
    Popular,
    Following,
}

internal readonly record struct GlamPhotoDraft(string Path, WallpaperCrop Crop);

internal sealed record GlamMore(string GlamId, GlamDto[] Items);

internal sealed class GlamStore : IDisposable
{
    public const int MaxPhotos = 4;

    private const int MoreLimit = 4;

    private const int PhotoBaseSize = 1080;
    private const PostAspect GlamAspect = PostAspect.Portrait;

    private static readonly string[] ScopeKeys =
    {
        GlamClient.ScopeToday, GlamClient.ScopeNew, GlamClient.ScopePopular, GlamClient.ScopeFollowing,
    };

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
        FeedLane<GlamDto>.ServerOrdered(),
    };
    private readonly FeedLane<GlamDto> list = FeedLane<GlamDto>.ServerOrdered();
    private readonly HashSet<string> triedOn = new(StringComparer.Ordinal);

    private Func<string?, CancellationToken, Action<AepFailure>?, Task<GlamPage?>>? listFetch;
    private volatile string listKey = string.Empty;
    private volatile GlamDto? detail;
    private volatile GlamDto? posted;
    private volatile bool detailPending;
    private volatile bool detailMissing;
    private volatile GlamMore? more;
    private volatile string moreFor = string.Empty;
    private volatile int listOpened;
    private volatile GlamProfileDto? profile;
    private volatile GlamItemDto? item;
    private volatile GlamSeasonDto? season;
    private volatile bool posting;
    private volatile bool postFailed;
    private volatile int epoch;
    private bool? vanillaOnly;
    private (int Race, int Gender) body;

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

    public bool FitsBody => body.Race > 0;

    public GlamDto? Detail => detail;

    public GlamDto? Posted => posted;

    public bool DetailPending => detailPending;

    public bool DetailMissing => detailMissing;

    public GlamDto[] MoreFrom(string glamId) =>
        more is { } shown && shown.GlamId == glamId ? shown.Items : Array.Empty<GlamDto>();

    public GlamProfileDto? Profile => profile;

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
        ClearDiscover();
    }

    public void SetBody(int race, int gender)
    {
        if (body == (race, gender))
        {
            return;
        }

        body = (race, gender);
        ClearDiscover();
    }

    private void ClearDiscover()
    {
        epoch++;
        for (var index = 0; index < discover.Length; index++)
        {
            if (index != (int)GlamScope.Today)
            {
                discover[index].Clear();
            }
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
        var daily = scope == GlamScope.Today;
        var only = daily ? null : vanillaOnly;
        var fits = daily ? default : body;
        var started = daily ? -1 : epoch;
        lane.Loading = true;
        work.Run("discover", async token =>
        {
            var reported = AepFailure.None;
            var page = await client.FeedAsync(key, only, fits, null, token, failure => reported = failure)
                .ConfigureAwait(false);
            if (started >= 0 && started != epoch)
            {
                return;
            }

            if (page is null)
            {
                lane.RecordFailure(reported.Failed ? reported : AepFailure.Transport(AepFailureKind.Offline));
                return;
            }

            lane.ApplyRefresh(page.Items ?? Array.Empty<GlamDto>(), page.NextCursor);
        }, () =>
        {
            if (started < 0 || started == epoch)
            {
                lane.Loading = false;
            }
        });
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
        var daily = scope == GlamScope.Today;
        var only = daily ? null : vanillaOnly;
        var fits = daily ? default : body;
        var started = daily ? -1 : epoch;
        lane.LoadingMore = true;
        work.Run("discover more", async token =>
        {
            var page = await client.FeedAsync(key, only, fits, cursor, token).ConfigureAwait(false);
            if (page is not null && (started < 0 || started == epoch))
            {
                lane.ApplyMore(page.Items ?? Array.Empty<GlamDto>(), page.NextCursor);
            }
        }, () =>
        {
            if (started < 0 || started == epoch)
            {
                lane.LoadingMore = false;
            }
        });
    }

    public void OpenProfile(GlamAuthorDto author, bool saved = false)
    {
        var userId = author.Id;
        if (profile?.User?.Id != userId)
        {
            profile = new GlamProfileDto(User: author);
        }

        if (saved)
        {
            OpenCollection(GlamClient.SavedCollection);
        }
        else
        {
            OpenList(UserListKey(userId),
                (cursor, token, failure) => client.UserGlamsAsync(userId, cursor, token, failure));
        }

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
        OpenList("collection:" + collectionId,
            (cursor, token, failure) => client.CollectionGlamsAsync(collectionId, cursor, token, failure));

    public void OpenItem(uint itemId)
    {
        item = null;
        var key = "item:" + itemId;
        OpenList(key, (cursor, token, failure) => client.ItemGlamsAsync(itemId, cursor, token, failure));
        work.Run("item", async token =>
        {
            var fetched = await client.ItemAsync(itemId, token).ConfigureAwait(false);
            if (fetched is not null && listKey == key)
            {
                item = fetched;
            }
        });
    }

    public void OpenTag(string tag) =>
        OpenList("tag:" + tag, (cursor, token, failure) => client.TagGlamsAsync(tag, cursor, token, failure));

    public void OpenMoreFrom(GlamDto glam)
    {
        if (moreFor == glam.Id || glam.Author is not { Id.Length: > 0 } author || !session.IsSignedIn)
        {
            return;
        }

        var glamId = glam.Id;
        var authorId = author.Id;
        moreFor = glamId;
        work.Run("more from", async token =>
        {
            var page = await client.UserGlamsAsync(authorId, null, token).ConfigureAwait(false);
            if (page?.Items is not { } items || moreFor != glamId)
            {
                return;
            }

            var kept = new List<GlamDto>(MoreLimit);
            for (var index = 0; index < items.Length && kept.Count < MoreLimit; index++)
            {
                if (items[index].Id != glamId)
                {
                    kept.Add(items[index]);
                }
            }

            more = new GlamMore(glamId, kept.ToArray());
        });
    }

    public void OpenMod(string name, string author) =>
        OpenList("mod:" + name + "|" + author,
            (cursor, token, failure) => client.ModGlamsAsync(name, author, cursor, token, failure));

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
            var page = await fetch(cursor, token, null).ConfigureAwait(false);
            if (page is not null && key == listKey)
            {
                list.ApplyMore(page.Items ?? Array.Empty<GlamDto>(), page.NextCursor);
            }
        }, () =>
        {
            if (key == listKey)
            {
                list.LoadingMore = false;
            }
        });
    }

    public void Open(GlamDto glam)
    {
        detail = glam;
        var glamId = glam.Id;
        detailPending = glamId.Length > 0 && glam.Photos is not { Length: > 0 };
        detailMissing = false;
        if (glamId.Length == 0)
        {
            return;
        }

        work.Run("glam", async token =>
        {
            var fetched = await client.GlamAsync(glamId, token).ConfigureAwait(false);
            if (detail?.Id != glamId)
            {
                return;
            }

            if (fetched is not null)
            {
                Replace(fetched);
            }
            else if (detailPending)
            {
                detailMissing = true;
            }

            detailPending = false;
        });
    }

    public void ToggleLike(GlamDto glam)
    {
        var liked = !glam.Liked;
        Replace(glam with { Liked = liked, Likes = Math.Max(0, glam.Likes + (liked ? 1 : -1)) });
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

        if (glam.Author?.Id != MyId)
        {
            Replace(glam with { TryOns = glam.TryOns + 1 });
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
        bool vanilla, string inspiredBy, Action<bool> onComplete)
    {
        if (posting || photos.Length == 0 || !session.IsSignedIn)
        {
            return;
        }

        posting = true;
        postFailed = false;
        posted = null;
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
                inspiredBy), token, received => failure = received).ConfigureAwait(false);
            if (created is null)
            {
                return FailPost(failure);
            }

            var newest = discover[(int)GlamScope.New];
            newest.Items = CopyOnWrite.Prepend(newest.Items, created);
            if (listKey == UserListKey(MyId))
            {
                list.Items = CopyOnWrite.Prepend(list.Items, created);
            }

            posted = created;
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

    private static string UserListKey(string userId) => "user:" + userId;

    private void OpenList(string key, Func<string?, CancellationToken, Action<AepFailure>?, Task<GlamPage?>> fetch)
    {
        if (key == listKey && !list.Failed && (list.Loading || !list.NeverLoaded))
        {
            return;
        }

        listKey = key;
        listFetch = fetch;
        list.Clear();
        list.Loading = true;
        var opened = ++listOpened;
        work.Run("list", async token =>
        {
            var reported = AepFailure.None;
            var page = await fetch(null, token, failure => reported = failure).ConfigureAwait(false);
            if (opened != listOpened)
            {
                return;
            }

            if (page is null)
            {
                list.RecordFailure(reported.Failed ? reported : AepFailure.Transport(AepFailureKind.Offline));
                return;
            }

            list.ApplyRefresh(page.Items ?? Array.Empty<GlamDto>(), page.NextCursor);
        }, () =>
        {
            if (opened == listOpened)
            {
                list.Loading = false;
            }
        });
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
