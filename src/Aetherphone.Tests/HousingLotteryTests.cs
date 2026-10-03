using Aetherphone.Core.Housing;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HousingLotteryTests
{
    private const uint World = 40u;
    private static readonly DateTime NowUtc = new(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void EntryProgressIsTheShareOfTheFiveDayWindowAlreadySpent()
    {
        var ends = NowUtc.AddDays(4);

        var progress = HousingLottery.Progress(HousingLotteryPhase.Entry, ends, NowUtc);

        Assert.Equal(0.2f, progress, 3);
    }

    [Fact]
    public void ResultsProgressUsesTheFourDayWindow()
    {
        var ends = NowUtc.AddDays(1);

        var progress = HousingLottery.Progress(HousingLotteryPhase.Results, ends, NowUtc);

        Assert.Equal(0.75f, progress, 3);
    }

    [Fact]
    public void ProgressClampsWhenTheDeadlineIsFurtherThanOnePhase()
    {
        var progress = HousingLottery.Progress(HousingLotteryPhase.Entry, NowUtc.AddDays(9), NowUtc);

        Assert.Equal(0f, progress);
    }

    [Fact]
    public void ProgressIsUnknownWithoutAPhaseLengthOrDeadline()
    {
        Assert.Equal(-1f, HousingLottery.Progress(HousingLotteryPhase.Unavailable, NowUtc.AddDays(1), NowUtc));
        Assert.Equal(-1f, HousingLottery.Progress(HousingLotteryPhase.Entry, null, NowUtc));
    }

    [Fact]
    public void ResolvePrefersTheLotteryCycleOverUnavailablePlots()
    {
        var plots = new[]
        {
            Plot(1, 1, HousingLotteryPhase.Unavailable, NowUtc.AddHours(2)),
            Plot(1, 2, HousingLotteryPhase.Entry, NowUtc.AddDays(3)),
            Plot(2, 3, HousingLotteryPhase.Entry, NowUtc.AddDays(2)),
        };

        var state = HousingLottery.Resolve(plots);

        Assert.Equal(HousingLotteryPhase.Entry, state.Phase);
        Assert.Equal(NowUtc.AddDays(2), state.EndsUtc);
    }

    [Fact]
    public void ResolveFallsBackToOtherPhasesWhenNoCycleIsReported()
    {
        var plots = new[]
        {
            Plot(1, 1, HousingLotteryPhase.Unavailable, NowUtc.AddHours(5)),
            Plot(1, 2, HousingLotteryPhase.Unknown, NowUtc.AddHours(1)),
        };

        var state = HousingLottery.Resolve(plots);

        Assert.Equal(HousingLotteryPhase.Unavailable, state.Phase);
        Assert.True(state.IsKnown);
    }

    [Fact]
    public void ResolveIsUnknownForAnEmptyDistrict()
    {
        Assert.False(HousingLottery.Resolve(Array.Empty<HousingPlot>()).IsKnown);
    }

    [Fact]
    public void PreferKeepsTheSoonerCycleDeadlineAcrossDistricts()
    {
        var later = new HousingLotteryState(HousingLotteryPhase.Entry, NowUtc.AddDays(3));
        var sooner = new HousingLotteryState(HousingLotteryPhase.Entry, NowUtc.AddDays(1));
        var unavailable = new HousingLotteryState(HousingLotteryPhase.Unavailable, NowUtc.AddHours(1));

        Assert.Equal(sooner, HousingLottery.Prefer(later, sooner));
        Assert.Equal(sooner, HousingLottery.Prefer(sooner, unavailable));
        Assert.Equal(sooner, HousingLottery.Prefer(unavailable, sooner));
        Assert.Equal(later, HousingLottery.Prefer(HousingLotteryState.Unknown, later));
    }

    [Fact]
    public void SummarizeCountsSizesWardsAndTheFewestEntriesInTheEntryPhase()
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
    public void SummarizeReportsNoFewestEntriesWhenNoneWereScanned()
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
