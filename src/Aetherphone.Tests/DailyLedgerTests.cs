using Aetherphone.Core.Dailies;
using Aetherphone.Core.Localization;
using Dalamud.Interface;
using Xunit;

namespace Aetherphone.Tests;

public sealed class DailyLedgerTests
{
    private const ulong Main = 1001;
    private const ulong Alt = 2002;
    private static readonly DateTime Wednesday = new(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ChecksAreKeptPerCharacter()
    {
        var records = new List<DailyCheckRecord>();
        var period = DailyLedger.PeriodStartUnix(DailyCadence.Daily, Wednesday);
        Assert.True(DailyLedger.SetChecked(records, "daily.gcSupply", Main, period, true));
        Assert.True(DailyLedger.IsChecked(records, "daily.gcSupply", Main, period));
        Assert.False(DailyLedger.IsChecked(records, "daily.gcSupply", Alt, period));
    }

    [Fact]
    public void LegacyChecksWithoutCharacterStillCount()
    {
        var period = DailyLedger.PeriodStartUnix(DailyCadence.Weekly, Wednesday);
        var records = new List<DailyCheckRecord>
        {
            new() { ItemId = "weekly.fashionReport", PeriodResetUnix = period },
        };

        Assert.True(DailyLedger.IsChecked(records, "weekly.fashionReport", Alt, period));
        Assert.True(DailyLedger.SetChecked(records, "weekly.fashionReport", Alt, period, false));
        Assert.Empty(records);
    }

    [Fact]
    public void CheckExpiresWhenThePeriodRollsOver()
    {
        var records = new List<DailyCheckRecord>();
        var today = DailyLedger.PeriodStartUnix(DailyCadence.Daily, Wednesday);
        DailyLedger.SetChecked(records, "daily.miniCactpot", Main, today, true);
        var tomorrow = DailyLedger.PeriodStartUnix(DailyCadence.Daily, Wednesday.AddDays(1));
        Assert.False(DailyLedger.IsChecked(records, "daily.miniCactpot", Main, tomorrow));
        Assert.True(DailyLedger.SetChecked(records, "daily.miniCactpot", Main, tomorrow, true));
        Assert.Single(records);
        Assert.False(DailyLedger.SetChecked(records, "daily.miniCactpot", Main, tomorrow, true));
    }

    [Fact]
    public void PruneKeepsTheCurrentDailyPeriodRightAfterWeeklyReset()
    {
        var afterWeekly = new DateTime(2026, 10, 6, 9, 0, 0, DateTimeKind.Utc);
        var records = new List<DailyCheckRecord>();
        var daily = DailyLedger.PeriodStartUnix(DailyCadence.Daily, afterWeekly);
        var stale = DailyLedger.PeriodStartUnix(DailyCadence.Weekly, afterWeekly.AddDays(-7));
        DailyLedger.SetChecked(records, "daily.gcSupply", Main, daily, true);
        DailyLedger.SetChecked(records, "weekly.challengeLog", Main, stale, true);
        Assert.Equal(1, DailyLedger.Prune(records, DailyLedger.OldestLiveUnix(afterWeekly)));
        Assert.True(DailyLedger.IsChecked(records, "daily.gcSupply", Main, daily));
    }

    [Fact]
    public void RemoveEverywhereClearsEveryCharacter()
    {
        var records = new List<DailyCheckRecord>();
        var period = DailyLedger.PeriodStartUnix(DailyCadence.Daily, Wednesday);
        DailyLedger.SetChecked(records, "task", Main, period, true);
        DailyLedger.SetChecked(records, "task", Alt, period, true);
        DailyLedger.SetChecked(records, "other", Alt, period, true);
        Assert.Equal(2, DailyLedger.RemoveEverywhere(records, "task"));
        Assert.Single(records);
    }

    [Fact]
    public void CleanTitleTrimsAndCaps()
    {
        Assert.Equal("Gather for the FC", DailyLedger.CleanTitle("  Gather for the FC  "));
        Assert.Equal(string.Empty, DailyLedger.CleanTitle("   "));
        Assert.Equal(DailyLedger.MaxTitleLength, DailyLedger.CleanTitle(new string('a', 200)).Length);
    }

    [Fact]
    public void ReminderFiresOnceWhenTheLeadMomentIsCrossed()
    {
        const long reset = 100_000;
        const long lead = 7_200;
        Assert.True(DailyLedger.ReminderDue(reset - lead - 1, reset - lead, reset, lead));
        Assert.False(DailyLedger.ReminderDue(reset - lead, reset - lead + 2, reset, lead));
        Assert.False(DailyLedger.ReminderDue(reset - lead - 10, reset - lead - 1, reset, lead));
    }

    [Fact]
    public void RowStateSeparatesManualAutoAndHidden()
    {
        var manual = Item(DailyTracking.Manual);
        var auto = Item(DailyTracking.BeastTribeAllowances);
        var leves = Item(DailyTracking.Levequests);
        var open = new DailyAutoStatus(true, false, 4, 12);
        var complete = new DailyAutoStatus(true, true, 0, 12);
        Assert.Equal(DailyRowState.Hidden, DailyProgress.State(manual, DailyAutoStatus.Unavailable, true, true));
        Assert.Equal(DailyRowState.Done, DailyProgress.State(manual, DailyAutoStatus.Unavailable, false, true));
        Assert.Equal(DailyRowState.Open, DailyProgress.State(manual, DailyAutoStatus.Unavailable, false, false));
        Assert.Equal(DailyRowState.Unavailable, DailyProgress.State(auto, DailyAutoStatus.Unavailable, false, true));
        Assert.Equal(DailyRowState.Open, DailyProgress.State(auto, open, false, false));
        Assert.Equal(DailyRowState.Done, DailyProgress.State(auto, complete, false, false));
        Assert.Equal(DailyRowState.Info, DailyProgress.State(leves, complete, false, false));
        Assert.False(DailyProgress.Counts(DailyRowState.Info));
        Assert.False(DailyProgress.Counts(DailyRowState.Unavailable));
    }

    [Fact]
    public void FractionCountsDoneAllowances()
    {
        var status = new DailyAutoStatus(true, false, 3, 12);
        Assert.Equal(9, DailyProgress.DoneCount(status, DailyTracking.BeastTribeAllowances));
        Assert.Equal(0.75f, DailyProgress.Fraction(status, DailyTracking.BeastTribeAllowances), 3);
        var leves = new DailyAutoStatus(true, true, 87, 100);
        Assert.Equal(87, DailyProgress.DoneCount(leves, DailyTracking.Levequests));
        Assert.Equal(0.87f, DailyProgress.Fraction(leves, DailyTracking.Levequests), 3);
    }

    private static DailyItem Item(DailyTracking tracking) =>
        new("test", new LocString("test", "Test"), FontAwesomeIcon.Check, default, DailyCadence.Daily, tracking, 12);
}
