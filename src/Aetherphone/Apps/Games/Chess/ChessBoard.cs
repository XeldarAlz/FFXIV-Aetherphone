using System.Runtime.CompilerServices;

namespace Aetherphone.Apps.Games.Chess;

internal enum ChessPieceType : byte
{
    None = 0,
    Pawn = 1,
    Knight = 2,
    Bishop = 3,
    Rook = 4,
    Queen = 5,
    King = 6,
}

[Flags]
internal enum ChessMoveFlags : byte
{
    None = 0,
    Capture = 1,
    DoublePush = 2,
    EnPassant = 4,
    CastleKing = 8,
    CastleQueen = 16,
}

internal enum ChessOutcome : byte
{
    Ongoing,
    Checkmate,
    Stalemate,
    FiftyMove,
    Repetition,
    InsufficientMaterial,
}

internal readonly struct ChessMove : IEquatable<ChessMove>
{
    public readonly byte From;
    public readonly byte To;
    public readonly byte Promotion;
    public readonly ChessMoveFlags Flags;

    public ChessMove(int from, int to, ChessMoveFlags flags, ChessPieceType promotion = ChessPieceType.None)
    {
        From = (byte)from;
        To = (byte)to;
        Promotion = (byte)promotion;
        Flags = flags;
    }

    public bool IsNone => From == To;

    public bool Equals(ChessMove other) =>
        From == other.From && To == other.To && Promotion == other.Promotion;

    public override bool Equals(object? obj) => obj is ChessMove other && Equals(other);

    public override int GetHashCode() => From | (To << 8) | (Promotion << 16);
}

internal readonly struct ChessUndo
{
    public readonly byte Captured;
    public readonly byte Castling;
    public readonly sbyte EnPassant;
    public readonly byte HalfmoveClock;
    public readonly ulong Hash;

    public ChessUndo(byte captured, byte castling, sbyte enPassant, byte halfmoveClock, ulong hash)
    {
        Captured = captured;
        Castling = castling;
        EnPassant = enPassant;
        HalfmoveClock = halfmoveClock;
        Hash = hash;
    }
}

internal static class ChessPiece
{
    public const byte BlackFlag = 8;
    public const byte TypeMask = 7;

    public static ChessPieceType Type(byte piece) => (ChessPieceType)(piece & TypeMask);

    public static bool IsBlack(byte piece) => (piece & BlackFlag) != 0;

    public static bool IsColor(byte piece, bool black) => piece != 0 && IsBlack(piece) == black;

    public static byte Make(ChessPieceType type, bool black) => (byte)((byte)type | (black ? BlackFlag : 0));
}

[SkipLocalsInit]
internal sealed class ChessBoard
{
    public const int Size = 8;
    public const int SquareCount = 64;
    public const int MaxMoves = 256;
    public const byte WhiteKingSide = 1;
    public const byte WhiteQueenSide = 2;
    public const byte BlackKingSide = 4;
    public const byte BlackQueenSide = 8;

    private static readonly int[] KnightSteps = { -2, -1, -2, 1, -1, -2, -1, 2, 1, -2, 1, 2, 2, -1, 2, 1 };
    private static readonly int[] KingSteps = { -1, -1, -1, 0, -1, 1, 0, -1, 0, 1, 1, -1, 1, 0, 1, 1 };
    private static readonly int[] RookRays = { -1, 0, 1, 0, 0, -1, 0, 1 };
    private static readonly int[] BishopRays = { -1, -1, -1, 1, 1, -1, 1, 1 };
    private static readonly ulong[] PieceKeys = BuildPieceKeys();
    private static readonly ulong[] CastlingKeys = BuildKeys(16, 7717);
    private static readonly ulong[] EnPassantKeys = BuildKeys(Size, 4231);
    private static readonly ulong SideKey = 0x9E3779B97F4A7C15UL;

    private readonly byte[] squares = new byte[SquareCount];
    private readonly List<ulong> history = new(512);
    private byte castling;
    private sbyte enPassant;
    private byte halfmoveClock;
    private bool blackToMove;
    private ulong hash;

