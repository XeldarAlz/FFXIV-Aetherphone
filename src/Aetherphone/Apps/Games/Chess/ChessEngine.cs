using System.Runtime.CompilerServices;

namespace Aetherphone.Apps.Games.Chess;

[SkipLocalsInit]
internal sealed class ChessEngine
{
    public const int MateScore = 30000;
    private const int Infinity = 40000;
    private const int MaxPly = 40;
    private const int BishopPairBonus = 32;
    private const int EndgameMaterial = 1300;

    private static readonly int[] PieceValues = { 0, 100, 320, 330, 500, 900, 0 };

    private static readonly int[] PawnTable =
    {
        0, 0, 0, 0, 0, 0, 0, 0,
        50, 50, 50, 50, 50, 50, 50, 50,
        10, 10, 20, 30, 30, 20, 10, 10,
        5, 5, 10, 25, 25, 10, 5, 5,
        0, 0, 0, 20, 20, 0, 0, 0,
        5, -5, -10, 0, 0, -10, -5, 5,
        5, 10, 10, -20, -20, 10, 10, 5,
        0, 0, 0, 0, 0, 0, 0, 0,
    };

    private static readonly int[] KnightTable =
    {
        -50, -40, -30, -30, -30, -30, -40, -50,
        -40, -20, 0, 0, 0, 0, -20, -40,
        -30, 0, 10, 15, 15, 10, 0, -30,
        -30, 5, 15, 20, 20, 15, 5, -30,
        -30, 0, 15, 20, 20, 15, 0, -30,
        -30, 5, 10, 15, 15, 10, 5, -30,
        -40, -20, 0, 5, 5, 0, -20, -40,
        -50, -40, -30, -30, -30, -30, -40, -50,
    };

    private static readonly int[] BishopTable =
    {
        -20, -10, -10, -10, -10, -10, -10, -20,
        -10, 0, 0, 0, 0, 0, 0, -10,
        -10, 0, 5, 10, 10, 5, 0, -10,
        -10, 5, 5, 10, 10, 5, 5, -10,
        -10, 0, 10, 10, 10, 10, 0, -10,
        -10, 10, 10, 10, 10, 10, 10, -10,
        -10, 5, 0, 0, 0, 0, 5, -10,
        -20, -10, -10, -10, -10, -10, -10, -20,
    };

    private static readonly int[] RookTable =
    {
        0, 0, 0, 0, 0, 0, 0, 0,
        5, 10, 10, 10, 10, 10, 10, 5,
        -5, 0, 0, 0, 0, 0, 0, -5,
        -5, 0, 0, 0, 0, 0, 0, -5,
        -5, 0, 0, 0, 0, 0, 0, -5,
        -5, 0, 0, 0, 0, 0, 0, -5,
        -5, 0, 0, 0, 0, 0, 0, -5,
        0, 0, 0, 5, 5, 0, 0, 0,
    };

    private static readonly int[] QueenTable =
    {
        -20, -10, -10, -5, -5, -10, -10, -20,
        -10, 0, 0, 0, 0, 0, 0, -10,
        -10, 0, 5, 5, 5, 5, 0, -10,
        -5, 0, 5, 5, 5, 5, 0, -5,
        0, 0, 5, 5, 5, 5, 0, -5,
        -10, 5, 5, 5, 5, 5, 0, -10,
        -10, 0, 5, 0, 0, 0, 0, -10,
        -20, -10, -10, -5, -5, -10, -10, -20,
    };

    private static readonly int[] KingMiddleTable =
    {
        -30, -40, -40, -50, -50, -40, -40, -30,
        -30, -40, -40, -50, -50, -40, -40, -30,
        -30, -40, -40, -50, -50, -40, -40, -30,
        -30, -40, -40, -50, -50, -40, -40, -30,
        -20, -30, -30, -40, -40, -30, -30, -20,
        -10, -20, -20, -20, -20, -20, -20, -10,
        20, 20, 0, 0, 0, 0, 20, 20,
        20, 30, 10, 0, 0, 10, 30, 20,
    };

    private static readonly int[] KingEndTable =
    {
        -50, -40, -30, -20, -20, -30, -40, -50,
        -30, -20, -10, 0, 0, -10, -20, -30,
        -30, -10, 20, 30, 30, 20, -10, -30,
        -30, -10, 30, 40, 40, 30, -10, -30,
        -30, -10, 30, 40, 40, 30, -10, -30,
        -30, -10, 20, 30, 30, 20, -10, -30,
        -30, -30, 0, 0, 0, 0, -30, -30,
        -50, -30, -30, -30, -30, -30, -30, -50,
    };

