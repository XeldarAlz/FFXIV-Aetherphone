using System.Runtime.InteropServices;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Hub;
using Aetherphone.Apps.Games.MiniGolf;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Games.Online;

// One room, three faces: the lobby with the code and the roster, the live table of whichever game
// the room hosts, and the winner screen that leads back to another round. Everything rendered
// here is the server's word; a tap only ever sends an intent and the next event repaints the
// truth.
internal sealed class OnlineRoomView : IDisposable
{
    private const string NavId = "games.room.nav";
    private const float HeaderHeight = 42f;
    private const float RosterRowHeight = 60f;
    private const float AvatarRadius = 18f;
    private const float RowTextGap = 12f;
    private const float CodeCardHeight = 112f;
    private const float CodeCardPadding = 16f;
    private const float CopyPillHeight = Button.SmallHeight;
    private const float RulesRowHeight = 52f;
    private const float PrimaryHeight = 44f;
    private const float SecondaryHeight = Button.RegularHeight;
    private const float KickPillHeight = 28f;
    private const float BannerHeight = 58f;
    private const float BannerIconSize = 20f;
    private const float WaitingSpinnerRadius = 7f;
    private const float ChevronSize = 12f;
    private const long NoticeMilliseconds = 4_000;
    private const long CopiedMilliseconds = 1_500;
    private const int MaxSeats = 8;

    private static readonly Vector4 StreakGold = new(0.98f, 0.78f, 0.30f, 1f);

    private static readonly LocString[] RuleSetLabels = [L.Games.OnlineRuleDefault, L.Games.OnlineRuleHouse];

    private static readonly LocString[] CourseLabels = [L.MiniGolf.NineHoles, L.MiniGolf.EighteenHoles];

    private static readonly int[] CourseHoles = [MiniGolfCourse.FrontNine, MiniGolfCourse.HoleCount];

    private static readonly string[] KickIds =
    [
        "games.room.kick.0", "games.room.kick.1", "games.room.kick.2", "games.room.kick.3", "games.room.kick.4",
        "games.room.kick.5", "games.room.kick.6", "games.room.kick.7",
    ];

    private readonly GameRoomsStore store;
    private readonly DropdownMenu rulesMenu = new();
    private readonly List<DropdownMenu.Item> rulesMenuItems = new();
    private readonly OnlineUnoTable unoTable;
    private readonly OnlineChessTable chessTable;
    private readonly OnlinePoolTable poolTable;
    private readonly OnlineConnectFourTable connectFourTable;
    private readonly OnlineBroadsideTable broadsideTable;
    private readonly OnlineLuckyDrawTable luckyDrawTable;
    private readonly OnlineCraterTable craterTable;
    private readonly OnlineMiniGolfTable miniGolfTable;
    private readonly OnlineFinishHold finishHold = new();
    private readonly string[] rosterNames = new string[MaxSeats];
    private readonly string[] rosterWins = new string[MaxSeats];
    private readonly string[] rosterInitials = new string[MaxSeats];

    private string inlineReason = string.Empty;
    private int selectedRuleSet;
    private long noticeAtTick;
    private long copiedAtTick;
    private int lastSeenPhase = -1;
    private string spacedCode = string.Empty;
    private string spacedSource = string.Empty;
    private string finishedLabel = string.Empty;
    private string[] finishedLines = Array.Empty<string>();
    private string finishedWrapSource = string.Empty;
    private float finishedWrapWidth;
    private float finishedWrapScale;
    private int finishedWrapGeneration = -1;
    private float finishedLinesWidth;
    private GameRoomRoster? labeledRoster;
    private LanguageInfo? labelLanguage;

