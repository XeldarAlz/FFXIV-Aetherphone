using System.Runtime.InteropServices;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Online;

// One room, three faces: the lobby with the code and the roster, the live table of whichever game
// the room hosts, and the winner screen that leads back to another round. Everything rendered
// here is the server's word; a tap only ever sends an intent and the next event repaints the
// truth.
internal sealed class OnlineRoomView
{
    private const float HeaderHeight = 42f;
    private const float RosterRowHeight = 56f;
    private const long NoticeMilliseconds = 4_000;

    private static readonly LocString[] RuleSetLabels = [L.Games.OnlineRuleDefault, L.Games.OnlineRuleHouse];

    private readonly GameRoomsStore store;
    private readonly DropdownMenu rulesMenu = new();
    private readonly List<DropdownMenu.Item> rulesMenuItems = new();
    private readonly OnlineUnoTable unoTable;
    private readonly OnlineChessTable chessTable;
    private readonly OnlinePoolTable poolTable;
    private readonly OnlineFinishHold finishHold = new();

    private string inlineReason = string.Empty;
    private int selectedRuleSet;
    private long noticeAtTick;
    private long copiedAtTick;
    private int lastSeenPhase = -1;

    public OnlineRoomView(GameRoomsStore store)
    {
        this.store = store;
        unoTable = new OnlineUnoTable(store);
        chessTable = new OnlineChessTable(store);
        poolTable = new OnlinePoolTable(store);
    }

    public void Enter()
    {
        inlineReason = string.Empty;
        selectedRuleSet = GameRoomWire.RuleSetDefault;
        rulesMenuItems.Clear();
        rulesMenu.Close();
        unoTable.Reset();
        chessTable.Reset();
        poolTable.Reset();
        finishHold.Clear();
        lastSeenPhase = -1;
    }

    public bool WantsLandscape => ShowsPool(store.Room.State) && store.Room.RoomId.Length > 0;

    public void Draw(in PhoneContext context, Action back, AppSkin ui, bool landscape)
    {
        var held = store.Room.State;
        TrackPhase(held);
        var scale = UiScale.Current;
        var content = context.Content;
        var theme = context.Theme;
        var fullScreenTable = landscape && WantsLandscape;
        Rect body;
        if (fullScreenTable)
        {
            body = content;
        }
        else
        {
            DrawHeader(context, back, ui, held, scale);
            body = new Rect(new Vector2(content.Min.X, content.Min.Y + HeaderHeight * scale), content.Max);
        }

        Consume(back);

        var session = store.Room;
        if (session.RoomId.Length == 0)
        {
            DrawClosed(body, theme, scale, session.ClosedReason, back);
            return;
        }

        if (held is null || held.Roster is null)
        {
            DrawCenteredNotice(body, theme, Loc.T(L.Games.OnlineLoading));
            return;
        }

        if (ShowsTable(held))
        {
            if (held.Uno is not null)
            {
                unoTable.Draw(body, theme, scale, held.Snapshot, held.Uno, FreshNotice(), finishHold);
                return;
            }

            if (held.Chess is not null)
            {
                chessTable.Draw(body, theme, scale, held.Snapshot, held.Chess, FreshNotice(), finishHold);
                return;
            }

            if (held.Pool is not null)
            {
                poolTable.Draw(body, theme, scale, held.Snapshot, held.Pool, FreshNotice(),
                    fullScreenTable ? back : null, finishHold);
                return;
            }
        }

        DrawLobby(body, theme, scale, held);
    }

    private void DrawHeader(in PhoneContext context, Action back, AppSkin ui, GameRoomState? held, float scale)
    {
        var title = Loc.T(GamesOnlineText.GameName(held?.Snapshot.GameKind));
        if (!ShowsTable(held) || store.Room.RoomId.Length == 0)
        {
            AppHeader.Draw(context, title, back);
            return;
        }

        var isHost = IsHost(held!.Roster!);
        var leaveLabel = LeaveLabel(isHost);
        AppHeader.Draw(context, "games.room.header", title, AppSkin.HeaderActionWidth(leaveLabel) + 18f * scale,
            back);
        if (ui.HeaderAction(context.Content, leaveLabel, !store.IntentInFlight))
        {
            LeaveOrClose(isHost);
        }
    }

