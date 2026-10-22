using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class BlackjackProjectionTests
{
    private const string Room = CasinoRoomIds.BlackjackPit;

    private const string Seated = "user-seated";

    [Fact]
    public void MySeatIsTheChairMyOwnAccountIsSittingIn()
    {
        var projection = new BlackjackProjection();
        projection.Apply(State(1, 4, Board(7, new[] { 0, 1 })));
        Assert.Equal(-1, projection.MySeat);

        projection.Watch(Seated);
        Assert.Equal(0, projection.MySeat);

        projection.Watch("someone-else");
        Assert.Equal(-1, projection.MySeat);
    }

    [Fact]
    public void TheActionMaskComesFromTheHandTheBoardIsActuallyOn()
    {
        var projection = new BlackjackProjection();
        projection.Apply(State(1, 4, Board(7, new[] { 0, 1 })));
        Assert.Equal(0, projection.ActionsMask);

        projection.ApplyPersonal(Offered(1, 4, 7, BlackjackRules.ActionHit | BlackjackRules.ActionStand));
        Assert.Equal(BlackjackRules.ActionHit | BlackjackRules.ActionStand, projection.ActionsMask);
        Assert.Equal(0, projection.ActiveHand);

        Assert.True(projection.Apply(State(1, 5, Board(8, new[] { 2, 3 }))));
        Assert.Equal(0, projection.ActionsMask);
        Assert.Equal(-1, projection.ActiveHand);
    }

    [Fact]
    public void TheFirstBoardIsAlwaysTaken()
    {
        var projection = new BlackjackProjection();
        Assert.True(projection.Apply(State(1, 4, Board(7, new[] { 0, 1 }))));
        Assert.NotNull(projection.Board);
        Assert.Equal(1, projection.Epoch);
        Assert.Equal(4, projection.Seq);
    }

    [Fact]
    public void AFrameFromBeforeTheOneOnScreenNeverTouchesTheHand()
    {
        var projection = new BlackjackProjection();
        projection.Apply(State(1, 5, Board(7, new[] { 0, 1 })));

        Assert.False(projection.Apply(State(1, 4, Board(7, new[] { 30, 31, 32 }))));
        Assert.False(projection.Apply(State(1, 5, Board(7, new[] { 30, 31, 32 }))));

        var seat = projection.SeatAt(0);
        Assert.NotNull(seat);
        Assert.Equal(new[] { 0, 1 }, seat!.Hands![0].Cards);
        Assert.Equal(5, projection.Seq);
    }

    [Fact]
    public void AFrameFromAnOlderEpochNeverTouchesTheHand()
    {
        var projection = new BlackjackProjection();
        projection.Apply(State(3, 2, Board(7, new[] { 0, 1 })));

        Assert.False(projection.Apply(State(2, 900, Board(7, new[] { 44, 45 }))));

        var seat = projection.SeatAt(0);
        Assert.Equal(new[] { 0, 1 }, seat!.Hands![0].Cards);
        Assert.Equal(3, projection.Epoch);
    }

    [Fact]
    public void ARestartedTableOutranksEverySequenceItLeftBehind()
    {
        var projection = new BlackjackProjection();
        projection.Apply(State(1, 900, Board(7, new[] { 0, 1 })));

        Assert.True(projection.Apply(State(2, 1, Board(8, new[] { 44, 45 }))));
        Assert.Equal(8, projection.Board!.HandIndex);
        Assert.Equal(2, projection.Epoch);
        Assert.Equal(1, projection.Seq);
    }

    [Fact]
    public void AResetLeavesNothingBehindForTheNextTable()
    {
        var projection = new BlackjackProjection();
        projection.Apply(State(1, 5, Board(7, new[] { 0, 1 })));
        projection.Reset();

        Assert.Null(projection.Board);
        Assert.Null(projection.SeatAt(0));
        Assert.True(projection.Apply(State(0, 0, Board(1, new[] { 2, 3 }))));
    }

    [Fact]
    public void APublicFaceIsNeverSecondGuessedByAPrivateFrame()
    {
        var projection = new BlackjackProjection();
        projection.Apply(State(1, 5, Board(7, new[] { 12, 13 })));
        projection.ApplyPersonal(Personal(1, 5, 7, 0, new[] { 40, 41 }));

        Assert.Equal(12, projection.CardAt(0, 0, 0, 12));
        Assert.Equal(13, projection.CardAt(0, 0, 1, 13));
    }

    [Fact]
    public void MyOwnClosedCardsOpenAndNobodyElseIsOpened()
    {
        var projection = new BlackjackProjection();
        projection.Apply(State(1, 5, Board(7, new[] { PlayingCards.FaceDown, PlayingCards.FaceDown })));
        projection.ApplyPersonal(Personal(1, 5, 7, 0, new[] { 40, 41 }));

        Assert.Equal(40, projection.CardAt(0, 0, 0, PlayingCards.FaceDown));
        Assert.Equal(41, projection.CardAt(0, 0, 1, PlayingCards.FaceDown));
        Assert.Equal(PlayingCards.FaceDown, projection.CardAt(1, 0, 0, PlayingCards.FaceDown));
        Assert.Equal(PlayingCards.FaceDown, projection.CardAt(0, 1, 0, PlayingCards.FaceDown));
        Assert.Equal(PlayingCards.FaceDown, projection.CardAt(0, 0, 5, PlayingCards.FaceDown));
    }

    [Fact]
    public void AnotherSeatsTurnedOverHandShowsItsFacesToEveryone()
    {
        var projection = new BlackjackProjection();
        projection.Apply(State(1, 5, Board(7, new[] { 12, 13 })));

        Assert.Equal(12, projection.CardAt(0, 0, 0, 12));
        Assert.Equal(13, projection.CardAt(0, 0, 1, 13));
        Assert.Equal(PlayingCards.FaceDown, projection.CardAt(0, 0, 2, PlayingCards.FaceDown));
    }

    [Fact]
    public void APrivateFrameFromAnotherRoundOpensNothing()
    {
        var projection = new BlackjackProjection();
        projection.Apply(State(1, 5, Board(8, new[] { PlayingCards.FaceDown, PlayingCards.FaceDown })));
        projection.ApplyPersonal(Personal(1, 5, 7, 0, new[] { 40, 41 }));

        Assert.Equal(PlayingCards.FaceDown, projection.CardAt(0, 0, 0, PlayingCards.FaceDown));
    }

    [Fact]
    public void AStalePrivateFrameCannotPaintTheOldFacesBack()
    {
        var projection = new BlackjackProjection();
        projection.Apply(State(1, 6, Board(7, new[] { PlayingCards.FaceDown, PlayingCards.FaceDown })));
        Assert.True(projection.ApplyPersonal(Personal(1, 6, 7, 0, new[] { 40, 41 })));
        Assert.False(projection.ApplyPersonal(Personal(1, 5, 7, 0, new[] { 2, 3 })));
        Assert.False(projection.ApplyPersonal(Personal(0, 99, 7, 0, new[] { 2, 3 })));

        Assert.Equal(40, projection.CardAt(0, 0, 0, PlayingCards.FaceDown));
    }

    [Fact]
    public void APrivateFrameFromTheTableThatRestartedOpensNothing()
    {
        var projection = new BlackjackProjection();
        projection.Apply(State(1, 5, Board(7, new[] { PlayingCards.FaceDown, PlayingCards.FaceDown })));
        Assert.True(projection.ApplyPersonal(Personal(1, 6, 7, 0, new[] { 40, 41 })));
        Assert.Equal(40, projection.CardAt(0, 0, 0, PlayingCards.FaceDown));

        Assert.True(projection.Apply(State(2, 1, Board(7, new[] { PlayingCards.FaceDown, PlayingCards.FaceDown }))));
        Assert.False(projection.ApplyPersonal(Personal(1, 4, 7, 0, new[] { 40, 41 })));

        Assert.Equal(PlayingCards.FaceDown, projection.CardAt(0, 0, 0, PlayingCards.FaceDown));
        Assert.Equal(PlayingCards.FaceDown, projection.CardAt(0, 0, 1, PlayingCards.FaceDown));

        Assert.True(projection.ApplyPersonal(Personal(2, 2, 7, 0, new[] { 12, 13 })));
        Assert.Equal(12, projection.CardAt(0, 0, 0, PlayingCards.FaceDown));
    }

    [Fact]
    public void APrivateFrameThatIsNotBlackjackIsNotAPrivateFrame()
    {
        var projection = new BlackjackProjection();
        projection.Apply(State(1, 5, Board(7, new[] { PlayingCards.FaceDown })));

        Assert.False(projection.ApplyPersonal(null));
        Assert.False(projection.ApplyPersonal(new CasinoRoomPrivate(Room, 1, 5,
            new CasinoPrivateDto("casino.other", "{}"), null)));
        Assert.Equal(PlayingCards.FaceDown, projection.CardAt(0, 0, 0, PlayingCards.FaceDown));
    }

    [Fact]
    public void TheCardCountIsEveryCardOnTheFelt()
    {
        var board = Board(7, new[] { 0, 1 });
        Assert.Equal(4, BlackjackProjection.CardCount(board));
        Assert.Equal(0, BlackjackProjection.CardCount(null));
    }

    [Fact]
    public void ARoomWithoutABlackjackBlobIsNotABlackjackBoard()
    {
        var projection = new BlackjackProjection();
        Assert.False(projection.Apply(null));
        Assert.False(projection.Apply(new CasinoRoomState(Room, 1, 1,
            new CasinoRoomSnapshotDto(RoomId: Room, GameKind: CasinoWire.BlackjackKind), null, null, null)));
        Assert.Null(projection.Board);
    }

    private static CasinoRoomPrivate Offered(int epoch, long seq, long handIndex, int mask)
    {
        var payload = new CasinoBlackjackYouDto(HandId: "hand-" + handIndex, SeatIndex: 0, ActiveHand: 0,
            ActionsMask: mask, Hands: new[] { new CasinoBlackjackHandDto(Cards: new[] { 0, 1 }) });
        return new CasinoRoomPrivate(Room, epoch, seq,
            new CasinoPrivateDto(CasinoWire.BlackjackHandEvent, string.Empty), payload);
    }

    private static CasinoBlackjackRoomStateDto Board(long handIndex, int[] seatCards)
    {
        var hand = new CasinoBlackjackHandDto(Cards: seatCards, Bet: 10);
        var seat = new CasinoBlackjackSeatDto(0, Seated, "Seat", 500, BlackjackSeatStates.Seated, true, false,
            false, 10, 0, new[] { hand });
        return new CasinoBlackjackRoomStateDto(
            HandId: "hand-" + handIndex,
            HandIndex: handIndex,
            Seats: new[] { seat },
            DealerCards: new[] { 20, PlayingCards.FaceDown });
    }

    private static CasinoRoomState State(int epoch, long seq, CasinoBlackjackRoomStateDto board)
    {
        var snapshot = new CasinoRoomSnapshotDto(
            RoomId: Room,
            GameKind: CasinoWire.BlackjackKind,
            Epoch: epoch,
            Seq: seq,
            RoundIndex: board.HandIndex);
        return new CasinoRoomState(Room, epoch, seq, snapshot, null, null, board);
    }

    private static CasinoRoomPrivate Personal(int epoch, long seq, long handIndex, int seatIndex, int[] cards)
    {
        var payload = new CasinoBlackjackYouDto(HandId: "hand-" + handIndex, SeatIndex: seatIndex,
            Hands: new[] { new CasinoBlackjackHandDto(Cards: cards) });
        return new CasinoRoomPrivate(Room, epoch, seq,
            new CasinoPrivateDto(CasinoWire.BlackjackHandEvent, string.Empty), payload);
    }
}
