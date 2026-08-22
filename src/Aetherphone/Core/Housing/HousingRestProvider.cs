using Aetherphone.Core.Net;

namespace Aetherphone.Core.Housing;

internal sealed class HousingRestProvider
{
    private const int PhaseEntry = 1;
    private const int PhaseResults = 2;
    private const int PhaseUnavailable = 3;
    private const int FlagLottery = 1;
    private const int FlagFreeCompany = 2;
    private const int FlagIndividual = 4;

    private const long ChinaLotteryCycleSeconds = 9 * 24 * 60 * 60;
    private const long ChinaLotteryEntrySeconds = 5 * 24 * 60 * 60;
    private const long ChinaLotteryResultsSeconds = 4 * 24 * 60 * 60;
    private static readonly DateTime ChinaLotteryAnchorUtc = new(2022, 8, 8, 15, 0, 0, DateTimeKind.Utc);
    private const int ChinaRegionTypeFreeCompany = 1;
    private const int ChinaRegionTypePersonal = 2;
    private const int ChinaRegionTypeUnrestricted = 0;
    private const int ChinaPurchaseTypeFirstComeFirstServed = 1;
    private const int ChinaPurchaseTypeLottery = 2;

    private readonly HttpService http;
    private readonly RequestThrottle throttle;
    private readonly Func<string> baseUrl;

    public HousingRestProvider(HttpService http, HousingProviderKind kind, string displayName, Func<string> baseUrl)
    {
        this.http = http;
        this.baseUrl = baseUrl;
        Kind = kind;
        DisplayName = displayName;
        throttle = new RequestThrottle(2, TimeSpan.FromMilliseconds(250));
    }

    public HousingProviderKind Kind { get; }

    public string DisplayName { get; }

    public int LastStatusCode { get; private set; }

    public int? LastProxyCacheAge { get; private set; }

    public string BaseUrl => baseUrl();

    public async Task<IReadOnlyList<HousingWorld>?> GetWorldsAsync(CancellationToken token)
    {
        using (await throttle.EnterAsync(token).ConfigureAwait(false))
        {
            var status = 0;
            var payload = await http.GetJsonAsync(HousingEndpoints.Worlds(BaseUrl),
                HousingJsonContext.Default.PaissaWorldSummaryArray, null, token, code => status = code)
                .ConfigureAwait(false);
            LastStatusCode = status;
            if (payload is null)
            {
                return null;
            }

            var worlds = new List<HousingWorld>(payload.Length);
            for (var index = 0; index < payload.Length; index++)
            {
                var entry = payload[index];
                if (entry.Id == 0 || string.IsNullOrEmpty(entry.Name))
                {
                    continue;
                }

                var dataCenter = entry.DataCenterName ?? string.Empty;
                worlds.Add(new HousingWorld(entry.Id, entry.Name, entry.DataCenterId, dataCenter,
                    HousingRegions.For(dataCenter)));
            }

            return worlds;
        }
    }

    public async Task<HousingDistrictSnapshot?> GetDistrictAsync(uint worldId, uint districtId,
        CancellationToken token)
    {
        if (worldId == 0 || districtId == 0)
        {
            return null;
        }

        if (Kind == HousingProviderKind.China)
        {
            LastProxyCacheAge = null;
            return await GetChinaDistrictAsync(worldId, districtId, token).ConfigureAwait(false);
        }

        using (await throttle.EnterAsync(token).ConfigureAwait(false))
        {
            var status = 0;
            var payload = await http.GetJsonAsync(HousingEndpoints.World(BaseUrl, worldId),
                HousingJsonContext.Default.PaissaWorldDetail, null, token, code => status = code)
                .ConfigureAwait(false);
            LastStatusCode = status;
            if (payload is null)
            {
                return null;
            }

            var district = FindDistrict(payload, districtId);
            LastProxyCacheAge = district?.Proxy?.CacheAgeSeconds ?? payload.Proxy?.CacheAgeSeconds;
            return district is null ? null : Map(worldId, districtId, district, payload.Proxy);
        }
    }

    private async Task<HousingDistrictSnapshot?> GetChinaDistrictAsync(uint worldId, uint districtId,
        CancellationToken token)
    {
        var area = AreaOf(districtId);
        if (area < 0)
        {
            LastStatusCode = 0;
            return null;
        }

        using (await throttle.EnterAsync(token).ConfigureAwait(false))
        {
            var status = 0;
            var payload = await http.GetJsonAsync(HousingEndpoints.ChinaSales(BaseUrl, worldId),
                    HousingJsonContext.Default.ChinaSalesPlotArray, null, token, code => status = code)
                .ConfigureAwait(false);
            LastStatusCode = status;
            if (payload is null)
            {
                return null;
            }

            var plots = new List<HousingPlot>(payload.Length);
            for (var index = 0; index < payload.Length; index++)
            {
                var entry = payload[index];
                if (entry.Area != area)
                {
                    continue;
                }

                if (TryMapChinaPlot(worldId, districtId, entry, out var plot))
                {
                    plots.Add(plot);
                }
            }

            plots.Sort(HousingPlotOrder.ByWardThenPlot);
            return new HousingDistrictSnapshot
            {
                WorldId = worldId,
                DistrictId = districtId,
                DistrictName = HousingDistricts.Name(districtId),
                FetchedUtc = DateTime.UtcNow,
                Source = Kind,
                Plots = plots,
            };
        }
    }

