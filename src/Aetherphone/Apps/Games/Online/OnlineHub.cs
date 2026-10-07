using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Online;

// The friends lobby: host a room, type a code, or walk back into a room you are already in. The
// code IS the door, so this screen owns exactly three verbs and the room screen owns the rest.
internal sealed class OnlineHub
{
    private const string SettingsAppId = "settings";
    private const float HostCardHeight = 84f;
    private const float HostCardGap = 10f;
    private const float MedallionRadius = 25f;
    private const float RoomMedallionRadius = 18f;
    private const float RoomRowHeight = 64f;
    private const float HostPillHeight = Button.RegularHeight;
    private const float HostPillMinWidth = 72f;
    private const float CardPadding = 16f;
    private const float TextGap = 14f;
    private const float JoinGap = 8f;
    private const float JoinPillMinWidth = 84f;
    private const float ChevronSize = 13f;
    private const float LampOffset = 16f;
    private const int CodeBufferLength = 16;

    private static readonly string[] HostIds =
        ["games.host.uno", "games.host.chess", "games.host.pool", "games.host.connectfour", "games.host.broadside",
            "games.host.luckydraw", "games.host.crater", "games.host.minigolf"];

    private readonly GameRoomsStore store;
    private readonly Action<string, string> openRoom;
    private readonly PullToRefresh refresh = new();

    private string codeBuffer = string.Empty;
    private string inlineReason = string.Empty;
    private string preferredKind = string.Empty;
    private string unoHint = string.Empty;
    private GameRoomCardDto[] labeledRooms = Array.Empty<GameRoomCardDto>();
    private string[] roomTitles = Array.Empty<string>();
    private string[] roomSubtitles = Array.Empty<string>();
    private bool roomsFailed;

    public OnlineHub(GameRoomsStore store, Action<string, string> openRoom)
    {
        this.store = store;
        this.openRoom = openRoom;
    }

    public void Reset()
    {
        preferredKind = string.Empty;
        inlineReason = string.Empty;
        codeBuffer = string.Empty;
        roomsFailed = false;
        ResetLabels();
    }

    public void ResetLabels()
    {
        unoHint = string.Empty;
        labeledRooms = Array.Empty<GameRoomCardDto>();
    }

    public void Highlight(string kind) => preferredKind = kind;

    public void Consume()
    {
        if (store.TakeRoomsFailure())
        {
            roomsFailed = true;
        }

        var answer = store.TakeRoomAnswer();
        if (answer is null)
        {
            return;
        }

        if (answer.Intent is not (GameRoomIntent.Created or GameRoomIntent.Joined))
        {
            return;
        }

        if (answer.Granted && answer.Room is not null)
        {
            inlineReason = string.Empty;
            codeBuffer = string.Empty;
            store.Enter(answer.Room.RoomId);
            openRoom(answer.Room.RoomId, answer.Room.GameKind);
            return;
        }

        inlineReason = answer.Reason;
        UiFeedback.Play(UiSound.Caution);
    }

    public void Draw(Rect body, float pull, bool dragging, AppSkin ui, INavigator navigation)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        if (store.AccountId.Length == 0)
        {
            if (GamesHubArt.StateScreen(drawList, ui, body, FontAwesomeIcon.UserFriends,
                    Loc.T(L.GamesHub.SignInTitle), Loc.T(L.Games.OnlineSignIn),
                    navigation.IsAvailable(SettingsAppId) ? Loc.T(L.GamesHub.OpenSettings) : string.Empty,
                    "games.together.signin"))
            {
                navigation.Open(SettingsAppId);
            }

            return;
        }

        store.EnsureFresh();
        refresh.Draw(body, pull, dragging, store.LoadingRooms, ui.MutedInk, RefreshNow);
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var left = origin.X;
        var y = origin.Y;
        GamesHubArt.Section(drawList, ui, left, y, width, Loc.T(L.Games.OnlineHost), string.Empty, string.Empty);
        y += GamesHubArt.SectionHeight * scale;
        var kinds = OnlineGameArt.Kinds;
        for (var index = 0; index < kinds.Length; index++)
        {
            y = DrawHostCard(drawList, ui, left, y, width, scale, kinds[index], HostIds[index]) + HostCardGap * scale;
        }

        y += GamesHubArt.SectionGap * scale - HostCardGap * scale;
        GamesHubArt.Section(drawList, ui, left, y, width, Loc.T(L.Games.OnlineJoinHeading), string.Empty,
            string.Empty);
        y += GamesHubArt.SectionHeight * scale;
        y = DrawJoinByCode(drawList, ui, left, y, width, scale);
        if (inlineReason.Length > 0)
        {
            y = DrawNotice(drawList, ui, left, y + Metrics.Space.Md * scale, width, scale,
                Loc.T(GamesOnlineText.ReasonMessage(inlineReason)), FontAwesomeIcon.ExclamationCircle,
                ui.Theme.Danger);
        }

