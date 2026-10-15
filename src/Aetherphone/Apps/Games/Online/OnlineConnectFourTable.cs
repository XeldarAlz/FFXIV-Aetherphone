using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Online;

// A landed disc is replayed from LastColumn/LastRow, facts the server already settled, never predicted.
internal sealed class OnlineConnectFourTable
{
    private const float DropDuration = 0.28f;
    private const float BannerSeconds = 1.4f;
    private const int BannerSeamRow = 2;
    internal const int NotObserved = int.MinValue;

    public static readonly Vector4 GridFrame = new(0.20f, 0.38f, 0.72f, 1f);
    private static readonly Vector4 SeatZeroDisc = new(0.86f, 0.24f, 0.24f, 1f);
    private static readonly Vector4 SeatOneDisc = new(0.95f, 0.78f, 0.20f, 1f);
    private static readonly Vector4 ResignTint = new(0.85f, 0.35f, 0.32f, 1f);

    private static readonly Vector4[] ConfettiPalette =
    {
        SeatZeroDisc, SeatOneDisc, new(1f, 1f, 1f, 1f), new(1f, 0.55f, 0.15f, 1f),
    };

    private readonly GameRoomsStore store;
    private readonly Action askResign;
    private readonly ParticleSystem particles = new(128);

    private int seenLastIndex = NotObserved;
    private long seenRound = -1;
    private int lastTurnSeat = -2;
    private int fallingColumn = -1;
    private int fallingRow = -1;
    private int fallingSeat = -1;
    private float fallingTimer;
    private string bannerText = string.Empty;
    private Vector4 bannerAccent;
    private float bannerProgress = 1f;

    public OnlineConnectFourTable(GameRoomsStore store, Action askResign)
    {
        this.store = store;
        this.askResign = askResign;
    }

    public void Reset()
    {
        particles.Clear();
        seenLastIndex = NotObserved;
        seenRound = -1;
        lastTurnSeat = -2;
        fallingColumn = -1;
        fallingRow = -1;
        fallingSeat = -1;
        fallingTimer = 0f;
        bannerProgress = 1f;
    }

    public static Vector4 SeatColor(int seat) => seat == 0 ? SeatZeroDisc : SeatOneDisc;

    private static bool IsLive(ConnectFourRoomStateDto board) => board.WinnerSeat < 0 && board.EndKind.Length == 0;

    internal static bool IsNewDrop(int seenIndex, int lastIndex, int[] cells)
    {
        if (seenIndex == NotObserved || lastIndex == seenIndex)
        {
            return false;
        }

        if (lastIndex < 0 || lastIndex >= GameRoomWire.ConnectFourCellCount)
        {
            return false;
        }

        return cells.Length == GameRoomWire.ConnectFourCellCount && cells[lastIndex] >= 0;
    }

    private static Vector2 TurnBannerCenter(GameGrid grid) =>
        new(grid.Bounds.Center.X, grid.Origin.Y + BannerSeamRow * grid.Pitch);

    public void Draw(Rect body, PhoneTheme theme, float scale, GameRoomSnapshotDto snapshot,
        ConnectFourRoomStateDto board, string notice, OnlineFinishHold hold)
    {
        using var surface = AppSurface.Begin(body, true);
        ImGui.Dummy(new Vector2(MathF.Max(1f, body.Width - 32f * scale), body.Height - 16f * scale));
        var drawList = ImGui.GetWindowDrawList();
        var accent = Core.Apps.AppAccents.For("games");
        GameScene.Ambient(drawList, body, accent);
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);

        var players = board.Players ?? Array.Empty<ConnectFourPlayerDto>();
        var cells = board.Cells ?? Array.Empty<int>();
        var mySeat = SeatOf(players, store.AccountId);
        var live = IsLive(board);
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
        var bannerCenter = TurnBannerCenter(grid);

        ObserveBoard(board, cells, mySeat, new Vector2(grid.Bounds.Center.X, grid.Bounds.Min.Y), scale);
        if (fallingColumn >= 0)
        {
            fallingTimer += delta;
            if (fallingTimer >= DropDuration)
            {
                fallingColumn = -1;
                fallingRow = -1;
                fallingSeat = -1;
                fallingTimer = 0f;
            }
        }

        particles.Update(delta);
        bannerProgress = GameBanner.Advance(bannerProgress, delta, BannerSeconds);

