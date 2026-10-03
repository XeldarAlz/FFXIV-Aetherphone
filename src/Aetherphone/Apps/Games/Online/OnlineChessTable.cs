using Aetherphone.Apps.Games.Chess;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Online;

// The live chess table. The server is the arbiter; the local ChessBoard here is only the usher,
// answering which of your taps are worth sending. The board flips so you always play up the
// screen, and the mover's clock counts down from the same deadline the server will flag on.
internal sealed class OnlineChessTable
{
    private static readonly Vector4 LightSquare = new(0.85f, 0.87f, 0.90f, 1f);
    private static readonly Vector4 DarkSquare = new(0.42f, 0.52f, 0.62f, 1f);
    private static readonly Vector4 CheckGlow = new(0.94f, 0.30f, 0.32f, 1f);
    private static readonly Vector4 ResignTint = new(0.85f, 0.35f, 0.32f, 1f);

    private readonly GameRoomsStore store;
    private readonly ChessBoard hints = new();
    private int selectedSquare = -1;
    private int hintedActionCount = -1;
    private int hintedSelected = -1;
    private ulong targets;
    private ulong captureTargets;
    private int promotionFrom = -1;
    private int promotionTo = -1;

    public OnlineChessTable(GameRoomsStore store)
    {
        this.store = store;
    }

    public void Reset()
    {
        selectedSquare = -1;
        hintedActionCount = -1;
        hintedSelected = -1;
        targets = 0;
        captureTargets = 0;
        promotionFrom = -1;
        promotionTo = -1;
    }

    public void Draw(Rect body, PhoneTheme theme, float scale, GameRoomSnapshotDto snapshot,
        ChessRoomStateDto board, string notice, OnlineFinishHold hold)
    {
        using var surface = AppSurface.Begin(body, true);
        ImGui.Dummy(new Vector2(MathF.Max(1f, body.Width - 32f * scale), body.Height - 16f * scale));
        var drawList = ImGui.GetWindowDrawList();
        var accent = Core.Apps.AppAccents.For("games");
        GameScene.Ambient(drawList, body, accent);

        var players = board.Players ?? Array.Empty<ChessPlayerDto>();
        var squares = board.Squares ?? Array.Empty<int>();
        var mySeat = SeatOf(players, store.AccountId);
        var flip = mySeat >= 0 && mySeat != board.WhiteSeat;
        var moverSeat = MoverSeat(board, players.Length);
        var live = board.WinnerSeat < 0 && board.EndKind.Length == 0;
        var myTurn = live && mySeat >= 0 && mySeat == moverSeat;
        if (!myTurn)
        {
            promotionFrom = -1;
            promotionTo = -1;
        }

        var rowHeight = 30f * scale;
        var side = MathF.Min(body.Width - 20f * scale, body.Height - (rowHeight * 2f + 96f * scale));
        var boardTop = body.Min.Y + rowHeight + 12f * scale;
        var origin = new Vector2(body.Center.X - side * 0.5f, boardTop);
        var cell = side / ChessBoard.Size;

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var moverRemaining = store.Room.RemainingMilliseconds(snapshot.PhaseEndsAtUnixMs, nowMs);
        DrawSeatRow(drawList, theme, scale,
            new Rect(new Vector2(origin.X, body.Min.Y + 4f * scale), new Vector2(origin.X + side, body.Min.Y + 4f * scale + rowHeight)),
            board, players, flip ? board.WhiteSeat : 1 - board.WhiteSeat, moverSeat, moverRemaining, accent);

        if (squares.Length == ChessBoard.SquareCount)
        {
            RefreshHints(board, squares, myTurn);
            DrawBoard(drawList, theme, scale, origin, cell, board, squares, flip, accent);
            HandleTaps(origin, cell, squares, flip, myTurn, board);
        }

        var myRowTop = boardTop + side + 8f * scale;
        DrawSeatRow(drawList, theme, scale,
            new Rect(new Vector2(origin.X, myRowTop), new Vector2(origin.X + side, myRowTop + rowHeight)),
            board, players, flip ? 1 - board.WhiteSeat : board.WhiteSeat, moverSeat, moverRemaining, accent);

        var footerTop = myRowTop + rowHeight + 8f * scale;
        if (hold.Holding)
        {
            hold.Draw(drawList, new Vector2(body.Center.X, (footerTop + body.Max.Y) * 0.5f),
                body.Width - 32f * scale, theme, scale, true);
        }
        else
        {
            DrawStatus(drawList, theme, scale, body, footerTop, board, players, mySeat, myTurn, notice);
        }

        if (mySeat >= 0 && live && GameHud.Button(
                new Vector2(body.Center.X, body.Max.Y - 26f * scale),
                new Vector2(110f * scale, 30f * scale), Loc.T(L.Games.OnlineResign), ResignTint, theme)
            && !store.ActInFlight)
        {
            store.SendResign();
        }

        if (promotionFrom >= 0)
        {
            var choice = ChessRenderer.DrawPromotionPicker(body, theme, accent, flip, 1f, scale);
            if (choice != ChessPieceType.None)
            {
                store.SendMove(promotionFrom, promotionTo, (int)choice);
                promotionFrom = -1;
                promotionTo = -1;
                selectedSquare = -1;
            }
        }
    }