    public bool TrackHistory { get; set; } = true;

    public bool BlackToMove => blackToMove;

    public int HalfmoveClock => halfmoveClock;

    public int PlyCount => history.Count - 1;

    public void Reset()
    {
        Array.Clear(squares, 0, SquareCount);
        PlaceBackRank(0, true);
        PlaceBackRank(7, false);
        for (var column = 0; column < Size; column++)
        {
            squares[Size + column] = ChessPiece.Make(ChessPieceType.Pawn, true);
            squares[6 * Size + column] = ChessPiece.Make(ChessPieceType.Pawn, false);
        }

        castling = WhiteKingSide | WhiteQueenSide | BlackKingSide | BlackQueenSide;
        enPassant = -1;
        halfmoveClock = 0;
        blackToMove = false;
        hash = ComputeHash();
        history.Clear();
        history.Add(hash);
    }

    public void CopyFrom(ChessBoard other)
    {
        Array.Copy(other.squares, squares, SquareCount);
        castling = other.castling;
        enPassant = other.enPassant;
        halfmoveClock = other.halfmoveClock;
        blackToMove = other.blackToMove;
        hash = other.hash;
        history.Clear();
        for (var index = 0; index < other.history.Count; index++)
        {
            history.Add(other.history[index]);
        }
    }

    public byte PieceAt(int square) => squares[square];

    public static int RowOf(int square) => square / Size;

    public static int ColumnOf(int square) => square % Size;

    public static int Shift(int square, int deltaColumn, int deltaRow)
    {
        var column = square % Size + deltaColumn;
        var row = square / Size + deltaRow;
        if (column < 0 || column >= Size || row < 0 || row >= Size)
        {
            return -1;
        }

        return row * Size + column;
    }

    public int GenerateMoves(Span<ChessMove> buffer) => GenerateLegalMoves(buffer, false);

    public int GenerateCaptures(Span<ChessMove> buffer) => GenerateLegalMoves(buffer, true);

    private int GenerateLegalMoves(Span<ChessMove> buffer, bool capturesOnly)
    {
        Span<ChessMove> pseudo = stackalloc ChessMove[MaxMoves];
        var pseudoCount = GeneratePseudoMoves(pseudo, capturesOnly);
        var legalCount = 0;
        for (var index = 0; index < pseudoCount; index++)
        {
            var move = pseudo[index];
            MakeInternal(move, out var undo);
            var illegal = IsKingAttacked(!blackToMove);
            UnmakeInternal(move, undo);
            if (!illegal)
            {
                buffer[legalCount++] = move;
            }
        }

        return legalCount;
    }

    public void Make(in ChessMove move, out ChessUndo undo)
    {
        MakeInternal(move, out undo);
        if (!TrackHistory)
        {
            return;
        }

        hash = ComputeHash();
        history.Add(hash);
    }

    public void Unmake(in ChessMove move, in ChessUndo undo)
    {
        if (TrackHistory)
        {
            history.RemoveAt(history.Count - 1);
        }

        UnmakeInternal(move, undo);
    }

    private void MakeInternal(in ChessMove move, out ChessUndo undo)
    {
        var piece = squares[move.From];
        var captured = squares[move.To];
        undo = new ChessUndo(captured, castling, enPassant, halfmoveClock, hash);
        var type = ChessPiece.Type(piece);
        var black = ChessPiece.IsBlack(piece);
        squares[move.From] = 0;
        squares[move.To] = move.Promotion != 0 ? ChessPiece.Make((ChessPieceType)move.Promotion, black) : piece;
        if ((move.Flags & ChessMoveFlags.EnPassant) != 0)
        {
            squares[EnPassantVictim(move.To, black)] = 0;
        }

        if ((move.Flags & ChessMoveFlags.CastleKing) != 0)
        {
            squares[move.To + 1] = 0;
            squares[move.To - 1] = ChessPiece.Make(ChessPieceType.Rook, black);
        }
        else if ((move.Flags & ChessMoveFlags.CastleQueen) != 0)
        {
            squares[move.To - 2] = 0;
            squares[move.To + 1] = ChessPiece.Make(ChessPieceType.Rook, black);
        }

        halfmoveClock = type == ChessPieceType.Pawn || captured != 0 ? (byte)0 : (byte)(halfmoveClock + 1);
        enPassant = (move.Flags & ChessMoveFlags.DoublePush) != 0
            ? (sbyte)((move.From + move.To) / 2)
            : (sbyte)-1;
        UpdateCastlingRights(move.From, move.To, type, black);
        blackToMove = !blackToMove;
    }

