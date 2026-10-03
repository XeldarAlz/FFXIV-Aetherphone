using System.Collections.Concurrent;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Market;

internal enum MarketState : byte
{
    Idle,
    Loading,
    Ready,
    Empty,
    Failed,
}

internal sealed class MarketEntry
{
    public volatile MarketState State = MarketState.Idle;
    public MarketSnapshot? Snapshot;
    public DateTime FetchedUtc;
}

internal readonly record struct MarketKey(uint ItemId, MarketScopeKind Kind, string Api)
{
    public static MarketKey Of(uint itemId, in MarketScope scope) => new(itemId, scope.Kind, scope.ApiName);
}

internal sealed class MarketboardService : IDisposable
{
    private const string ApiRoot = "https://universalis.app/api/v2";
    private const int ListingCount = 50;
    private const int ListingFetchCount = 100;
    private const int HistoryCount = 25;
    private const int AggregatedBatch = 80;
    private const int WatchHistoryBatch = 20;
    private const int WatchHistoryEntries = 400;
    private const int MonthHistoryEntries = 3000;
    private static readonly TimeSpan FreshFor = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan AggregatedFreshFor = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan HistoryFreshFor = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan HistoryRetryAfter = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan TaxFreshFor = TimeSpan.FromHours(6);
    private static readonly MarketEntry Invalid = new() { State = MarketState.Idle };
    private static readonly MarketHistoryEntry InvalidHistory = new() { State = MarketState.Idle };
    private readonly HttpService http;
    private readonly RequestThrottle throttle;
    private readonly CancellationTokenSource cancellation = new();
    private readonly ConcurrentDictionary<MarketKey, MarketEntry> items = new();
    private readonly ConcurrentDictionary<MarketKey, AggregatedEntry> aggregated = new();
    private readonly ConcurrentDictionary<MarketKey, byte> aggregatedInFlight = new();
    private readonly ConcurrentDictionary<(MarketKey, MarketHistoryWindow), MarketHistoryEntry> histories = new();
    private readonly ConcurrentDictionary<string, TaxEntry> taxRates = new();
    private readonly ConcurrentDictionary<string, byte> taxInFlight = new();

    public MarketboardService(HttpService http)
    {
        this.http = http;
        throttle = new RequestThrottle(4, TimeSpan.FromMilliseconds(100));
    }

    public MarketEntry RequestItem(uint itemId, MarketScope scope, bool forceRefresh)
    {
        if (!scope.IsValid)
        {
            return Invalid;
        }

        var key = MarketKey.Of(itemId, scope);
        var entry = items.GetOrAdd(key, static _ => new MarketEntry());
        if (entry.State == MarketState.Loading)
        {
            return entry;
        }

        var stale = entry.State == MarketState.Idle || DateTime.UtcNow - entry.FetchedUtc >= FreshFor;
        if (forceRefresh || stale)
        {
            entry.State = MarketState.Loading;
            _ = LoadItemAsync(key, scope, entry);
        }

        return entry;
    }

    public MarketHistoryEntry RequestHistory(uint itemId, MarketScope scope)
    {
        if (!scope.IsValid)
        {
            return InvalidHistory;
        }

        var key = (MarketKey.Of(itemId, scope), MarketHistoryWindow.Month);
        var entry = histories.GetOrAdd(key, static _ => new MarketHistoryEntry());
        if (entry.State == MarketState.Loading || !IsHistoryStale(entry))
        {
            return entry;
        }

        entry.State = MarketState.Loading;
        _ = LoadMonthHistoryAsync(itemId, scope, entry);
        return entry;
    }

    public MarketHistory? WatchHistory(uint itemId, MarketScope scope)
    {
        if (!scope.IsValid)
        {
            return null;
        }

        var key = MarketKey.Of(itemId, scope);
        if (histories.TryGetValue((key, MarketHistoryWindow.Week), out var week) && week.History is { } weekly)
        {
            return weekly;
        }

        return histories.TryGetValue((key, MarketHistoryWindow.Month), out var month) ? month.History : null;
    }

