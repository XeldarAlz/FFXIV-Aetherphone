using System.Numerics;
using Aetherphone.Apps.Casino.Cabinets;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class BingoPlaybackTests
{
    private const float Frame = 0.016f;

    private static int[] SequentialCard()
    {
        var card = new int[BingoRules.CardNumbers];
        for (var slot = 0; slot < BingoRules.CardNumbers; slot++)
        {
            var cell = BingoRules.CardCells[slot];
            card[slot] = BingoRules.ColumnFloorFor(cell / BingoRules.Columns) + cell % BingoRules.Columns;
        }

        return card;
    }

    private static CasinoRoomSnapshotDto Snapshot(int phase = CasinoRoomPhases.Locked) =>
        new(RoomId: CasinoRoomIds.BingoHall, GameKind: CasinoWire.BingoKind, Phase: phase, RoundIndex: 7);

    private static CasinoBingoRoomStateDto Board(int[] balls, params CasinoBingoStageDto[] stages) =>
        new(RoundIndex: 7, Cards: 1, Balls: balls, BallIndex: balls.Length, Stages: stages);

    private static CasinoBingoCardsDto Mine(params int[][] cards) =>
        new(Granted: true, RoomId: CasinoRoomIds.BingoHall, RoundIndex: 7, RoundId: "entry-1", Cards: cards,
            Stake: BingoRules.StakeFor(cards.Length), RoundState: CasinoRoundStates.Open);

    [Fact]
    public void JoiningMidGameJumpsToTheCalledBallsWithoutReplayingThem()
    {
        var playback = new BingoRoundPlayback();
        var balls = new[] { 1, 2, 3, 4, 40, 61 };

        playback.Update(Snapshot(), Board(balls), Mine(SequentialCard()), Frame);

        Assert.False(playback.CalledLive);
        Assert.False(playback.Flying);
        Assert.Equal(61, playback.LatestBall);
        Assert.True(playback.IsLit(61));
        Assert.Equal(playback.AutoMaskOf(0), playback.StampedMaskOf(0));
        Assert.Equal(0f, playback.PopOf(0, BingoRules.CellForSlot(0)));
        Assert.Equal(BingoCue.None, playback.TakeCues());
    }

    [Fact]
    public void ALiveBallPopsFliesAndLandsBeforeTheBoardLightsIt()
    {
        var playback = new BingoRoundPlayback();
        var mine = Mine(SequentialCard());
        playback.Update(Snapshot(), Board(new[] { 1 }), mine, Frame);

        playback.Update(Snapshot(), Board(new[] { 1, 2 }), mine, Frame);
        Assert.True(playback.Flying);
        Assert.False(playback.IsLit(2));
        Assert.True(playback.IsLit(1));
        Assert.Equal(BingoCue.BallPopped, playback.TakeCues());
        Assert.False(BingoRules.IsMarked(playback.VisibleMaskOf(0), BingoRules.CellForSlot(1)));

        playback.Update(Snapshot(), Board(new[] { 1, 2 }), mine, BingoRoundPlayback.FlightSeconds);
        Assert.False(playback.Flying);
        Assert.True(playback.IsLit(2));
        Assert.True(playback.CalledLive);
        Assert.Equal(BingoCue.BallLanded, playback.TakeCues() & BingoCue.BallLanded);
    }

    [Fact]
    public void ABurstOfBallsAfterAGapSnapsInsteadOfFlying()
    {
        var playback = new BingoRoundPlayback();
        var mine = Mine(SequentialCard());
        playback.Update(Snapshot(), Board(new[] { 1 }), mine, Frame);

        playback.Update(Snapshot(), Board(new[] { 1, 2, 3, 4 }), mine, Frame);

        Assert.False(playback.Flying);
        Assert.True(playback.IsLit(4));
        Assert.Equal(BingoCue.None, playback.TakeCues() & BingoCue.BallPopped);
    }

    [Fact]
    public void AutoDaubStampsAfterTheLandingWithAPop()
    {
        var playback = new BingoRoundPlayback();
        var mine = Mine(SequentialCard());
        var cell = BingoRules.CellForSlot(1);
        playback.Update(Snapshot(), Board(new[] { 1 }), mine, Frame);
        playback.Update(Snapshot(), Board(new[] { 1, 2 }), mine, Frame);
        playback.Update(Snapshot(), Board(new[] { 1, 2 }), mine, BingoRoundPlayback.FlightSeconds);
        Assert.False(BingoRules.IsMarked(playback.StampedMaskOf(0), cell));
        playback.TakeCues();

        playback.Update(Snapshot(), Board(new[] { 1, 2 }), mine, BingoRoundPlayback.StampDelaySeconds);

        Assert.True(BingoRules.IsMarked(playback.StampedMaskOf(0), cell));
        Assert.True(playback.PopOf(0, cell) > 0f);
        Assert.Equal(BingoCue.Daubed, playback.TakeCues() & BingoCue.Daubed);
    }

    [Fact]
    public void ManualDaubWaitsForATapButTheResultStillShowsTheTruth()
    {
        var playback = new BingoRoundPlayback();
        var mine = Mine(SequentialCard());
        var cell = BingoRules.CellForSlot(1);
        playback.Update(Snapshot(), Board(new[] { 1 }), mine, Frame, true);
        playback.Update(Snapshot(), Board(new[] { 1, 2 }), mine, Frame, true);
        playback.Update(Snapshot(), Board(new[] { 1, 2 }), mine, BingoRoundPlayback.StampAfterSeconds * 3f, true);

        Assert.False(BingoRules.IsMarked(playback.StampedMaskOf(0), cell));
        Assert.True(BingoRules.IsMarked(playback.AutoMaskOf(0), cell));

        playback.Update(Snapshot(CasinoRoomPhases.Result), Board(new[] { 1, 2 }), mine, Frame, true);

        Assert.True(BingoRules.IsMarked(playback.StampedMaskOf(0), cell));
    }

    [Fact]
    public void OneAwayIsCuedWhenTheBallThatMakesItLands()
    {
        var playback = new BingoRoundPlayback();
        var mine = Mine(SequentialCard());
        playback.Update(Snapshot(), Board(new[] { 1, 2, 3 }), mine, Frame);
        playback.Update(Snapshot(), Board(new[] { 1, 2, 3, 4 }), mine, Frame);
        Assert.False(playback.OneAway(0));

        playback.Update(Snapshot(), Board(new[] { 1, 2, 3, 4 }), mine, BingoRoundPlayback.FlightSeconds);

        Assert.True(playback.OneAway(0));
        Assert.Equal(BingoCue.OneAway, playback.TakeCues() & BingoCue.OneAway);
    }

    [Fact]
    public void AStageIsMineOnlyWhenOneOfMyCardsReachedItOnTheAwardedBall()
    {
        var playback = new BingoRoundPlayback();
        var mine = Mine(SequentialCard());
        var balls = new[] { 1, 2, 3, 4, 5 };
        playback.Update(Snapshot(), Board(new[] { 1, 2, 3, 4 }), mine, Frame);

        playback.Update(Snapshot(), Board(balls, new CasinoBingoStageDto(BingoRules.StageLine, 5, 180, 1, 180)),
            mine, Frame);

        Assert.True(playback.WonStage(BingoRules.StageLine));
        Assert.False(playback.WonStage(BingoRules.StageFullHouse));
        Assert.Equal(BingoCue.StageWon, playback.TakeCues() & BingoCue.StageWon);

        var other = new BingoRoundPlayback();
        other.Update(Snapshot(), Board(balls, new CasinoBingoStageDto(BingoRules.StageLine, 4, 180, 1, 180)), mine,
            Frame);
        Assert.False(other.WonStage(BingoRules.StageLine));
    }

    [Fact]
    public void TheHeroFollowsTheBestCardUntilThePlayerPromotesOne()
    {
        var playback = new BingoRoundPlayback();
        var plain = SequentialCard();
        var lucky = SequentialCard();
        (lucky[0], lucky[23]) = (lucky[23], lucky[0]);
        var mine = Mine(lucky, plain);
        playback.Update(Snapshot(), Board(new[] { 1, 2, 3, 4 }), mine, Frame);
        Assert.Equal(1, playback.HeroIndex);

        playback.Promote(0);
        Assert.Equal(0, playback.HeroIndex);

        playback.Promote(5);
        Assert.Equal(0, playback.HeroIndex);
    }

    [Fact]
    public void SnapToTruthLandsTheBallAndClearsThePops()
    {
        var playback = new BingoRoundPlayback();
        var mine = Mine(SequentialCard());
        playback.Update(Snapshot(), Board(new[] { 1 }), mine, Frame);
        playback.Update(Snapshot(), Board(new[] { 1, 2 }), mine, Frame);
        Assert.True(playback.Flying);

        playback.Snap();
        playback.Update(Snapshot(), Board(new[] { 1, 2 }), mine, Frame);

        Assert.False(playback.Flying);
        Assert.Equal(playback.AutoMaskOf(0), playback.StampedMaskOf(0));
    }

    [Fact]
    public void TheCallThatReachesAStageIsCountedFromTheFirstBall()
    {
        var card = SequentialCard();
        var balls = new[] { 70, 1, 2, 3, 4, 5, 16, 17, 18, 19, 20 };
        Assert.Equal(6, BingoRules.CallReaching(card, balls, BingoRules.StageLine));
        Assert.Equal(11, BingoRules.CallReaching(card, balls, BingoRules.StageTwoLines));
        Assert.Equal(0, BingoRules.CallReaching(card, balls, BingoRules.StageFullHouse));
        Assert.Equal(0, BingoRules.CallReaching(null, balls, BingoRules.StageLine));
        Assert.True(BingoRules.IsEarlyBird(45));
        Assert.False(BingoRules.IsEarlyBird(46));
        Assert.False(BingoRules.IsEarlyBird(0));
    }

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = new BingoTumbler(42);
        var second = new BingoTumbler(42);
        for (var frame = 0; frame < 240; frame++)
        {
            first.Update(Frame);
            second.Update(Frame);
            if (frame == 90)
            {
                Assert.Equal(first.Pop(0.5f), second.Pop(0.5f));
            }
        }

        for (var ball = 0; ball < BingoTumbler.BallCount; ball++)
        {
            Assert.Equal(first.BallPosition(ball), second.BallPosition(ball));
            Assert.Equal(first.TintOf(ball), second.TintOf(ball));
        }
    }

    [Fact]
    public void TheTumblerKeepsEveryBallInsideTheDrum()
    {
        var tumbler = new BingoTumbler();
        for (var frame = 0; frame < 600; frame++)
        {
            tumbler.Update(Frame);
        }

        for (var ball = 0; ball < BingoTumbler.BallCount; ball++)
        {
            Assert.True(tumbler.BallPosition(ball).Length() < BingoTumbler.DrumRadius);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(6)]
    public void TheHallFitsInsideTheSafeRect(int cards)
    {
        var safe = new Rect(new Vector2(10f, 100f), new Vector2(370f, 560f));
        var layout = BingoHallLayout.Compute(safe, cards, 1f);

        Assert.True(Inside(safe, layout.Tumbler));
        Assert.True(Inside(safe, layout.Caller));
        Assert.True(Inside(safe, layout.Board));
        Assert.True(Inside(safe, layout.Hero));
        Assert.True(Inside(safe, layout.Podiums));
        Assert.True(layout.Board.Max.Y <= layout.Cards.Min.Y);
        Assert.True(layout.Hero.Max.Y <= layout.Podiums.Min.Y);
        Assert.Equal(cards > 1, layout.HasRail);
        if (layout.HasRail)
        {
            Assert.True(Inside(safe, layout.Rail));
            Assert.True(layout.Hero.Max.X <= layout.Rail.Min.X);
        }
    }

    [Fact]
    public void EveryBallHasItsOwnCellOnTheCallBoard()
    {
        var board = new Rect(Vector2.Zero, new Vector2(320f, 100f));
        var seen = new HashSet<Vector2>();
        for (var ball = 1; ball <= BingoRules.Balls; ball++)
        {
            var center = BingoHallLayout.BoardCellCenter(board, 20f, ball);
            Assert.True(seen.Add(center));
            Assert.True(center.X > board.Min.X + 20f && center.X < board.Max.X);
            Assert.True(center.Y > board.Min.Y && center.Y < board.Max.Y);
        }
    }

    private static bool Inside(Rect outer, Rect inner) =>
        inner.Min.X >= outer.Min.X - 0.01f && inner.Min.Y >= outer.Min.Y - 0.01f
        && inner.Max.X <= outer.Max.X + 0.01f && inner.Max.Y <= outer.Max.Y + 0.01f;
}
