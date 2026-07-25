using System.Collections.Concurrent;
using System.Text.Json;
using Aetherphone.Core.Net;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using EmoteSheet = Lumina.Excel.Sheets.Emote;

namespace Aetherphone.Core.Collections;

internal sealed class CatalogEntry
{
    public volatile CollectionState State = CollectionState.Idle;
    public CollectionItem[] Items = Array.Empty<CollectionItem>();
    public int Total;
}

internal sealed class OwnedEntry
{
    public volatile OwnedState State = OwnedState.Unknown;
    public HashSet<int> Ids = new();
    public int Count;
    public int Total;
    public DateTime FetchedUtc;
}

internal sealed class CategoryProgress
{
    public int Count;
    public int Total;
    public CollectionAccess Access;

    public bool HasPercent => Access == CollectionAccess.Public || Count > 0;
}

internal sealed class LocalUnlocks
{
    public static readonly LocalUnlocks Empty = new(new HashSet<int>(), 0);

    public readonly HashSet<int> OwnedIds;
    public readonly int Total;

    public LocalUnlocks(HashSet<int> ownedIds, int total)
    {
        OwnedIds = ownedIds;
        Total = total;
    }
}

internal sealed class SummaryEntry
{
    public volatile SummaryState State = SummaryState.Unknown;
    public readonly CategoryProgress[] Categories = Create();
    public DateTime FetchedUtc;

    public CategoryProgress For(CollectionCategory category) => Categories[(int)category];

    private static CategoryProgress[] Create()
    {
        var array = new CategoryProgress[CollectionCategories.All.Length];
        for (var index = 0; index < array.Length; index++)
        {
            array[index] = new CategoryProgress();
        }

        return array;
    }
}

internal sealed class CollectionsCatalogService : IDisposable
{
    private const string ApiRoot = "https://ffxivcollect.com/api";
    private const string LocalCharacterKey = "local";
    private const uint HiddenHairstyleIcon = 131094;
    private static readonly TimeSpan CatalogFreshFor = TimeSpan.FromDays(14);
    private static readonly TimeSpan OwnedFailedRetryFor = TimeSpan.FromMinutes(1);
    private readonly HttpService http;
    private readonly DiskCache disk;
    private readonly RequestThrottle throttle;
    private readonly IDataManager dataManager;
    private readonly IUnlockState unlockState;
    private readonly IFramework framework;
    private readonly CancellationTokenSource cancellation = new();
    private readonly ConcurrentDictionary<CollectionCategory, CatalogEntry> catalogs = new();
    private readonly ConcurrentDictionary<string, OwnedEntry> owned = new();
    private readonly ConcurrentDictionary<string, SummaryEntry> summaries = new();
    private readonly ConcurrentDictionary<CollectionCategory, LocalUnlocks> localUnlocks = new();

    public CollectionsCatalogService(HttpService http, DiskCache disk, IDataManager dataManager,
        IUnlockState unlockState, IFramework framework)
    {
        this.http = http;
        this.disk = disk;
        this.dataManager = dataManager;
        this.unlockState = unlockState;
        this.framework = framework;
        throttle = new RequestThrottle(2, TimeSpan.FromMilliseconds(600));
    }

    public CatalogEntry RequestCatalog(CollectionCategory category)
    {
        var entry = catalogs.GetOrAdd(category, static _ => new CatalogEntry());

        if (entry.State == CollectionState.Idle)
        {
            entry.State = CollectionState.Loading;
            _ = LoadCatalogAsync(category, entry);
        }

        return entry;
    }

