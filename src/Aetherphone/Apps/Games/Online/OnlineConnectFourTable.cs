using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Online;

// The live Connect Four table. The server is the arbiter; a column tap only ever sends an intent,
// and the next snapshot repaints the truth, the same static repaint Chess uses for a finished
// move rather than a flight path for the dropped disc.
internal sealed class OnlineConnectFourTable
{
    private static readonly Vector4 GridFrame = new(0.20f, 0.38f, 0.72f, 1f);
    private static readonly Vector4 SeatZeroDisc = new(0.86f, 0.28f, 0.26f, 1f);
    private static readonly Vector4 SeatOneDisc = new(0.26f, 0.52f, 0.86f, 1f);
    private static readonly Vector4 ResignTint = new(0.85f, 0.35f, 0.32f, 1f);

    private readonly GameRoomsStore store;

    public OnlineConnectFourTable(GameRoomsStore store)
    {
        this.store = store;
    }

    public void Reset()
    {
    }

    public static Vector4 SeatColor(int seat) => seat == 0 ? SeatZeroDisc : SeatOneDisc;

    public void Draw(Rect body, PhoneTheme theme, float scale, GameRoomSnapshotDto snapshot,
        ConnectFourRoomStateDto board, string notice, OnlineFinishHold hold)
    {
        using var surface = AppSurface.Begin(body, true);
        ImGui.Dummy(new Vector2(MathF.Max(1f, body.Width - 32f * scale), body.Height - 16f * scale));
        var drawList = ImGui.GetWindowDrawList();
        var accent = Core.Apps.AppAccents.For("games");
        GameScene.Ambient(drawList, body, accent);

        var players = board.Players ?? Array.Empty<ConnectFourPlayerDto>();
        var cells = board.Cells ?? Array.Empty<int>();
        var mySeat = SeatOf(players, store.AccountId);
        var live = board.WinnerSeat < 0 && board.EndKind.Length == 0;
        var myTurn = live && mySeat >= 0 && mySeat == board.TurnSeat;

        var rowHeight = 30f * scale;
        var topRow = new Rect(new Vector2(body.Min.X + 14f * scale, body.Min.Y + 4f * scale),
            new Vector2(body.Max.X - 14f * scale, body.Min.Y + 4f * scale + rowHeight));
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var remaining = store.Room.RemainingMilliseconds(snapshot.PhaseEndsAtUnixMs, nowMs);
        DrawSeatRow(drawList, theme, scale, topRow, board, players, remaining);

        var area = new Rect(new Vector2(body.Min.X + 6f * scale, topRow.Max.Y + 12f * scale),
            new Vector2(body.Max.X - 6f * scale, body.Max.Y - 46f * scale));
        var grid = GameGrid.Centered(area, GameRoomWire.ConnectFourColumns, GameRoomWire.ConnectFourRows, 0f);

        if (cells.Length == GameRoomWire.ConnectFourCellCount)
        {
            var hoveredColumn = myTurn ? HitTestColumn(grid) : -1;
            if (hoveredColumn >= 0 && IsColumnFull(cells, hoveredColumn))
            {
                hoveredColumn = -1;
            }

            DrawBoard(drawList, grid, scale, cells, board, mySeat, hoveredColumn);
            if (hoveredColumn >= 0)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (!store.ActInFlight && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    store.SendDrop(hoveredColumn);
                }
            }
        }

        var statusY = grid.Bounds.Max.Y + 10f * scale;
        if (hold.Holding)
        {
            hold.Draw(drawList, new Vector2(body.Center.X, (statusY + body.Max.Y) * 0.5f), body.Width - 32f * scale,
                theme, scale, true);
        }
        else
        {
            DrawStatus(drawList, theme, scale, body, statusY, board, players, mySeat, myTurn, notice);
        }