    private void UnmakeInternal(in ChessMove move, in ChessUndo undo)
    {
        var moved = squares[move.To];
        var black = ChessPiece.IsBlack(moved);
        squares[move.From] = move.Promotion != 0 ? ChessPiece.Make(ChessPieceType.Pawn, black) : moved;
        squares[move.To] = undo.Captured;
        if ((move.Flags & ChessMoveFlags.EnPassant) != 0)
        {
            squares[move.To] = 0;
            squares[EnPassantVictim(move.To, black)] = ChessPiece.Make(ChessPieceType.Pawn, !black);
        }

        if ((move.Flags & ChessMoveFlags.CastleKing) != 0)
        {
            squares[move.To + 1] = ChessPiece.Make(ChessPieceType.Rook, black);
            squares[move.To - 1] = 0;
        }
        else if ((move.Flags & ChessMoveFlags.CastleQueen) != 0)
        {
            squares[move.To - 2] = ChessPiece.Make(ChessPieceType.Rook, black);
            squares[move.To + 1] = 0;
        }

        castling = undo.Castling;
        enPassant = undo.EnPassant;
        halfmoveClock = undo.HalfmoveClock;
        blackToMove = !blackToMove;
        hash = undo.Hash;
    }

    public bool InCheck(bool black) => IsKingAttacked(black);

    public bool IsKingAttacked(bool black)
    {
        var king = FindKing(black);
        return king >= 0 && IsAttacked(king, !black);
    }

    public int FindKing(bool black)
    {
        var target = ChessPiece.Make(ChessPieceType.King, black);
        for (var square = 0; square < SquareCount; square++)
        {
            if (squares[square] == target)
            {
                return square;
            }
        }

        return -1;
    }

    public bool IsAttacked(int square, bool byBlack)
    {
        for (var step = 0; step < KnightSteps.Length; step += 2)
        {
            var from = Shift(square, KnightSteps[step], KnightSteps[step + 1]);
            if (from >= 0 && squares[from] == ChessPiece.Make(ChessPieceType.Knight, byBlack))
            {
                return true;
            }
        }

        for (var step = 0; step < KingSteps.Length; step += 2)
        {
            var from = Shift(square, KingSteps[step], KingSteps[step + 1]);
            if (from >= 0 && squares[from] == ChessPiece.Make(ChessPieceType.King, byBlack))
            {
                return true;
            }
        }

        var pawnRow = byBlack ? -1 : 1;
        var pawn = ChessPiece.Make(ChessPieceType.Pawn, byBlack);
        var pawnLeft = Shift(square, -1, pawnRow);
        var pawnRight = Shift(square, 1, pawnRow);
        if ((pawnLeft >= 0 && squares[pawnLeft] == pawn) || (pawnRight >= 0 && squares[pawnRight] == pawn))
        {
            return true;
        }

        return RayAttack(square, RookRays, ChessPieceType.Rook, byBlack) ||
               RayAttack(square, BishopRays, ChessPieceType.Bishop, byBlack);
    }

