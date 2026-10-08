using Aetherphone.Apps.Casino.Venue;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class VenuePlaybackTests
{
    private static CasinoVenueRollDto Roll(long seq, long value, string userId = "u1", long bound = 1000) =>
        new(seq, userId, userId == "u1" ? "Mira" : "Rhan", bound, value, seq * 1000, "aa");

    private static CasinoDiceTableStateDto Dice(params CasinoVenueRollDto[] newestFirst) =>
        new(Rolls: newestFirst, LastSeq: newestFirst.Length == 0 ? 0 : newestFirst[0].Seq);

    [Fact]
    public void JoiningMidLogShowsTheNewestRollWithoutATumble()
    {
        var playback = new DiceTablePlayback();
        playback.Update(Dice(Roll(5, 777), Roll(4, 12)), 0.016f, false);
        Assert.False(playback.Tumbling);
        Assert.Equal(777, playback.DisplayValue);
        Assert.Equal(5, playback.HeroSeq);
    }

    [Fact]
    public void ANewRollTumblesThenLandsOnTheServerValue()
    {
        var playback = new DiceTablePlayback();
        playback.Update(Dice(Roll(5, 777)), 0.016f, false);
        playback.Update(Dice(Roll(6, 42), Roll(5, 777)), 0.016f, false);
        Assert.True(playback.Tumbling);
        playback.Update(Dice(Roll(6, 42), Roll(5, 777)), DiceTablePlayback.TumbleSeconds, false);
        Assert.False(playback.Tumbling);
        Assert.Equal(42, playback.DisplayValue);
    }

    [Fact]
    public void MyRollTumblesUntilTheAnswerArrivesAndNeverInventsAResult()
    {
        var playback = new DiceTablePlayback();
        playback.Update(Dice(Roll(5, 777)), 0.016f, false);
        playback.BeginMine(1000);
        playback.Update(Dice(Roll(5, 777)), 2f, false);
        Assert.True(playback.AwaitingMine);
        Assert.True(playback.Tumbling);
        playback.Update(Dice(Roll(6, 3), Roll(5, 777)), 2f, false);
        Assert.False(playback.AwaitingMine);
        playback.Update(Dice(Roll(6, 3), Roll(5, 777)), DiceTablePlayback.TumbleSeconds, false);
        Assert.Equal(3, playback.DisplayValue);
    }

    [Fact]
    public void ARoundClosedBeforeIJoinedIsNotCelebrated()
    {
        var closed = new CasinoDiceRoundDto(Index: 3, WinnerUserId: "u1", Closed: true);
        var playback = new DiceTablePlayback();
        playback.Update(Dice(Roll(5, 777)) with { LastRound = closed }, 0.016f, false);
        Assert.False(playback.TakeRoundClose(out _));
        var next = new CasinoDiceRoundDto(Index: 4, WinnerUserId: "u1", Closed: true);
        playback.Update(Dice(Roll(5, 777)) with { LastRound = next }, 0.016f, false);
        Assert.True(playback.TakeRoundClose(out var index));
        Assert.Equal(4, index);
        Assert.False(playback.TakeRoundClose(out _));
    }

    [Fact]
    public void TheRoundLeaderCountsOnlyEachPlayersFirstRoll()
    {
        var round = new CasinoDiceRoundDto(Index: 1, OpenedSeq: 10);
        var rolls = new[] { Roll(14, 999, "u2"), Roll(13, 500), Roll(12, 300, "u2"), Roll(11, 400), Roll(9, 1000) };
        var leader = DiceRounds.Leader(rolls, round);
        Assert.NotNull(leader);
        Assert.Equal(11, leader!.Seq);
    }

    [Fact]
    public void TheTumbleIsDeterministic()
    {
        Assert.Equal(VenueTumble.Value(7, 3, 1000), VenueTumble.Value(7, 3, 1000));
        Assert.InRange(VenueTumble.Value(7, 3, 1000), 1, 1000);
        Assert.Equal(1, VenueTumble.Value(7, 3, 1));
    }

    private static CasinoDeathrollDuelDto Duel(int phase, long current, string turn, params CasinoVenueRollDto[] rolls) =>
        new(Seq: 3, ChallengerUserId: "u1", ChallengerName: "Mira", OpponentUserId: "u2", OpponentName: "Rhan",
            Stake: 10_000, Phase: phase, TurnUserId: turn, Current: current, Rolls: rolls);

    [Fact]
    public void ADeathrollJoinedMidDuelSnapsToTheCurrentNumber()
    {
        var playback = new DeathrollPlayback();
        var board = new CasinoDeathrollStateDto(Duel: Duel(DuelPhases.Live, 412, "u1", Roll(5, 412, "u2")));
        playback.Update(board, 0.016f, false);
        Assert.False(playback.Rolling);
        Assert.Equal(412, playback.Shown);
    }

    [Fact]
    public void ANewDeathrollRollCountsDownThenLands()
    {
        var playback = new DeathrollPlayback();
        playback.Update(new CasinoDeathrollStateDto(Duel: Duel(DuelPhases.Live, 1000, "u1")), 0.016f, false);
        var rolled = new CasinoDeathrollStateDto(Duel: Duel(DuelPhases.Live, 412, "u2", Roll(5, 412, "u1")));
        playback.Update(rolled, 0.016f, false);
        Assert.True(playback.Rolling);
        Assert.InRange(playback.Shown, 1, 1000);
        playback.Update(rolled, DeathrollPlayback.RollSeconds, false);
        Assert.Equal(412, playback.Shown);
    }

    [Fact]
    public void TheFinalOneIsReportedOnceWhenTheDuelMovesToRecent()
    {
        var playback = new DeathrollPlayback();
        playback.Update(new CasinoDeathrollStateDto(Duel: Duel(DuelPhases.Live, 2, "u2", Roll(6, 2, "u1"))), 0.016f,
            false);
        var finished = Duel(DuelPhases.Finished, 1, string.Empty, Roll(6, 2, "u1"), Roll(7, 1, "u2"))
            with { LoserUserId = "u2" };
        var board = new CasinoDeathrollStateDto(Recent: new[] { finished });
        playback.Update(board, 0.016f, false);
        Assert.False(playback.TakeFinish(out _));
        playback.Update(board, DeathrollPlayback.RollSeconds, false);
        Assert.True(playback.TakeFinish(out var loser));
        Assert.Equal("u2", loser);
        Assert.False(playback.TakeFinish(out _));
    }

    private static CasinoRaffleDto Drawn(string seed) =>
        new(Seq: 2, Title: "Bar tab", Winners: 2, Tickets: 6,
            Entrants: new[]
            {
                new CasinoRaffleEntrantDto("u1", "Mira", 3),
                new CasinoRaffleEntrantDto("u2", "Rhan", 1),
                new CasinoRaffleEntrantDto("u3", "Ysol", 2),
            },
            Drawn: true, DrawSeq: 30, Seed: seed,
            WinnersDrawn: new[] { new CasinoRaffleEntrantDto("u3", "Ysol", 2), new CasinoRaffleEntrantDto("u1", "Mira", 3) });

    private static List<float> Replay(string seed)
    {
        var playback = new RafflePlayback();
        var live = new CasinoRaffleStateDto(Raffle: Drawn(seed) with { Drawn = false, WinnersDrawn = null });
        playback.Update(live, 0.016f, false);
        var after = new CasinoRaffleStateDto(Last: Drawn(seed));
        var angles = new List<float>();
        for (var frame = 0; frame < 600; frame++)
        {
            playback.Update(after, 0.033f, false);
            angles.Add(playback.Angle);
        }

        Assert.Equal(RaffleStage.Done, playback.Stage);
        Assert.Equal(2, playback.WinnersShown);
        Assert.Equal(2, playback.WinnerEntrant(0));
        Assert.Equal(0, playback.WinnerEntrant(1));
        return angles;
    }

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        Assert.Equal(Replay("abcdef01"), Replay("abcdef01"));
        Assert.NotEqual(Replay("abcdef01"), Replay("12345678"));
    }

    [Fact]
    public void TheSpinLandsInsideTheWinnersSegment()
    {
        var playback = new RafflePlayback();
        playback.Update(new CasinoRaffleStateDto(Raffle: Drawn("ff") with { Drawn = false }), 0.016f, false);
        var after = new CasinoRaffleStateDto(Last: Drawn("ff"));
        playback.Update(after, 0.016f, false);
        while (playback.Stage == RaffleStage.Spinning)
        {
            playback.Update(after, 0.05f, false);
        }

        var pointer = (-playback.Angle / (MathF.PI * 2f)) % 1f;
        if (pointer < 0f)
        {
            pointer += 1f;
        }

        var landed = -1;
        for (var segment = 0; segment < playback.SegmentCount; segment++)
        {
            if (pointer >= playback.StartOf(segment) && pointer < playback.EndOf(segment))
            {
                landed = playback.OwnerOf(segment);
            }
        }

        Assert.Equal(2, landed);
    }

    [Fact]
    public void ADrawThatHappenedBeforeIJoinedShowsTheResultWithoutASpin()
    {
        var playback = new RafflePlayback();
        playback.Update(new CasinoRaffleStateDto(Last: Drawn("ff")), 0.016f, false);
        Assert.Equal(RaffleStage.Done, playback.Stage);
        Assert.Equal(2, playback.WinnersShown);
        Assert.False(playback.TakeFinish(out _));
    }

    [Fact]
    public void TheRaffleComposerSendsMinutesAsSeconds()
    {
        var draft = RaffleComposer.Build(" Bar tab ", 3, 1, 10, 500, 1_000_000, false);
        Assert.Equal(VenueActions.RaffleOpen, draft.Action);
        Assert.Equal("Bar tab", draft.Title);
        Assert.Equal(600, draft.DurationSeconds);
        Assert.Equal(0, draft.TicketPrice);
        Assert.Equal(0, draft.Prize);
        Assert.Equal(1_000_000, RaffleComposer.Build("Pot", 1, 1, 1, 0, 1_000_000, true).Prize);
    }
}
