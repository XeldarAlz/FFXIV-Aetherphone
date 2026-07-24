using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Notifications;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.YellowPages;

/// <summary>Account-scoped in-memory Yellow Pages state. My-ads sync rides the backstop poll; the
/// warn-first alerts (ad hidden by reports, ad expiring within a day) are derived by diffing consecutive
/// my-ads snapshots so they survive missed pushes. Never persisted.</summary>
internal sealed class YellowPagesStore : IDisposable
{
    public const string AppId = "yellowpages";

    private const long ExpiryWarningLeadSeconds = 24L * 3600L;

    private static readonly TimeSpan ForegroundPollInterval = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan BackgroundPollInterval = TimeSpan.FromSeconds(300);

    private readonly AethernetSession session;
    private readonly YellowPagesClient client;
    private readonly NotificationService notifications;
    private readonly Configuration configuration;
    private readonly RealtimeSignalBus signals;
    private readonly PollCadence cadence;
    private readonly StoreWork work = new("YellowPages");

    private string? lastAccountId;
    private volatile AdDto[] mine = Array.Empty<AdDto>();
    private volatile Dictionary<string, AdDto> knownAds = new(StringComparer.Ordinal);
    private volatile bool primed;
    private volatile bool syncing;
    private long lastMineCheckUnix;

    private readonly object chatLock = new();
    private readonly HashSet<string> chatRequested = new(StringComparer.Ordinal);
    private readonly HashSet<string> chatMisses = new(StringComparer.Ordinal);
    private volatile Dictionary<string, AdDto> chatAds = new(StringComparer.Ordinal);

    private volatile AdDto[] directory = Array.Empty<AdDto>();
    private int directoryCategories;
    private bool directoryOpenNow;
    private string? directorySearch;
    private int directoryDataCenterId;
    private int directoryRegions;
    private string? directoryCursor;
    private volatile bool directoryLoading;
    private volatile bool directoryLoadingMore;
    private volatile bool directoryHasMore;
    private volatile bool directoryLoadedOnce;
    private int directoryGeneration;

    private volatile AdDto[] saved = Array.Empty<AdDto>();
    private string? savedCursor;
    private volatile bool savedLoading;
    private volatile bool savedHasMore;
    private volatile bool savedLoadedOnce;