    public OwnedEntry RequestOwned(string? lodestoneId, CollectionCategory category)
    {
        var key = string.Concat(lodestoneId ?? LocalCharacterKey, ":", CollectionCategories.OwnedPath(category));
        var entry = owned.GetOrAdd(key, static _ => new OwnedEntry());
        var retryFailed = entry.State == OwnedState.Failed && DateTime.UtcNow - entry.FetchedUtc >= OwnedFailedRetryFor;
        if (entry.State != OwnedState.Unknown && !retryFailed)
        {
            return entry;
        }

        if (HasLocalUnlocks(category))
        {
            var local = EnsureLocalUnlocks(category);
            if (local is not null)
            {
                entry.Ids = local.OwnedIds;
                entry.Count = local.OwnedIds.Count;
                entry.Total = local.Total;
                entry.FetchedUtc = DateTime.UtcNow;
                entry.State = OwnedState.Ready;
            }

            return entry;
        }

        if (lodestoneId is null)
        {
            return entry;
        }

        entry.State = OwnedState.Loading;
        _ = LoadOwnedAsync(lodestoneId, category, entry);
        return entry;
    }

    public SummaryEntry RequestSummary(string? lodestoneId)
    {
        var entry = summaries.GetOrAdd(lodestoneId ?? LocalCharacterKey, static _ => new SummaryEntry());
        if (entry.State != SummaryState.Unknown)
        {
            return entry;
        }

        if (!ApplyLocalSummary(entry))
        {
            return entry;
        }

        if (lodestoneId is null)
        {
            Apply(entry, CollectionCategory.Achievements, null);
            entry.FetchedUtc = DateTime.UtcNow;
            entry.State = SummaryState.Ready;
            return entry;
        }

        entry.State = SummaryState.Loading;
        _ = LoadSummaryAsync(lodestoneId, entry);
        return entry;
    }

    public void Retry(CollectionCategory category)
    {
        if (catalogs.TryGetValue(category, out var entry) && entry.State == CollectionState.Failed)
        {
            entry.State = CollectionState.Loading;
            _ = LoadCatalogAsync(category, entry);
        }
    }

    public void ResetOwned()
    {
        owned.Clear();
        localUnlocks.Clear();
    }

    public void ResetSummaries()
    {
        summaries.Clear();
    }

    private async Task LoadCatalogAsync(CollectionCategory category, CatalogEntry entry)
    {
        try
        {
            var token = cancellation.Token;
            var path = CollectionCategories.CatalogPath(category);
            var cacheKey = string.Concat("collect:catalog:", path);
            var cached = disk.Get(cacheKey, CatalogFreshFor);
            CollectionResponse? response;

            if (cached is not null)
            {
                response = Deserialize(cached);
            }
            else
            {
                var url = string.Concat(ApiRoot, "/", path);
                response = await FetchCatalogAsync(url, token).ConfigureAwait(false);
                if (response?.Results is not null)
                {
                    disk.Set(cacheKey, Serialize(response));
                }
            }

            if (response?.Results is null)
            {
                entry.State = CollectionState.Failed;
                return;
            }

            entry.Items = Build(response.Results);
            entry.Total = response.Count != 0 ? response.Count : entry.Items.Length;
            entry.State = CollectionState.Ready;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            entry.State = CollectionState.Failed;
            AepLog.Warning($"Collections catalog fetch failed for {category}: {exception.Message}");
        }
    }

    private async Task<CollectionResponse?> FetchCatalogAsync(string url, CancellationToken token)
    {
        using (await throttle.EnterAsync(token).ConfigureAwait(false))
        {
            return await http.GetJsonAsync(url, CollectionJsonContext.Default.CollectionResponse, null, token)
                .ConfigureAwait(false);
        }
    }

    private static bool HasLocalUnlocks(CollectionCategory category) => category != CollectionCategory.Achievements;

    private bool ApplyLocalSummary(SummaryEntry entry)
    {
        for (var index = 0; index < CollectionCategories.All.Length; index++)
        {
            var category = CollectionCategories.All[index];
            if (!HasLocalUnlocks(category))
            {
                continue;
            }

            var local = EnsureLocalUnlocks(category);
            if (local is null)
            {
                return false;
            }

            Apply(entry, category, local.OwnedIds.Count, local.Total);
        }

        return true;
    }

    private LocalUnlocks? EnsureLocalUnlocks(CollectionCategory category)
    {
        if (localUnlocks.TryGetValue(category, out var cached))
        {
            return cached;
        }

        if (!framework.IsInFrameworkUpdateThread)
        {
            return null;
        }

        LocalUnlocks built;
        try
        {
            built = Collect(category);
        }
        catch (Exception exception)
        {
            built = LocalUnlocks.Empty;
            AepLog.Warning($"Collections local unlocks failed for {category}: {exception.Message}");
        }

        localUnlocks[category] = built;
        return built;
    }