        y += GamesHubArt.SectionGap * scale;
        GamesHubArt.Section(drawList, ui, left, y, width, Loc.T(L.Games.OnlineMyRooms), string.Empty, string.Empty);
        if (store.LoadingRooms)
        {
            LoadingPulse.Spinner(new Vector2(left + width - Metrics.Space.Sm * scale,
                y + GamesHubArt.SectionHeight * scale * 0.5f), 7f * scale, ui.Accent);
        }

        y += GamesHubArt.SectionHeight * scale;
        y = DrawRooms(drawList, ui, left, y, width, scale);
        ImGui.SetCursorScreenPos(new Vector2(left, y));
        ImGui.Dummy(new Vector2(width, Metrics.Space.Xl * scale));
    }

    private void RefreshNow()
    {
        roomsFailed = false;
        store.RefreshNow();
    }

    private string HostHint(string kind)
    {
        if (string.Equals(kind, GameRoomWire.ChessKind, StringComparison.Ordinal))
        {
            return Loc.T(L.Games.OnlineChessHostHint);
        }

        if (string.Equals(kind, GameRoomWire.PoolKind, StringComparison.Ordinal))
        {
            return Loc.T(L.Games.OnlinePoolHostHint);
        }

        if (string.Equals(kind, GameRoomWire.ConnectFourKind, StringComparison.Ordinal))
        {
            return Loc.T(L.Games.OnlineConnectFourHostHint);
        }

        if (string.Equals(kind, GameRoomWire.BroadsideKind, StringComparison.Ordinal))
        {
            return Loc.T(L.Games.OnlineBroadsideHostHint);
        }

        if (string.Equals(kind, GameRoomWire.LuckyDrawKind, StringComparison.Ordinal))
        {
            return Loc.T(L.Games.OnlineLuckyDrawHostHint);
        }

        if (string.Equals(kind, GameRoomWire.CraterKind, StringComparison.Ordinal))
        {
            return Loc.T(L.Games.OnlineCraterHostHint);
        }

        if (string.Equals(kind, GameRoomWire.MiniGolfKind, StringComparison.Ordinal))
        {
            return Loc.T(L.Games.OnlineMiniGolfHostHint);
        }

        if (unoHint.Length == 0)
        {
            unoHint = Loc.T(L.Games.OnlineHostHint,
                OnlineGameArt.MaxPlayers(GameRoomWire.UnoKind).ToString(Loc.Culture));
        }

        return unoHint;
    }

    private float DrawHostCard(ImDrawListPtr drawList, AppSkin ui, float left, float top, float width, float scale,
        string kind, string pillId)
    {
        var height = HostCardHeight * scale;
        var rect = new Rect(new Vector2(left, top), new Vector2(left + width, top + height));
        var rounding = Metrics.Radius.Widget * scale;
        var accent = OnlineGameArt.Accent(kind);
        var pad = CardPadding * scale;
        var pillLabel = Loc.T(L.Games.OnlineHostShort);
        var pillHeight = HostPillHeight * scale;
        var pillWidth = MathF.Max(GamesHubArt.ButtonWidth(pillLabel, pillHeight), HostPillMinWidth * scale);
        var pillRect = new Rect(new Vector2(rect.Max.X - pad - pillWidth, rect.Center.Y - pillHeight * 0.5f),
            new Vector2(rect.Max.X - pad, rect.Center.Y + pillHeight * 0.5f));
        var overPill = UiInteract.Hover(pillRect.Min, pillRect.Max);
        var hovered = !overPill && UiInteract.Hover(rect.Min, rect.Max);
        var enabled = !store.IntentInFlight;
        ui.Card(drawList, rect.Min, rect.Max, rounding);
        drawList.PushClipRect(rect.Min, rect.Max, true);
        var radius = MedallionRadius * scale;
        var medallion = new Vector2(rect.Min.X + pad + radius, rect.Center.Y);
        drawList.AddCircleFilled(medallion, radius * 2.6f, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.10f)), 48);
        drawList.PopClipRect();
        if (hovered && enabled)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (string.Equals(kind, preferredKind, StringComparison.Ordinal))
        {
            Squircle.Stroke(drawList, rect.Min, rect.Max, rounding,
                ImGui.GetColorU32(Palette.WithAlpha(accent, 0.30f + 0.35f * Pulse.Wave())), 1.5f * scale);
        }

        if (hovered || overPill)
        {
            ProgressRing.Glow(medallion, radius * 1.1f, accent, 0.4f);
        }

        GamesHubArt.Medallion(drawList, kind, medallion, radius, ui.Palette.BackdropBottom, scale);
        var textLeft = medallion.X + radius + TextGap * scale;
        var textWidth = MathF.Max(1f, pillRect.Min.X - Metrics.Space.Md * scale - textLeft);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var hintHeight = Typography.LineHeight(TextStyles.Footnote);
        var textTop = rect.Center.Y - (titleHeight + hintHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText(Loc.T(GamesOnlineText.GameName(kind)), textWidth, TextStyles.Headline), ui.TitleInk,
            TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + titleHeight),
            Typography.FitText(HostHint(kind), textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        var pillClicked = Button.Draw(drawList, pillRect, pillLabel, ui.Ink.WithAccent(accent), enabled: enabled,
            id: pillId);
        var cardClicked = enabled && !overPill && UiInteract.Click(rect.Min, rect.Max, hovered);
        if ((pillClicked || cardClicked) && enabled)
        {
            inlineReason = string.Empty;
            preferredKind = kind;
            store.CreateRoom(kind);
        }

        return rect.Max.Y;
    }

    private float DrawJoinByCode(ImDrawListPtr drawList, AppSkin ui, float left, float top, float width, float scale)
    {
        var height = GlassField.HeightUnits * scale;
        var label = Loc.T(L.Games.OnlineJoin);
        var buttonHeight = Button.RegularHeight * scale;
        var pillWidth = MathF.Max(JoinPillMinWidth * scale, GamesHubArt.ButtonWidth(label, buttonHeight));
        var field = new Rect(new Vector2(left, top),
            new Vector2(left + width - pillWidth - JoinGap * scale, top + height));
        SearchBar.Surface(drawList, field, ControlInk.From(ui.Theme));
        var submitted = GlassField.Text(field, "##gameRoomCode", Loc.T(L.Games.OnlineJoinHint), ref codeBuffer,
            ui.Theme, scale, CodeBufferLength, false, ImGuiInputTextFlags.EnterReturnsTrue);
        var trimmed = codeBuffer.AsSpan().Trim();
        var ready = trimmed.Length > 0 && !store.IntentInFlight;
        var buttonTop = top + (height - buttonHeight) * 0.5f;
        var pillRect = new Rect(new Vector2(field.Max.X + JoinGap * scale, buttonTop),
            new Vector2(left + width, buttonTop + buttonHeight));
        var tapped = Button.Draw(drawList, pillRect, label, ui.Ink, enabled: ready, id: "games.join");
        if ((tapped || submitted) && ready)
        {
            inlineReason = string.Empty;
            store.JoinByCode(trimmed.ToString());
        }

        GamesHubArt.ReportAnchor("games.join", new Rect(field.Min, pillRect.Max));
        return top + height;
    }

    private float DrawRooms(ImDrawListPtr drawList, AppSkin ui, float left, float top, float width, float scale)
    {
        var rooms = store.Rooms;
        if (rooms.Length == 0)
        {
            if (roomsFailed && !store.LoadingRooms)
            {
                return DrawFailure(drawList, ui, left, top, width, scale);
            }

            var message = Loc.T(store.LoadedRooms ? L.Games.OnlineNoRooms : L.Games.OnlineLoading);
            return DrawNotice(drawList, ui, left, top, width, scale, message, FontAwesomeIcon.DoorOpen, ui.MutedInk);
        }

        RefreshRoomLabels(rooms);
        ImGui.SetCursorScreenPos(new Vector2(left, top));
        var card = GroupCard.Begin(ui, rooms.Length, RoomRowHeight);
        card.SeparatorInset = RoomMedallionRadius * 2f + TextGap;
        var entered = -1;
        for (var index = 0; index < rooms.Length; index++)
        {
            if (DrawRoomRow(drawList, ui, card.NextRow(), scale, rooms[index], roomTitles[index],
                    roomSubtitles[index]))
            {
                entered = index;
            }
        }

        card.End();
        if (entered >= 0)
        {
            inlineReason = string.Empty;
            store.Enter(rooms[entered].RoomId);
            openRoom(rooms[entered].RoomId, rooms[entered].GameKind);
        }

        return card.Bounds.Max.Y;
    }

    private float DrawFailure(ImDrawListPtr drawList, AppSkin ui, float left, float top, float width, float scale)
    {
        var bottom = DrawNotice(drawList, ui, left, top, width, scale, Loc.T(L.Common.LoadFailed),
            FontAwesomeIcon.ExclamationTriangle, ui.MutedInk);
        var label = Loc.T(L.Common.Retry);
        var height = HostPillHeight * scale;
        var pillWidth = GamesHubArt.ButtonWidth(label, height);
        var rect = new Rect(new Vector2(left + (width - pillWidth) * 0.5f, bottom + Metrics.Space.Md * scale),
            new Vector2(left + (width + pillWidth) * 0.5f, bottom + Metrics.Space.Md * scale + height));
        if (Button.Draw(drawList, rect, label, ui.Ink, enabled: !store.LoadingRooms, id: "games.rooms.retry"))
        {
            RefreshNow();
        }

        return rect.Max.Y;
    }

    private void RefreshRoomLabels(GameRoomCardDto[] rooms)
    {
        if (ReferenceEquals(rooms, labeledRooms))
        {
            return;
        }

        labeledRooms = rooms;
        roomsFailed = false;
        if (roomTitles.Length < rooms.Length)
        {
            roomTitles = new string[rooms.Length];
            roomSubtitles = new string[rooms.Length];
        }

        for (var index = 0; index < rooms.Length; index++)
        {
            var room = rooms[index];
            roomTitles[index] = Loc.T(GamesOnlineText.GameName(room.GameKind)) + " · "
                                + Loc.T(L.Games.OnlineHostedBy, room.OwnerName);
            var phase = room.Phase switch
            {
                GameRoomWire.PhasePlaying => L.Games.OnlinePhasePlaying,
                GameRoomWire.PhaseFinished => L.Games.OnlinePhaseFinished,
                _ => L.Games.OnlinePhaseLobby,
            };
            roomSubtitles[index] = Loc.T(L.Games.OnlineSeats, room.SeatedCount.ToString(Loc.Culture),
                room.MaxSeats.ToString(Loc.Culture)) + " · " + Loc.T(phase);
        }
    }

    private static bool DrawRoomRow(ImDrawListPtr drawList, AppSkin ui, Rect row, float scale, GameRoomCardDto room,
        string title, string subtitle)
    {
        var padding = Metrics.Space.Lg * scale;
        var hit = new Rect(new Vector2(row.Min.X - padding, row.Min.Y), new Vector2(row.Max.X + padding, row.Max.Y));
        var hovered = UiInteract.Hover(hit.Min, hit.Max);
        if (hovered)
        {
            drawList.AddRectFilled(hit.Min, hit.Max, ImGui.GetColorU32(ui.HoverWash));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var accent = OnlineGameArt.Accent(room.GameKind);
        var radius = RoomMedallionRadius * scale;
        var medallion = new Vector2(row.Min.X + radius, row.Center.Y);
        GamesHubArt.Medallion(drawList, room.GameKind, medallion, radius, ui.Palette.BackdropBottom, scale);
        var chevron = ChevronSize * scale;
        PhoneIcon.Draw(drawList, new Vector2(row.Max.X - chevron * 0.5f, row.Center.Y), PhoneIcons.ChevronRight,
            ui.MutedInk, chevron);
        var textLeft = medallion.X + radius + TextGap * scale;
        var textWidth = MathF.Max(1f, row.Max.X - chevron - Metrics.Space.Sm * scale - textLeft);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var subtitleHeight = Typography.LineHeight(TextStyles.Footnote);
        var textTop = row.Center.Y - (titleHeight + subtitleHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText(title, textWidth, TextStyles.Headline), ui.TitleInk, TextStyles.Headline);
        var subtitleLeft = textLeft;
        var subtitleY = textTop + titleHeight;
        if (room.Phase == GameRoomWire.PhasePlaying)
        {
            LivePill.DrawLamp(drawList, new Vector2(textLeft + 5f * scale, subtitleY + subtitleHeight * 0.5f), accent,
                (float)ImGui.GetTime(), scale);
            subtitleLeft += LampOffset * scale;
        }

        Typography.Draw(drawList, new Vector2(subtitleLeft, subtitleY),
            Typography.FitText(subtitle, MathF.Max(1f, textWidth - (subtitleLeft - textLeft)), TextStyles.Footnote),
            ui.MutedInk, TextStyles.Footnote);
        return UiInteract.Click(hit.Min, hit.Max, hovered);
    }

    private static float DrawNotice(ImDrawListPtr drawList, AppSkin ui, float left, float top, float width,
        float scale, string message, FontAwesomeIcon icon, Vector4 tint) =>
        GamesHubArt.Notice(drawList, ui, left, top, width, scale, message, icon, tint);
}
