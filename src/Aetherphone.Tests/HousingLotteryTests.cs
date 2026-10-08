using Aetherphone.Core.Housing;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HousingLotteryTests
{
    private const uint World = 40u;
    private static readonly DateTime NowUtc = new(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void EntryProgressCountsFiveDays()
    {
        var ends = NowUtc.AddDays(4);

        var progress = HousingLottery.Progress(HousingLotteryPhase.Entry, ends, NowUtc);

        Assert.Equal(0.2f, progress, 3);
    }

    [Fact]
    public void ResultsProgressCountsFourDays()
    {
        var ends = NowUtc.AddDays(1);

        var progress = HousingLottery.Progress(HousingLotteryPhase.Results, ends, NowUtc);

        Assert.Equal(0.75f, progress, 3);
    }

    [Fact]
    public void ProgressStopsAtZero()
    {
        var progress = HousingLottery.Progress(HousingLotteryPhase.Entry, NowUtc.AddDays(9), NowUtc);

        Assert.Equal(0f, progress);
    }

    [Fact]
    public void ProgressNeedsPhaseAndDeadline()
    {
        Assert.Equal(-1f, HousingLottery.Progress(HousingLotteryPhase.Unavailable, NowUtc.AddDays(1), NowUtc));
        Assert.Equal(-1f, HousingLottery.Progress(HousingLotteryPhase.Entry, null, NowUtc));
    }

    [Fact]
    public void ResolvePicksLotteryOverUnavailable()
    {
        var plots = new[]
        {
            Plot(1, 1, HousingLotteryPhase.Unavailable, NowUtc.AddHours(2)),
            Plot(1, 2, HousingLotteryPhase.Entry, NowUtc.AddDays(3)),
            Plot(2, 3, HousingLotteryPhase.Entry, NowUtc.AddDays(2)),
        };

        var state = HousingLottery.Resolve(plots, NowUtc);

        Assert.Equal(HousingLotteryPhase.Entry, state.Phase);
        Assert.Equal(NowUtc.AddDays(2), state.EndsUtc);
    }

    [Fact]
    public void ResolveFallsBackWithoutLottery()
    {
        var plots = new[]
        {
            Plot(1, 1, HousingLotteryPhase.Unavailable, NowUtc.AddHours(5)),
            Plot(1, 2, HousingLotteryPhase.Unknown, NowUtc.AddHours(1)),
        };

        var state = HousingLottery.Resolve(plots, NowUtc);

        Assert.Equal(HousingLotteryPhase.Unavailable, state.Phase);
        Assert.True(state.IsKnown);
    }

    [Fact]
    public void EmptyDistrictIsUnknown()
    {
        Assert.False(HousingLottery.Resolve(Array.Empty<HousingPlot>(), NowUtc).IsKnown);
    }

    [Fact]
    public void PreferSoonerDeadline()
    {
        var later = new HousingLotteryState(HousingLotteryPhase.Entry, NowUtc.AddDays(3));
        var sooner = new HousingLotteryState(HousingLotteryPhase.Entry, NowUtc.AddDays(1));
        var unavailable = new HousingLotteryState(HousingLotteryPhase.Unavailable, NowUtc.AddHours(1));

        Assert.Equal(sooner, HousingLottery.Prefer(later, sooner, NowUtc));
        Assert.Equal(sooner, HousingLottery.Prefer(sooner, unavailable, NowUtc));
        Assert.Equal(sooner, HousingLottery.Prefer(unavailable, sooner, NowUtc));
        Assert.Equal(later, HousingLottery.Prefer(HousingLotteryState.Unknown, later, NowUtc));
    }

    [Fact]
    public void ResolveSkipsEndedPhases()
    {
        var plots = new[]
        {
            Plot(1, 50, HousingLotteryPhase.Results, NowUtc.AddDays(-20)),
            Plot(7, 44, HousingLotteryPhase.Entry, NowUtc.AddDays(-6)),
            Plot(7, 13, HousingLotteryPhase.Entry, NowUtc.AddDays(3)),
        };

        var state = HousingLottery.Resolve(plots, NowUtc);

        Assert.Equal(HousingLotteryPhase.Entry, state.Phase);
        Assert.Equal(NowUtc.AddDays(3), state.EndsUtc);
    }

    [Fact]
    public void ResolveFallsBackToLatestEnded()
    {
        var plots = new[]
        {
            Plot(1, 50, HousingLotteryPhase.Results, NowUtc.AddDays(-20)),
            Plot(7, 13, HousingLotteryPhase.Entry, NowUtc.AddMinutes(-2)),
        };

        var state = HousingLottery.Resolve(plots, NowUtc);

        Assert.Equal(HousingLotteryPhase.Entry, state.Phase);
        Assert.Equal(NowUtc.AddMinutes(-2), state.EndsUtc);
    }

    [Fact]
    public void PreferRunningOverEnded()
    {
        var ended = new HousingLotteryState(HousingLotteryPhase.Results, NowUtc.AddDays(-20));
        var running = new HousingLotteryState(HousingLotteryPhase.Entry, NowUtc.AddDays(3));

        Assert.Equal(running, HousingLottery.Prefer(ended, running, NowUtc));
        Assert.Equal(running, HousingLottery.Prefer(running, ended, NowUtc));
    }

    [Fact]
    public void SummarizeCountsPlots()
    {
        var plots = new[]
        {
            Plot(1, 1, HousingLotteryPhase.Entry, NowUtc, HousingPlotSize.Small, 7),
            Plot(1, 2, HousingLotteryPhase.Entry, NowUtc, HousingPlotSize.Medium, 3),
            Plot(4, 5, HousingLotteryPhase.Results, NowUtc, HousingPlotSize.Large, 0),
            Plot(9, 30, HousingLotteryPhase.Entry, NowUtc, HousingPlotSize.Small, null),
        };

        var stats = HousingLottery.Summarize(plots);

        Assert.Equal(4, stats.Open);
        Assert.Equal(2, stats.Small);
        Assert.Equal(1, stats.Medium);
        Assert.Equal(1, stats.Large);
        Assert.Equal(3, stats.Wards);
        Assert.Equal(3, stats.Lottery);
        Assert.Equal(3, stats.FewestEntries);
    }

    [Fact]
    public void SummarizeWithoutEntries()
    {
        var stats = HousingLottery.Summarize(new[]
        {
            Plot(1, 1, HousingLotteryPhase.Entry, NowUtc, HousingPlotSize.Small, null),
        });

        Assert.False(stats.HasFewestEntries);
    }

    private static HousingPlot Plot(int ward, int plot, HousingLotteryPhase phase, DateTime ends,
        HousingPlotSize size = HousingPlotSize.Small, int? entries = null) => new()
    {
        Key = new HousingPlotKey(World, HousingDistricts.MistId, ward, plot),
        Phase = phase,
        PhaseEndsUtc = ends,
        Size = size,
        Entries = entries,
    };
}