    private readonly ChessBoard board = new() { TrackHistory = false, };
    private readonly ChessMove[][] moveBuffers = BuildMoveBuffers();
    private readonly int[][] orderBuffers = BuildOrderBuffers();
    private readonly Random random = new();
    private long nodes;
    private long budget;
    private bool stopped;

    public void Prepare(ChessBoard position)
    {
        board.CopyFrom(position);
    }

    public ChessMove Search(int depth, long nodeBudget, int slack)
    {
        Span<ChessMove> moves = stackalloc ChessMove[ChessBoard.MaxMoves];
        Span<int> order = stackalloc int[ChessBoard.MaxMoves];
        Span<int> results = stackalloc int[ChessBoard.MaxMoves];
        var count = board.GenerateMoves(moves);
        if (count == 0)
        {
            return default;
        }

        nodes = 0;
        budget = nodeBudget;
        stopped = false;
        ScoreMoves(moves, order, count);
        var best = -Infinity;
        var bestIndex = 0;
        for (var index = 0; index < count; index++)
        {
            SelectBest(moves, order, index, count);
            var alpha = best == -Infinity ? -Infinity : best - slack;
            board.Make(moves[index], out var undo);
            var score = -Negamax(depth - 1, 1, -Infinity, -alpha);
            board.Unmake(moves[index], undo);
            results[index] = score;
            if (score > best)
            {
                best = score;
                bestIndex = index;
            }

            if (stopped)
            {
                return PickMove(moves, results, index + 1, bestIndex, best, slack);
            }
        }

        return PickMove(moves, results, count, bestIndex, best, slack);
    }

    // Root moves are searched with alpha set to best - slack, so a move that fails low is only
    // known to be no better than that bound. Only scores strictly above it are exact, which is why
    // the best move is carried separately instead of being recovered by comparing against the bound.
    private ChessMove PickMove(ReadOnlySpan<ChessMove> moves, ReadOnlySpan<int> results, int count, int bestIndex,
        int best, int slack)
    {
        if (count <= 0)
        {
            return default;
        }

        var threshold = best - slack;
        var candidates = 1;
        for (var index = 0; index < count; index++)
        {
            if (index != bestIndex && results[index] > threshold)
            {
                candidates++;
            }
        }

        if (candidates == 1)
        {
            return moves[bestIndex];
        }

        var target = random.Next(candidates);
        if (target == 0)
        {
            return moves[bestIndex];
        }

        target--;
        for (var index = 0; index < count; index++)
        {
            if (index == bestIndex || results[index] <= threshold)
            {
                continue;
            }

            if (target == 0)
            {
                return moves[index];
            }

            target--;
        }

        return moves[bestIndex];
    }

    private int Negamax(int depth, int ply, int alpha, int beta)
    {
        if (stopped)
        {
            return 0;
        }

        if (nodes >= budget)
        {
            stopped = true;
            return 0;
        }

        nodes++;
        if (board.HalfmoveClock >= 100 || ply >= MaxPly - 1)
        {
            return 0;
        }

        if (depth <= 0)
        {
            return Quiescence(ply, alpha, beta);
        }

        var moves = moveBuffers[ply].AsSpan();
        var order = orderBuffers[ply].AsSpan();
        var count = board.GenerateMoves(moves);
        if (count == 0)
        {
            return board.InCheck(board.BlackToMove) ? -MateScore + ply : 0;
        }

        ScoreMoves(moves, order, count);
        var best = -Infinity;
        for (var index = 0; index < count; index++)
        {
            SelectBest(moves, order, index, count);
            board.Make(moves[index], out var undo);
            var score = -Negamax(depth - 1, ply + 1, -beta, -alpha);
            board.Unmake(moves[index], undo);
            if (score > best)
            {
                best = score;
            }

            if (best > alpha)
            {
                alpha = best;
            }

            if (alpha >= beta || stopped)
            {
                break;
            }
        }

        return best;
    }

