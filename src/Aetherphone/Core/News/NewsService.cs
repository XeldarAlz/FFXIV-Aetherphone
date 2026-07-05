using System.Collections.Concurrent;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.News;

internal enum NewsState : byte
{
    Idle,
    Loading,
    Ready,
    Empty,
    Failed,
}

internal sealed class NewsEntry
{
    public volatile NewsState State = NewsState.Idle;
    public LodestoneNewsItem[] Items = Array.Empty<LodestoneNewsItem>();
    public DateTime FetchedUtc;
}

internal sealed class NewsService : IDisposable
{
    private const string ApiRoot = "https://lodestonenews.com/news";
    private static readonly TimeSpan FreshFor = TimeSpan.FromMinutes(5);
    private readonly HttpService http;
    private readonly CancellationTokenSource cancellation = new();
    private readonly ConcurrentDictionary<string, NewsEntry> entries = new();

    public NewsService(HttpService http)
    {
        this.http = http;
    }

    public NewsEntry Request(NewsCategory category, string locale, bool forceRefresh)
    {
        var key = string.Concat(NewsCategories.Path(category), ":", locale);
        var entry = entries.GetOrAdd(key, static _ => new NewsEntry());
        if (entry.State == NewsState.Loading)
        {
            return entry;
        }

        var stale = entry.State == NewsState.Idle || DateTime.UtcNow - entry.FetchedUtc >= FreshFor;
        if (forceRefresh || stale)
        {
            entry.State = NewsState.Loading;
            _ = LoadAsync(category, locale, entry);
        }

        return entry;
    }

    private async Task LoadAsync(NewsCategory category, string locale, NewsEntry entry)
    {
        try
        {
            var token = cancellation.Token;
            var url = string.Concat(ApiRoot, "/", NewsCategories.Path(category), "?locale=", locale);
            var items = await http.GetJsonAsync(url, LodestoneNewsJsonContext.Default.NewsItems, null, token)
                .ConfigureAwait(false);
            if (items is null)
            {
                entry.FetchedUtc = DateTime.UtcNow;
                entry.State = NewsState.Failed;
                return;
            }

            entry.Items = items;
            entry.FetchedUtc = DateTime.UtcNow;
            entry.State = items.Length == 0 ? NewsState.Empty : NewsState.Ready;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            entry.FetchedUtc = DateTime.UtcNow;
            entry.State = NewsState.Failed;
            AepLog.Warning($"News fetch failed for {category}/{locale}: {exception.Message}");
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