    private static PaissaDistrictDetail? FindDistrict(PaissaWorldDetail payload, uint districtId)
    {
        var districts = payload.Districts ?? Array.Empty<PaissaDistrictDetail>();
        for (var index = 0; index < districts.Length; index++)
        {
            if (districts[index].Id == districtId)
            {
                return districts[index];
            }
        }

        return null;
    }

    private HousingDistrictSnapshot Map(uint worldId, uint districtId, PaissaDistrictDetail payload,
        PaissaProxyInfo? worldProxy)
    {
        var raw = payload.OpenPlots ?? Array.Empty<PaissaOpenPlot>();
        var plots = new List<HousingPlot>(raw.Length);
        for (var index = 0; index < raw.Length; index++)
        {
            if (TryMapPlot(worldId, districtId, raw[index], out var plot))
            {
                plots.Add(plot);
            }
        }

        plots.Sort(HousingPlotOrder.ByWardThenPlot);

        var fetched = DateTime.UtcNow;
        if ((payload.Proxy?.CacheAgeSeconds ?? worldProxy?.CacheAgeSeconds) is { } age && age > 0)
        {
            fetched = fetched.AddSeconds(-age);
        }

        return new HousingDistrictSnapshot
        {
            WorldId = worldId,
            DistrictId = districtId,
            DistrictName = string.IsNullOrEmpty(payload.Name) ? HousingDistricts.Name(districtId) : payload.Name,
            FetchedUtc = fetched,
            Source = Kind,
            Plots = plots,
        };
    }

    private static bool TryMapPlot(uint worldId, uint districtId, PaissaOpenPlot entry, out HousingPlot plot)
    {
        plot = null!;
        var ward = NormalizeWard(entry.WardNumber);
        var plotNumber = NormalizePlot(entry.PlotNumber);
        if (ward <= 0 || plotNumber <= 0)
        {
            return false;
        }

        plot = new HousingPlot
        {
            Key = new HousingPlotKey(worldId, districtId, ward, plotNumber),
            Size = MapSize(entry.Size),
            Price = entry.Price > 0 ? entry.Price : 0L,
            Eligibility = MapEligibility(entry.PurchaseSystem),
            Mode = MapMode(entry.PurchaseSystem),
            Phase = MapPhase(entry.LottoPhase),
            PhaseEndsUtc = FromUnixSeconds(entry.LottoPhaseUntil),
            Entries = entry.LottoEntries,
            LastSeenUtc = FromUnixSeconds(entry.LastUpdatedTime) ?? default,
            FirstSeenUtc = FromUnixSeconds(entry.FirstSeenTime) ?? default,
        };
        return true;
    }

    private static bool TryMapChinaPlot(uint worldId, uint districtId, ChinaSalesPlot entry, out HousingPlot plot)
    {
        plot = null!;
        var ward = entry.Slot + 1;
        var plotNumber = entry.Id;
        if (ward is < 1 or > HousingDistricts.DefaultWards ||
            plotNumber is < 1 or > HousingDistricts.PlotsPerWard)
        {
            return false;
        }

        var lastSeen = FromUnixSeconds((long?)entry.LastSeen) ?? DateTime.UtcNow;
        var (phase, phaseEnds) = InferChinaLotteryPhase(entry, DateTime.UtcNow);
        plot = new HousingPlot
        {
            Key = new HousingPlotKey(worldId, districtId, ward, plotNumber),
            Size = MapSize(entry.Size),
            Price = entry.Price > 0 ? entry.Price : 0L,
            Eligibility = MapChinaEligibility(entry.RegionType),
            Mode = MapChinaMode(entry.PurchaseType),
            Phase = phase,
            PhaseEndsUtc = phaseEnds,
            Entries = null,
            LastSeenUtc = lastSeen,
            FirstSeenUtc = FromUnixSeconds((long?)entry.FirstSeen) ?? lastSeen,
        };
        return true;
    }

