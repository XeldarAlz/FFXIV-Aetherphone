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

internal sealed class MarketboardService : IDisposable
{
    private const string ApiRoot = "https://universalis.app/api/v2";
    private const int ListingCount = 20;
    private const int HistoryCount = 25;
    private const int AggregatedBatch = 80;
    private static readonly TimeSpan FreshFor = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan AggregatedFreshFor = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan TaxFreshFor = TimeSpan.FromHours(6);
    private static readonly MarketEntry Invalid = new() { State = MarketState.Idle };
    private readonly HttpService http;
    private readonly RequestThrottle throttle;
    private readonly CancellationTokenSource cancellation = new();
    private readonly ConcurrentDictionary<string, MarketEntry> items = new();
    private readonly ConcurrentDictionary<string, AggregatedEntry> aggregated = new();
    private readonly ConcurrentDictionary<string, byte> aggregatedInFlight = new();
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

        var key = $"{itemId}:{scope.Key}";
        var entry = items.GetOrAdd(key, static _ => new MarketEntry());
        if (entry.State == MarketState.Loading)
        {
            return entry;
        }

        var stale = entry.State == MarketState.Idle || DateTime.UtcNow - entry.FetchedUtc >= FreshFor;
        if (forceRefresh || stale)
        {
            entry.State = MarketState.Loading;
            _ = LoadItemAsync(key, itemId, scope, entry);
        }

        return entry;
    }

    public long AggregatedMin(uint itemId, MarketScope scope)
    {
        if (!scope.IsValid)
        {
            return 0;
        }

        return aggregated.TryGetValue($"{itemId}:{scope.Key}", out var entry) ? entry.Price : 0;
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
            AepLog.Warning($"Market tax fetch failed for {worldName}: {exception.Message}");
        }
        finally
        {
            taxInFlight.TryRemove(worldName, out _);
        }
    }

    public bool TryFindCheaperScope(uint itemId, MarketScope scope, long activePrice, out long price,
        out uint worldId)
    {
        price = 0;
        worldId = 0;
        if (!scope.IsValid || activePrice <= 0 || scope.Kind == MarketScopeKind.Region)
        {
            return false;
        }

        if (!aggregated.TryGetValue($"{itemId}:{scope.Key}", out var entry))
        {
            return false;
        }

        if (scope.Kind == MarketScopeKind.World && entry.DataCenterPrice > 0 && entry.DataCenterPrice < activePrice)
        {
            price = entry.DataCenterPrice;
            worldId = entry.DataCenterWorldId;
        }

        if (entry.RegionPrice > 0 && entry.RegionPrice < activePrice &&
            (price == 0 || entry.RegionPrice < price))
        {
            price = entry.RegionPrice;
            worldId = entry.RegionWorldId;
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
            var key = $"{ids[index]}:{scope.Key}";
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

    private async Task LoadItemAsync(string key, uint itemId, MarketScope scope, MarketEntry entry)
    {
        try
        {
            var token = cancellation.Token;
            using (await throttle.EnterAsync(token).ConfigureAwait(false))
            {
                var url =
                    $"{ApiRoot}/{Uri.EscapeDataString(scope.ApiName)}/{itemId}?listings={ListingCount}&entries={HistoryCount}";
                var data = await http
                    .GetJsonAsync(url, UniversalisJsonContext.Default.UniversalisCurrentData, null, token)
                    .ConfigureAwait(false);
                if (data is null)
                {
                    entry.FetchedUtc = DateTime.UtcNow;
                    entry.State = MarketState.Failed;
                    return;
                }

                var snapshot = BuildSnapshot(itemId, scope, data);
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
            AepLog.Warning($"Market fetch failed for {key}: {exception.Message}");
        }
    }

    private async Task LoadAggregatedAsync(List<uint> ids, MarketScope scope)
    {
        var keys = new string[ids.Count];
        for (var index = 0; index < ids.Count; index++)
        {
            keys[index] = $"{ids[index]}:{scope.Key}";
        }

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
                        var price = SelectAggregatedPrice(result, scope.Kind);
                        var dataCenter = SelectAggregatedScope(result, MarketScopeKind.DataCenter);
                        var region = SelectAggregatedScope(result, MarketScopeKind.Region);
                        aggregated[$"{result.ItemId}:{scope.Key}"] = new AggregatedEntry(price,
                            dataCenter.Price, region.Price, dataCenter.WorldId, region.WorldId, now);
                    }
                }

                for (var index = 0; index < keys.Length; index++)
                {
                    aggregated.TryAdd(keys[index], new AggregatedEntry(0, now));
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Market aggregated fetch failed: {exception.Message}");
        }
        finally
        {
            for (var index = 0; index < keys.Length; index++)
            {
                aggregatedInFlight.TryRemove(keys[index], out _);
            }
        }
    }

    private static MarketSnapshot BuildSnapshot(uint itemId, MarketScope scope, UniversalisCurrentData data)
    {
        var rawListings = data.Listings ?? Array.Empty<UniversalisListing>();
        var listings = new MarketListing[rawListings.Length];
        var hasHq = false;
        for (var index = 0; index < rawListings.Length; index++)
        {
            var listing = rawListings[index];
            hasHq |= listing.Hq;
            listings[index] = new MarketListing(listing.PricePerUnit, listing.Quantity, listing.Total, listing.Hq,
                listing.WorldName ?? string.Empty, listing.RetainerName ?? string.Empty);
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
            listings, sales, data.MinPriceNq, data.MinPriceHq, data.AveragePriceNq, data.AveragePriceHq,
            data.MaxPriceNq, data.MaxPriceHq, data.NqSaleVelocity, data.HqSaleVelocity, data.UnitsForSale,
            data.UnitsSold);
    }

    private static long SelectAggregatedPrice(UniversalisAggregatedResult result, MarketScopeKind kind) =>
        SelectAggregatedScope(result, kind).Price;

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
        public readonly long DataCenterPrice;
        public readonly long RegionPrice;
        public readonly uint DataCenterWorldId;
        public readonly uint RegionWorldId;
        public readonly DateTime FetchedUtc;

        public AggregatedEntry(long price, DateTime fetchedUtc)
            : this(price, 0L, 0L, 0u, 0u, fetchedUtc)
        {
        }

        public AggregatedEntry(long price, long dataCenterPrice, long regionPrice, uint dataCenterWorldId,
            uint regionWorldId, DateTime fetchedUtc)
        {
            Price = price;
            DataCenterPrice = dataCenterPrice;
            RegionPrice = regionPrice;
            DataCenterWorldId = dataCenterWorldId;
            RegionWorldId = regionWorldId;
            FetchedUtc = fetchedUtc;
        }
    }
}
