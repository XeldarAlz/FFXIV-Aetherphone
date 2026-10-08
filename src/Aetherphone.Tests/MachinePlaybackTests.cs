using Aetherphone.Apps.Casino.Machines;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class MachinePlaybackTests
{
    [Fact]
    public void EveryVectorRoundPlaysToItsServerTotal()
    {
        foreach (var (name, round) in SlotsVectorFiles.Rounds())
        {
            var spin = SlotsVectorFiles.Spin(name, round);
            var playback = new MachineRoundPlayback();
            Assert.True(playback.Begin(spin), spin.RoundId);
            Assert.True(playback.Active);
            var guard = 0;
            while (!playback.Finished && guard < 100_000)
            {
                playback.Update(1f / 60f);
                Assert.True(playback.Committed <= spin.TotalWin);
                guard++;
            }

            Assert.True(playback.Finished, spin.RoundId);
            Assert.Equal(spin.TotalWin, playback.Committed);
        }
    }

    [Fact]
    public void CommittedNeverRunsAheadOfTheLandedStep()
    {
        var spin = FirstWhere(round => round.Steps!.Length > 6);
        var playback = new MachineRoundPlayback();
        playback.Begin(spin);
        while (!playback.Finished)
        {
            var landedRunning = playback.StepIndex > 0 ? spin.Steps![playback.StepIndex - 1].Running : 0;
            if (playback.Beat is MachineBeat.Spin or MachineBeat.Tumble)
            {
                Assert.True(playback.Committed <= landedRunning);
            }

            playback.Update(0.05f);
        }
    }

    [Fact]
    public void AMidRoundJoinSnapsToTheFinalState()
    {
        var spin = FirstWhere(round => round.BonusTriggered);
        var playback = new MachineRoundPlayback();
        playback.Begin(spin);
        playback.Update(0.5f);
        playback.Skip();
        Assert.True(playback.Finished);
        Assert.Equal(spin.TotalWin, playback.Committed);
        Assert.Equal(spin.Steps!.Length - 1, playback.StepIndex);
        Assert.Equal(spin.FreeSpinsPlayed, playback.FeaturePlayed);
    }

    [Fact]
    public void ALongFrameAdvancesThroughManyBeatsAtOnce()
    {
        var spin = FirstWhere(round => round.BonusTriggered);
        var playback = new MachineRoundPlayback();
        playback.Begin(spin);
        playback.Update(10_000f);
        Assert.True(playback.Finished);
        Assert.Equal(spin.TotalWin, playback.Committed);
    }

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var spin = FirstWhere(round => round.Expander >= 0);
        var first = new MachineRoundPlayback();
        var second = new MachineRoundPlayback();
        first.Begin(spin);
        second.Begin(spin);
        for (var reel = 0; reel < MachineRoundPlayback.MaxReels; reel++)
        {
            Assert.Equal(first.BlurOffset(reel), second.BlurOffset(reel));
        }

        for (var index = 0; index < MachineRoundPlayback.PickerLength; index++)
        {
            Assert.Equal(first.PickerAt(index), second.PickerAt(index));
        }

        Assert.Equal(spin.Expander, first.PickerAt(MachineRoundPlayback.PickerLength - 1));
        Assert.Equal(spin.Expander, first.PickerSymbol(1f));
    }

    [Fact]
    public void TurboHalvesTheReelCycle()
    {
        var spin = FirstWhere(round => !round.BonusTriggered && round.Steps!.Length == 1);
        var normal = new MachineRoundPlayback();
        normal.Begin(spin);
        var fast = new MachineRoundPlayback { Turbo = true };
        fast.Begin(spin);
        var reels = SlotsMachines.For(spin.MachineId).Reels;
        Assert.InRange(normal.StopSeconds(reels - 1), 2.5f, 3.0f);
        Assert.Equal(normal.StopSeconds(reels - 1) * 0.5f, fast.StopSeconds(reels - 1), 3);
        Assert.Equal(MachineTiming.Stagger, normal.StopSeconds(1) - normal.StopSeconds(0), 3);
    }

    [Fact]
    public void AnticipationOnlyRunsWhenTheOutcomeTriggers()
    {
        foreach (var (name, round) in SlotsVectorFiles.Rounds())
        {
            var spin = SlotsVectorFiles.Spin(name, round);
            var playback = new MachineRoundPlayback();
            playback.Begin(spin);
            var anticipated = false;
            while (!playback.Finished)
            {
                anticipated |= playback.Beat == MachineBeat.Spin && playback.FirstAnticipatedReel >= 0;
                playback.Update(0.1f);
            }

            if (anticipated)
            {
                Assert.True(spin.BonusTriggered || HasRetrigger(spin), spin.RoundId);
            }
        }
    }

    [Fact]
    public void ABonusRoundIntroducesItsFeature()
    {
        var spin = FirstWhere(round => round.BonusTriggered && round.Steps![0].Kind == SlotsRules.StepBase
            && round.MachineId != SlotsRules.MoogleId);
        var playback = new MachineRoundPlayback();
        playback.Begin(spin);
        var sawIntro = false;
        var sawOutro = false;
        while (!playback.Finished)
        {
            sawIntro |= playback.Beat == MachineBeat.Intro;
            sawOutro |= playback.Beat == MachineBeat.Outro;
            playback.Update(0.05f);
        }

        Assert.True(sawIntro);
        Assert.True(sawOutro);
    }

    [Fact]
    public void HoldAndSpinCollectsEveryCoin()
    {
        var spin = FirstWhere(round => Array.Exists(round.Steps!, step => step.Kind == SlotsRules.StepCollect));
        var playback = new MachineRoundPlayback();
        playback.Begin(spin);
        var sawRespin = false;
        var sawCollect = false;
        while (!playback.Finished)
        {
            sawRespin |= playback.Beat == MachineBeat.Respin && playback.InHold;
            sawCollect |= playback.Beat == MachineBeat.Collect;
            playback.Update(0.05f);
        }

        Assert.True(sawRespin);
        Assert.True(sawCollect);
    }

    [Fact]
    public void RefusedOrMalformedRoundsDoNotPlay()
    {
        var playback = new MachineRoundPlayback();
        Assert.False(playback.Begin(new CasinoSlotsSpinDto(Granted: false, Reason: "cooldown")));
        Assert.False(playback.Begin(new CasinoSlotsSpinDto(Granted: true, MachineId: SlotsRules.BirdId)));
        Assert.False(playback.Begin(new CasinoSlotsSpinDto(Granted: true, MachineId: SlotsRules.BirdId,
            Steps: new[] { new CasinoSlotsStepDto(Kind: SlotsRules.StepBase, Grid: new int[3]) })));
        Assert.False(playback.Begin(new CasinoSlotsSpinDto(Granted: true, MachineId: "slots.mystery",
            Steps: new[] { new CasinoSlotsStepDto(Kind: SlotsRules.StepBase, Grid: new int[15]) })));
        Assert.False(playback.Active);
    }

    [Fact]
    public void RollupCountsHalfABetASecondThenCompresses()
    {
        var rollup = default(MachineRollup);
        rollup.Snap(0);
        var seconds = 0f;
        while (!rollup.Done || rollup.Shown < 2_000)
        {
            rollup.Update(2_000, 1_000, 0.01f, false);
            seconds += 0.01f;
        }

        Assert.InRange(seconds, 3.9f, 4.1f);
        rollup.Snap(0);
        seconds = 0f;
        while (rollup.Shown < 100_000)
        {
            rollup.Update(100_000, 1_000, 0.01f, false);
            seconds += 0.01f;
        }

        Assert.InRange(seconds, 40f, 46f);
        rollup.Snap(0);
        rollup.Update(5_000, 1_000, 1f, true);
        Assert.Equal(1_000, rollup.Shown);
    }

    [Fact]
    public void TheGambleLadderOnlyOffersSmallBirdWins()
    {
        var ladder = new GambleLadder();
        ladder.Offer("round1", 1_000, 25_000);
        Assert.False(ladder.Offered);
        ladder.Offer("round1", 1_000, 4_000);
        Assert.True(ladder.Offered);
        Assert.Equal(8_000, ladder.RungAmount(1));
        Assert.Equal(128_000, ladder.RungAmount(5));
        ladder.Choose();
        Assert.Equal(GamblePhase.Choosing, ladder.Phase);
        ladder.Collect();
        Assert.Equal(GamblePhase.None, ladder.Phase);
    }

    private static bool HasRetrigger(CasinoSlotsSpinDto spin)
    {
        for (var index = 1; index < spin.Steps!.Length; index++)
        {
            if (spin.Steps[index].SpinsAdded > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static CasinoSlotsSpinDto FirstWhere(Func<CasinoSlotsSpinDto, bool> match)
    {
        foreach (var (name, round) in SlotsVectorFiles.Rounds())
        {
            var spin = SlotsVectorFiles.Spin(name, round);
            if (match(spin))
            {
                return spin;
            }
        }

        throw new InvalidOperationException("no vector round matches");
    }
}