    private bool ShowsPool(GameRoomState? held) => held is { Pool: not null } && ShowsTable(held);

    private bool ShowsTable(GameRoomState? held)
    {
        if (held is null || held.Roster is null || (held.Uno is null && held.Chess is null && held.Pool is null))
        {
            return false;
        }

        var phase = held.Snapshot.Phase;
        return phase == GameRoomWire.PhasePlaying || (phase == GameRoomWire.PhaseFinished && finishHold.Holding);
    }

    private void TrackPhase(GameRoomState? held)
    {
        if (held is null || held.Roster is null)
        {
            finishHold.Clear();
            lastSeenPhase = -1;
            return;
        }

        var phase = held.Snapshot.Phase;
        if (phase == lastSeenPhase)
        {
            return;
        }

        if (phase == GameRoomWire.PhaseFinished && lastSeenPhase == GameRoomWire.PhasePlaying)
        {
            finishHold.Begin(FinishedText(held));
        }
        else
        {
            finishHold.Clear();
        }

        lastSeenPhase = phase;
    }

    private bool IsHost(GameRoomRoster roster) =>
        string.Equals(roster.HostUserId, store.AccountId, StringComparison.Ordinal);

    private static string LeaveLabel(bool isHost) =>
        isHost ? Loc.T(L.Games.OnlineCloseRoom) : Loc.T(L.Games.OnlineLeave);

    private void LeaveOrClose(bool isHost)
    {
        var roomId = store.Room.RoomId;
        if (isHost)
        {
            store.CloseRoom(roomId);
            return;
        }

        store.LeaveRoom(roomId);
    }

    private string FreshNotice()
    {
        if (inlineReason.Length > 0 && Environment.TickCount64 - noticeAtTick < NoticeMilliseconds)
        {
            return Loc.T(GamesOnlineText.ReasonMessage(inlineReason));
        }

        return string.Empty;
    }

    private void Consume(Action back)
    {
        var act = store.TakeActOutcome();
        if (act is not null && !act.Granted && act.Reason.Length > 0)
        {
            inlineReason = act.Reason;
            noticeAtTick = Environment.TickCount64;
        }

        var answer = store.TakeRoomAnswer();
        if (answer is null)
        {
            return;
        }

        if (answer.Intent is GameRoomIntent.Left or GameRoomIntent.Closed && answer.Granted)
        {
            back();
            return;
        }

        if (!answer.Granted && answer.Reason.Length > 0)
        {
            inlineReason = answer.Reason;
            noticeAtTick = Environment.TickCount64;
        }
    }

    private void DrawClosed(Rect body, PhoneTheme theme, float scale, string reason, Action back)
    {
        var message = reason switch
        {
            GameRoomWire.ReasonKicked => Loc.T(L.Games.OnlineKicked),
            GameRoomWire.ReasonRestarting => Loc.T(L.Games.OnlineRestarting),
            _ => Loc.T(L.Games.OnlineRoomEnded),
        };
        DrawCenteredNotice(body, theme, message);
        var accent = Core.Apps.AppAccents.For("games");
        if (GameHud.Button(new Vector2(body.Center.X, body.Center.Y + 48f * scale),
                new Vector2(140f * scale, 36f * scale), Loc.T(L.Common.Cancel), accent, theme))
        {
            back();
        }
    }

    private static void DrawCenteredNotice(Rect body, PhoneTheme theme, string message)
    {
        Typography.DrawWrappedCentered(ImGui.GetWindowDrawList(), body.Center, message, theme.TextMuted,
            TextStyles.Subheadline, MathF.Min(body.Width - 48f, 280f * UiScale.Current));
    }

