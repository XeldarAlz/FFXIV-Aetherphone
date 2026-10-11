using Aetherphone.Apps.Casino.Cabinets;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class BarkeepPlaybackTests
{
    private static BarkeepShift Shift()
    {
        var patrons = new[]
        {
            new BarkeepPatronScript(0, new[] { 0, 1 }),
            new BarkeepPatronScript(2, new[] { 2 }),
            new BarkeepPatronScript(5, new[] { 3, 0 }),
            new BarkeepPatronScript(40, new[] { 1 }),
        };
        return new BarkeepShift("round-1", patrons, 1_000);
    }

    private static void Run(BarkeepBarFlow flow, BarkeepShift shift, double from, double to)
    {
        for (var elapsed = from; elapsed < to; elapsed += 1.0 / 60.0)
        {
            flow.Update(shift, elapsed, 1f / 60f);
        }
    }

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = new BarkeepBarFlow();
        var second = new BarkeepBarFlow();
        var other = new BarkeepBarFlow();
        var seed = BarkeepBarFlow.SeedOf("round-1");
        first.Begin(seed, 4);
        second.Begin(seed, 4);
        other.Begin(BarkeepBarFlow.SeedOf("round-2"), 4);
        var differs = false;
        for (var patronIndex = 0; patronIndex < BarkeepRules.MaxPatrons; patronIndex++)
        {
            Assert.Equal(first.LookOf(patronIndex), second.LookOf(patronIndex));
            differs |= first.LookOf(patronIndex) != other.LookOf(patronIndex);
        }

        Assert.True(differs);
        Assert.Equal(BarkeepBarFlow.SeedOf("abc"), BarkeepBarFlow.SeedOf("abc"));
    }

    [Fact]
    public void PatronsWaitOffstageUntilTheirScriptedSecond()
    {
        var shift = Shift();
        var flow = new BarkeepBarFlow();
        flow.Begin(1, shift.PatronCount);
        Run(flow, shift, 0, 1);
        Assert.True(flow.IsVisible(0));
        Assert.False(flow.IsVisible(1));
        Assert.False(flow.IsVisible(3));
    }

    [Fact]
    public void TheFirstPatronSlidesToTheRailAndTheNextQueuesBehind()
    {
        var shift = Shift();
        var flow = new BarkeepBarFlow();
        flow.Begin(1, shift.PatronCount);
        Assert.False(flow.AtRail(shift, 0));
        Run(flow, shift, 0, 4);
        Assert.True(flow.AtRail(shift, 0));
        Assert.True(flow.PositionOf(1) > flow.PositionOf(0));
        Assert.False(flow.AtRail(shift, 1));
    }

    [Fact]
    public void AServedPatronLeavesAndTheQueueStepsUp()
    {
        var shift = Shift();
        var flow = new BarkeepBarFlow();
        flow.Begin(1, shift.PatronCount);
        Run(flow, shift, 0, 6);
        shift.CommitStepGrade(100);
        shift.CommitStepGrade(70);
        Run(flow, shift, 6, 9);
        Assert.False(flow.IsVisible(0));
        Assert.True(flow.AtRail(shift, 1));
    }

    [Fact]
    public void AMidShiftJoinSnapsEveryoneToWhereTheyStand()
    {
        var shift = Shift();
        shift.CommitStepGrade(100);
        shift.CommitStepGrade(100);
        var flow = new BarkeepBarFlow();
        flow.Begin(1, shift.PatronCount);
        flow.Snap(shift, 10);
        Assert.False(flow.IsVisible(0));
        Assert.True(flow.AtRail(shift, 1));
        Assert.True(flow.IsVisible(2));
        Assert.False(flow.IsVisible(3));
    }

    [Fact]
    public void QueueSlotsRunRightOfTheRailAndOverflowStaysOffstage()
    {
        Assert.Equal(BarkeepBarFlow.ServeX, BarkeepBarFlow.TargetFor(2, 2));
        Assert.Equal(BarkeepBarFlow.ExitX, BarkeepBarFlow.TargetFor(1, 2));
        Assert.True(BarkeepBarFlow.TargetFor(3, 2) > BarkeepBarFlow.ServeX);
        Assert.Equal(BarkeepBarFlow.EntryX, BarkeepBarFlow.TargetFor(2 + BarkeepBarFlow.VisibleQueue + 1, 2));
    }

    [Fact]
    public void FeverLightsAtFivePerfectStepsInARowAndAnyMissPutsItOut()
    {
        var tips = new BarkeepTipMeter();
        for (var step = 0; step < BarkeepTipMeter.FeverStreak - 1; step++)
        {
            Assert.Equal(BarkeepFeverChange.None, tips.Grade(BarkeepGrading.PerfectGrade));
        }

        Assert.False(tips.Fever);
        Assert.Equal(BarkeepFeverChange.Started, tips.Grade(BarkeepGrading.PerfectGrade));
        Assert.True(tips.Fever);
        Assert.Equal(BarkeepFeverChange.None, tips.Grade(BarkeepGrading.PerfectGrade));
        Assert.Equal(BarkeepFeverChange.Ended, tips.Grade(BarkeepGrading.GoodGrade));
        Assert.False(tips.Fever);
        Assert.Equal(0, tips.PerfectStreak);
    }

    [Fact]
    public void TheComboCountsGoodPoursAndARoughOneBreaksIt()
    {
        var tips = new BarkeepTipMeter();
        tips.Grade(BarkeepGrading.GoodGrade);
        tips.Grade(BarkeepGrading.PerfectGrade);
        tips.Grade(BarkeepGrading.GoodGrade);
        tips.Grade(BarkeepGrading.GoodGrade);
        Assert.Equal(4, tips.Count);
        Assert.Equal(2, tips.Multiplier);
        tips.Update(10f);
        Assert.Equal(4, tips.Count);
        tips.Grade(BarkeepGrading.RoughGrade);
        Assert.Equal(0, tips.Count);
        tips.Reset();
        Assert.False(tips.Fever);
    }
}
