using System.Collections.Concurrent;
using System.Text.Json;
using Aetherphone.Core.Net;

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
    public DateTime FetchedUtc;
}

internal sealed class CollectionsCatalogService : IDisposable
{
    private const string ApiRoot = "https://ffxivcollect.com/api";
    private static readonly TimeSpan CatalogFreshFor = TimeSpan.FromDays(14);
    private static readonly TimeSpan OwnedFailedRetryFor = TimeSpan.FromMinutes(1);
    private readonly HttpService http;
    private readonly DiskCache disk;
    private readonly RequestThrottle throttle;
    private readonly CancellationTokenSource cancellation = new();
    private readonly ConcurrentDictionary<CollectionCategory, CatalogEntry> catalogs = new();
    private readonly ConcurrentDictionary<string, OwnedEntry> owned = new();

    public CollectionsCatalogService(HttpService http, DiskCache disk)
    {
        this.http = http;
        this.disk = disk;
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

    public OwnedEntry RequestOwned(string lodestoneId, CollectionCategory category)
    {
        var key = string.Concat(lodestoneId, ":", CollectionCategories.OwnedPath(category));
        var entry = owned.GetOrAdd(key, static _ => new OwnedEntry());
        var retryFailed = entry.State == OwnedState.Failed && DateTime.UtcNow - entry.FetchedUtc >= OwnedFailedRetryFor;

        if (entry.State == OwnedState.Unknown || retryFailed)
        {
            entry.State = OwnedState.Loading;
            _ = LoadOwnedAsync(lodestoneId, category, entry);
        }

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
