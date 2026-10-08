using Aetherphone.Apps.Casino.Race;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RacePlaybackTests
{
    private const long Start = 1_000_000;

    private static CasinoRoomSnapshotDto Snapshot(int phase, long roundIndex = 0)
    {
        return new CasinoRoomSnapshotDto(RoomId: CasinoRoomIds.RaceTrack, GameKind: CasinoWire.RaceKind, Phase: phase,
            RoundIndex: roundIndex);
    }

    private static CasinoRaceRunnerDto[] Field()
    {
        var runners = new CasinoRaceRunnerDto[RaceRules.FieldSize];
        for (var slot = 0; slot < runners.Length; slot++)
        {
            runners[slot] = new CasinoRaceRunnerDto(Slot: slot, Name: "Bird " + slot, OddsHundredths: 700);
        }

        return runners;
    }

    private static CasinoRaceRoomStateDto Board(RaceVector vector, bool drawn)
    {
        return new CasinoRaceRoomStateDto(Runners: Field(), Seed: drawn ? vector.Seed : string.Empty,
            Order: drawn ? vector.Order : Array.Empty<int>(), PhotoFinish: drawn && vector.PhotoFinish,
            RaceStartUnixMs: drawn ? Start : 0);
    }

    private static long At(long milliseconds) => Start + milliseconds;

    private static List<RaceCueEvent> Drain(RaceRoundPlayback playback)
    {
        var cues = new List<RaceCueEvent>();
        while (playback.TryTakeCue(out var cue))
        {
            cues.Add(cue);
        }

        return cues;
    }

    private static List<RaceCueEvent> RunLive(RaceRoundPlayback playback, RaceVector vector, long untilMilliseconds)
    {
        var cues = new List<RaceCueEvent>();
        var board = Board(vector, true);
        for (var elapsed = 0L; elapsed <= untilMilliseconds; elapsed += 16)
        {
            playback.Update(Snapshot(CasinoRoomPhases.Locked), board, At(elapsed), false);
            cues.AddRange(Drain(playback));
        }

        return cues;
    }

    [Fact]
    public void AnOpenRoomWithAFieldIsThePaddock()
    {
        var vector = RaceVectors.Load()[0];
        var playback = new RaceRoundPlayback();
        playback.Update(Snapshot(CasinoRoomPhases.Open), Board(vector, false), At(-30_000), false);
        Assert.Equal(RaceStage.Paddock, playback.Stage);
        Assert.False(playback.HasOrder);
        Assert.Equal("race-track#0", playback.RoundKey);
    }

    [Fact]
    public void ALockWithoutTheSeedWaitsAtTheGates()
    {
        var vector = RaceVectors.Load()[0];
        var playback = new RaceRoundPlayback();
        playback.Update(Snapshot(CasinoRoomPhases.Locked), Board(vector, false), At(200), false);
        Assert.Equal(RaceStage.Gates, playback.Stage);
        Assert.Empty(Drain(playback));
    }

    [Fact]
    public void JoiningMidRaceJumpsToTheServerClockWithoutReplayingCues()
    {
        var vector = RaceVectors.Load()[0];
        var plan = RaceScript.Build(vector.Order, vector.Seed);
        var playback = new RaceRoundPlayback();
        playback.Update(Snapshot(CasinoRoomPhases.Locked), Board(vector, true), At(12_000), false);
        Assert.Equal(RaceStage.Running, playback.Stage);
        Assert.Empty(Drain(playback));
        var subTick = 12_000L * RaceScript.SubTicks / RaceRules.MillisecondsPerTick;
        Assert.Equal(subTick, playback.SubTick);
        for (var slot = 0; slot < RaceRules.FieldSize; slot++)
        {
            Assert.Equal(RaceScript.PositionAt(plan, slot, subTick), playback.Positions[slot]);
        }

        Assert.Equal(RaceScript.LeaderAt(plan, subTick), playback.Leader);
        Assert.True(playback.WatchedLive);
    }

    [Fact]
    public void JoiningAfterTheWinnerCrossedIsNotWatchedLive()
    {
        var vector = RaceVectors.Load()[0];
        var playback = new RaceRoundPlayback();
        var winner = RaceRules.MillisecondsOf(vector.FinishSubTicks[vector.Order[0]]);
        playback.Update(Snapshot(CasinoRoomPhases.Locked), Board(vector, true), At(winner + 100), false);
        Assert.False(playback.WatchedLive);
        Assert.Empty(Drain(playback));
    }

    [Fact]
    public void ALiveRaceCallsTheOffSurgesLeadChangesAndTheWinnerInOrder()
    {
        var vectors = RaceVectors.Load();
        for (var race = 0; race < vectors.Length; race++)
        {
            var vector = vectors[race];
            var playback = new RaceRoundPlayback();
            var cues = RunLive(playback, vector, 33_000);
            Assert.Equal(RaceCue.Off, cues[0].Cue);
            var winnerIndex = cues.FindIndex(cue => cue.Cue == RaceCue.Winner);
            Assert.True(winnerIndex > 0);
            Assert.Equal(vector.Order[0], cues[winnerIndex].Slot);
            Assert.Equal(vector.PhotoFinish, cues.Exists(cue => cue.Cue == RaceCue.PhotoFinish));
            var finishes = cues.FindAll(cue => cue.Cue == RaceCue.Finish);
            Assert.Equal(RaceRules.FieldSize - 1, finishes.Count);
            for (var place = 1; place < RaceRules.FieldSize; place++)
            {
                Assert.Equal(vector.Order[place], finishes[place - 1].Slot);
            }

            Assert.Contains(cues, cue => cue.Cue == RaceCue.FinalStretch);
        }
    }

    [Fact]
    public void LeadChangesAreCalledAfterTheQuietStart()
    {
        var vectors = RaceVectors.Load();
        var called = 0;
        for (var race = 0; race < vectors.Length; race++)
        {
            var playback = new RaceRoundPlayback();
            var cues = RunLive(playback, vectors[race], 33_000);
            called += cues.FindAll(cue => cue.Cue == RaceCue.LeadChange).Count;
        }

        Assert.True(called > 0);
    }

    [Fact]
    public void AfterTheLastBirdTheLineReplaysInSlowMotionThenSettles()
    {
        var vector = RaceVectors.Load()[0];
        var playback = new RaceRoundPlayback();
        var last = RaceRules.MillisecondsOf(vector.FinishSubTicks[vector.Order[RaceRules.FieldSize - 1]]);
        RunLive(playback, vector, last + 1_500);
        Assert.Equal(RaceStage.Replay, playback.Stage);
        Assert.InRange(playback.ReplayProgress, 0.4f, 0.6f);
        var winner = vector.FinishSubTicks[vector.Order[0]];
        Assert.InRange(playback.DisplaySubTick, winner - RaceRoundPlayback.ReplayWindowSubTicks / 2,
            winner + RaceRoundPlayback.ReplayWindowSubTicks / 2);
        playback.Update(Snapshot(CasinoRoomPhases.Locked), Board(vector, true),
            At(last + RaceRoundPlayback.ReplayMilliseconds + 50), false);
        Assert.Equal(RaceStage.Finished, playback.Stage);
        Assert.Equal(vector.Order, playback.Ranking.ToArray());
    }

    [Fact]
    public void TheResultPhaseShowsTheDrawnOrder()
    {
        var vector = RaceVectors.Load()[5];
        var playback = new RaceRoundPlayback();
        playback.Update(Snapshot(CasinoRoomPhases.Result), Board(vector, true), At(40_000), false);
        Assert.Equal(RaceStage.Result, playback.Stage);
        Assert.Equal(vector.Order, playback.Ranking.ToArray());
        for (var slot = 0; slot < RaceRules.FieldSize; slot++)
        {
            Assert.Equal(RaceScript.TrackUnits, playback.Positions[slot]);
        }

        Assert.False(playback.WatchedLive);
    }

    [Fact]
    public void InstantModeSkipsStraightToTheFinish()
    {
        var vector = RaceVectors.Load()[2];
        var playback = new RaceRoundPlayback();
        playback.Update(Snapshot(CasinoRoomPhases.Locked), Board(vector, true), At(100), true);
        Assert.Equal(RaceStage.Finished, playback.Stage);
        Assert.Equal(vector.Order, playback.Ranking.ToArray());
        Assert.Empty(Drain(playback));
    }

    [Fact]
    public void ANewRoundClearsTheOldRace()
    {
        var vector = RaceVectors.Load()[0];
        var playback = new RaceRoundPlayback();
        RunLive(playback, vector, 10_000);
        playback.Update(Snapshot(CasinoRoomPhases.Open, 1), Board(vector, false), At(60_000), false);
        Assert.Equal(RaceStage.Paddock, playback.Stage);
        Assert.False(playback.HasOrder);
        Assert.Equal("race-track#1", playback.RoundKey);
        Assert.Equal(0, playback.Positions[0]);
    }

    [Fact]
    public void ARejectedOrderNeverBuildsARace()
    {
        var vector = RaceVectors.Load()[0];
        var board = new CasinoRaceRoomStateDto(Runners: Field(), Seed: vector.Seed, Order: new[] { 0, 0, 1, 2, 3, 4, 5, 6 },
            RaceStartUnixMs: Start);
        var playback = new RaceRoundPlayback();
        playback.Update(Snapshot(CasinoRoomPhases.Locked), board, At(5_000), false);
        Assert.False(playback.HasOrder);
        Assert.Equal(RaceStage.Gates, playback.Stage);
    }

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var vector = RaceVectors.Load()[7];
        var first = new RaceRoundPlayback();
        var second = new RaceRoundPlayback();
        var firstCues = RunLive(first, vector, 34_000);
        var secondCues = RunLive(second, vector, 34_000);
        Assert.Equal(firstCues, secondCues);
        Assert.Equal(first.Positions.ToArray(), second.Positions.ToArray());
        var commentary = new RaceCommentary();
        var echo = new RaceCommentary();
        commentary.Begin(vector.Seed);
        echo.Begin(vector.Seed);
        for (var index = 0; index < firstCues.Count; index++)
        {
            Assert.Equal(commentary.Pick(firstCues[index].Cue).Key, echo.Pick(secondCues[index].Cue).Key);
        }
    }

    [Fact]
    public void EveryCalledCueHasALine()
    {
        Assert.NotEmpty(RaceCommentary.LinesFor(RaceCue.Off));
        Assert.NotEmpty(RaceCommentary.LinesFor(RaceCue.Surge));
        Assert.NotEmpty(RaceCommentary.LinesFor(RaceCue.LeadChange));
        Assert.NotEmpty(RaceCommentary.LinesFor(RaceCue.Fade));
        Assert.NotEmpty(RaceCommentary.LinesFor(RaceCue.FinalStretch));
        Assert.NotEmpty(RaceCommentary.LinesFor(RaceCue.PhotoFinish));
        Assert.NotEmpty(RaceCommentary.LinesFor(RaceCue.Winner));
        Assert.Empty(RaceCommentary.LinesFor(RaceCue.Finish));
    }

    [Fact]
    public void TheTrackCoastsBirdsPastTheLineOnlyAfterTheyCross()
    {
        var vector = RaceVectors.Load()[0];
        var plan = RaceScript.Build(vector.Order, vector.Seed);
        var winner = vector.Order[0];
        var finish = plan.FinishSubTicks[winner];
        Assert.Equal(RaceTrackView.TrackLength,
            RaceTrackView.VisualDistance(plan, winner, RaceScript.TrackUnits, finish));
        var later = RaceTrackView.VisualDistance(plan, winner, RaceScript.TrackUnits, finish + 25 * RaceScript.SubTicks);
        Assert.InRange(later, RaceTrackView.TrackLength + 1f, RaceTrackView.TrackLength + RaceTrackView.CoastUnits);
        Assert.Equal(500f, RaceTrackView.VisualDistance(plan, winner, 500_000, finish - 1), 3);
    }
}