    // The legal-move hints follow (position, selection) and nothing else, so the mirror board is
    // reloaded only when the server's action count moves or a different piece is picked up.
    private void RefreshHints(ChessRoomStateDto board, int[] squares, bool myTurn)
    {
        if (!myTurn || selectedSquare < 0)
        {
            targets = 0;
            captureTargets = 0;
            hintedSelected = -1;
            return;
        }

        if (hintedActionCount == board.ActionCount && hintedSelected == selectedSquare)
        {
            return;
        }

        hints.LoadFrom(squares, board.BlackToMove, board.Castling, board.EnPassant, board.HalfmoveClock);
        targets = 0;
        captureTargets = 0;
        Span<ChessMove> moves = stackalloc ChessMove[ChessBoard.MaxMoves];
        var count = hints.GenerateMoves(moves);
        for (var index = 0; index < count; index++)
        {
            if (moves[index].From != selectedSquare)
            {
                continue;
            }

            targets |= 1UL << moves[index].To;
            if ((moves[index].Flags & ChessMoveFlags.Capture) != 0)
            {
                captureTargets |= 1UL << moves[index].To;
            }
        }

        hintedActionCount = board.ActionCount;
        hintedSelected = selectedSquare;
    }

    private void DrawBoard(ImDrawListPtr drawList, PhoneTheme theme, float scale, Vector2 origin,
        float cell, ChessRoomStateDto board, int[] squares, bool flip, Vector4 accent)
    {
        GameScene.Arena(drawList,
            new Rect(origin - new Vector2(4f * scale, 4f * scale),
                origin + new Vector2(cell * ChessBoard.Size + 4f * scale, cell * ChessBoard.Size + 4f * scale)),
            Metrics.Radius.Md * scale, scale, accent);
        var checkSquare = board.InCheck ? KingSquare(squares, board.BlackToMove) : -1;
        var coordinateScale = MathF.Max(0.42f, MathF.Min(0.62f, cell / (56f * scale)));
        for (var display = 0; display < ChessBoard.SquareCount; display++)
        {
            var actual = flip ? ChessBoard.SquareCount - 1 - display : display;
            var column = display % ChessBoard.Size;
            var row = display / ChessBoard.Size;
            var min = new Vector2(origin.X + column * cell, origin.Y + row * cell);
            var max = min + new Vector2(cell, cell);
            var light = (column + row) % 2 == 0;
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(light ? LightSquare : DarkSquare));
            if (actual == board.LastFrom || actual == board.LastTo)
            {
                drawList.AddRectFilled(min, max, ImGui.GetColorU32(accent with { W = 0.34f }));
            }

            if (actual == checkSquare)
            {
                drawList.AddRectFilled(min, max, ImGui.GetColorU32(CheckGlow with { W = 0.34f }));
            }

            if (actual == selectedSquare)
            {
                drawList.AddRectFilled(min, max, ImGui.GetColorU32(accent with { W = 0.45f }));
            }

            DrawCoordinates(drawList, min, max, actual, column, row, light, coordinateScale, cell);
            if ((targets & (1UL << actual)) != 0)
            {
                var center = (min + max) * 0.5f;
                if ((captureTargets & (1UL << actual)) != 0)
                {
                    drawList.AddCircle(center, cell * 0.42f,
                        ImGui.GetColorU32(new Vector4(0.08f, 0.10f, 0.13f, 0.42f)), 0, cell * 0.10f);
                }
                else
                {
                    drawList.AddCircleFilled(center, cell * 0.16f,
                        ImGui.GetColorU32(new Vector4(0.08f, 0.10f, 0.13f, 0.34f)), 24);
                }
            }
        }

