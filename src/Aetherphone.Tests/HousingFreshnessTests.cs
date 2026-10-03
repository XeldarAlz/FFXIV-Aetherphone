using Aetherphone.Core.Housing;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HousingFreshnessTests
{
    private static readonly DateTime NowUtc = new(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);
    private static readonly HousingFreshnessThresholds Thresholds = HousingFreshnessThresholds.Default;

    [Fact]
    public void AWardWalkedThreeHoursAgoReadsRecentNotStale()
    {
        var walked = NowUtc.AddHours(-3);

        Assert.Equal(HousingDataFreshness.Stale, Thresholds.Classify(walked, NowUtc, HousingProviderKind.Service));
        Assert.Equal(HousingDataFreshness.Recent,
            Thresholds.ClassifyScan(walked, NowUtc, HousingProviderKind.Service));
    }

    [Fact]
    public void ScanThresholdsAreFourTimesTheRefreshThresholds()
    {
        var liveEdge = NowUtc.AddMinutes(-Thresholds.LiveMinutes * HousingDefaults.ScanThresholdMultiple);
        var recentEdge = NowUtc.AddMinutes(-Thresholds.RecentMinutes * HousingDefaults.ScanThresholdMultiple);

        Assert.Equal(4, HousingDefaults.ScanThresholdMultiple);
        Assert.Equal(HousingDataFreshness.Live,
            Thresholds.ClassifyScan(liveEdge.AddSeconds(1), NowUtc, HousingProviderKind.Service));
        Assert.Equal(HousingDataFreshness.Recent,
            Thresholds.ClassifyScan(liveEdge, NowUtc, HousingProviderKind.Service));
        Assert.Equal(HousingDataFreshness.Recent,
            Thresholds.ClassifyScan(recentEdge.AddSeconds(1), NowUtc, HousingProviderKind.Service));
        Assert.Equal(HousingDataFreshness.Stale,
            Thresholds.ClassifyScan(recentEdge, NowUtc, HousingProviderKind.Service));
    }

    [Fact]
    public void AWardWalkedDaysAgoStillReadsStale()
    {
        var walked = NowUtc.AddDays(-4);

        Assert.Equal(HousingDataFreshness.Stale,
            Thresholds.ClassifyScan(walked, NowUtc, HousingProviderKind.Service));
    }

    [Fact]
    public void ScanRuleFollowsTheConfiguredThresholds()
    {
        var thresholds = new HousingFreshnessThresholds(30, 90);
        var walked = NowUtc.AddMinutes(-100);

        Assert.Equal(HousingDataFreshness.Live,
            thresholds.ClassifyScan(walked, NowUtc, HousingProviderKind.Service));
        Assert.Equal(HousingDataFreshness.Recent,
            Thresholds.ClassifyScan(walked, NowUtc, HousingProviderKind.Service));
    }

    [Fact]
    public void CachedScansWithinTheWindowReadCached()
    {
        var walked = NowUtc.AddMinutes(-30);

        Assert.Equal(HousingDataFreshness.Cached,
            Thresholds.ClassifyScan(walked, NowUtc, HousingProviderKind.Cache));
    }

    [Fact]
    public void AnUnknownScanTimeStaysUnknown()
    {
        Assert.Equal(HousingDataFreshness.Unknown,
            Thresholds.ClassifyScan(default, NowUtc, HousingProviderKind.Service));
    }

    [Fact]
    public void FreshOnlyFilterKeepsPlotsFromRecentlyWalkedWards()
    {
        var filters = new HousingFilterState { FreshOnly = true };
        var plot = new HousingPlot
        {
            Key = new HousingPlotKey(40u, HousingDistricts.MistId, 3, 12),
            Size = HousingPlotSize.Medium,
            LastSeenUtc = NowUtc.AddHours(-3),
        };

        Assert.True(filters.Matches(plot, NowUtc, Thresholds, false));
    }
}