    private int Quiescence(int ply, int alpha, int beta)
    {
        if (stopped)
        {
            return 0;
        }

        if (nodes >= budget)
        {
            stopped = true;
            return 0;
        }

        nodes++;
        var standPat = Evaluate();
        if (standPat >= beta)
        {
            return beta;
        }

        if (standPat > alpha)
        {
            alpha = standPat;
        }

        if (ply >= MaxPly - 1)
        {
            return alpha;
        }

        var moves = moveBuffers[ply].AsSpan();
        var order = orderBuffers[ply].AsSpan();
        var count = board.GenerateCaptures(moves);
        ScoreMoves(moves, order, count);
        for (var index = 0; index < count; index++)
        {
            SelectBest(moves, order, index, count);
            board.Make(moves[index], out var undo);
            var score = -Quiescence(ply + 1, -beta, -alpha);
            board.Unmake(moves[index], undo);
            if (stopped)
            {
                return alpha;
            }

            if (score >= beta)
            {
                return beta;
            }

            if (score > alpha)
            {
                alpha = score;
            }
        }

        return alpha;
    }

    private int Evaluate()
    {
        var score = 0;
        var phaseMaterial = 0;
        var whiteBishops = 0;
        var blackBishops = 0;
        var whiteKing = -1;
        var blackKing = -1;
        for (var square = 0; square < ChessBoard.SquareCount; square++)
        {
            var piece = board.PieceAt(square);
            if (piece == 0)
            {
                continue;
            }

            var black = ChessPiece.IsBlack(piece);
            var type = (int)ChessPiece.Type(piece);
            if (type == (int)ChessPieceType.King)
            {
                if (black)
                {
                    blackKing = square;
                }
                else
                {
                    whiteKing = square;
                }

                continue;
            }

            if (type != (int)ChessPieceType.Pawn)
            {
                phaseMaterial += PieceValues[type];
            }

            if (type == (int)ChessPieceType.Bishop)
            {
                if (black)
                {
                    blackBishops++;
                }
                else
                {
                    whiteBishops++;
                }
            }

            var value = PieceValues[type] + TableFor(type)[black ? square ^ 56 : square];
            score += black ? -value : value;
        }

        var endgame = phaseMaterial <= EndgameMaterial;
        var kingTable = endgame ? KingEndTable : KingMiddleTable;
        if (whiteKing >= 0)
        {
            score += kingTable[whiteKing];
        }

        if (blackKing >= 0)
        {
            score -= kingTable[blackKing ^ 56];
        }

        if (whiteBishops >= 2)
        {
            score += BishopPairBonus;
        }

        if (blackBishops >= 2)
        {
            score -= BishopPairBonus;
        }

        return board.BlackToMove ? -score : score;
    }

    private void ScoreMoves(ReadOnlySpan<ChessMove> moves, Span<int> order, int count)
    {
        for (var index = 0; index < count; index++)
        {
            var move = moves[index];
            var value = 0;
            if (move.Promotion != 0)
            {
                value += 900 + PieceValues[move.Promotion];
            }

            var victim = board.PieceAt(move.To);
            if (victim != 0)
            {
                var attacker = board.PieceAt(move.From);
                value += 1000 + PieceValues[(int)ChessPiece.Type(victim)] * 8 -
                         PieceValues[(int)ChessPiece.Type(attacker)];
            }
            else if ((move.Flags & ChessMoveFlags.EnPassant) != 0)
            {
                value += 1000 + PieceValues[(int)ChessPieceType.Pawn] * 8;
            }

            order[index] = value;
        }
    }

    private static void SelectBest(Span<ChessMove> moves, Span<int> order, int start, int count)
    {
        var best = start;
        for (var index = start + 1; index < count; index++)
        {
            if (order[index] > order[best])
            {
                best = index;
            }
        }

        if (best == start)
        {
            return;
        }

        (moves[start], moves[best]) = (moves[best], moves[start]);
        (order[start], order[best]) = (order[best], order[start]);
    }

    private static int[] TableFor(int type)
    {
        return type switch
        {
            (int)ChessPieceType.Pawn => PawnTable,
            (int)ChessPieceType.Knight => KnightTable,
            (int)ChessPieceType.Bishop => BishopTable,
            (int)ChessPieceType.Rook => RookTable,
            (int)ChessPieceType.Queen => QueenTable,
            _ => KingMiddleTable,
        };
    }

    private static ChessMove[][] BuildMoveBuffers()
    {
        var buffers = new ChessMove[MaxPly][];
        for (var ply = 0; ply < MaxPly; ply++)
        {
            buffers[ply] = new ChessMove[ChessBoard.MaxMoves];
        }

        return buffers;
    }

    private static int[][] BuildOrderBuffers()
    {
        var buffers = new int[MaxPly][];
        for (var ply = 0; ply < MaxPly; ply++)
        {
            buffers[ply] = new int[ChessBoard.MaxMoves];
        }

        return buffers;
    }
}
