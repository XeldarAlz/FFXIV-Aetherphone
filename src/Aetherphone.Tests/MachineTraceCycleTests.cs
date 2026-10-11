using Aetherphone.Apps.Casino.Machines;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class MachineTraceCycleTests
{
    private const float Frame = 1f / 60f;
    private const int BirdCells = SlotsRules.CellCount;

    [Fact]
    public void ANewRoundOnTheSameStepTracesItsOwnWins()
    {
        var cycle = new MachineTraceCycle();
        var threeLines = new[] { Line(0, 3, 400), Line(3, 4, 900), Line(7, 3, 100) };
        cycle.Track(1, 0, threeLines, BirdCells);
        cycle.Advance(MachineTraceCycle.SlotSeconds * 2.5f);
        Assert.Equal(3, cycle.Count);
        Assert.True(cycle.TryActive(threeLines, out _));

        var none = Array.Empty<CasinoSlotsWinDto>();
        cycle.Track(2, 0, none, BirdCells);
        Assert.Equal(0, cycle.Count);
        Assert.False(cycle.TryActive(none, out _));

        var oneLine = new[] { Line(5, 3, 250) };
        cycle.Track(3, 0, oneLine, BirdCells);
        Assert.Equal(1, cycle.Count);
        Assert.False(cycle.Overview);
        Assert.True(cycle.TryActive(oneLine, out var active));
        Assert.Equal(0, active);
    }

    [Fact]
    public void WinsShowTogetherFirstThenCycleInPayOrder()
    {
        var cycle = new MachineTraceCycle();
        var wins = new[] { Line(0, 3, 100), Line(1, 5, 500), Line(2, 4, 200) };
        cycle.Track(1, 0, wins, BirdCells);
        Assert.True(cycle.Overview);
        Assert.False(cycle.TryActive(wins, out _));
        cycle.Advance(MachineTraceCycle.OverviewSeconds + MachineTraceCycle.SlotSeconds * 0.5f);
        Assert.False(cycle.Overview);
        var seen = new int[wins.Length];
        for (var slot = 0; slot < wins.Length; slot++)
        {
            Assert.True(cycle.TryActive(wins, out seen[slot]));
            cycle.Advance(MachineTraceCycle.SlotSeconds);
        }

        Assert.Equal(new[] { 1, 2, 0 }, seen);
    }

    [Fact]
    public void PaylinesTraceAsLinesAndEveryOtherWinTracesItsCells()
    {
        Assert.True(MachineTraceCycle.IsLine(Line(9, 3, 1)));
        Assert.False(MachineTraceCycle.IsLine(Line(SlotsRules.LineScatter, 3, 1)));
        Assert.False(MachineTraceCycle.IsLine(Line(SlotsRules.LineCluster, 9, 1)));
        Assert.False(MachineTraceCycle.IsLine(Line(SlotsRules.PaylineCount, 3, 1)));
        Assert.True(MachineTraceCycle.Traceable(Cells(SlotsRules.LineCluster, 40, 0, 1, 2), 30));
        Assert.True(MachineTraceCycle.Traceable(Cells(SlotsRules.LineScatter, 40, 3, 7, 12), BirdCells));
        Assert.False(MachineTraceCycle.Traceable(Cells(SlotsRules.LineGrand, 40), BirdCells));
        Assert.False(MachineTraceCycle.Traceable(Cells(SlotsRules.LineCluster, 40, 30, 31), 30));
    }

    [Fact]
    public void BackToBackRoundsNeverPointPastTheShownWins()
    {
        var playback = new MachineRoundPlayback();
        var cycle = new MachineTraceCycle();
        var traced = 0;
        var clusters = 0;
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var (name, round) in SlotsVectorFiles.Rounds())
            {
                var spin = SlotsVectorFiles.Spin(name, round);
                Assert.True(playback.Begin(spin), spin.RoundId);
                var cells = playback.Info.CellCount;
                var guard = 0;
                while (guard < 200_000)
                {
                    playback.Update(Frame);
                    guard++;
                    if (!MachineReels.Presenting(playback))
                    {
                        continue;
                    }

                    var wins = playback.Current.Wins;
                    cycle.Track(playback.Serial, playback.StepIndex, wins, cells);
                    cycle.Advance(Frame);
                    for (var slot = 0; slot < cycle.Count; slot++)
                    {
                        Assert.InRange(cycle.WinAt(slot), 0, wins!.Length - 1);
                    }

                    if (cycle.TryActive(wins, out var active))
                    {
                        Assert.InRange(active, 0, wins!.Length - 1);
                        Assert.True(MachineTraceCycle.Traceable(wins[active], cells), spin.RoundId);
                        traced++;
                        clusters += wins[active].Line == SlotsRules.LineCluster ? 1 : 0;
                    }

                    if (playback.Finished)
                    {
                        break;
                    }
                }
            }
        }

        Assert.True(traced > 0);
        Assert.True(clusters > 0);
    }

    [Fact]
    public void EveryTraceableWinOfAStepIsShownOnce()
    {
        var (spin, step) = FirstStepWith(wins => wins.Length > 1);
        var wins = spin.Steps![step].Wins!;
        var cells = SlotsMachines.For(spin.MachineId).CellCount;
        var cycle = new MachineTraceCycle();
        cycle.Track(1, step, wins, cells);
        cycle.Advance((cycle.Count > 1 ? MachineTraceCycle.OverviewSeconds : 0f)
            + MachineTraceCycle.SlotSeconds * 0.5f);
        var shown = new bool[wins.Length];
        for (var slot = 0; slot < cycle.Count; slot++)
        {
            Assert.True(cycle.TryActive(wins, out var active));
            Assert.False(shown[active]);
            shown[active] = true;
            cycle.Advance(MachineTraceCycle.SlotSeconds);
        }

        for (var index = 0; index < wins.Length; index++)
        {
            Assert.Equal(MachineTraceCycle.Traceable(wins[index], cells), shown[index]);
        }
    }

    private static CasinoSlotsWinDto Line(int line, int count, long pay) =>
        new(line, 1, count, Array.Empty<int>(), pay);

    private static CasinoSlotsWinDto Cells(int line, long pay, params int[] cells) =>
        new(line, 1, cells.Length, cells, pay);

    private static (CasinoSlotsSpinDto Spin, int Step) FirstStepWith(Func<CasinoSlotsWinDto[], bool> wanted)
    {
        foreach (var (name, round) in SlotsVectorFiles.Rounds())
        {
            var spin = SlotsVectorFiles.Spin(name, round);
            for (var index = 0; index < spin.Steps!.Length; index++)
            {
                if (spin.Steps[index].Wins is { } wins && wanted(wins))
                {
                    return (spin, index);
                }
            }
        }

        throw new InvalidOperationException("no matching step in the vectors");
    }
}