    public YellowPagesStore(AethernetSession session, YellowPagesClient client, NotificationService notifications,
        Configuration configuration, PhoneVisibility visibility, RealtimeSignalBus signals)
    {
        this.session = session;
        this.client = client;
        this.notifications = notifications;
        this.configuration = configuration;
        this.signals = signals;
        cadence = new PollCadence(visibility, ForegroundPollInterval, BackgroundPollInterval);
        session.Changed += OnSessionChanged;
        signals.ConnectedChanged += OnRealtimeConnected;
        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    public bool IsSignedIn => session.IsSignedIn;

    public bool Syncing => syncing;

    public bool Primed => primed;

    public AdDto[] Mine => mine;

    public AdDto[] Directory => directory;

    public bool DirectoryLoading => directoryLoading;

    public bool DirectoryLoadingMore => directoryLoadingMore;

    public bool DirectoryHasMore => directoryHasMore;

    public bool DirectoryLoadedOnce => directoryLoadedOnce;

    public AdDto[] Saved => saved;

    public bool SavedLoading => savedLoading;

    public bool SavedHasMore => savedHasMore;

    public bool SavedLoadedOnce => savedLoadedOnce;

    public int LiveMineCount
    {
        get
        {
            var snapshot = mine;
            var count = 0;
            for (var index = 0; index < snapshot.Length; index++)
            {
                if (snapshot[index].Status == AdStatuses.Live)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public void SyncNow()
    {
        if (!session.IsSignedIn || syncing)
        {
            return;
        }

        syncing = true;
        work.Run("ads mine", async token =>
        {
            var page = await client.MineAsync(token).ConfigureAwait(false);
            if (page is not null)
            {
                ApplyMine(page.Items);
            }
        }, () => syncing = false);
    }

    /// <summary>Resolves the persisted scope against the player's current world. Framework thread only,
    /// so the world read happens here and the captured values carry through paging continuations.</summary>
    private void CaptureScopeFilters()
    {
        var worldId = MusterWorlds.CurrentWorldId();
        switch (configuration.YellowPagesScope)
        {
            case AdScopes.Everywhere:
                directoryDataCenterId = 0;
                directoryRegions = 0;
                break;
            case AdScopes.DataCenter:
                directoryDataCenterId = MusterWorlds.DataCenterIdForWorld(worldId);
                directoryRegions = 0;
                break;
            default:
                directoryDataCenterId = 0;
                directoryRegions = MusterCategories.RegionBitForWorld(worldId);
                break;
        }
    }

    public void RefreshDirectory(int categories, bool openNow, string? search)
    {
        if (!session.IsSignedIn || directoryLoading)
        {
            return;
        }

        directoryLoading = true;
        CaptureScopeFilters();
        directoryCategories = categories;
        directoryOpenNow = openNow;
        directorySearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        var dataCenterId = directoryDataCenterId;
        var regions = directoryRegions;
        var capturedSearch = directorySearch;
        var afterDark = configuration.YellowPagesAfterDark;
        var generation = Interlocked.Increment(ref directoryGeneration);
        work.Run("ads directory", async token =>
        {
            var page = await client.DirectoryAsync(categories, regions, dataCenterId, openNow, afterDark,
                capturedSearch, null, token).ConfigureAwait(false);
            if (page is not null && generation == Volatile.Read(ref directoryGeneration))
            {
                directory = page.Items;
                directoryCursor = page.NextCursor;
                directoryHasMore = page.NextCursor is not null;
                directoryLoadedOnce = true;
                MergeKnown(page.Items);
            }
        }, () => directoryLoading = false);
    }

    public void LoadMoreDirectory()
    {
        if (!session.IsSignedIn || directoryLoading || directoryLoadingMore || directoryCursor is null)
        {
            return;
        }

        directoryLoadingMore = true;
        var generation = Volatile.Read(ref directoryGeneration);
        var cursor = directoryCursor;
        var categories = directoryCategories;
        var openNow = directoryOpenNow;
        var search = directorySearch;
        var dataCenterId = directoryDataCenterId;
        var regions = directoryRegions;
        var afterDark = configuration.YellowPagesAfterDark;
        work.Run("ads directory more", async token =>
        {
            var page = await client.DirectoryAsync(categories, regions, dataCenterId, openNow, afterDark,
                search, cursor, token).ConfigureAwait(false);
            if (page is not null && generation == Volatile.Read(ref directoryGeneration))
            {
                directory = AppendNew(directory, page.Items);
                directoryCursor = page.NextCursor;
                directoryHasMore = page.NextCursor is not null;
                MergeKnown(page.Items);
            }
        }, () => directoryLoadingMore = false);
    }

    public void RefreshSaved()
    {
        if (!session.IsSignedIn || savedLoading)
        {
            return;
        }

        savedLoading = true;
        work.Run("ads saved", async token =>
        {
            var page = await client.SavedAsync(null, token).ConfigureAwait(false);
            if (page is not null)
            {
                saved = page.Items;
                savedCursor = page.NextCursor;
                savedHasMore = page.NextCursor is not null;
                savedLoadedOnce = true;
                MergeKnown(page.Items);
            }
        }, () => savedLoading = false);
    }

    public void LoadMoreSaved()
    {
        if (!session.IsSignedIn || savedLoading || savedCursor is null)
        {
            return;
        }

        savedLoading = true;
        var cursor = savedCursor;
        work.Run("ads saved more", async token =>
        {
            var page = await client.SavedAsync(cursor, token).ConfigureAwait(false);
            if (page is not null)
            {
                saved = AppendNew(saved, page.Items);
                savedCursor = page.NextCursor;
                savedHasMore = page.NextCursor is not null;
            }
        }, () => savedLoading = false);
    }

    /// <summary>Resolves a chat ad token without re-fetching per frame: the first call queues one fetch,
    /// later calls read the cache. A miss is remembered so dead tokens never retry.</summary>
    public AdChatResolution ResolveForChat(string adId)
    {
        if (knownAds.TryGetValue(adId, out var known))
        {
            return new AdChatResolution(known, Missed: false);
        }

        if (chatAds.TryGetValue(adId, out var cached))
        {
            return new AdChatResolution(cached, Missed: false);
        }

        if (!session.IsSignedIn)
        {
            return new AdChatResolution(null, Missed: false);
        }

        lock (chatLock)
        {
            if (chatMisses.Contains(adId))
            {
                return new AdChatResolution(null, Missed: true);
            }

            if (!chatRequested.Add(adId))
            {
                return new AdChatResolution(null, Missed: false);
            }
        }

        work.Run("ads chat resolve", async token =>
        {
            var ad = await client.GetAsync(adId, token).ConfigureAwait(false);
            if (ad is not null)
            {
                var next = new Dictionary<string, AdDto>(chatAds, StringComparer.Ordinal);
                next[ad.Id] = ad;
                chatAds = next;
                return;
            }

            lock (chatLock)
            {
                chatMisses.Add(adId);
            }
        });
        return new AdChatResolution(null, Missed: false);
    }

    public void FetchDetail(string adId, Action<AdDto?> done)
    {
        if (!session.IsSignedIn)
        {
            done(null);
            return;
        }

        work.Run("ads detail", async token =>
        {
            var ad = await client.GetAsync(adId, token).ConfigureAwait(false);
            if (ad is not null)
            {
                MergeKnown(new[] { ad });
            }

            done(ad);
        });
    }

    public void Create(CreateAdRequest request, Action<AdCreateOutcome> done)
    {
        if (!session.IsSignedIn)
        {
            done(AdCreateOutcome.Failed);
            return;
        }

        var status = 0;
        work.Run("ads create", async token =>
        {
            var created = await client.CreateAsync(request, token, code => status = code).ConfigureAwait(false);
            if (created is null)
            {
                return false;
            }

            mine = Prepend(mine, created);
            MergeKnown(new[] { created });
            return true;
        }, ok => done(ok ? AdCreateOutcome.Created : OutcomeFor(status)));
    }

    public void Update(string adId, CreateAdRequest request, Action<AdCreateOutcome> done)
    {
        if (!session.IsSignedIn)
        {
            done(AdCreateOutcome.Failed);
            return;
        }

        var status = 0;
        work.Run("ads update", async token =>
        {
            var updated = await client.UpdateAsync(adId, request, token, code => status = code).ConfigureAwait(false);
            if (updated is null)
            {
                return false;
            }

            mine = Replace(mine, updated);
            directory = Replace(directory, updated);
            MergeKnown(new[] { updated });
            return true;
        }, ok => done(ok ? AdCreateOutcome.Created : OutcomeFor(status)));
    }

    public void Delete(string adId, Action<bool> done)
    {
        if (!session.IsSignedIn)
        {
            done(false);
            return;
        }

        work.Run("ads delete", async token =>
            await client.DeleteAsync(adId, token).ConfigureAwait(false),
            ok =>
            {
                if (ok)
                {
                    mine = Without(mine, adId);
                    directory = Without(directory, adId);
                    saved = Without(saved, adId);
                }

                done(ok);
            });
    }

    public void Renew(string adId, Action<bool> done)
    {
        if (!session.IsSignedIn)
        {
            done(false);
            return;
        }

        work.Run("ads renew", async token =>
        {
            var renewed = await client.RenewAsync(adId, token).ConfigureAwait(false);
            if (renewed is null)
            {
                return false;
            }

            mine = Replace(mine, renewed);
            MergeKnown(new[] { renewed });
            return true;
        }, done);
    }

    public void SetOpen(string adId, bool open, int minutes, Action<bool> done)
    {
        if (!session.IsSignedIn)
        {
            done(false);
            return;
        }

        work.Run("ads open", async token =>
        {
            var result = await client.OpenAsync(adId, open, minutes, token).ConfigureAwait(false);
            if (result is null)
            {
                return false;
            }

            ApplyOpenUntil(adId, result.OpenUntilUnix);
            return true;
        }, done);
    }

    public void SetSaved(string adId, bool save, Action<bool> done)
    {
        if (!session.IsSignedIn)
        {
            done(false);
            return;
        }

        work.Run("ads save", async token =>
            await client.SaveAsync(adId, save, token).ConfigureAwait(false),
            ok =>
            {
                if (ok)
                {
                    ApplySaved(adId, save);
                }

                done(ok);
            });
    }

    private static AdCreateOutcome OutcomeFor(int status)
    {
        return status switch
        {
            409 => AdCreateOutcome.TooMany,
            400 => AdCreateOutcome.Invalid,
            429 => AdCreateOutcome.RateLimited,
            _ => AdCreateOutcome.Failed,
        };
    }

    private void OnRealtimeConnected(bool connected)
    {
        if (connected)
        {
            cadence.RequestImmediate();
        }
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!session.IsSignedIn)
        {
            primed = false;
            return;
        }

        if (!cadence.Due(DateTime.UtcNow))
        {
            return;
        }

        SyncNow();
    }

    private void OnSessionChanged()
    {
        var accountId = session.CurrentUser?.Id;
        if (string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            return;
        }

        lastAccountId = accountId;
        mine = Array.Empty<AdDto>();
        knownAds = new Dictionary<string, AdDto>(StringComparer.Ordinal);
        lock (chatLock)
        {
            chatRequested.Clear();
            chatMisses.Clear();
        }

        chatAds = new Dictionary<string, AdDto>(StringComparer.Ordinal);
        directory = Array.Empty<AdDto>();
        directoryCursor = null;
        directoryHasMore = false;
        directoryLoadedOnce = false;
        saved = Array.Empty<AdDto>();
        savedCursor = null;
        savedHasMore = false;
        savedLoadedOnce = false;
        primed = false;
        lastMineCheckUnix = 0;
        cadence.Reset();
    }

    private void ApplyMine(AdDto[] current)
    {
        var previous = mine;
        var previousCheckUnix = lastMineCheckUnix;
        var wasPrimed = primed;
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        mine = current;
        MergeKnown(current);
        primed = true;
        lastMineCheckUnix = nowUnix;

        if (!wasPrimed)
        {
            return;
        }

        for (var index = 0; index < current.Length; index++)
        {
            var ad = current[index];
            AdDto? before = null;
            for (var previousIndex = 0; previousIndex < previous.Length; previousIndex++)
            {
                if (previous[previousIndex].Id == ad.Id)
                {
                    before = previous[previousIndex];
                    break;
                }
            }

            if (ad.Status == AdStatuses.Hidden && before is not null && before.Status != AdStatuses.Hidden)
            {
                notifications.Notify(new PhoneNotification(AppId, Loc.T(L.YellowPages.NotifHiddenTitle),
                    Loc.T(L.YellowPages.NotifHiddenBody, ad.Title), DateTime.Now, AppAccents.For(AppId), ad.Id));
                continue;
            }

            if (ad.Status != AdStatuses.Live)
            {
                continue;
            }

            var remaining = ad.ExpiresAtUnix - nowUnix;
            var remainingAtLastCheck = previousCheckUnix > 0 ? ad.ExpiresAtUnix - previousCheckUnix : long.MaxValue;
            if (remaining > 0 && remaining <= ExpiryWarningLeadSeconds && remainingAtLastCheck > ExpiryWarningLeadSeconds)
            {
                notifications.Notify(new PhoneNotification(AppId, Loc.T(L.YellowPages.NotifExpiringTitle),
                    Loc.T(L.YellowPages.NotifExpiringBody, ad.Title), DateTime.Now, AppAccents.For(AppId), ad.Id));
            }
        }
    }

    private void ApplyOpenUntil(string adId, long openUntilUnix)
    {
        mine = WithOpenUntil(mine, adId, openUntilUnix);
        directory = WithOpenUntil(directory, adId, openUntilUnix);
        if (knownAds.TryGetValue(adId, out var cached))
        {
            MergeKnown(new[] { cached with { OpenUntilUnix = openUntilUnix } });
        }
    }

    private void ApplySaved(string adId, bool save)
    {
        directory = WithSaved(directory, adId, save);
        if (save)
        {
            if (knownAds.TryGetValue(adId, out var cached))
            {
                saved = Prepend(Without(saved, adId), cached with { Saved = true });
            }
        }
        else
        {
            saved = Without(saved, adId);
        }

        if (knownAds.TryGetValue(adId, out var known))
        {
            MergeKnown(new[] { known with { Saved = save } });
        }
    }

    private void MergeKnown(IReadOnlyList<AdDto> ads)
    {
        if (ads.Count == 0)
        {
            return;
        }

        var next = new Dictionary<string, AdDto>(knownAds, StringComparer.Ordinal);
        for (var index = 0; index < ads.Count; index++)
        {
            next[ads[index].Id] = ads[index];
        }

        knownAds = next;
    }

    private static AdDto[] WithOpenUntil(AdDto[] source, string adId, long openUntilUnix)
    {
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index].Id != adId)
            {
                continue;
            }

            var next = (AdDto[])source.Clone();
            next[index] = next[index] with { OpenUntilUnix = openUntilUnix };
            return next;
        }

        return source;
    }