        if (cells.Length == GameRoomWire.ConnectFourCellCount)
        {
            var hoveredColumn = myTurn ? HitTestColumn(grid) : -1;
            if (hoveredColumn >= 0 && IsColumnFull(cells, hoveredColumn))
            {
                hoveredColumn = -1;
            }

            DrawBoard(drawList, grid, scale, cells, board, mySeat, hoveredColumn, fallingColumn, fallingRow,
                fallingSeat, fallingTimer / DropDuration);
            if (hoveredColumn >= 0)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (!store.ActInFlight && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    store.SendDrop(hoveredColumn);
                }
            }
        }

        particles.Draw(drawList, scale);
        if (bannerProgress < 1f)
        {
            GameBanner.Draw(drawList, bannerCenter, bannerText, bannerAccent, theme, bannerProgress,
                TextStyles.SubheadlineEmphasized);
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
            askResign();
        }
    }

    private void ObserveBoard(ConnectFourRoomStateDto board, int[] cells, int mySeat, Vector2 confettiOrigin,
        float scale)
    {
        if (board.RoundIndex != seenRound)
        {
            seenRound = board.RoundIndex;
            seenLastIndex = NotObserved;
            lastTurnSeat = -2;
            fallingColumn = -1;
            particles.Clear();
        }

        var lastIndex = board.LastRow * GameRoomWire.ConnectFourColumns + board.LastColumn;
        if (IsNewDrop(seenLastIndex, lastIndex, cells))
        {
            ReactToDrop(board, cells[lastIndex], mySeat, confettiOrigin, scale);
        }

        seenLastIndex = lastIndex;

        var live = IsLive(board);
        if (board.TurnSeat != lastTurnSeat)
        {
            var wasObserved = lastTurnSeat != -2;
            lastTurnSeat = board.TurnSeat;
            if (live && wasObserved && mySeat >= 0 && board.TurnSeat == mySeat)
            {
                ShowBanner(Loc.T(L.Games.OnlineYourTurn), SeatColor(mySeat));
            }
        }
    }

    private void ReactToDrop(ConnectFourRoomStateDto board, int dropSeat, int mySeat, Vector2 confettiOrigin,
        float scale)
    {
        fallingColumn = board.LastColumn;
        fallingRow = board.LastRow;
        fallingSeat = dropSeat;
        fallingTimer = 0f;
        UiFeedback.Play(UiSound.GamePiece);

        if (mySeat < 0 || board.WinnerSeat != mySeat
            || !string.Equals(board.EndKind, GameRoomWire.ConnectFourEndConnect, StringComparison.Ordinal))
        {
            return;
        }

        particles.Confetti(confettiOrigin, 70, ConfettiPalette, 260f * scale, 4f, 1.4f);
        UiFeedback.Play(UiSound.GameWin);
    }

    private void ShowBanner(string text, Vector4 accent)
    {
        bannerText = text;
        bannerAccent = accent;
        bannerProgress = 0f;
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
        var live = IsLive(board);
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
        ConnectFourRoomStateDto board, int mySeat, int hoveredColumn, int fallingColumn, int fallingRow,
        int fallingSeat, float fallingProgress)
    {
        GameScene.Arena(drawList, grid.Bounds.Inset(-6f * scale), Metrics.Radius.Md * scale, scale, GridFrame);
        var radius = grid.Pitch * 0.40f;
        var landingRow = hoveredColumn >= 0 ? LandingRow(cells, hoveredColumn) : -1;
        if (hoveredColumn >= 0 && landingRow >= 0)
        {
            var laneMin = new Vector2(grid.Origin.X + hoveredColumn * grid.Pitch, grid.Origin.Y);
            var laneMax = new Vector2(laneMin.X + grid.Pitch, grid.Origin.Y + (landingRow + 1) * grid.Pitch);
            drawList.AddRectFilled(laneMin, laneMax, ImGui.GetColorU32(SeatColor(mySeat) with { W = 0.14f }));
        }

        var lastIndex = board.LastRow * GameRoomWire.ConnectFourColumns + board.LastColumn;
        for (var row = 0; row < GameRoomWire.ConnectFourRows; row++)
        {
            for (var column = 0; column < GameRoomWire.ConnectFourColumns; column++)
            {
                var index = row * GameRoomWire.ConnectFourColumns + column;
                var center = grid.CellCenter(column, row);
                var isFallingTarget = column == fallingColumn && row == fallingRow;
                var occupant = isFallingTarget ? -1 : cells[index];
                if (occupant >= 0)
                {
                    DrawDisc(drawList, center, radius, SeatColor(occupant), scale);
                }
                else
                {
                    drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(GamePalette.Darken(GridFrame, 0.45f)),
                        28);
                }

                if (column == hoveredColumn && row == landingRow)
                {
                    DrawDisc(drawList, center, radius, SeatColor(mySeat) with { W = 0.5f }, scale);
                }

                if (index == lastIndex && fallingColumn < 0)
                {
                    drawList.AddCircle(center, radius + 2f * scale, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.7f)),
                        28, 1.5f * scale);
                }
            }
        }

        if (fallingColumn < 0)
        {
            return;
        }

        var fallT = Easing.EaseInCubic(Math.Clamp(fallingProgress, 0f, 1f));
        var start = grid.CellCenter(fallingColumn, -1);
        var end = grid.CellCenter(fallingColumn, fallingRow);
        DrawDisc(drawList, Vector2.Lerp(start, end, fallT), radius, SeatColor(fallingSeat), scale);
    }

    // Squircle draws a superellipse (the iOS app-icon shape), not a circle, even at a radius equal
    // to half the bounding box, so a disc needs the real circle primitives rather than Squircle's
    // usual rounded-rect technique.
    private static void DrawDisc(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 color, float scale)
    {
        drawList.AddCircleFilled(center + new Vector2(0f, 2f * scale), radius,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.28f)), 28);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(color), 28);
        drawList.PushClipRect(center - new Vector2(radius, radius), new Vector2(center.X + radius, center.Y), true);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(GamePalette.Lighten(color, 0.22f) with { W = 0.5f }),
            28);
        drawList.PopClipRect();
        drawList.AddCircle(center, radius, ImGui.GetColorU32(GamePalette.Darken(color, 0.3f) with { W = 0.6f }), 28,
            1f * scale);
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
        var live = IsLive(board);
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
