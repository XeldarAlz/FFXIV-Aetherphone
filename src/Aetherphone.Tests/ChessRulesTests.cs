using Aetherphone.Apps.Games.Chess;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ChessRulesTests
{
    private const int E1 = 60;
    private const int E2 = 52;
    private const int E4 = 36;
    private const int E5 = 28;
    private const int E7 = 12;
    private const int D5 = 27;
    private const int D6 = 19;
    private const int D7 = 11;
    private const int D8 = 3;
    private const int C7 = 10;
    private const int B8 = 1;
    private const int A6 = 16;
    private const int A5 = 24;
    private const int A7 = 8;
    private const int F1 = 61;
    private const int F2 = 53;
    private const int F3 = 45;
    private const int F6 = 21;
    private const int F8 = 5;
    private const int G1 = 62;
    private const int G2 = 54;
    private const int G4 = 38;
    private const int G8 = 6;
    private const int H1 = 63;
    private const int H4 = 39;
    private const int C4 = 34;
    private const int C5 = 26;
    private const int C6 = 18;

    [Theory]
    [InlineData(1, 20)]
    [InlineData(2, 400)]
    [InlineData(3, 8902)]
    [InlineData(4, 197281)]
    [InlineData(5, 4865609)]
    public void PerftFromTheStartingPositionMatchesKnownCounts(int depth, long expected)
    {
        var board = new ChessBoard { TrackHistory = false, };
        board.Reset();

        Assert.Equal(expected, Perft(board, depth));
    }

    [Fact]
    public void EnPassantCaptureRemovesTheAdjacentPawn()
    {
        var board = new ChessBoard();
        board.Reset();
        Play(board, E2, E4);
        Play(board, A7, A6);
        Play(board, E4, E5);
        Play(board, D7, D5);

        Assert.True(board.TryFindMove(E5, D6, ChessPieceType.None, out var capture));
        Assert.True((capture.Flags & ChessMoveFlags.EnPassant) != 0);
        board.Make(capture, out var undo);

        Assert.Equal(ChessPieceType.Pawn, ChessPiece.Type(board.PieceAt(D6)));
        Assert.False(ChessPiece.IsBlack(board.PieceAt(D6)));
        Assert.Equal(0, board.PieceAt(D5));

        board.Unmake(capture, undo);

        Assert.Equal(ChessPieceType.Pawn, ChessPiece.Type(board.PieceAt(D5)));
        Assert.True(ChessPiece.IsBlack(board.PieceAt(D5)));
        Assert.Equal(0, board.PieceAt(D6));
    }

    [Fact]
    public void CastlingShortMovesTheKingAndTheRook()
    {
        var board = new ChessBoard();
        board.Reset();
        Play(board, E2, E4);
        Play(board, E7, E5);
        Play(board, G1, F3);
        Play(board, B8, C6);
        Play(board, F1, C4);
        Play(board, F8, C5);

        Assert.True(board.TryFindMove(E1, G1, ChessPieceType.None, out var castle));
        Assert.True((castle.Flags & ChessMoveFlags.CastleKing) != 0);
        board.Make(castle, out var undo);

        Assert.Equal(ChessPieceType.King, ChessPiece.Type(board.PieceAt(G1)));
        Assert.Equal(ChessPieceType.Rook, ChessPiece.Type(board.PieceAt(F1)));
        Assert.Equal(0, board.PieceAt(H1));
        Assert.Equal(0, board.PieceAt(E1));

        board.Unmake(castle, undo);

        Assert.Equal(ChessPieceType.King, ChessPiece.Type(board.PieceAt(E1)));
        Assert.Equal(ChessPieceType.Rook, ChessPiece.Type(board.PieceAt(H1)));
        Assert.Equal(0, board.PieceAt(G1));
        Assert.Equal(0, board.PieceAt(F1));
    }

    [Fact]
    public void PromotionReplacesThePawnWithTheChosenPiece()
    {
        var board = new ChessBoard();
        board.Reset();
        Play(board, E2, E4);
        Play(board, D7, D5);
        Play(board, E4, D5);
        Play(board, G8, F6);
        Play(board, D5, D6);
        Play(board, A7, A6);
        Play(board, D6, C7);
        Play(board, A6, A5);

        Assert.True(board.NeedsPromotion(C7, B8));
        Assert.True(board.TryFindMove(C7, B8, ChessPieceType.Knight, out var promotion));
        board.Make(promotion, out var undo);

        Assert.Equal(ChessPieceType.Knight, ChessPiece.Type(board.PieceAt(B8)));
        Assert.False(ChessPiece.IsBlack(board.PieceAt(B8)));

        board.Unmake(promotion, undo);

        Assert.Equal(ChessPieceType.Pawn, ChessPiece.Type(board.PieceAt(C7)));
        Assert.Equal(ChessPieceType.Knight, ChessPiece.Type(board.PieceAt(B8)));
        Assert.True(ChessPiece.IsBlack(board.PieceAt(B8)));
    }

    [Fact]
    public void FoolsMateIsReportedAsCheckmate()
    {
        var board = new ChessBoard();
        board.Reset();
        Play(board, F2, F3);
        Play(board, E7, E5);
        Play(board, G2, G4);
        Play(board, D8, H4);

        var outcome = board.Evaluate(out var legalCount);

        Assert.Equal(ChessOutcome.Checkmate, outcome);
        Assert.Equal(0, legalCount);
        Assert.True(board.InCheck(false));
    }

    [Fact]
    public void UnmakeRestoresTheExactPositionAfterEveryOpeningMove()
    {
        var board = new ChessBoard();
        board.Reset();
        var before = Snapshot(board);
        Span<ChessMove> moves = stackalloc ChessMove[ChessBoard.MaxMoves];
        var count = board.GenerateMoves(moves);

        for (var index = 0; index < count; index++)
        {
            board.Make(moves[index], out var undo);
            board.Unmake(moves[index], undo);
            Assert.Equal(before, Snapshot(board));
        }
    }

    [Fact]
    public void TheEngineFindsAMateInOne()
    {
        var board = new ChessBoard();
        board.Reset();
        Play(board, E2, E4);
        Play(board, E7, E5);
        Play(board, F1, C4);
        Play(board, B8, C6);
        Play(board, D1, H5);
        Play(board, G8, F6);

        var engine = new ChessEngine();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            engine.Prepare(board);
            var move = engine.Search(3, 2_000_000, 0);

            Assert.Equal(H5, move.From);
            Assert.Equal(F7, move.To);
        }
    }

    private const int D1 = 59;
    private const int H5 = 31;
    private const int F7 = 13;

    private static void Play(ChessBoard board, int from, int to)
    {
        Assert.True(board.TryFindMove(from, to, ChessPieceType.None, out var move));
        board.Make(move, out _);
    }

    private static string Snapshot(ChessBoard board)
    {
        var builder = new System.Text.StringBuilder(80);
        for (var square = 0; square < ChessBoard.SquareCount; square++)
        {
            builder.Append((char)('0' + board.PieceAt(square)));
        }

        builder.Append(board.BlackToMove ? 'b' : 'w');
        builder.Append((char)('0' + board.HalfmoveClock));
        return builder.ToString();
    }

    private static long Perft(ChessBoard board, int depth)
    {
        Span<ChessMove> moves = stackalloc ChessMove[ChessBoard.MaxMoves];
        var count = board.GenerateMoves(moves);
        if (depth <= 1)
        {
            return count;
        }

        long total = 0;
        for (var index = 0; index < count; index++)
        {
            board.Make(moves[index], out var undo);
            total += Perft(board, depth - 1);
            board.Unmake(moves[index], undo);
        }

        return total;
    }
}
