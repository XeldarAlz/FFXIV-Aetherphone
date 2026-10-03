using Aetherphone.Core.Hunts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HuntBoardTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static HuntMobDefinition CreateMob(double min, double? cap, double? average = null) => new()
    {
        Id = "test_mob",
        Rank = "S",
        Windows = new[]
        {
            new HuntMobWindowDef
            {
                Timing = new HuntMobTiming
                {
                    Normal = new HuntMobTimingWindow { Min = min, Cap = cap, Avg = average },
                },
            },
        },
    };

    private static HuntWindowDto CreateWindow() => new()
    {
        Num = 1,
        StartedAtNormal = Start,
        MobId = "test_mob",
        WorldId = "cerberus",
    };

    [Fact]
    public void SectionFollowsStatus()
    {
        Assert.Equal(HuntBoardSection.Live, HuntBoard.SectionFor(HuntWindowStatus.Spawned));
        Assert.Equal(HuntBoardSection.Live, HuntBoard.SectionFor(HuntWindowStatus.Scheduled));
        Assert.Equal(HuntBoardSection.Open, HuntBoard.SectionFor(HuntWindowStatus.Open));
        Assert.Equal(HuntBoardSection.Open, HuntBoard.SectionFor(HuntWindowStatus.Capped));
        Assert.Equal(HuntBoardSection.Waiting, HuntBoard.SectionFor(HuntWindowStatus.Unmet));
        Assert.Equal(HuntBoardSection.Soon, HuntBoard.SectionFor(HuntWindowStatus.Closed));
        Assert.Equal(HuntBoardSection.Soon, HuntBoard.SectionFor(HuntWindowStatus.Unknown));
    }

    [Fact]
    public void ClosedRingFillsTowardTheMinimum()
    {
        var mob = CreateMob(min: 40d, cap: 60d);
        var fraction = HuntBoard.RingFraction(HuntWindowStatus.Closed, CreateWindow(), mob, Start.AddHours(10d));

        Assert.Equal(0.25f, fraction, 3);
    }

    [Fact]
    public void OpenRingTracksTheWindowPercentage()
    {
        var mob = CreateMob(min: 40d, cap: 60d);
        var fraction = HuntBoard.RingFraction(HuntWindowStatus.Open, CreateWindow(), mob, Start.AddHours(50d));

        Assert.Equal(0.5f, fraction, 3);
    }

    [Fact]
    public void CappedAndLiveRingsAreFull()
    {
        var mob = CreateMob(min: 40d, cap: 60d);

        Assert.Equal(1f, HuntBoard.RingFraction(HuntWindowStatus.Capped, CreateWindow(), mob, Start));
        Assert.Equal(1f, HuntBoard.RingFraction(HuntWindowStatus.Spawned, CreateWindow(), mob, Start));
    }

    [Fact]
    public void TimelinePlacesTicksOnAnAxisWithHeadroomPastTheCap()
    {
        var mob = CreateMob(min: 40d, cap: 60d, average: 50d);
        var timeline = HuntBoard.TimelineFor(CreateWindow(), mob, Start.AddHours(30d));

        Assert.NotNull(timeline);
        var axis = 60d * HuntBoard.TimelineHeadroom;
        Assert.Equal((float)(40d / axis), timeline.Value.Minimum, 4);
        Assert.Equal((float)(50d / axis), timeline.Value.Average, 4);
        Assert.Equal((float)(60d / axis), timeline.Value.Cap, 4);
        Assert.Equal((float)(30d / axis), timeline.Value.Now, 4);
        Assert.False(timeline.Value.PastEnd);
    }

    [Fact]
    public void TimelineClampsNowOnceTheAxisIsExceeded()
    {
        var mob = CreateMob(min: 40d, cap: 60d);
        var timeline = HuntBoard.TimelineFor(CreateWindow(), mob, Start.AddHours(200d));

        Assert.NotNull(timeline);
        Assert.Equal(1f, timeline.Value.Now);
        Assert.True(timeline.Value.PastEnd);
        Assert.False(timeline.Value.HasAverage);
    }

    [Fact]
    public void TimelineNeedsACap()
    {
        Assert.Null(HuntBoard.TimelineFor(CreateWindow(), CreateMob(min: 40d, cap: null), Start));
    }

    [Fact]
    public void SoonerOpeningsSortFirst()
    {
        var mob = CreateMob(min: 40d, cap: 60d);
        var now = Start.AddHours(10d);
        var later = CreateWindow();
        var sooner = CreateWindow();
        sooner.StartedAtNormal = Start.AddHours(-20d);

        var laterKey = HuntBoard.SortKey(HuntBoardSection.Soon, HuntWindowStatus.Closed, later, mob, now, null, null);
        var soonerKey = HuntBoard.SortKey(HuntBoardSection.Soon, HuntWindowStatus.Closed, sooner, mob, now, null, null);

        Assert.True(soonerKey < laterKey);
    }

    [Fact]
    public void FreshestSpawnSortsFirst()
    {
        var mob = CreateMob(min: 40d, cap: 60d);
        var window = CreateWindow();
        var older = HuntBoard.SortKey(HuntBoardSection.Live, HuntWindowStatus.Spawned, window, mob, Start,
            Start.AddMinutes(-30d), null);
        var newer = HuntBoard.SortKey(HuntBoardSection.Live, HuntWindowStatus.Spawned, window, mob, Start,
            Start.AddMinutes(-5d), null);

        Assert.True(newer < older);
    }
}