    public bool HistoryLoading(uint itemId, MarketScope scope) =>
        scope.IsValid &&
        histories.TryGetValue((MarketKey.Of(itemId, scope), MarketHistoryWindow.Week), out var entry) &&
        entry.State is MarketState.Loading or MarketState.Idle;

    public void PrefetchWatchHistory(IReadOnlyList<uint> ids, MarketScope scope)
    {
        if (!scope.IsValid || ids.Count == 0)
        {
            return;
        }

        List<uint>? pending = null;
        List<MarketHistoryEntry>? pendingEntries = null;
        for (var index = 0; index < ids.Count; index++)
        {
            var entry = histories.GetOrAdd((MarketKey.Of(ids[index], scope), MarketHistoryWindow.Week),
                static _ => new MarketHistoryEntry());
            if (entry.State == MarketState.Loading || !IsHistoryStale(entry))
            {
                continue;
            }

            entry.State = MarketState.Loading;
            pending ??= new List<uint>();
            pendingEntries ??= new List<MarketHistoryEntry>();
            pending.Add(ids[index]);
            pendingEntries.Add(entry);
            if (pending.Count >= WatchHistoryBatch)
            {
                break;
            }
        }

        if (pending is null || pendingEntries is null)
        {
            return;
        }

        _ = LoadWatchHistoryAsync(pending, pendingEntries, scope);
    }

    public long AggregatedMin(uint itemId, MarketScope scope)
    {
        if (!scope.IsValid)
        {
            return 0;
        }

        return aggregated.TryGetValue(MarketKey.Of(itemId, scope), out var entry) ? entry.Price : 0;
    }

    public bool TryGetAggregated(uint itemId, MarketScope scope, out long price)
    {
        price = 0;
        if (!scope.IsValid || !aggregated.TryGetValue(MarketKey.Of(itemId, scope), out var entry))
        {
            return false;
        }

        price = entry.Price;
        return true;
    }

    public bool TryGetAggregated(uint itemId, MarketScope scope, bool hq, out long price)
    {
        price = 0;
        if (!scope.IsValid || !aggregated.TryGetValue(MarketKey.Of(itemId, scope), out var entry))
        {
            return false;
        }

        price = hq ? entry.HqPrice : entry.NqPrice;
        return true;
    }

    public bool TryGetLowestTax(string worldName, out int rate, out string city)
    {
        rate = 0;
        city = string.Empty;
        if (worldName.Length == 0)
        {
            return false;
        }

        if (taxRates.TryGetValue(worldName, out var entry) && DateTime.UtcNow - entry.FetchedUtc < TaxFreshFor)
        {
            rate = entry.Rate;
            city = entry.City;
            return entry.Rate > 0;
        }

        if (taxInFlight.TryAdd(worldName, 0))
        {
            _ = LoadTaxRatesAsync(worldName);
        }

        return false;
    }

    private static bool IsHistoryStale(MarketHistoryEntry entry)
    {
        if (entry.State == MarketState.Idle)
        {
            return true;
        }

        var age = DateTime.UtcNow - entry.FetchedUtc;
        return entry.State == MarketState.Failed ? age >= HistoryRetryAfter : age >= HistoryFreshFor;
    }

