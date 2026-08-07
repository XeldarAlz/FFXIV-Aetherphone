using System.Globalization;

namespace Aetherphone.Core.Housing;

internal readonly record struct HousingPlotKey(uint WorldId, uint DistrictId, int Ward, int Plot)
{
    public bool IsValid => WorldId != 0 && DistrictId != 0 && Ward > 0 && Plot > 0;

    public override string ToString() =>
        string.Concat(WorldId.ToString(CultureInfo.InvariantCulture), ":",
            DistrictId.ToString(CultureInfo.InvariantCulture), ":",
            Ward.ToString(CultureInfo.InvariantCulture), ":",
            Plot.ToString(CultureInfo.InvariantCulture));
}

internal sealed record HousingPlot
{
    public required HousingPlotKey Key { get; init; }
    public HousingPlotSize Size { get; init; } = HousingPlotSize.Unknown;

    public long Price { get; init; }

    public HousingPurchaseEligibility Eligibility { get; init; } = HousingPurchaseEligibility.Unknown;
    public HousingPurchaseMode Mode { get; init; } = HousingPurchaseMode.Unknown;
    public HousingLotteryPhase Phase { get; init; } = HousingLotteryPhase.Unknown;

    public DateTime? PhaseEndsUtc { get; init; }

    public int? Entries { get; init; }

    public DateTime LastSeenUtc { get; init; }
    public DateTime FirstSeenUtc { get; init; }

    public bool IsSubdivision => Key.Plot > HousingDistricts.PlotsPerDivision;

    public int Ward => Key.Ward;
    public int Plot => Key.Plot;
}

internal sealed record HousingDistrictSnapshot
{
    public static readonly IReadOnlyList<HousingPlot> NoPlots = Array.Empty<HousingPlot>();

    public required uint WorldId { get; init; }
    public required uint DistrictId { get; init; }
    public string WorldName { get; init; } = string.Empty;
    public string DistrictName { get; init; } = string.Empty;
    public required DateTime FetchedUtc { get; init; }
    public required HousingProviderKind Source { get; init; }
    public IReadOnlyList<HousingPlot> Plots { get; init; } = NoPlots;

    public int OpenPlotCount => Plots.Count;

    public DateTime NewestScanUtc
    {
        get
        {
            var newest = default(DateTime);
            for (var index = 0; index < Plots.Count; index++)
            {
                if (Plots[index].LastSeenUtc > newest)
                {
                    newest = Plots[index].LastSeenUtc;
                }
            }

            return newest == default ? FetchedUtc : newest;
        }
    }
}

internal sealed record HousingWorld(uint Id, string Name, int DataCenterId, string DataCenterName, string RegionName);

internal readonly record struct HousingFreshnessThresholds(int LiveMinutes, int RecentMinutes)
{
    public static readonly HousingFreshnessThresholds Default = new(15, 60);

    public HousingDataFreshness Classify(DateTime scannedUtc, DateTime nowUtc, HousingProviderKind source)
    {
        if (scannedUtc == default)
        {
            return HousingDataFreshness.Unknown;
        }

        var age = nowUtc - scannedUtc;
        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        if (age.TotalMinutes >= RecentMinutes)
        {
            return HousingDataFreshness.Stale;
        }

        if (source == HousingProviderKind.Cache)
        {
            return HousingDataFreshness.Cached;
        }

        return age.TotalMinutes < LiveMinutes ? HousingDataFreshness.Live : HousingDataFreshness.Recent;
    }
}

internal readonly record struct HousingProviderStatus(
    HousingProviderKind Source,
    HousingLoadState State,
    DateTime LastSuccessUtc,
    string? LastError)
{
    public static readonly HousingProviderStatus Idle =
        new(HousingProviderKind.None, HousingLoadState.Idle, default, null);
}