    public ChessOutcome Evaluate(out int legalCount)
    {
        Span<ChessMove> moves = stackalloc ChessMove[MaxMoves];
        legalCount = GenerateMoves(moves);
        if (legalCount == 0)
        {
            return InCheck(blackToMove) ? ChessOutcome.Checkmate : ChessOutcome.Stalemate;
        }

        if (halfmoveClock >= 100)
        {
            return ChessOutcome.FiftyMove;
        }

        if (RepetitionCount() >= 3)
        {
            return ChessOutcome.Repetition;
        }

        return HasInsufficientMaterial() ? ChessOutcome.InsufficientMaterial : ChessOutcome.Ongoing;
    }

    public int RepetitionCount()
    {
        var count = 0;
        for (var index = 0; index < history.Count; index++)
        {
            if (history[index] == hash)
            {
                count++;
            }
        }

        return count;
    }

    public bool HasInsufficientMaterial()
    {
        var minorCount = 0;
        var bishopSquare = -1;
        var secondBishopSquare = -1;
        for (var square = 0; square < SquareCount; square++)
        {
            var type = ChessPiece.Type(squares[square]);
            switch (type)
            {
                case ChessPieceType.None:
                case ChessPieceType.King:
                    continue;
                case ChessPieceType.Bishop:
                    minorCount++;
                    if (bishopSquare < 0)
                    {
                        bishopSquare = square;
                    }
                    else
                    {
                        secondBishopSquare = square;
                    }

                    continue;
                case ChessPieceType.Knight:
                    minorCount++;
                    continue;
                default:
                    return false;
            }
        }

        if (minorCount <= 1)
        {
            return true;
        }

        if (minorCount != 2 || secondBishopSquare < 0)
        {
            return false;
        }

        return SquareColor(bishopSquare) == SquareColor(secondBishopSquare);
    }

    public bool TryFindMove(int from, int to, ChessPieceType promotion, out ChessMove move)
    {
        Span<ChessMove> moves = stackalloc ChessMove[MaxMoves];
        var count = GenerateMoves(moves);
        for (var index = 0; index < count; index++)
        {
            var candidate = moves[index];
            if (candidate.From != from || candidate.To != to)
            {
                continue;
            }

            if (promotion != ChessPieceType.None && candidate.Promotion != (byte)promotion)
            {
                continue;
            }

            move = candidate;
            return true;
        }

        move = default;
        return false;
    }

    public bool NeedsPromotion(int from, int to)
    {
        var piece = squares[from];
        if (ChessPiece.Type(piece) != ChessPieceType.Pawn)
        {
            return false;
        }

        var targetRow = ChessPiece.IsBlack(piece) ? Size - 1 : 0;
        return RowOf(to) == targetRow;
    }

    private int GeneratePseudoMoves(Span<ChessMove> buffer, bool capturesOnly)
    {
        var count = 0;
        for (var square = 0; square < SquareCount; square++)
        {
            var piece = squares[square];
            if (!ChessPiece.IsColor(piece, blackToMove))
            {
                continue;
            }

            switch (ChessPiece.Type(piece))
            {
                case ChessPieceType.Pawn:
                    count = AddPawnMoves(buffer, count, square, capturesOnly);
                    break;
                case ChessPieceType.Knight:
                    count = AddStepMoves(buffer, count, square, KnightSteps, capturesOnly);
                    break;
                case ChessPieceType.Bishop:
                    count = AddRayMoves(buffer, count, square, BishopRays, capturesOnly);
                    break;
                case ChessPieceType.Rook:
                    count = AddRayMoves(buffer, count, square, RookRays, capturesOnly);
                    break;
                case ChessPieceType.Queen:
                    count = AddRayMoves(buffer, count, square, RookRays, capturesOnly);
                    count = AddRayMoves(buffer, count, square, BishopRays, capturesOnly);
                    break;
                case ChessPieceType.King:
                    count = AddStepMoves(buffer, count, square, KingSteps, capturesOnly);
                    if (!capturesOnly)
                    {
                        count = AddCastleMoves(buffer, count, square);
                    }

                    break;
            }
        }

        return count;
    }

