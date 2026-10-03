using Aetherphone.Core.Game;
using Aetherphone.Core.Shell;
using Aetherphone.Core.Timers;
using Xunit;

namespace Aetherphone.Tests;

public sealed class GameTimersTests
{
    private const long Now = 1_800_000_000L;
    private const ulong Main = 11UL;
    private const ulong Alt = 22UL;

    private static readonly DateTime Thursday = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("NA", 2026, 10, 4, 2)]
    [InlineData("EU", 2026, 10, 3, 19)]
    [InlineData("JP", 2026, 10, 3, 12)]
    [InlineData("CN", 2026, 10, 3, 12)]
    [InlineData("OCE", 2026, 10, 3, 9)]
    [InlineData("", 2026, 10, 4, 2)]
    public void JumboCactpotDrawsAtTheRegionsTime(string region, int year, int month, int day, int hour)
    {
        var expected = new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Utc);
        Assert.Equal(expected, GameSchedule.NextJumboCactpot(Thursday, region));
    }

    [Fact]
    public void JumboCactpotRollsToNextWeekOnceTheDrawingPasses()
    {
        var justAfter = new DateTime(2026, 10, 3, 19, 0, 1, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2026, 10, 10, 19, 0, 0, DateTimeKind.Utc),
            GameSchedule.NextJumboCactpot(justAfter, "EU"));
    }

    [Fact]
    public void FashionReportOpensOnFriday()
    {
        Assert.Equal(new DateTime(2026, 10, 2, 8, 0, 0, DateTimeKind.Utc), GameSchedule.NextFashionReportOpen(Thursday));
    }

    [Theory]
    [InlineData(100, 200, 150, true)]
    [InlineData(100, 200, 200, true)]
    [InlineData(100, 200, 100, false)]
    [InlineData(100, 200, 201, false)]
    [InlineData(100, 200, 0, false)]
    public void CrossedFiresOnlyInsideTheTickWindow(long previous, long now, long moment, bool expected)
    {
        Assert.Equal(expected, TimerLedger.Crossed(previous, now, moment));
    }

    [Theory]
    [InlineData(0, 200, 100, 0f)]
    [InlineData(100, 200, 150, 0.5f)]
    [InlineData(100, 200, 250, 1f)]
    [InlineData(100, 0, 150, 0f)]
    public void ProgressIsClampedAndSafeWithoutAStart(long start, long end, long now, float expected)
    {
        Assert.Equal(expected, TimerLedger.Progress(start, end, now), 3);
    }

    [Fact]
    public void ApplyRetainersCreatesTheCharacterAndStampsIt()
    {
        var records = new List<TimerCharacterRecord>();
        var captured = new[] { new CapturedRetainer(1, "Lyra", Now + 600, 3600) };

        Assert.True(TimerLedger.ApplyRetainers(records, Main, "Alpha", "Ultros", captured, Now));

        var record = Assert.Single(records);
        Assert.Equal("Alpha", record.Name);
        Assert.Equal("Ultros", record.World);
        Assert.Equal(Now, record.RetainersSeenUnix);
        Assert.Equal(Now + 600, Assert.Single(record.Retainers).CompleteUnix);
    }

    [Fact]
    public void UnchangedRetainersReportNoChangeButRefreshTheSeenStamp()
    {
        var records = new List<TimerCharacterRecord>();
        var captured = new[] { new CapturedRetainer(1, "Lyra", Now + 600, 3600) };
        TimerLedger.ApplyRetainers(records, Main, "Alpha", "Ultros", captured, Now);

        Assert.False(TimerLedger.ApplyRetainers(records, Main, "Alpha", "Ultros", captured, Now + 30));
        Assert.Equal(Now + 30, records[0].RetainersSeenUnix);
    }

    [Fact]
    public void ApplyRetainersRecordsANewVentureImmediately()
    {
        var records = new List<TimerCharacterRecord>();
        TimerLedger.ApplyRetainers(records, Main, "Alpha", "Ultros",
            new[] { new CapturedRetainer(1, "Lyra", Now + 600, 3600) }, Now);

        Assert.True(TimerLedger.ApplyRetainers(records, Main, "Alpha", "Ultros",
            new[] { new CapturedRetainer(1, "Lyra", Now + 64800, 64800) }, Now + 5));
        Assert.Equal(Now + 64800, records[0].Retainers[0].CompleteUnix);
    }

    [Fact]
    public void RetainersOwnedByAnotherCharacterAreNotReassigned()
    {
        var records = new List<TimerCharacterRecord>();
        var captured = new[] { new CapturedRetainer(1, "Lyra", Now + 600, 3600) };
        TimerLedger.ApplyRetainers(records, Main, "Alpha", "Ultros", captured, Now);

        Assert.False(TimerLedger.ApplyRetainers(records, Alt, "Beta", "Ultros", captured, Now + 5));
        Assert.Single(records);
    }

    [Fact]
    public void ApplyMapAllowanceIgnoresUnknownTimestamps()
    {
        var records = new List<TimerCharacterRecord>();
        Assert.False(TimerLedger.ApplyMapAllowance(records, Main, "Alpha", "Ultros", 0, Now));
        Assert.True(TimerLedger.ApplyMapAllowance(records, Main, "Alpha", "Ultros", Now + 100, Now));
        Assert.Equal(Now + 100, records[0].MapAllowanceUnix);
    }

    [Fact]
    public void WorkshopsAreKeyedByCompanyAndWorldWithAContentIdFallback()
    {
        Assert.Equal("ABC@Ultros", TimerLedger.WorkshopKey("ABC", "Ultros", Main));
        Assert.Equal("11", TimerLedger.WorkshopKey(string.Empty, "Ultros", Main));
    }

    [Fact]
    public void ApplyVesselsStoresEveryVessel()
    {
        var records = new List<TimerWorkshopRecord>();
        var captured = new[]
        {
            new CapturedVessel("Nautilus", false, Now - 100, Now + 100),
            new CapturedVessel("Falcon", true, 0, 0),
        };

        Assert.True(TimerLedger.ApplyVessels(records, "ABC@Ultros", "ABC", "Ultros", captured, Now));
        Assert.Equal(2, records[0].Vessels.Count);
        Assert.False(TimerLedger.ApplyVessels(records, "ABC@Ultros", "ABC", "Ultros", captured, Now + 1));
    }

    [Fact]
    public void TallyCountsReadyAndPicksTheSoonestRunningTimer()
    {
        var characters = new List<TimerCharacterRecord>
        {
            Character(Main, "Alpha", Retainer("Ready", Now - 10, 3600), Retainer("Late", Now + 900, 3600),
                Retainer("Idle", 0, 0)),
            Character(Alt, "Beta", Retainer("Soon", Now + 300, 3600)),
        };
        var workshops = new List<TimerWorkshopRecord>
        {
            new()
            {
                Key = "ABC@Ultros",
                Vessels = new List<TimerVesselRecord>
                {
                    new() { Name = "Docked", ReturnUnix = Now - 1 },
                    new() { Name = "Far", ReturnUnix = Now + 7200, RegisterUnix = Now - 100 },
                },
            },
        };

        var tally = TimerBoard.Tally(characters, workshops, Now);

        Assert.Equal(1, tally.ReadyVentures);
        Assert.Equal(1, tally.ReadyVoyages);
        Assert.Equal(2, tally.Ready);
        Assert.Equal("Soon", tally.Soonest.Name);
        Assert.Equal(Now + 300 - 3600, tally.Soonest.StartUnix);
        Assert.False(tally.Soonest.Voyage);
    }

    [Fact]
    public void TallyWithNothingCapturedHasNoSoonestTimer()
    {
        var tally = TimerBoard.Tally(new List<TimerCharacterRecord>(), new List<TimerWorkshopRecord>(), Now);
        Assert.False(tally.Soonest.Exists);
        Assert.Equal(0, tally.Ready);
    }

    [Theory]
    [InlineData(300, true)]
    [InlineData(600, true)]
    [InlineData(601, false)]
    [InlineData(0, false)]
    public void IslandShowsOnlyTheLastTenMinutes(long remaining, bool expected)
    {
        var timer = new RunningTimer("Lyra", false, false, 0, Now + remaining);
        Assert.Equal(expected, TimerBoard.InIslandWindow(timer, Now));
    }

    [Fact]
    public void CharactersOrderCurrentFirstThenByNameAndSkipEmpty()
    {
        var source = new List<TimerCharacterRecord>
        {
            Character(1, "Zed", Retainer("A", 0, 0)),
            Character(2, "Amy", Retainer("B", 0, 0)),
            Character(3, "Empty"),
            Character(4, "Mia", Retainer("C", 0, 0)),
        };
        var ordered = new List<TimerCharacterRecord>();

        TimerBoard.OrderCharacters(source, 4, ordered);

        Assert.Equal(new[] { "Mia", "Amy", "Zed" }, ordered.ConvertAll(record => record.Name));
    }

    [Fact]
    public void GameTimerIsTheLowestIslandPriority()
    {
        Assert.Equal(IslandActivity.Muster,
            IslandActivities.Select(new IslandSignals(false, false, false, false, true, GameTimer: true)));
        Assert.Equal(IslandActivity.GameTimer,
            IslandActivities.Select(new IslandSignals(false, false, false, false, false, GameTimer: true)));
        Assert.Equal("timers", IslandActivities.OwnerAppId(IslandActivity.GameTimer));
    }

    private static TimerCharacterRecord Character(ulong contentId, string name, params TimerRetainerRecord[] retainers)
    {
        var record = new TimerCharacterRecord { ContentId = contentId, Name = name };
        record.Retainers.AddRange(retainers);
        return record;
    }

    private static TimerRetainerRecord Retainer(string name, long completeUnix, int durationSeconds) =>
        new() { Name = name, CompleteUnix = completeUnix, DurationSeconds = durationSeconds, RetainerId = 0 };
}