    public OnlineRoomView(GameRoomsStore store, ITextureProvider textures)
    {
        this.store = store;
        unoTable = new OnlineUnoTable(store);
        chessTable = new OnlineChessTable(store);
        poolTable = new OnlinePoolTable(store);
        connectFourTable = new OnlineConnectFourTable(store);
        broadsideTable = new OnlineBroadsideTable(store);
        luckyDrawTable = new OnlineLuckyDrawTable(store);
        craterTable = new OnlineCraterTable(store, textures);
        miniGolfTable = new OnlineMiniGolfTable(store);
    }

    public void Dispose()
    {
        craterTable.Dispose();
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
        connectFourTable.Reset();
        broadsideTable.Reset();
        luckyDrawTable.Reset();
        craterTable.Reset();
        miniGolfTable.Reset();
        finishHold.Clear();
        lastSeenPhase = -1;
        labeledRoster = null;
        finishedLabel = string.Empty;
    }

    public bool WantsLandscape =>
        (ShowsPool(store.Room.State) || ShowsCrater(store.Room.State)) && store.Room.RoomId.Length > 0;

    public void Draw(in PhoneContext context, Action back, AppSkin ui, bool landscape, string backTitle)
    {
        var held = store.Room.State;
        TrackPhase(held);
        var scale = UiScale.Current;
        if (store.Room.RoomId.Length > 0 && ShowsTable(held))
        {
            DrawTable(context, back, ui, landscape, held!, scale);
            return;
        }

        var navBar = AppHeader.BeginLargeTitle(context);
        Consume(back);
        var session = store.Room;
        if (session.RoomId.Length == 0)
        {
            DrawClosed(navBar.Body, ui, session.ClosedReason, back);
        }
        else if (held is null || held.Roster is null)
        {
            LoadingPulse.Draw(navBar.Body.Center, 16f * scale, ui.Accent, ui.MutedInk, Loc.T(L.Games.OnlineLoading));
        }
        else
        {
            DrawLobby(navBar.Body, ui, scale, held);
        }

        AppHeader.EndLargeTitle(in navBar, context, NavId, Loc.T(GamesOnlineText.GameName(held?.Snapshot.GameKind)),
            NavBarStyle.From(ui), ReadOnlySpan<NavBarButton>.Empty, backTitle, back);
    }

    private void DrawTable(in PhoneContext context, Action back, AppSkin ui, bool landscape, GameRoomState held,
        float scale)
    {
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

        if (held.ConnectFour is not null)
        {
            connectFourTable.Draw(body, theme, scale, held.Snapshot, held.ConnectFour, FreshNotice(), finishHold);
            return;
        }

        if (held.Broadside is not null)
        {
            broadsideTable.Draw(body, theme, scale, held.Snapshot, held.Broadside, store.Room.Private?.Broadside,
                FreshNotice(), finishHold);
            return;
        }

        if (held.LuckyDraw is not null)
        {
            luckyDrawTable.Draw(body, theme, scale, held.Snapshot, held.LuckyDraw, FreshNotice(), finishHold);
            return;
        }

        if (held.Crater is not null)
        {
            craterTable.Draw(body, theme, scale, held.Snapshot, held.Crater, FreshNotice(),
                fullScreenTable ? back : null, finishHold);
            return;
        }

        if (held.MiniGolf is not null)
        {
            miniGolfTable.Draw(body, theme, scale, held.Snapshot, held.MiniGolf, FreshNotice(), finishHold);
        }
    }

    private void DrawHeader(in PhoneContext context, Action back, AppSkin ui, GameRoomState held, float scale)
    {
        var title = Loc.T(GamesOnlineText.GameName(held.Snapshot.GameKind));
        var isHost = IsHost(held.Roster!);
        var leaveLabel = LeaveLabel(isHost);
        AppHeader.Draw(context, "games.room.header", title, AppSkin.HeaderActionWidth(leaveLabel) + 18f * scale,
            back);
        if (ui.HeaderAction(context.Content, leaveLabel, !store.IntentInFlight))
        {
            LeaveOrClose(isHost);
        }
    }

    private bool ShowsPool(GameRoomState? held) => held is { Pool: not null } && ShowsTable(held);