    private LocalUnlocks Collect(CollectionCategory category) => category switch
    {
        CollectionCategory.Mounts => CollectMounts(),
        CollectionCategory.Minions => CollectMinions(),
        CollectionCategory.Emotes => CollectEmotes(),
        CollectionCategory.Orchestrions => CollectOrchestrions(),
        CollectionCategory.Hairstyles => CollectHairstyles(),
        CollectionCategory.Facewear => CollectFacewear(),
        CollectionCategory.TriadCards => CollectTriadCards(),
        _ => LocalUnlocks.Empty,
    };

    private LocalUnlocks CollectMounts()
    {
        var ids = new HashSet<int>();
        var total = 0;
        foreach (var row in dataManager.GetExcelSheet<Mount>())
        {
            if (row.Singular == "" || row.Order == -1)
            {
                continue;
            }

            total++;
            if (unlockState.IsMountUnlocked(row))
            {
                ids.Add((int)row.RowId);
            }
        }

        return new LocalUnlocks(ids, total);
    }

    private LocalUnlocks CollectMinions()
    {
        var ids = new HashSet<int>();
        var total = 0;
        foreach (var row in dataManager.GetExcelSheet<Companion>())
        {
            if (row.Singular == "")
            {
                continue;
            }

            total++;
            if (unlockState.IsCompanionUnlocked(row))
            {
                ids.Add((int)row.RowId);
            }
        }

        return new LocalUnlocks(ids, total);
    }

    private LocalUnlocks CollectEmotes()
    {
        var ids = new HashSet<int>();
        var total = 0;
        foreach (var row in dataManager.GetExcelSheet<EmoteSheet>())
        {
            if (row.Name == "" || row.Icon == 0 || row.UnlockLink == 0)
            {
                continue;
            }

            total++;
            if (unlockState.IsEmoteUnlocked(row))
            {
                ids.Add((int)row.RowId);
            }
        }

        return new LocalUnlocks(ids, total);
    }

    private LocalUnlocks CollectOrchestrions()
    {
        var ids = new HashSet<int>();
        var total = 0;
        foreach (var row in dataManager.GetExcelSheet<Orchestrion>())
        {
            if (row.Name == "" || row.Name == "0")
            {
                continue;
            }

            total++;
            if (unlockState.IsOrchestrionUnlocked(row))
            {
                ids.Add((int)row.RowId);
            }
        }

        return new LocalUnlocks(ids, total);
    }

    private LocalUnlocks CollectHairstyles()
    {
        var ids = new HashSet<int>();
        var seen = new HashSet<int>();
        foreach (var row in dataManager.GetExcelSheet<CharaMakeCustomize>())
        {
            if (!row.IsPurchasable || row.Icon == HiddenHairstyleIcon)
            {
                continue;
            }

            var unlockLink = (int)row.UnlockLink;
            if (!seen.Add(unlockLink))
            {
                continue;
            }

            if (unlockState.IsCharaMakeCustomizeUnlocked(row))
            {
                ids.Add(unlockLink);
            }
        }

        return new LocalUnlocks(ids, seen.Count);
    }

    private LocalUnlocks CollectFacewear()
    {
        var ids = new HashSet<int>();
        var total = 0;
        foreach (var row in dataManager.GetExcelSheet<Glasses>())
        {
            if (row.Icon == 0 || !row.Style.IsValid || row.Name != row.Style.Value.Name)
            {
                continue;
            }

            total++;
            if (unlockState.IsGlassesUnlocked(row))
            {
                ids.Add((int)row.RowId);
            }
        }

        return new LocalUnlocks(ids, total);
    }