    private int AddPawnMoves(Span<ChessMove> buffer, int count, int square, bool capturesOnly)
    {
        var black = blackToMove;
        var forward = black ? 1 : -1;
        var startRow = black ? 1 : Size - 2;
        var promotionRow = black ? Size - 1 : 0;
        var single = Shift(square, 0, forward);
        if (!capturesOnly && single >= 0 && squares[single] == 0)
        {
            count = AddPawnTarget(buffer, count, square, single, ChessMoveFlags.None, promotionRow);
            if (RowOf(square) == startRow)
            {
                var doubleStep = Shift(square, 0, forward * 2);
                if (doubleStep >= 0 && squares[doubleStep] == 0)
                {
                    buffer[count++] = new ChessMove(square, doubleStep, ChessMoveFlags.DoublePush);
                }
            }
        }

        for (var side = -1; side <= 1; side += 2)
        {
            var target = Shift(square, side, forward);
            if (target < 0)
            {
                continue;
            }

            if (ChessPiece.IsColor(squares[target], !black))
            {
                count = AddPawnTarget(buffer, count, square, target, ChessMoveFlags.Capture, promotionRow);
                continue;
            }

            if (squares[target] == 0 && target == enPassant)
            {
                buffer[count++] = new ChessMove(square, target, ChessMoveFlags.Capture | ChessMoveFlags.EnPassant);
            }
        }

        return count;
    }

    private static int AddPawnTarget(Span<ChessMove> buffer, int count, int from, int to, ChessMoveFlags flags,
        int promotionRow)
    {
        if (RowOf(to) != promotionRow)
        {
            buffer[count++] = new ChessMove(from, to, flags);
            return count;
        }

        buffer[count++] = new ChessMove(from, to, flags, ChessPieceType.Queen);
        buffer[count++] = new ChessMove(from, to, flags, ChessPieceType.Rook);
        buffer[count++] = new ChessMove(from, to, flags, ChessPieceType.Bishop);
        buffer[count++] = new ChessMove(from, to, flags, ChessPieceType.Knight);
        return count;
    }

    private int AddStepMoves(Span<ChessMove> buffer, int count, int square, int[] steps, bool capturesOnly)
    {
        for (var step = 0; step < steps.Length; step += 2)
        {
            var target = Shift(square, steps[step], steps[step + 1]);
            if (target < 0)
            {
                continue;
            }

            var occupant = squares[target];
            if (ChessPiece.IsColor(occupant, blackToMove))
            {
                continue;
            }

            if (occupant != 0)
            {
                buffer[count++] = new ChessMove(square, target, ChessMoveFlags.Capture);
                continue;
            }

            if (!capturesOnly)
            {
                buffer[count++] = new ChessMove(square, target, ChessMoveFlags.None);
            }
        }

        return count;
    }

    private int AddRayMoves(Span<ChessMove> buffer, int count, int square, int[] rays, bool capturesOnly)
    {
        for (var ray = 0; ray < rays.Length; ray += 2)
        {
            var target = square;
            while (true)
            {
                target = Shift(target, rays[ray], rays[ray + 1]);
                if (target < 0)
                {
                    break;
                }

                var occupant = squares[target];
                if (occupant == 0)
                {
                    if (!capturesOnly)
                    {
                        buffer[count++] = new ChessMove(square, target, ChessMoveFlags.None);
                    }

                    continue;
                }

                if (!ChessPiece.IsColor(occupant, blackToMove))
                {
                    buffer[count++] = new ChessMove(square, target, ChessMoveFlags.Capture);
                }

                break;
            }
        }

        return count;
    }