        if (mySeat >= 0 && live && GameHud.Button(
                new Vector2(body.Center.X, body.Max.Y - 20f * scale),
                new Vector2(110f * scale, 30f * scale), Loc.T(L.Games.OnlineResign), ResignTint, theme)
            && !store.ActInFlight)
        {
            store.SendResign();
        }
    }

    private static void DrawSeatRow(ImDrawListPtr drawList, PhoneTheme theme, float scale, Rect row,
        ConnectFourRoomStateDto board, ConnectFourPlayerDto[] players, long remaining)
    {
        var half = row.Width * 0.5f;
        DrawSeatSlot(drawList, theme, scale, new Rect(row.Min, new Vector2(row.Min.X + half - 4f * scale, row.Max.Y)),
            board, players, 0, remaining);
        DrawSeatSlot(drawList, theme, scale, new Rect(new Vector2(row.Min.X + half + 4f * scale, row.Min.Y), row.Max),
            board, players, 1, remaining);
    }

    private static void DrawSeatSlot(ImDrawListPtr drawList, PhoneTheme theme, float scale, Rect slot,
        ConnectFourRoomStateDto board, ConnectFourPlayerDto[] players, int seat, long remaining)
    {
        if (seat >= players.Length)
        {
            return;
        }

        var player = players[seat];
        var live = board.WinnerSeat < 0 && board.EndKind.Length == 0;
        var isMover = live && seat == board.TurnSeat;
        var discColor = SeatColor(seat);
        var discRadius = 9f * scale;
        var discCenter = new Vector2(slot.Min.X + discRadius, slot.Center.Y);
        drawList.AddCircleFilled(discCenter, discRadius, ImGui.GetColorU32(discColor), 20);
        drawList.AddCircle(discCenter, discRadius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.45f)), 20, 1f * scale);
        if (isMover)
        {
            TurnTimerRing.Draw(drawList, discCenter, discRadius + 4f * scale, remaining, board.TurnSeconds,
                discColor, scale);
        }

        var name = player.Away ? player.DisplayName + " · " + Loc.T(L.Games.OnlineAway) : player.DisplayName;
        var textLeft = discCenter.X + discRadius + 10f * scale;
        var textWidth = MathF.Max(1f, slot.Max.X - textLeft);
        Typography.Draw(drawList, new Vector2(textLeft, slot.Center.Y - 8f * scale),
            Typography.FitText(name, textWidth, TextStyles.SubheadlineEmphasized),
            isMover ? theme.TextStrong : theme.TextMuted, TextStyles.SubheadlineEmphasized);
    }

    private static void DrawBoard(ImDrawListPtr drawList, GameGrid grid, float scale, int[] cells,
        ConnectFourRoomStateDto board, int mySeat, int hoveredColumn)
    {
        GameScene.Arena(drawList, grid.Bounds.Inset(-6f * scale), Metrics.Radius.Md * scale, scale, GridFrame);
        var radius = grid.Pitch * 0.40f;
        var landingRow = hoveredColumn >= 0 ? LandingRow(cells, hoveredColumn) : -1;
        var lastIndex = board.LastRow * GameRoomWire.ConnectFourColumns + board.LastColumn;
        for (var row = 0; row < GameRoomWire.ConnectFourRows; row++)
        {
            for (var column = 0; column < GameRoomWire.ConnectFourColumns; column++)
            {
                var index = row * GameRoomWire.ConnectFourColumns + column;
                var center = grid.CellCenter(column, row);
                var occupant = cells[index];
                if (occupant >= 0)
                {
                    DrawDisc(drawList, center, radius, SeatColor(occupant));
                }
                else
                {
                    drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(GamePalette.Darken(GridFrame, 0.45f)),
                        28);
                }

                if (column == hoveredColumn && row == landingRow)
                {
                    DrawDisc(drawList, center, radius, SeatColor(mySeat) with { W = 0.5f });
                }

                if (index == lastIndex)
                {
                    drawList.AddCircle(center, radius + 2f * scale, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.7f)),
                        28, 1.5f * scale);
                }
            }
        }
    }

    private static void DrawDisc(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 color)
    {
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(color), 28);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(GamePalette.Darken(color, 0.35f) with { W = 0.6f }), 28,
            1f);
    }

    private static int HitTestColumn(GameGrid grid)
    {
        if (!UiInteract.Hover(grid.Bounds.Min, grid.Bounds.Max))
        {
            return -1;
        }

        var local = ImGui.GetMousePos() - grid.Origin;
        var column = (int)(local.X / grid.Pitch);
        return column >= 0 && column < GameRoomWire.ConnectFourColumns ? column : -1;
    }

    // A column is full exactly when its top cell, row 0, is occupied: rows fill from the bottom up.
    private static bool IsColumnFull(int[] cells, int column) => cells[column] >= 0;

    private static int LandingRow(int[] cells, int column)
    {
        for (var row = GameRoomWire.ConnectFourRows - 1; row >= 0; row--)
        {
            if (cells[row * GameRoomWire.ConnectFourColumns + column] < 0)
            {
                return row;
            }
        }

        return -1;
    }

    private void DrawStatus(ImDrawListPtr drawList, PhoneTheme theme, float scale, Rect body, float y,
        ConnectFourRoomStateDto board, ConnectFourPlayerDto[] players, int mySeat, bool myTurn, string notice)
    {
        var status = notice.Length > 0 ? notice : TurnText(players, mySeat, myTurn, board);
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

    private static string TurnText(ConnectFourPlayerDto[] players, int mySeat, bool myTurn,
        ConnectFourRoomStateDto board)
    {
        if (myTurn)
        {
            return Loc.T(L.Games.OnlineYourTurn);
        }

        var moverSeat = board.TurnSeat;
        var live = board.WinnerSeat < 0 && board.EndKind.Length == 0;
        if (live && moverSeat >= 0 && moverSeat < players.Length)
        {
            return Loc.T(L.Games.OnlineTheirTurn, players[moverSeat].DisplayName);
        }

        return string.Empty;
    }

    private static int SeatOf(ConnectFourPlayerDto[] players, string userId)
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