    private LocalUnlocks CollectTriadCards()
    {
        var ids = new HashSet<int>();
        var total = 0;
        foreach (var row in dataManager.GetExcelSheet<TripleTriadCard>())
        {
            if (row.Name == "" || row.Name == "0")
            {
                continue;
            }

            total++;
            if (unlockState.IsTripleTriadCardUnlocked(row))
            {
                ids.Add((int)row.RowId);
            }
        }

        return new LocalUnlocks(ids, total);
    }

    private async Task LoadOwnedAsync(string lodestoneId, CollectionCategory category, OwnedEntry entry)
    {
        try
        {
            var token = cancellation.Token;
            var url = string.Concat(ApiRoot, "/characters/", lodestoneId, "/", CollectionCategories.OwnedPath(category),
                "/owned");
            var statusCode = 0;
            OwnedItemDto[]? items;

            using (await throttle.EnterAsync(token).ConfigureAwait(false))
            {
                items = await http.GetJsonAsync(url, CollectionJsonContext.Default.OwnedItemDtoArray, null, token,
                    status => statusCode = status).ConfigureAwait(false);
            }

            entry.FetchedUtc = DateTime.UtcNow;

            if (items is null)
            {
                entry.State = statusCode is 403 or 404 ? OwnedState.Private : OwnedState.Failed;
                return;
            }

            var ids = new HashSet<int>(items.Length);

            for (var index = 0; index < items.Length; index++)
            {
                ids.Add(items[index].Id);
            }

            entry.Ids = ids;
            entry.Count = ids.Count;
            entry.State = OwnedState.Ready;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            entry.FetchedUtc = DateTime.UtcNow;
            entry.State = OwnedState.Failed;
            AepLog.Warning($"Collections owned fetch failed for {category}: {exception.Message}");
        }
    }

    private async Task LoadSummaryAsync(string lodestoneId, SummaryEntry entry)
    {
        try
        {
            var token = cancellation.Token;
            var url = string.Concat(ApiRoot, "/characters/", lodestoneId);
            CharacterSummaryDto? dto;

            using (await throttle.EnterAsync(token).ConfigureAwait(false))
            {
                dto = await http.GetJsonAsync(url, CollectionJsonContext.Default.CharacterSummaryDto, null, token)
                    .ConfigureAwait(false);
            }

            entry.FetchedUtc = DateTime.UtcNow;
            Apply(entry, CollectionCategory.Achievements, dto?.Achievements);
            entry.State = SummaryState.Ready;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            entry.FetchedUtc = DateTime.UtcNow;
            Apply(entry, CollectionCategory.Achievements, null);
            entry.State = SummaryState.Ready;
            AepLog.Warning($"Collections summary fetch failed: {exception.Message}");
        }
    }

    private static void Apply(SummaryEntry entry, CollectionCategory category, int count, int total)
    {
        var progress = entry.For(category);
        progress.Count = count;
        progress.Total = total;
        progress.Access = CollectionAccess.Public;
    }

    private static void Apply(SummaryEntry entry, CollectionCategory category, CharacterCollectionStat? stat)
    {
        var progress = entry.For(category);
        if (stat is null)
        {
            progress.Count = 0;
            progress.Total = 0;
            progress.Access = CollectionAccess.NotSynced;
            return;
        }

        progress.Count = stat.Count;
        progress.Total = stat.Total;
        progress.Access = stat.Public switch
        {
            true => CollectionAccess.Public,
            false => CollectionAccess.Private,
            _ => CollectionAccess.NotSynced,
        };
    }

    private static CollectionItem[] Build(CollectionItemDto[] results)
    {
        var items = new CollectionItem[results.Length];

        for (var index = 0; index < results.Length; index++)
        {
            items[index] = new CollectionItem(results[index]);
        }

        return items;
    }

    private static byte[] Serialize(CollectionResponse response) =>
        JsonSerializer.SerializeToUtf8Bytes(response, CollectionJsonContext.Default.CollectionResponse);

    private static CollectionResponse? Deserialize(byte[] bytes) =>
        JsonSerializer.Deserialize(bytes, CollectionJsonContext.Default.CollectionResponse);

    public void Dispose()
    {
        cancellation.Cancel();
        throttle.Dispose();
        cancellation.Dispose();
    }
}