    private int AddCastleMoves(Span<ChessMove> buffer, int count, int square)
    {
        var black = blackToMove;
        var homeSquare = black ? 4 : 60;
        if (square != homeSquare || IsAttacked(square, !black))
        {
            return count;
        }

        var kingRight = black ? BlackKingSide : WhiteKingSide;
        var queenRight = black ? BlackQueenSide : WhiteQueenSide;
        if ((castling & kingRight) != 0 && squares[square + 1] == 0 && squares[square + 2] == 0 &&
            !IsAttacked(square + 1, !black) && !IsAttacked(square + 2, !black))
        {
            buffer[count++] = new ChessMove(square, square + 2, ChessMoveFlags.CastleKing);
        }

        if ((castling & queenRight) != 0 && squares[square - 1] == 0 && squares[square - 2] == 0 &&
            squares[square - 3] == 0 && !IsAttacked(square - 1, !black) && !IsAttacked(square - 2, !black))
        {
            buffer[count++] = new ChessMove(square, square - 2, ChessMoveFlags.CastleQueen);
        }

        return count;
    }

    private bool RayAttack(int square, int[] rays, ChessPieceType sliderType, bool byBlack)
    {
        var slider = ChessPiece.Make(sliderType, byBlack);
        var queen = ChessPiece.Make(ChessPieceType.Queen, byBlack);
        for (var ray = 0; ray < rays.Length; ray += 2)
        {
            var target = square;
            while (true)
            {
                target = Shift(target, rays[ray], rays[ray + 1]);
                if (target < 0)
                {
                    break;
                }

                var occupant = squares[target];
                if (occupant == 0)
                {
                    continue;
                }

                if (occupant == slider || occupant == queen)
                {
                    return true;
                }

                break;
            }
        }

        return false;
    }

    private void UpdateCastlingRights(int from, int to, ChessPieceType type, bool black)
    {
        if (type == ChessPieceType.King)
        {
            castling &= (byte)~(black ? BlackKingSide | BlackQueenSide : WhiteKingSide | WhiteQueenSide);
        }

        castling &= (byte)~RightsForSquare(from);
        castling &= (byte)~RightsForSquare(to);
    }

    private static byte RightsForSquare(int square)
    {
        return square switch
        {
            0 => BlackQueenSide,
            7 => BlackKingSide,
            56 => WhiteQueenSide,
            63 => WhiteKingSide,
            _ => 0,
        };
    }

    private static int EnPassantVictim(int target, bool black) => black ? target - Size : target + Size;

    private static int SquareColor(int square) => (RowOf(square) + ColumnOf(square)) & 1;

    private void PlaceBackRank(int row, bool black)
    {
        var offset = row * Size;
        squares[offset] = ChessPiece.Make(ChessPieceType.Rook, black);
        squares[offset + 1] = ChessPiece.Make(ChessPieceType.Knight, black);
        squares[offset + 2] = ChessPiece.Make(ChessPieceType.Bishop, black);
        squares[offset + 3] = ChessPiece.Make(ChessPieceType.Queen, black);
        squares[offset + 4] = ChessPiece.Make(ChessPieceType.King, black);
        squares[offset + 5] = ChessPiece.Make(ChessPieceType.Bishop, black);
        squares[offset + 6] = ChessPiece.Make(ChessPieceType.Knight, black);
        squares[offset + 7] = ChessPiece.Make(ChessPieceType.Rook, black);
    }

    private ulong ComputeHash()
    {
        var value = 0UL;
        for (var square = 0; square < SquareCount; square++)
        {
            var piece = squares[square];
            if (piece == 0)
            {
                continue;
            }

            value ^= PieceKeys[piece * SquareCount + square];
        }

        if (blackToMove)
        {
            value ^= SideKey;
        }

        value ^= CastlingKeys[castling];
        if (enPassant >= 0)
        {
            value ^= EnPassantKeys[ColumnOf(enPassant)];
        }

        return value;
    }

    private static ulong[] BuildPieceKeys() => BuildKeys(16 * SquareCount, 20240607);

    private static ulong[] BuildKeys(int count, int seed)
    {
        var keys = new ulong[count];
        var random = new Random(seed);
        for (var index = 0; index < count; index++)
        {
            var high = (ulong)random.Next() << 40;
            var middle = (ulong)random.Next() << 16;
            var low = (ulong)random.Next() & 0xFFFF;
            keys[index] = high ^ middle ^ low;
        }

        return keys;
    }
}
