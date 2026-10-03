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
    // Anchor source: CN housing lotteries run on a 9-day cycle since 2022-08-08 15:00 UTC
    // (5 days entry, 4 days results). Independently verified: every EndTime in the CN source
    // lands on this grid (156 rows with EndTime > 0 across 14 CN servers all hit it), with
    // State 1 (entry) rows ending 5 days past a grid point and State 2 (results) rows ending on
    // one, so a grid point is where results end and entry begins.
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

    public async Task<IReadOnlyList<HousingDistrictSnapshot>?> GetWorldAsync(uint worldId, CancellationToken token)
    {
        if (worldId == 0)
        {
            return null;
        }

        if (Kind == HousingProviderKind.China)
        {
            LastProxyCacheAge = null;
            return await GetChinaWorldAsync(worldId, token).ConfigureAwait(false);
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

            LastProxyCacheAge = payload.Proxy?.CacheAgeSeconds;
            var districts = payload.Districts ?? Array.Empty<PaissaDistrictDetail>();
            var snapshots = new List<HousingDistrictSnapshot>(districts.Length);
            for (var index = 0; index < districts.Length; index++)
            {
                var district = districts[index];
                if (!HousingDistricts.TryGet(district.Id, out _))
                {
                    continue;
                }

                snapshots.Add(Map(worldId, district.Id, district, payload.Proxy));
            }

            return snapshots;
        }
    }

    private async Task<IReadOnlyList<HousingDistrictSnapshot>?> GetChinaWorldAsync(uint worldId,
        CancellationToken token)
    {
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

            var now = DateTime.UtcNow;
            var all = HousingDistricts.All;
            var snapshots = new List<HousingDistrictSnapshot>(all.Count);
            for (var districtIndex = 0; districtIndex < all.Count; districtIndex++)
            {
                var districtId = all[districtIndex].Id;
                var area = AreaOf(districtId);
                var plots = new List<HousingPlot>();
                for (var index = 0; index < payload.Length; index++)
                {
                    var entry = payload[index];
                    if (entry.Area == area && TryMapChinaPlot(worldId, districtId, entry, out var plot))
                    {
                        plots.Add(plot);
                    }
                }

                plots.Sort(HousingPlotOrder.ByWardThenPlot);
                snapshots.Add(new HousingDistrictSnapshot
                {
                    WorldId = worldId,
                    DistrictId = districtId,
                    DistrictName = HousingDistricts.Name(districtId),
                    FetchedUtc = now,
                    Source = Kind,
                    Plots = plots,
                });
            }

            return snapshots;
        }
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
        // The CN source's Slot is a 0-based ward index; +1 maps to 1-based ward numbering
        // (same convention as the international API's 0-based ward_number).
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

    internal static (HousingLotteryPhase Phase, DateTime? PhaseEndsUtc) InferChinaLotteryPhase(
        ChinaSalesPlot entry, DateTime nowUtc)
    {
        // EndTime sits on the anchor grid (see ChinaLotteryAnchorUtc) and carries its own phase
        // boundary, so advance it along the same phase-alternation rule instead of going through
        // the FirstSeen grid alignment below.
        if (entry.EndTime > 0 && entry.UpdateTime > 0 && entry.State > 0)
        {
            var end = FromUnixSeconds((long?)entry.EndTime);
            if (end is { } endUtc)
            {
                return AdvanceOnGrid(endUtc, MapPhase(entry.State), nowUtc);
            }
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

        var anchor = AlignToGrid(firstSeen);
        return nowUtc < anchor
            ? (HousingLotteryPhase.Unavailable, anchor)
            : AdvanceOnGrid(anchor, HousingLotteryPhase.Results, nowUtc);
    }

    /// Aligns seed to the first anchor grid point at or after it (9-day cycle, see ChinaLotteryAnchorUtc).
    private static DateTime AlignToGrid(DateTime seed)
    {
        var cycle = TimeSpan.FromSeconds(ChinaLotteryCycleSeconds);
        var anchor = ChinaLotteryAnchorUtc;
        while (anchor > seed + cycle)
        {
            anchor -= cycle;
        }

        while (anchor < seed)
        {
            anchor += cycle;
        }

        return anchor;
    }

    /// Advances from a boundary where endingPhase ends, alternating entry (5 days) and results
    /// (4 days), until the first boundary after nowUtc.
    private static (HousingLotteryPhase Phase, DateTime? PhaseEndsUtc) AdvanceOnGrid(
        DateTime boundary, HousingLotteryPhase endingPhase, DateTime nowUtc)
    {
        var deadline = boundary;
        var phase = endingPhase;
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