    private static (HousingLotteryPhase Phase, DateTime? PhaseEndsUtc) InferChinaLotteryPhase(
        ChinaSalesPlot entry, DateTime nowUtc)
    {
        if (entry.EndTime > 0 && entry.UpdateTime > 0 && entry.State > 0)
        {
            var end = FromUnixSeconds((long?)entry.EndTime);
            if (end is not { } endUtc)
            {
                return (HousingLotteryPhase.Unknown, null);
            }

            if (nowUtc < endUtc)
            {
                return (MapPhase(entry.State), endUtc);
            }

            var phase = MapPhase(entry.State);
            var deadline = endUtc;
            while (nowUtc >= deadline)
            {
                if (phase == HousingLotteryPhase.Entry)
                {
                    deadline = deadline.AddSeconds(ChinaLotteryResultsSeconds);
                    phase = HousingLotteryPhase.Results;
                }
                else
                {
                    deadline = deadline.AddSeconds(ChinaLotteryEntrySeconds);
                    phase = HousingLotteryPhase.Entry;
                }
            }

            return (phase, deadline);
        }

        if (entry.FirstSeen <= 0)
        {
            return (HousingLotteryPhase.Unknown, null);
        }

        var seen = FromUnixSeconds((long?)entry.FirstSeen);
        if (seen is not { } firstSeen)
        {
            return (HousingLotteryPhase.Unknown, null);
        }

        var cycle = TimeSpan.FromSeconds(ChinaLotteryCycleSeconds);
        var entryPeriod = TimeSpan.FromSeconds(ChinaLotteryEntrySeconds);
        var anchor = ChinaLotteryAnchorUtc;
        while (anchor > firstSeen + cycle)
        {
            anchor -= cycle;
        }

        while (anchor < firstSeen)
        {
            anchor += cycle;
        }

        if (nowUtc < anchor)
        {
            return (HousingLotteryPhase.Unavailable, anchor);
        }

        while (nowUtc > anchor + cycle)
        {
            anchor += cycle;
        }

        return nowUtc < anchor + entryPeriod
            ? (HousingLotteryPhase.Entry, anchor + entryPeriod)
            : (HousingLotteryPhase.Results, anchor + cycle);
    }

    private static HousingPurchaseEligibility MapChinaEligibility(int regionType) => regionType switch
    {
        ChinaRegionTypeFreeCompany => HousingPurchaseEligibility.FreeCompany,
        ChinaRegionTypePersonal => HousingPurchaseEligibility.Private,
        ChinaRegionTypeUnrestricted => HousingPurchaseEligibility.Both,
        _ => HousingPurchaseEligibility.Unknown,
    };

    private static HousingPurchaseMode MapChinaMode(int purchaseType) => purchaseType switch
    {
        ChinaPurchaseTypeLottery => HousingPurchaseMode.Lottery,
        ChinaPurchaseTypeFirstComeFirstServed => HousingPurchaseMode.FirstComeFirstServed,
        _ => HousingPurchaseMode.Unknown,
    };

    public static int NormalizeWard(int apiWardNumber) =>
        apiWardNumber is < 0 or >= HousingDistricts.DefaultWards ? 0 : apiWardNumber + 1;

    public static int NormalizePlot(int apiPlotNumber) =>
        apiPlotNumber is < 0 or >= HousingDistricts.PlotsPerWard ? 0 : apiPlotNumber + 1;

    public static HousingPlotSize MapSize(int apiSize) => apiSize switch
    {
        0 => HousingPlotSize.Small,
        1 => HousingPlotSize.Medium,
        2 => HousingPlotSize.Large,
        _ => HousingPlotSize.Unknown,
    };

    public static HousingLotteryPhase MapPhase(int? apiPhase) => apiPhase switch
    {
        PhaseEntry => HousingLotteryPhase.Entry,
        PhaseResults => HousingLotteryPhase.Results,
        PhaseUnavailable => HousingLotteryPhase.Unavailable,
        _ => HousingLotteryPhase.Unknown,
    };

    public static HousingPurchaseMode MapMode(int purchaseSystem)
    {
        if (purchaseSystem <= 0)
        {
            return HousingPurchaseMode.Unknown;
        }

        return (purchaseSystem & FlagLottery) != 0
            ? HousingPurchaseMode.Lottery
            : HousingPurchaseMode.FirstComeFirstServed;
    }

    public static HousingPurchaseEligibility MapEligibility(int purchaseSystem)
    {
        var freeCompany = (purchaseSystem & FlagFreeCompany) != 0;
        var individual = (purchaseSystem & FlagIndividual) != 0;
        if (freeCompany && individual)
        {
            return HousingPurchaseEligibility.Both;
        }

        if (freeCompany)
        {
            return HousingPurchaseEligibility.FreeCompany;
        }

        return individual ? HousingPurchaseEligibility.Private : HousingPurchaseEligibility.Unknown;
    }

    private static DateTime? FromUnixSeconds(double seconds) =>
        seconds <= 0d ? null : DateTimeOffset.FromUnixTimeMilliseconds((long)(seconds * 1000d)).UtcDateTime;

    private static DateTime? FromUnixSeconds(long? seconds) =>
        seconds is null or <= 0L ? null : DateTimeOffset.FromUnixTimeSeconds(seconds.Value).UtcDateTime;

    private static int AreaOf(uint districtId) => districtId switch
    {
        HousingDistricts.MistId => 0,
        HousingDistricts.LavenderBedsId => 1,
        HousingDistricts.GobletId => 2,
        HousingDistricts.ShiroganeId => 3,
        HousingDistricts.EmpyreumId => 4,
        _ => -1,
    };

    public void Dispose() => throttle.Dispose();
}

internal static class HousingPlotOrder
{
    public static readonly Comparison<HousingPlot> ByWardThenPlot = static (first, second) =>
    {
        var ward = first.Key.Ward.CompareTo(second.Key.Ward);
        return ward != 0 ? ward : first.Key.Plot.CompareTo(second.Key.Plot);
    };
}