    private bool ShowsCrater(GameRoomState? held) => held is { Crater: not null } && ShowsTable(held);

    private bool ShowsTable(GameRoomState? held)
    {
        if (held is null || held.Roster is null
            || (held.Uno is null && held.Chess is null && held.Pool is null && held.ConnectFour is null
                && held.Broadside is null && held.LuckyDraw is null && held.Crater is null
                && held.MiniGolf is null))
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
        if (phase == lastSeenPhase && ReferenceEquals(labelLanguage, Loc.Current))
        {
            return;
        }

        if (phase != lastSeenPhase)
        {
            if (phase == GameRoomWire.PhaseFinished && lastSeenPhase == GameRoomWire.PhasePlaying)
            {
                finishHold.Begin(FinishedText(held));
            }
            else
            {
                finishHold.Clear();
            }
        }

        finishedLabel = phase == GameRoomWire.PhaseFinished ? FinishedText(held) : string.Empty;
        labelLanguage = Loc.Current;
        labeledRoster = null;
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

    private static void DrawClosed(Rect body, AppSkin ui, string reason, Action back)
    {
        var message = reason switch
        {
            GameRoomWire.ReasonKicked => Loc.T(L.Games.OnlineKicked),
            GameRoomWire.ReasonRestarting => Loc.T(L.Games.OnlineRestarting),
            _ => Loc.T(L.Games.OnlineRoomEnded),
        };
        if (EmptyState.Draw(body, ui, FontAwesomeIcon.DoorClosed, message, string.Empty, Loc.T(L.Common.Close)))
        {
            back();
        }
    }

    // The lobby and the finished screen are the same room at rest: the roster, the code, and one
    // primary button whose label is the only thing the phase changes.
    private void DrawLobby(Rect body, AppSkin ui, float scale, GameRoomState held)
    {
        using var surface = AppSurface.Begin(body);
        var theme = ui.Theme;
        var phase = held.Snapshot.Phase;
        var roster = held.Roster!;
        var players = roster.Players;
        var isHost = IsHost(roster);
        var accent = OnlineGameArt.Accent(held.Snapshot.GameKind);
        RefreshRosterLabels(roster);
        rulesMenu.Gate();
        var picked = DrawRulesMenu(body, theme);
        if (picked >= 0)
        {
            selectedRuleSet = picked;
        }

        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var left = origin.X;
        var y = origin.Y;
        if (phase == GameRoomWire.PhaseFinished && finishedLabel.Length > 0)
        {
            y = DrawFinishedBanner(drawList, ui, left, y, width, scale, accent) + Metrics.Space.Md * scale;
        }

        y = DrawCodeCard(drawList, ui, left, y, width, scale, accent);
        if (inlineReason.Length > 0 && Environment.TickCount64 - noticeAtTick < NoticeMilliseconds)
        {
            var message = Loc.T(GamesOnlineText.ReasonMessage(inlineReason));
            y += Metrics.Space.Sm * scale;
            y += Typography.DrawWrappedLeft(new Vector2(left, y), message, theme.Danger, TextStyles.Footnote, width);
        }

        var kind = held.Snapshot.GameKind;
        var options = LobbyOptions(kind);
        if (isHost && options.Length > 0)
        {
            y = DrawRulesRow(drawList, ui, left, y + Metrics.Space.Md * scale, width, scale, kind, options);
        }

        y += HubMetrics.SectionGap * scale;
        GamesHubArt.Section(drawList, ui, left, y, width, Loc.T(L.GamesHub.Players), string.Empty, string.Empty);
        y += GamesHubArt.SectionHeight * scale;
        ImGui.SetCursorScreenPos(new Vector2(left, y));
        var rowCount = Math.Max(1, Math.Min(players.Length, MaxSeats));
        var card = GroupCard.Begin(ui, rowCount, RosterRowHeight);
        card.SeparatorInset = AvatarRadius * 2f + RowTextGap;
        for (var index = 0; index < players.Length && index < MaxSeats; index++)
        {
            DrawRosterRow(drawList, card.NextRow(), ui, scale, roster, players[index], index, isHost, accent);
        }

        if (players.Length == 0)
        {
            card.NextRow();
        }

        card.End();
        y = card.Bounds.Max.Y + HubMetrics.SectionGap * scale;
        y = DrawPrimary(drawList, ui, left, y, width, scale, isHost, phase, players.Length, accent, StartOption(kind));
        y += Metrics.Space.Lg * scale;
        var leaveLabel = LeaveLabel(isHost);
        var leaveWidth = MathF.Min(width, GamesHubArt.ButtonWidth(leaveLabel, SecondaryHeight * scale) +
            Metrics.Space.Xxl * scale);
        var leaveRect = new Rect(new Vector2(left + (width - leaveWidth) * 0.5f, y),
            new Vector2(left + (width + leaveWidth) * 0.5f, y + SecondaryHeight * scale));
        if (Button.Draw(drawList, leaveRect, leaveLabel, ui.Ink, ButtonStyle.Tinted, ButtonRole.Destructive,
                !store.IntentInFlight, id: "games.room.leave"))
        {
            LeaveOrClose(isHost);
        }

        y = leaveRect.Max.Y;
        ImGui.SetCursorScreenPos(new Vector2(left, y));
        ImGui.Dummy(new Vector2(width, Metrics.Space.Xl * scale));
    }

    private float DrawPrimary(ImDrawListPtr drawList, AppSkin ui, float left, float top, float width, float scale,
        bool isHost, int phase, int playerCount, Vector4 accent, int startOption)
    {
        var height = PrimaryHeight * scale;
        if (!isHost)
        {
            var label = Loc.T(L.Games.OnlineWaitingHost);
            var labelSize = Typography.Measure(label, TextStyles.Subheadline);
            var spinner = WaitingSpinnerRadius * scale;
            var total = MathF.Min(width, labelSize.X + spinner * 2f + Metrics.Space.Sm * scale);
            var startX = left + (width - total) * 0.5f;
            LoadingPulse.Spinner(new Vector2(startX + spinner, top + height * 0.5f), spinner, accent);
            Typography.Draw(drawList, new Vector2(startX + spinner * 2f + Metrics.Space.Sm * scale,
                    top + (height - labelSize.Y) * 0.5f),
                Typography.FitText(label, MathF.Max(1f, width - spinner * 2f - Metrics.Space.Sm * scale),
                    TextStyles.Subheadline), ui.MutedInk, TextStyles.Subheadline);
            return top + height;
        }

        var enough = playerCount >= 2;
        var startLabel = phase == GameRoomWire.PhaseFinished
            ? Loc.T(L.Games.OnlineRematch)
            : Loc.T(L.Games.OnlineStart);
        var rect = new Rect(new Vector2(left, top), new Vector2(left + width, top + height));
        if (Button.Draw(drawList, rect, startLabel, ui.Ink.WithAccent(accent), enabled: enough && !store.ActInFlight,
                id: "games.room.start"))
        {
            store.SendStart(startOption);
        }

        if (enough)
        {
            return rect.Max.Y;
        }

        var hint = Loc.T(L.Games.OnlineNeedPlayers);
        var hintTop = rect.Max.Y + Metrics.Space.Sm * scale;
        return Typography.DrawWrappedCentered(drawList, hint, TextStyles.Footnote, ui.MutedInk,
            new Vector2(left + width * 0.5f, hintTop), width);
    }

    private static LocString[] LobbyOptions(string kind)
    {
        if (string.Equals(kind, GameRoomWire.MiniGolfKind, StringComparison.Ordinal))
        {
            return CourseLabels;
        }

        return string.Equals(kind, GameRoomWire.UnoKind, StringComparison.Ordinal)
            ? RuleSetLabels
            : Array.Empty<LocString>();
    }

    private int StartOption(string kind) => string.Equals(kind, GameRoomWire.MiniGolfKind, StringComparison.Ordinal)
        ? CourseHoles[Math.Clamp(selectedRuleSet, 0, CourseHoles.Length - 1)]
        : selectedRuleSet;

    private float DrawRulesRow(ImDrawListPtr drawList, AppSkin ui, float left, float top, float width, float scale,
        string kind, LocString[] options)
    {
        var height = RulesRowHeight * scale;
        var rect = new Rect(new Vector2(left, top), new Vector2(left + width, top + height));
        var rounding = Metrics.Radius.Widget * scale;
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        ui.Card(drawList, rect.Min, rect.Max, rounding);
        if (hovered)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var pad = Metrics.Space.Lg * scale;
        var labelHeight = Typography.LineHeight(TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(rect.Min.X + pad, rect.Center.Y - labelHeight * 0.5f),
            Loc.T(string.Equals(kind, GameRoomWire.MiniGolfKind, StringComparison.Ordinal)
                ? L.Games.OnlineMiniGolfCourse
                : L.GamesHub.Rules), ui.TitleInk, TextStyles.Headline);
        var chevron = ChevronSize * scale;
        PhoneIcon.Draw(drawList, new Vector2(rect.Max.X - pad - chevron * 0.5f, rect.Center.Y),
            PhoneIcons.ChevronDown, ui.MutedInk, chevron);
        var value = Loc.T(options[Math.Clamp(selectedRuleSet, 0, options.Length - 1)]);
        var valueSize = Typography.Measure(value, TextStyles.Body);
        Typography.Draw(drawList,
            new Vector2(rect.Max.X - pad - chevron - Metrics.Space.Sm * scale - valueSize.X,
                rect.Center.Y - valueSize.Y * 0.5f), value, ui.MutedInk, TextStyles.Body);
        if (UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            OpenRulesMenu(rect, options);
        }

        return rect.Max.Y;
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

    private void OpenRulesMenu(Rect anchor, LocString[] options)
    {
        rulesMenuItems.Clear();
        for (var index = 0; index < options.Length; index++)
        {
            rulesMenuItems.Add(new DropdownMenu.Item(Loc.T(options[index]), string.Empty, false,
                index == selectedRuleSet));
        }

        rulesMenu.Toggle("uno.ruleset", anchor);
    }

    private float DrawFinishedBanner(ImDrawListPtr drawList, AppSkin ui, float left, float top, float width,
        float scale, Vector4 accent)
    {
        var pad = Metrics.Space.Lg * scale;
        var gap = Metrics.Space.Md * scale;
        var iconSize = BannerIconSize * scale;
        var style = TextStyles.SubheadlineEmphasized;
        WrapFinished(MathF.Max(1f, width - pad * 2f - iconSize - gap), style);
        var lineHeight = Typography.LineHeight(style);
        var textHeight = lineHeight * finishedLines.Length;
        var height = MathF.Max(BannerHeight * scale, textHeight + pad * 2f);
        var min = new Vector2(left, top);
        var max = new Vector2(left + width, top + height);
        var rounding = Metrics.Radius.Widget * scale;
        Material.AccentGlass(drawList, min, max, rounding, scale, Palette.WithAlpha(accent, 0.22f), 1f);
        var blockWidth = iconSize + gap + finishedLinesWidth;
        var blockLeft = min.X + MathF.Max(pad, (width - blockWidth) * 0.5f);
        var centerY = min.Y + height * 0.5f;
        ProgressRing.CenterIcon(drawList, new Vector2(blockLeft + iconSize * 0.5f, centerY), FontAwesomeIcon.Trophy,
            GamePalette.Star, iconSize);
        var textLeft = blockLeft + iconSize + gap;
        var textTop = centerY - textHeight * 0.5f;
        for (var lineIndex = 0; lineIndex < finishedLines.Length; lineIndex++)
        {
            Typography.Draw(drawList, new Vector2(textLeft, textTop + lineHeight * lineIndex), finishedLines[lineIndex],
                ui.TitleInk, style);
        }

        return max.Y;
    }

    private void WrapFinished(float maxWidth, in TextStyle style)
    {
        var generation = Plugin.Fonts.Generation;
        if (maxWidth == finishedWrapWidth && style.Scale == finishedWrapScale && generation == finishedWrapGeneration &&
            string.Equals(finishedLabel, finishedWrapSource, StringComparison.Ordinal))
        {
            return;
        }

        finishedWrapSource = finishedLabel;
        finishedWrapWidth = maxWidth;
        finishedWrapScale = style.Scale;
        finishedWrapGeneration = generation;
        finishedLines = Typography.WrapText(finishedLabel, style, maxWidth);
        finishedLinesWidth = 0f;
        for (var lineIndex = 0; lineIndex < finishedLines.Length; lineIndex++)
        {
            finishedLinesWidth = MathF.Max(finishedLinesWidth, Typography.Measure(finishedLines[lineIndex], style).X);
        }
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

        if (held.ConnectFour is not null)
        {
            return held.ConnectFour.EndKind switch
            {
                GameRoomWire.ConnectFourEndConnect => Loc.T(L.Games.OnlineConnectFourWin, winnerName),
                GameRoomWire.ConnectFourEndDraw => Loc.T(L.Games.OnlineConnectFourDraw),
                GameRoomWire.ConnectFourEndTimeout => Loc.T(L.Games.OnlineTimeoutWin, winnerName),
                GameRoomWire.ConnectFourEndResign => Loc.T(L.Games.OnlineResignWin, winnerName),
                GameRoomWire.ConnectFourEndDesertion => Loc.T(L.Games.OnlineDesertWin, winnerName),
                _ => winnerName.Length > 0
                    ? Loc.T(L.Games.OnlineWinner, winnerName)
                    : Loc.T(L.Games.OnlineRoundVoid),
            };
        }

        if (held.Broadside is not null)
        {
            return held.Broadside.EndKind switch
            {
                GameRoomWire.BroadsideEndFleet => Loc.T(L.Games.OnlineBroadsideWin, winnerName),
                GameRoomWire.BroadsideEndTimeout => Loc.T(L.Games.OnlineTimeoutWin, winnerName),
                GameRoomWire.BroadsideEndResign => Loc.T(L.Games.OnlineResignWin, winnerName),
                GameRoomWire.BroadsideEndDesertion => Loc.T(L.Games.OnlineDesertWin, winnerName),
                _ => winnerName.Length > 0
                    ? Loc.T(L.Games.OnlineWinner, winnerName)
                    : Loc.T(L.Games.OnlineRoundVoid),
            };
        }

        if (held.LuckyDraw is not null && winnerName.Length > 0)
        {
            return held.LuckyDraw.EndKind switch
            {
                GameRoomWire.LuckyDrawEndTarget => Loc.T(L.Games.OnlineLuckyDrawWin, winnerName),
                GameRoomWire.LuckyDrawEndDesertion => Loc.T(L.Games.OnlineLuckyDrawDesertWin, winnerName),
                _ => Loc.T(L.Games.OnlineWinner, winnerName),
            };
        }

        if (held.Crater is not null)
        {
            return held.Crater.EndKind switch
            {
                GameRoomWire.CraterEndKnockout => Loc.T(L.Games.OnlineCraterWin, winnerName),
                GameRoomWire.CraterEndDraw => Loc.T(L.Games.OnlineCraterDraw),
                GameRoomWire.CraterEndTimeout => Loc.T(L.Games.OnlineTimeoutWin, winnerName),
                GameRoomWire.CraterEndResign => Loc.T(L.Games.OnlineResignWin, winnerName),
                GameRoomWire.CraterEndDesertion => Loc.T(L.Games.OnlineDesertWin, winnerName),
                _ => winnerName.Length > 0
                    ? Loc.T(L.Games.OnlineWinner, winnerName)
                    : Loc.T(L.Games.OnlineRoundVoid),
            };
        }

        if (held.MiniGolf is not null)
        {
            return MiniGolfFinishedText(held.MiniGolf, winnerName);
        }

        return winnerName.Length > 0
            ? Loc.T(L.Games.OnlineWinner, winnerName)
            : Loc.T(L.Games.OnlineRoundVoid);
    }

    // A finished course names the winner and their total, or the shared total when first place is tied.
    private static string MiniGolfFinishedText(MiniGolfRoomStateDto board, string winnerName)
    {
        if (string.Equals(board.EndKind, GameRoomWire.MiniGolfEndDesertion, StringComparison.Ordinal))
        {
            return winnerName.Length > 0
                ? Loc.T(L.Games.OnlineLuckyDrawDesertWin, winnerName)
                : Loc.T(L.Games.OnlineRoundVoid);
        }

        var players = board.Players ?? Array.Empty<MiniGolfPlayerDto>();
        var best = -1;
        for (var index = 0; index < players.Length; index++)
        {
            if (players[index].Place == 1)
            {
                best = players[index].Total;
            }
        }

        if (best < 0)
        {
            return Loc.T(L.Games.OnlineRoundVoid);
        }

        return winnerName.Length > 0
            ? Loc.T(L.Games.OnlineMiniGolfWin, winnerName, GameNumber.Label(best))
            : Loc.T(L.Games.OnlineMiniGolfTie, GameNumber.Label(best));
    }

    private float DrawCodeCard(ImDrawListPtr drawList, AppSkin ui, float left, float top, float width, float scale,
        Vector4 accent)
    {
        var code = RoomCode();
        var height = CodeCardHeight * scale;
        var min = new Vector2(left, top);
        var max = new Vector2(left + width, top + height);
        var rounding = Metrics.Radius.Widget * scale;
        ui.Card(drawList, min, max, rounding);
        var pad = CodeCardPadding * scale;
        var copied = Environment.TickCount64 - copiedAtTick < CopiedMilliseconds;
        var copyLabel = Loc.T(copied ? L.Games.OnlineCodeCopied : L.Games.OnlineCopyCode);
        var pillHeight = CopyPillHeight * scale;
        var pillWidth = GamesHubArt.ButtonWidth(copyLabel, pillHeight);
        var pillRect = new Rect(new Vector2(max.X - pad - pillWidth, min.Y + pad),
            new Vector2(max.X - pad, min.Y + pad + pillHeight));
        Typography.Draw(drawList, new Vector2(min.X + pad, min.Y + pad), Loc.T(L.Games.OnlineRoomCode), ui.MutedInk,
            TextStyles.FootnoteEmphasized);
        var codeTop = min.Y + pad + Typography.LineHeight(TextStyles.FootnoteEmphasized) + Metrics.Space.Xxs * scale;
        var codeWidth = MathF.Max(1f, pillRect.Min.X - Metrics.Space.Md * scale - min.X - pad);
        Typography.Draw(drawList, new Vector2(min.X + pad, codeTop),
            Typography.FitText(SpacedCode(code), codeWidth, TextStyles.Title1), ui.TitleInk, TextStyles.Title1);
        var hintHeight = Typography.LineHeight(TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(min.X + pad, max.Y - pad - hintHeight),
            Typography.FitText(Loc.T(L.GamesHub.CodeHint), MathF.Max(1f, width - pad * 2f), TextStyles.Footnote),
            ui.MutedInk, TextStyles.Footnote);
        if (Button.Draw(drawList, pillRect, copyLabel, ui.Ink.WithAccent(accent),
                copied ? ButtonStyle.Gray : ButtonStyle.Prominent, enabled: code.Length > 0, id: "games.room.copy"))
        {
            ImGui.SetClipboardText(code);
            copiedAtTick = Environment.TickCount64;
        }

        return max.Y;
    }

    private string SpacedCode(string code)
    {
        if (code.Length == 0)
        {
            return "······";
        }

        if (!ReferenceEquals(code, spacedSource))
        {
            spacedSource = code;
            spacedCode = string.Join(' ', code.ToCharArray());
        }

        return spacedCode;
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

    private void RefreshRosterLabels(GameRoomRoster roster)
    {
        if (ReferenceEquals(roster, labeledRoster))
        {
            return;
        }

        labeledRoster = roster;
        var players = roster.Players;
        for (var index = 0; index < players.Length && index < MaxSeats; index++)
        {
            var player = players[index];
            var name = player.DisplayName;
            if (string.Equals(player.UserId, roster.HostUserId, StringComparison.Ordinal))
            {
                name = name + " · " + Loc.T(L.Games.OnlineHostBadge);
            }

            if (player.Away)
            {
                name = name + " · " + Loc.T(L.Games.OnlineAway);
            }

            rosterNames[index] = name;
            rosterWins[index] = Loc.T(L.Games.OnlineWins, player.Wins.ToString(Loc.Culture));
            rosterInitials[index] = player.DisplayName.Length > 0
                ? char.IsSurrogate(player.DisplayName[0])
                    ? player.DisplayName[..Math.Min(2, player.DisplayName.Length)]
                    : player.DisplayName[..1].ToUpper(Loc.Culture)
                : "?";
        }
    }

    private void DrawRosterRow(ImDrawListPtr drawList, Rect row, AppSkin ui, float scale, GameRoomRoster roster,
        GameRoomMemberView player, int index, bool viewerIsHost, Vector4 accent)
    {
        var theme = ui.Theme;
        var isRoomHost = string.Equals(player.UserId, roster.HostUserId, StringComparison.Ordinal);
        var radius = AvatarRadius * scale;
        var avatar = new Vector2(row.Min.X + radius, row.Center.Y);
        var avatarTint = player.Away ? ui.MutedInk : accent;
        drawList.AddCircleFilled(avatar, radius, ImGui.GetColorU32(Palette.WithAlpha(avatarTint, 0.20f)), 32);
        Typography.DrawCentered(drawList, avatar, rosterInitials[index], avatarTint, TextStyles.Headline);
        if (isRoomHost)
        {
            ProgressRing.CenterIcon(drawList, avatar + new Vector2(radius * 0.72f, -radius * 0.72f),
                FontAwesomeIcon.Crown, StreakGold, radius * 0.6f);
        }

        var kickLabel = Loc.T(L.Games.OnlineKick);
        var kickHeight = KickPillHeight * scale;
        var kickWidth = viewerIsHost && !isRoomHost
            ? GamesHubArt.ButtonWidth(kickLabel, kickHeight)
            : 0f;
        var textLeft = avatar.X + radius + RowTextGap * scale;
        var textWidth = MathF.Max(1f, row.Max.X - kickWidth - (kickWidth > 0f ? Metrics.Space.Sm * scale : 0f)
                                      - textLeft);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var subtitleHeight = Typography.LineHeight(TextStyles.Footnote);
        var textTop = row.Center.Y - (titleHeight + subtitleHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText(rosterNames[index], textWidth, TextStyles.Headline),
            player.Away ? theme.TextMuted : ui.TitleInk, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + titleHeight),
            Typography.FitText(rosterWins[index], textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        if (kickWidth <= 0f)
        {
            return;
        }

        var kickRect = new Rect(new Vector2(row.Max.X - kickWidth, row.Center.Y - kickHeight * 0.5f),
            new Vector2(row.Max.X, row.Center.Y + kickHeight * 0.5f));
        if (Button.Draw(drawList, kickRect, kickLabel, ui.Ink, ButtonStyle.Tinted, ButtonRole.Destructive,
                !store.IntentInFlight, id: KickIds[index]))
        {
            store.Kick(player.UserId);
        }
    }
}