    // The lobby and the finished screen are the same room at rest: the roster, the code, and one
    // primary button whose label is the only thing the phase changes.
    private void DrawLobby(Rect body, PhoneTheme theme, float scale, GameRoomState held)
    {
        using var surface = AppSurface.Begin(body);
        var accent = Core.Apps.AppAccents.For("games");
        var phase = held.Snapshot.Phase;
        var roster = held.Roster!;
        var players = roster.Players;
        var isHost = IsHost(roster);
        rulesMenu.Gate();
        var picked = DrawRulesMenu(body, theme);

        if (phase == GameRoomWire.PhaseFinished)
        {
            DrawFinishedBanner(theme, scale, held);
        }

        DrawCodeCard(theme, scale, accent);
        if (inlineReason.Length > 0 && Environment.TickCount64 - noticeAtTick < NoticeMilliseconds)
        {
            DrawInlineNotice(theme, scale);
        }

        if (isHost && held.Snapshot.GameKind == GameRoomWire.UnoKind)
        {
            var pickOrigin = ImGui.GetCursorScreenPos();
            var pickWidth = ScrollLayout.StableContentWidth();
            var dropdownRect = new Rect(pickOrigin, new Vector2(pickOrigin.X + pickWidth, pickOrigin.Y + 36f * scale));

            var label = Loc.T(RuleSetLabels[selectedRuleSet]);
            if (GameHud.Button(new Vector2(dropdownRect.Center.X, dropdownRect.Center.Y),
                new Vector2(pickWidth, 36f * scale), label, accent, theme))
            {
                OpenRulesMenu(dropdownRect);
            }

            if (picked >= 0)
            {
                selectedRuleSet = picked;
            }

            ImGui.Dummy(new Vector2(pickWidth, 40f * scale));
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        var card = GroupCard.Begin(theme, players.Length == 0 ? 1 : players.Length, RosterRowHeight);
        for (var index = 0; index < players.Length; index++)
        {
            DrawRosterRow(card.NextRow(), theme, scale, roster, players[index], isHost);
        }

        if (players.Length == 0)
        {
            card.NextRow();
        }

        card.End();
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));

        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var buttonSize = new Vector2(width * 0.62f, 40f * scale);
        var primaryCenter = new Vector2(origin.X + width * 0.5f, origin.Y + buttonSize.Y * 0.5f);
        if (isHost)
        {
            var enough = players.Length >= 2;
            var label = phase == GameRoomWire.PhaseFinished
                ? Loc.T(L.Games.OnlineRematch)
                : Loc.T(L.Games.OnlineStart);
            if (GameHud.Button(primaryCenter, buttonSize, label, enough ? accent : theme.TextMuted, theme)
                && enough && !store.ActInFlight)
            {
                store.SendStart(selectedRuleSet);
            }

            if (!enough)
            {
                Typography.DrawCentered(ImGui.GetWindowDrawList(),
                    new Vector2(primaryCenter.X, primaryCenter.Y + 32f * scale),
                    Loc.T(L.Games.OnlineNeedPlayers), theme.TextMuted, TextStyles.Footnote);
            }
        }
        else
        {
            Typography.DrawCentered(ImGui.GetWindowDrawList(), primaryCenter,
                Loc.T(L.Games.OnlineWaitingHost), theme.TextMuted, TextStyles.Subheadline);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, buttonSize.Y + 44f * scale));

        var leaveOrigin = ImGui.GetCursorScreenPos();
        var leaveCenter = new Vector2(leaveOrigin.X + width * 0.5f, leaveOrigin.Y + 18f * scale);
        if (GameHud.Button(leaveCenter, new Vector2(width * 0.5f, 34f * scale), LeaveLabel(isHost),
                new Vector4(0.85f, 0.35f, 0.32f, 1f), theme) && !store.IntentInFlight)
        {
            LeaveOrClose(isHost);
        }

        ImGui.SetCursorScreenPos(leaveOrigin);
        ImGui.Dummy(new Vector2(width, 40f * scale + Metrics.Space.Lg * scale));
    }

    private int DrawRulesMenu(Rect body, PhoneTheme theme)
    {
        if (!rulesMenu.Open || rulesMenuItems.Count == 0)
        {
            return -1;
        }

        var picked = rulesMenu.Draw(body, theme, CollectionsMarshal.AsSpan(rulesMenuItems));
        if (picked >= 0)
        {
            rulesMenuItems.Clear();
        }

        return picked;
    }

    private void OpenRulesMenu(Rect anchor)
    {
        rulesMenuItems.Clear();
        for (var index = 0; index < RuleSetLabels.Length; index++)
        {
            rulesMenuItems.Add(new DropdownMenu.Item(Loc.T(RuleSetLabels[index]), string.Empty, false,
                index == selectedRuleSet));
        }

        rulesMenu.Toggle("uno.ruleset", anchor);
    }

    private void DrawFinishedBanner(PhoneTheme theme, float scale, GameRoomState held)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var height = 54f * scale;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var accent = Core.Apps.AppAccents.For("games");
        Squircle.Fill(drawList, origin, max, Metrics.Radius.Card * scale,
            ImGui.GetColorU32(Palette.WithAlpha(accent, 0.14f)));
        Squircle.Stroke(drawList, origin, max, Metrics.Radius.Card * scale,
            ImGui.GetColorU32(Palette.WithAlpha(accent, 0.4f)), 1f * scale);
        Typography.DrawCentered(drawList, new Vector2(origin.X + width * 0.5f, origin.Y + height * 0.5f),
            Typography.FitText(FinishedText(held), width - 24f * scale, TextStyles.SubheadlineEmphasized),
            theme.TextStrong, TextStyles.SubheadlineEmphasized);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Md * scale));
    }

    private static string FinishedText(GameRoomState held)
    {
        var roster = held.Roster!;
        var winnerName = roster.WinnerSeat >= 0 && roster.WinnerSeat < roster.Players.Length
            ? roster.Players[roster.WinnerSeat].DisplayName
            : string.Empty;
        if (held.Chess is not null)
        {
            return held.Chess.EndKind switch
            {
                GameRoomWire.ChessEndCheckmate => Loc.T(L.Games.OnlineCheckmateWin, winnerName),
                GameRoomWire.ChessEndTimeout => Loc.T(L.Games.OnlineTimeoutWin, winnerName),
                GameRoomWire.ChessEndResign => Loc.T(L.Games.OnlineResignWin, winnerName),
                GameRoomWire.ChessEndDesertion => Loc.T(L.Games.OnlineDesertWin, winnerName),
                GameRoomWire.ChessEndStalemate => Loc.T(L.Games.OnlineStalemateDraw),
                GameRoomWire.ChessEndFiftyMove => Loc.T(L.Games.OnlineFiftyDraw),
                GameRoomWire.ChessEndMaterial => Loc.T(L.Games.OnlineMaterialDraw),
                _ => winnerName.Length > 0
                    ? Loc.T(L.Games.OnlineWinner, winnerName)
                    : Loc.T(L.Games.OnlineRoundVoid),
            };
        }

        if (held.Pool is not null)
        {
            return held.Pool.EndKind switch
            {
                GameRoomWire.PoolEndEight => Loc.T(L.Games.OnlineEightWin, winnerName),
                GameRoomWire.PoolEndEightEarly => Loc.T(L.Games.OnlineEightEarlyLoss, winnerName),
                GameRoomWire.PoolEndEightScratch => Loc.T(L.Games.OnlineEightScratchLoss, winnerName),
                GameRoomWire.PoolEndTimeout => Loc.T(L.Games.OnlineTimeoutWin, winnerName),
                GameRoomWire.PoolEndResign => Loc.T(L.Games.OnlineResignWin, winnerName),
                GameRoomWire.PoolEndDesertion => Loc.T(L.Games.OnlineDesertWin, winnerName),
                _ => winnerName.Length > 0
                    ? Loc.T(L.Games.OnlineWinner, winnerName)
                    : Loc.T(L.Games.OnlineRoundVoid),
            };
        }

        return winnerName.Length > 0
            ? Loc.T(L.Games.OnlineWinner, winnerName)
            : Loc.T(L.Games.OnlineRoundVoid);
    }

    private void DrawCodeCard(PhoneTheme theme, float scale, Vector4 accent)
    {
        var code = RoomCode();
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var height = 64f * scale;
        var max = new Vector2(origin.X + width, origin.Y + height);
        Squircle.Fill(drawList, origin, max, Metrics.Radius.Card * scale,
            ImGui.GetColorU32(theme.GroupedCard));

        Typography.Draw(drawList, new Vector2(origin.X + 14f * scale, origin.Y + 10f * scale),
            Loc.T(L.Games.OnlineRoomCode), theme.TextMuted, TextStyles.Caption1);
        var spaced = code.Length == 0 ? "······" : string.Join(' ', code.ToCharArray());
        Typography.Draw(drawList, new Vector2(origin.X + 14f * scale, origin.Y + 28f * scale), spaced,
            theme.TextStrong, TextStyles.Title2);

        var copied = Environment.TickCount64 - copiedAtTick < 1500;
        var pillCenter = new Vector2(max.X - 52f * scale, origin.Y + height * 0.5f);
        if (GameHud.Button(pillCenter, new Vector2(80f * scale, 32f * scale),
                Loc.T(copied ? L.Games.OnlineCodeCopied : L.Games.OnlineCopyCode), accent, theme)
            && code.Length > 0)
        {
            ImGui.SetClipboardText(code);
            copiedAtTick = Environment.TickCount64;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private string RoomCode()
    {
        var rooms = store.Rooms;
        var roomId = store.Room.RoomId;
        for (var index = 0; index < rooms.Length; index++)
        {
            if (string.Equals(rooms[index].RoomId, roomId, StringComparison.Ordinal))
            {
                return rooms[index].JoinCode;
            }
        }

        return string.Empty;
    }

    private void DrawRosterRow(Rect row, PhoneTheme theme, float scale, GameRoomRoster roster,
        GameRoomMemberView player, bool viewerIsHost)
    {
        var drawList = ImGui.GetWindowDrawList();
        var isRoomHost = string.Equals(player.UserId, roster.HostUserId, StringComparison.Ordinal);
        var name = player.DisplayName;
        if (isRoomHost)
        {
            name = name + " · " + Loc.T(L.Games.OnlineHostBadge);
        }

        if (player.Away)
        {
            name = name + " · " + Loc.T(L.Games.OnlineAway);
        }

        var kickReserve = viewerIsHost && !isRoomHost ? 86f * scale : 0f;
        var textWidth = row.Width - kickReserve - 8f * scale;
        Typography.Draw(drawList, new Vector2(row.Min.X, row.Center.Y - 16f * scale),
            Typography.FitText(name, textWidth, TextStyles.SubheadlineEmphasized),
            player.Away ? theme.TextMuted : theme.TextStrong, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(row.Min.X, row.Center.Y + 4f * scale),
            Loc.T(L.Games.OnlineWins, player.Wins.ToString(Loc.Culture)), theme.TextMuted,
            TextStyles.Footnote);

        if (kickReserve > 0f && GameHud.Button(
                new Vector2(row.Max.X - 40f * scale, row.Center.Y),
                new Vector2(76f * scale, 28f * scale), Loc.T(L.Games.OnlineKick),
                new Vector4(0.85f, 0.35f, 0.32f, 1f), theme) && !store.IntentInFlight)
        {
            store.Kick(player.UserId);
        }
    }

    private void DrawInlineNotice(PhoneTheme theme, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var message = Loc.T(GamesOnlineText.ReasonMessage(inlineReason));
        Typography.DrawWrappedLeft(new Vector2(origin.X, origin.Y + 4f * scale), message, theme.TextMuted,
            TextStyles.Footnote, width);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 22f * scale));
    }
}