    private static AdDto[] WithSaved(AdDto[] source, string adId, bool saveFlag)
    {
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index].Id != adId)
            {
                continue;
            }

            var next = (AdDto[])source.Clone();
            next[index] = next[index] with { Saved = saveFlag };
            return next;
        }

        return source;
    }

    private static AdDto[] Prepend(AdDto[] source, AdDto ad)
    {
        var next = new AdDto[source.Length + 1];
        next[0] = ad;
        Array.Copy(source, 0, next, 1, source.Length);
        return next;
    }

    private static AdDto[] Replace(AdDto[] source, AdDto ad)
    {
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index].Id != ad.Id)
            {
                continue;
            }

            var next = (AdDto[])source.Clone();
            next[index] = ad;
            return next;
        }

        return source;
    }

    private static AdDto[] Without(AdDto[] source, string adId)
    {
        var found = false;
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index].Id == adId)
            {
                found = true;
                break;
            }
        }

        if (!found)
        {
            return source;
        }

        var next = new List<AdDto>(source.Length - 1);
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index].Id != adId)
            {
                next.Add(source[index]);
            }
        }

        return next.ToArray();
    }

    private static AdDto[] AppendNew(AdDto[] existing, AdDto[] incoming)
    {
        if (incoming.Length == 0)
        {
            return existing;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < existing.Length; index++)
        {
            seen.Add(existing[index].Id);
        }

        var merged = new List<AdDto>(existing.Length + incoming.Length);
        merged.AddRange(existing);
        for (var index = 0; index < incoming.Length; index++)
        {
            if (seen.Add(incoming[index].Id))
            {
                merged.Add(incoming[index]);
            }
        }

        return merged.ToArray();
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkUpdate;
        session.Changed -= OnSessionChanged;
        signals.ConnectedChanged -= OnRealtimeConnected;
        work.Dispose();
    }
}