        var pieceHeight = cell * 0.66f;
        for (var display = 0; display < ChessBoard.SquareCount; display++)
        {
            var actual = flip ? ChessBoard.SquareCount - 1 - display : display;
            var piece = (byte)squares[actual];
            if (piece == 0)
            {
                continue;
            }

            var column = display % ChessBoard.Size;
            var row = display / ChessBoard.Size;
            var center = new Vector2(origin.X + (column + 0.5f) * cell, origin.Y + (row + 0.5f) * cell);
            ChessRenderer.DrawPiece(drawList, center, piece, pieceHeight, scale, 1f);
        }
    }

    private static void DrawCoordinates(ImDrawListPtr drawList, Vector2 min, Vector2 max, int actual,
        int column, int row, bool light, float coordinateScale, float cell)
    {
        var ink = light ? DarkSquare : LightSquare;
        var inset = cell * 0.09f;
        if (column == 0)
        {
            Typography.Draw(drawList, new Vector2(min.X + inset, min.Y + inset * 0.6f),
                (ChessBoard.Size - ChessBoard.RowOf(actual)).ToString(Loc.Culture), ink, coordinateScale,
                FontWeight.SemiBold);
        }

        if (row != ChessBoard.Size - 1)
        {
            return;
        }

        var letter = ((char)('a' + ChessBoard.ColumnOf(actual))).ToString();
        var letterSize = Typography.Measure(letter, coordinateScale, FontWeight.SemiBold);
        Typography.Draw(drawList, new Vector2(max.X - inset - letterSize.X, max.Y - inset * 0.6f - letterSize.Y),
            letter, ink, coordinateScale, FontWeight.SemiBold);
    }

    private void HandleTaps(Vector2 origin, float cell, int[] squares, bool flip, bool myTurn,
        ChessRoomStateDto board)
    {
        if (promotionFrom >= 0 || !myTurn || !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        var mouse = ImGui.GetMousePos();
        var column = (int)MathF.Floor((mouse.X - origin.X) / cell);
        var row = (int)MathF.Floor((mouse.Y - origin.Y) / cell);
        if (column < 0 || column >= ChessBoard.Size || row < 0 || row >= ChessBoard.Size)
        {
            return;
        }

        var display = row * ChessBoard.Size + column;
        var actual = flip ? ChessBoard.SquareCount - 1 - display : display;
        if (selectedSquare >= 0 && (targets & (1UL << actual)) != 0 && !store.ActInFlight)
        {
            if (hints.NeedsPromotion(selectedSquare, actual))
            {
                promotionFrom = selectedSquare;
                promotionTo = actual;
                return;
            }

            store.SendMove(selectedSquare, actual, 0);
            selectedSquare = -1;
            return;
        }

        var piece = (byte)squares[actual];
        if (piece != 0 && ChessPiece.IsBlack(piece) == board.BlackToMove)
        {
            selectedSquare = actual == selectedSquare ? -1 : actual;
            return;
        }

        selectedSquare = -1;
    }

    private void DrawSeatRow(ImDrawListPtr drawList, PhoneTheme theme, float scale, Rect row,
        ChessRoomStateDto board, ChessPlayerDto[] players, int seat, int moverSeat, long moverRemaining,
        Vector4 accent)
    {
        if (seat < 0 || seat >= players.Length)
        {
            return;
        }

        var player = players[seat];
        var isWhite = seat == board.WhiteSeat;
        var isMover = seat == moverSeat && board.WinnerSeat < 0 && board.EndKind.Length == 0;
        ChessRenderer.DrawPiece(drawList, new Vector2(row.Min.X + 12f * scale, row.Center.Y),
            ChessPiece.Make(ChessPieceType.King, !isWhite), row.Height * 0.8f, scale,
            player.Away ? 0.4f : 1f);

        var clock = isMover ? moverRemaining : isWhite ? board.WhiteMsRemaining : board.BlackMsRemaining;
        var clockText = ClockText(clock);
        var clockSize = Typography.Measure(clockText, TextStyles.SubheadlineEmphasized);
        var clockLeft = row.Max.X - clockSize.X - 10f * scale;
        var urgent = isMover && clock < 30_000;
        Typography.Draw(drawList, new Vector2(clockLeft, row.Center.Y - clockSize.Y * 0.5f), clockText,
            urgent ? CheckGlow : isMover ? theme.TextStrong : theme.TextMuted,
            TextStyles.SubheadlineEmphasized);

        var name = player.DisplayName;
        if (player.Away)
        {
            name = name + " · " + Loc.T(L.Games.OnlineAway);
        }

        var textLeft = row.Min.X + 26f * scale;
        Typography.Draw(drawList, new Vector2(textLeft, row.Center.Y - 8f * scale),
            Typography.FitText(name, clockLeft - textLeft - 8f * scale, TextStyles.SubheadlineEmphasized),
            isMover ? theme.TextStrong : theme.TextMuted, TextStyles.SubheadlineEmphasized);
        if (isMover)
        {
            drawList.AddCircleFilled(new Vector2(clockLeft - 12f * scale, row.Center.Y), 3.5f * scale,
                ImGui.GetColorU32(accent), 16);
        }
    }

    private void DrawStatus(ImDrawListPtr drawList, PhoneTheme theme, float scale, Rect body, float y,
        ChessRoomStateDto board, ChessPlayerDto[] players, int mySeat, bool myTurn, string notice)
    {
        string status;
        if (notice.Length > 0)
        {
            status = notice;
        }
        else if (board.InCheck && board.WinnerSeat < 0)
        {
            status = Loc.T(L.Games.OnlineCheck) + " " + TurnText(players, mySeat, myTurn, board);
        }
        else
        {
            status = TurnText(players, mySeat, myTurn, board);
        }

        if (!store.Room.Attached)
        {
            status = status.Length == 0
                ? Loc.T(L.Games.OnlineReconnecting)
                : status + " · " + Loc.T(L.Games.OnlineReconnecting);
        }

        if (status.Length == 0)
        {
            return;
        }

        Typography.DrawCentered(drawList, new Vector2(body.Center.X, y + 8f * scale),
            Typography.FitText(status, body.Width - 24f * scale, TextStyles.Subheadline),
            myTurn ? theme.TextStrong : theme.TextMuted, TextStyles.Subheadline);
    }

    private static string TurnText(ChessPlayerDto[] players, int mySeat, bool myTurn,
        ChessRoomStateDto board)
    {
        if (myTurn)
        {
            var color = mySeat == board.WhiteSeat ? L.Games.OnlineYouPlayWhite : L.Games.OnlineYouPlayBlack;
            return Loc.T(L.Games.OnlineYourTurn) + " · " + Loc.T(color);
        }

        var moverSeat = MoverSeat(board, players.Length);
        if (moverSeat >= 0 && moverSeat < players.Length)
        {
            return Loc.T(L.Games.OnlineTheirTurn, players[moverSeat].DisplayName);
        }

        return string.Empty;
    }

    private static string ClockText(long milliseconds)
    {
        if (milliseconds < 0)
        {
            milliseconds = 0;
        }

        var totalSeconds = milliseconds / 1000;
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        return string.Concat(minutes.ToString(Loc.Culture), ":", seconds.ToString("D2", Loc.Culture));
    }

    private static int KingSquare(int[] squares, bool black)
    {
        var target = ChessPiece.Make(ChessPieceType.King, black);
        for (var square = 0; square < squares.Length; square++)
        {
            if (squares[square] == target)
            {
                return square;
            }
        }

        return -1;
    }

    private static int MoverSeat(ChessRoomStateDto board, int playerCount)
    {
        if (board.WhiteSeat < 0 || playerCount < 2)
        {
            return -1;
        }

        return board.BlackToMove ? 1 - board.WhiteSeat : board.WhiteSeat;
    }

    private static int SeatOf(ChessPlayerDto[] players, string userId)
    {
        for (var index = 0; index < players.Length; index++)
        {
            if (string.Equals(players[index].UserId, userId, StringComparison.Ordinal))
            {
                return players[index].Seat;
            }
        }

        return -1;
    }
}