    private async Task LoadTaxRatesAsync(string worldName)
    {
        try
        {
            var token = cancellation.Token;
            using (await throttle.EnterAsync(token).ConfigureAwait(false))
            {
                var url = $"{ApiRoot}/tax-rates?world={Uri.EscapeDataString(worldName)}";
                var rates = await http
                    .GetJsonAsync(url, UniversalisJsonContext.Default.DictionaryStringInt32, null, token)
                    .ConfigureAwait(false);
                var lowestRate = 0;
                var lowestCity = string.Empty;
                if (rates is not null)
                {
                    foreach (var pair in rates)
                    {
                        if (pair.Value <= 0 || (lowestRate > 0 && pair.Value >= lowestRate))
                        {
                            continue;
                        }

                        lowestRate = pair.Value;
                        lowestCity = pair.Key;
                    }
                }

                taxRates[worldName] = new TaxEntry(lowestRate, lowestCity, DateTime.UtcNow);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            taxRates[worldName] = new TaxEntry(0, string.Empty, DateTime.UtcNow);
            AepLog.Warning(exception, $"Market tax fetch failed for {worldName}");
        }
        finally
        {
            taxInFlight.TryRemove(worldName, out _);
        }
    }

    public bool TryFindCheaperScope(uint itemId, MarketScope scope, bool hq, long activePrice, out long price,
        out uint worldId)
    {
        price = 0;
        worldId = 0;
        if (!scope.IsValid || activePrice <= 0 || scope.Kind == MarketScopeKind.Region)
        {
            return false;
        }

        if (!aggregated.TryGetValue(MarketKey.Of(itemId, scope), out var entry))
        {
            return false;
        }

        var dataCenter = hq ? entry.DataCenterHq : entry.DataCenterNq;
        var region = hq ? entry.RegionHq : entry.RegionNq;
        if (scope.Kind == MarketScopeKind.World && dataCenter.Price > 0 && dataCenter.Price < activePrice)
        {
            price = dataCenter.Price;
            worldId = dataCenter.WorldId;
        }

        if (region.Price > 0 && region.Price < activePrice && (price == 0 || region.Price < price))
        {
            price = region.Price;
            worldId = region.WorldId;
        }

        return price > 0 && worldId != 0;
    }

    public void PrefetchAggregated(IReadOnlyList<uint> ids, MarketScope scope)
    {
        if (!scope.IsValid || ids.Count == 0)
        {
            return;
        }

        List<uint>? pending = null;
        var now = DateTime.UtcNow;
        for (var index = 0; index < ids.Count; index++)
        {
            var key = MarketKey.Of(ids[index], scope);
            if (aggregated.TryGetValue(key, out var existing) && now - existing.FetchedUtc < AggregatedFreshFor)
            {
                continue;
            }

            if (!aggregatedInFlight.TryAdd(key, 0))
            {
                continue;
            }

            pending ??= new List<uint>();
            pending.Add(ids[index]);
            if (pending.Count >= AggregatedBatch)
            {
                break;
            }
        }

        if (pending is null)
        {
            return;
        }

        _ = LoadAggregatedAsync(pending, scope);
    }

    public async Task<MarketSnapshot?> FetchAsync(uint itemId, MarketScope scope, CancellationToken token)
    {
        if (!scope.IsValid)
        {
            return null;
        }

        using (await throttle.EnterAsync(token).ConfigureAwait(false))
        {
            var url = $"{ApiRoot}/{Uri.EscapeDataString(scope.ApiName)}/{itemId}?listings=1&entries=1";
            var data = await http.GetJsonAsync(url, UniversalisJsonContext.Default.UniversalisCurrentData, null, token)
                .ConfigureAwait(false);
            return data is null ? null : BuildSnapshot(itemId, scope, data);
        }
    }

    private async Task LoadItemAsync(MarketKey key, MarketScope scope, MarketEntry entry)
    {
        try
        {
            var token = cancellation.Token;
            using (await throttle.EnterAsync(token).ConfigureAwait(false))
            {
                var url =
                    $"{ApiRoot}/{Uri.EscapeDataString(scope.ApiName)}/{key.ItemId}?listings={ListingFetchCount}&entries={HistoryCount}";
                var data = await http
                    .GetJsonAsync(url, UniversalisJsonContext.Default.UniversalisCurrentData, null, token)
                    .ConfigureAwait(false);
                if (data is null)
                {
                    entry.FetchedUtc = DateTime.UtcNow;
                    entry.State = MarketState.Failed;
                    return;
                }

                var snapshot = BuildSnapshot(key.ItemId, scope, data);
                entry.Snapshot = snapshot;
                entry.FetchedUtc = DateTime.UtcNow;
                entry.State = snapshot.Listings.Length == 0 && snapshot.Sales.Length == 0
                    ? MarketState.Empty
                    : MarketState.Ready;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            entry.FetchedUtc = DateTime.UtcNow;
            entry.State = MarketState.Failed;
            AepLog.Warning(exception, $"Market fetch failed for {key.ItemId} on {key.Api}");
        }
    }

    private async Task LoadMonthHistoryAsync(uint itemId, MarketScope scope, MarketHistoryEntry entry)
    {
        try
        {
            var token = cancellation.Token;
            using (await throttle.EnterAsync(token).ConfigureAwait(false))
            {
                var seconds = MarketTrend.Seconds(MarketRange.Month);
                var url =
                    $"{ApiRoot}/history/{Uri.EscapeDataString(scope.ApiName)}/{itemId}?entriesToReturn={MonthHistoryEntries}&entriesWithin={seconds}";
                var data = await http.GetJsonAsync(url, UniversalisJsonContext.Default.UniversalisHistory, null, token)
                    .ConfigureAwait(false);
                Complete(entry, data is null ? null : MarketHistory.From(itemId, data));
            }
        }
        catch (OperationCanceledException)
        {
            entry.State = MarketState.Idle;
        }
        catch (Exception exception)
        {
            Complete(entry, null);
            AepLog.Warning(exception, $"Market history fetch failed for {itemId} on {scope.ApiName}");
        }
    }

    private async Task LoadWatchHistoryAsync(List<uint> ids, List<MarketHistoryEntry> entries, MarketScope scope)
    {
        try
        {
            var token = cancellation.Token;
            using (await throttle.EnterAsync(token).ConfigureAwait(false))
            {
                var url =
                    $"{ApiRoot}/history/{Uri.EscapeDataString(scope.ApiName)}/{string.Join(',', ids)}?entriesToReturn={WatchHistoryEntries}&entriesWithin={MarketTrend.WatchSeconds}";
                if (ids.Count == 1)
                {
                    var single = await http
                        .GetJsonAsync(url, UniversalisJsonContext.Default.UniversalisHistory, null, token)
                        .ConfigureAwait(false);
                    Complete(entries[0], single is null ? null : MarketHistory.From(ids[0], single));
                    return;
                }

                var batch = await http
                    .GetJsonAsync(url, UniversalisJsonContext.Default.UniversalisHistoryBatch, null, token)
                    .ConfigureAwait(false);
                var results = batch?.Items;
                for (var index = 0; index < ids.Count; index++)
                {
                    if (results is null)
                    {
                        Complete(entries[index], null);
                        continue;
                    }

                    results.TryGetValue(ids[index].ToString(System.Globalization.CultureInfo.InvariantCulture),
                        out var history);
                    Complete(entries[index], MarketHistory.From(ids[index], history));
                }
            }
        }
        catch (OperationCanceledException)
        {
            for (var index = 0; index < entries.Count; index++)
            {
                entries[index].State = MarketState.Idle;
            }
        }
        catch (Exception exception)
        {
            for (var index = 0; index < entries.Count; index++)
            {
                Complete(entries[index], null);
            }

            AepLog.Warning(exception, $"Market watch history fetch failed on {scope.ApiName}");
        }
    }

    private static void Complete(MarketHistoryEntry entry, MarketHistory? history)
    {
        entry.FetchedUtc = DateTime.UtcNow;
        if (history is null)
        {
            entry.State = MarketState.Failed;
            return;
        }

        entry.History = history;
        entry.State = history.Trades.Length == 0 ? MarketState.Empty : MarketState.Ready;
    }

    private async Task LoadAggregatedAsync(List<uint> ids, MarketScope scope)
    {
        var keys = new MarketKey[ids.Count];
        for (var index = 0; index < ids.Count; index++)
        {
            keys[index] = MarketKey.Of(ids[index], scope);
        }

        var startedUtc = DateTime.UtcNow;
        try
        {
            var token = cancellation.Token;
            using (await throttle.EnterAsync(token).ConfigureAwait(false))
            {
                var url = $"{ApiRoot}/aggregated/{Uri.EscapeDataString(scope.ApiName)}/{string.Join(',', ids)}";
                var response = await http
                    .GetJsonAsync(url, UniversalisJsonContext.Default.UniversalisAggregatedResponse, null, token)
                    .ConfigureAwait(false);
                var now = DateTime.UtcNow;
                var results = response?.Results;
                if (results is not null)
                {
                    for (var index = 0; index < results.Length; index++)
                    {
                        var result = results[index];
                        var price = SelectAggregatedScope(result, scope.Kind).Price;
                        var nqPrice = SelectAggregatedField(result.Nq?.MinListing, scope.Kind).Price;
                        var hqPrice = SelectAggregatedField(result.Hq?.MinListing, scope.Kind).Price;
                        aggregated[MarketKey.Of(result.ItemId, scope)] = new AggregatedEntry(price, nqPrice, hqPrice,
                            SelectAggregatedField(result.Nq?.MinListing, MarketScopeKind.DataCenter),
                            SelectAggregatedField(result.Hq?.MinListing, MarketScopeKind.DataCenter),
                            SelectAggregatedField(result.Nq?.MinListing, MarketScopeKind.Region),
                            SelectAggregatedField(result.Hq?.MinListing, MarketScopeKind.Region), now);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "Market aggregated fetch failed");
        }
        finally
        {
            StampUnanswered(keys, startedUtc);
            for (var index = 0; index < keys.Length; index++)
            {
                aggregatedInFlight.TryRemove(keys[index], out _);
            }
        }
    }

    private void StampUnanswered(MarketKey[] keys, DateTime startedUtc)
    {
        var now = DateTime.UtcNow;
        for (var index = 0; index < keys.Length; index++)
        {
            var key = keys[index];
            if (!aggregated.TryGetValue(key, out var existing))
            {
                aggregated.TryAdd(key, new AggregatedEntry(0, 0, 0, default, default, default, default, now));
                continue;
            }

            if (existing.FetchedUtc < startedUtc)
            {
                aggregated[key] = existing.FetchedAt(now);
            }
        }
    }

    private static MarketSnapshot BuildSnapshot(uint itemId, MarketScope scope, UniversalisCurrentData data)
    {
        var all = DedupeListings(data.Listings, out var unitsForSale);
        var hasHq = false;
        for (var index = 0; index < all.Length; index++)
        {
            hasHq |= all[index].Hq;
        }

        var offers = new List<MarketWorldOffer>();
        var worldsNq = Array.Empty<MarketWorldOffer>();
        var worldsHq = Array.Empty<MarketWorldOffer>();
        if (scope.IsMultiWorld)
        {
            MarketTrend.GroupWorlds(all, false, offers);
            worldsNq = offers.ToArray();
            MarketTrend.GroupWorlds(all, true, offers);
            worldsHq = offers.ToArray();
        }

        var listings = all;
        if (listings.Length > ListingCount)
        {
            listings = all.AsSpan(0, ListingCount).ToArray();
        }

        var rawSales = data.RecentHistory ?? Array.Empty<UniversalisSale>();
        var sales = new MarketSale[rawSales.Length];
        for (var index = 0; index < rawSales.Length; index++)
        {
            var sale = rawSales[index];
            hasHq |= sale.Hq;
            sales[index] = new MarketSale(sale.PricePerUnit, sale.Quantity, sale.Hq,
                MarketFormat.FromUnix(sale.Timestamp), sale.WorldName ?? string.Empty, sale.BuyerName ?? string.Empty);
        }

        hasHq |= data.MinPriceHq > 0 || data.MaxPriceHq > 0 || data.HqSaleVelocity > 0;
        return new MarketSnapshot(itemId, MarketFormat.FromUnix(data.LastUploadTime), scope.IsMultiWorld, hasHq,
            listings, sales, worldsNq, worldsHq, data.MinPriceNq, data.MinPriceHq, data.AveragePriceNq,
            data.AveragePriceHq, data.MaxPriceNq, data.MaxPriceHq, data.NqSaleVelocity, data.HqSaleVelocity,
            unitsForSale, data.UnitsSold);
    }

    private static MarketListing[] DedupeListings(UniversalisListing[]? rawListings, out int unitsForSale)
    {
        unitsForSale = 0;
        if (rawListings is null || rawListings.Length == 0)
        {
            return Array.Empty<MarketListing>();
        }

        var listings = new MarketListing[rawListings.Length];
        var seen = new HashSet<string>(rawListings.Length, StringComparer.Ordinal);
        var kept = 0;
        for (var index = 0; index < rawListings.Length; index++)
        {
            var listing = rawListings[index];
            var listingId = listing.ListingId;
            if (listingId is { Length: > 0 } && !seen.Add(listingId))
            {
                continue;
            }

            unitsForSale += listing.Quantity;
            listings[kept] = new MarketListing(listing.PricePerUnit, listing.Quantity, listing.Total, listing.Hq,
                listing.WorldId, listing.WorldName ?? string.Empty, listing.RetainerName ?? string.Empty);
            kept++;
        }

        if (kept != listings.Length)
        {
            Array.Resize(ref listings, kept);
        }

        return listings;
    }

    private static ScopeOffer SelectAggregatedScope(UniversalisAggregatedResult result, MarketScopeKind kind)
    {
        var nq = SelectAggregatedField(result.Nq?.MinListing, kind);
        var hq = SelectAggregatedField(result.Hq?.MinListing, kind);
        if (nq.Price > 0 && hq.Price > 0)
        {
            return nq.Price <= hq.Price ? nq : hq;
        }

        return nq.Price > 0 ? nq : hq;
    }

    private static ScopeOffer SelectAggregatedField(UniversalisAggregatedField? field, MarketScopeKind kind)
    {
        if (field is null)
        {
            return default;
        }

        var value = kind switch
        {
            MarketScopeKind.World => field.World,
            MarketScopeKind.DataCenter => field.Dc,
            _ => field.Region,
        };
        return value is null ? default : new ScopeOffer(value.Price, value.WorldId);
    }

    private readonly struct TaxEntry
    {
        public readonly int Rate;
        public readonly string City;
        public readonly DateTime FetchedUtc;

        public TaxEntry(int rate, string city, DateTime fetchedUtc)
        {
            Rate = rate;
            City = city;
            FetchedUtc = fetchedUtc;
        }
    }

    private readonly struct ScopeOffer
    {
        public readonly long Price;
        public readonly uint WorldId;

        public ScopeOffer(long price, uint worldId)
        {
            Price = price;
            WorldId = worldId;
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        throttle.Dispose();
        cancellation.Dispose();
    }

    private readonly struct AggregatedEntry
    {
        public readonly long Price;
        public readonly long NqPrice;
        public readonly long HqPrice;
        public readonly ScopeOffer DataCenterNq;
        public readonly ScopeOffer DataCenterHq;
        public readonly ScopeOffer RegionNq;
        public readonly ScopeOffer RegionHq;
        public readonly DateTime FetchedUtc;

        public AggregatedEntry(long price, long nqPrice, long hqPrice, ScopeOffer dataCenterNq,
            ScopeOffer dataCenterHq, ScopeOffer regionNq, ScopeOffer regionHq, DateTime fetchedUtc)
        {
            Price = price;
            NqPrice = nqPrice;
            HqPrice = hqPrice;
            DataCenterNq = dataCenterNq;
            DataCenterHq = dataCenterHq;
            RegionNq = regionNq;
            RegionHq = regionHq;
            FetchedUtc = fetchedUtc;
        }

        public AggregatedEntry FetchedAt(DateTime fetchedUtc) =>
            new(Price, NqPrice, HqPrice, DataCenterNq, DataCenterHq, RegionNq, RegionHq, fetchedUtc);
    }
}
