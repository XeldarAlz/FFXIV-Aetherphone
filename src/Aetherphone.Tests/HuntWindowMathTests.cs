using Aetherphone.Core.Hunts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HuntWindowMathTests
{
    private static HuntMobDefinition CreateMob(double normalMin, double? normalCap, double? maintenanceMin = null)
    {
        return new HuntMobDefinition
        {
            Id = "test_mob",
            Windows = new[]
            {
                new HuntMobWindowDef
                {
                    Timing = new HuntMobTiming
                    {
                        Normal = new HuntMobTimingWindow { Min = normalMin, Cap = normalCap },
                        Maintenance = maintenanceMin is { } min ? new HuntMobTimingWindow { Min = min } : null,
                    },
                },
            },
        };
    }

    private static HuntWindowDto CreateWindow(DateTimeOffset startedAt, int? snipedNum) => new()
    {
        Num = 1,
        StartedAtNormal = startedAt,
        SnipedNum = snipedNum,
        MobId = "test_mob",
        WorldId = "adamantoise",
    };

    [Fact]
    public void SnipedWindowOpensAfterOrdinaryNormalMinRegardlessOfMaintenanceTiming()
    {
        var mob = CreateMob(normalMin: 6d, normalCap: 10d, maintenanceMin: 0d);
        var startedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var window = CreateWindow(startedAt, snipedNum: 1);

        Assert.Equal(startedAt + TimeSpan.FromHours(6d), HuntWindowMath.MinimumReachedAt(window, mob));
    }

    [Fact]
    public void SnipedWindowCapStretchesByOneExtraNormalSpanPerSnipe()
    {
        var mob = CreateMob(normalMin: 6d, normalCap: 10d);
        var startedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var window = CreateWindow(startedAt, snipedNum: 1);

        var atOrdinaryCap = startedAt + TimeSpan.FromHours(10d);
        Assert.Equal(50d, HuntWindowMath.Percentage(window, mob, atOrdinaryCap));
        Assert.Equal(HuntWindowStatus.Open, HuntWindowMath.Status(window, mob, atOrdinaryCap));

        var atStretchedCap = startedAt + TimeSpan.FromHours(14d);
        Assert.Equal(HuntWindowStatus.Capped, HuntWindowMath.Status(window, mob, atStretchedCap));
    }

    [Fact]
    public void UnsnipedWindowUsesTheOrdinaryCapUnstretched()
    {
        var mob = CreateMob(normalMin: 6d, normalCap: 10d);
        var startedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var window = CreateWindow(startedAt, snipedNum: null);

        var atOrdinaryCap = startedAt + TimeSpan.FromHours(10d);
        Assert.Equal(HuntWindowStatus.Capped, HuntWindowMath.Status(window, mob, atOrdinaryCap));
    }

    [Fact]
    public void SnipedNumFallsBackToBaseTimingWhenNormalCapIsMissing()
    {
        var mob = CreateMob(normalMin: 6d, normalCap: null);
        var startedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var window = CreateWindow(startedAt, snipedNum: 2);

        Assert.Null(HuntWindowMath.Percentage(window, mob, startedAt + TimeSpan.FromHours(20d)));
        Assert.Equal(startedAt + TimeSpan.FromHours(6d), HuntWindowMath.MinimumReachedAt(window, mob));
    }
}
