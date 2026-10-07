using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Hub;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Online;

internal sealed class OnlineHub
{
    private const string SettingsAppId = "settings";
    private const string JoinId = "games.join";
    private const string CodeFieldId = "##gameRoomCode";
    private const string RetryId = "games.rooms.retry";
    private const float RoomCardHeight = RoomCard.Height;
    private const float RoomIconSize = RoomCard.IconSize;
    private const float CardPad = RoomCard.Pad;
    private const float TextGap = RoomCard.TextGap;
    private const float StatusGap = RoomCard.StatusGap;
    private const float CardGap = 12f;
    private const float HostTileHeight = 132f;
    private const float HostIconSize = 52f;
    private const float HostLighten = 0.10f;
    private const float HostDarken = 0.50f;
    private const float HostRimAlpha = 0.10f;
    private const float HostHoverAlpha = 0.06f;
    private const float HoverFloor = RoomCard.HoverFloor;
    private const float PlayersAlpha = 0.8f;
    private const float BusyAlpha = 0.5f;
    private const float SpinnerRadius = 9f;
    private const float HighlightRest = 0.35f;
    private const float HighlightPulse = 0.45f;
    private const float HighlightStroke = 2f;
    private const float NoticeGlyph = 18f;
    private const float SkeletonLine = 9f;
    private const float SkeletonTitleShare = 0.45f;
    private const float SkeletonSubtitleShare = 0.3f;
    private const float JoinGap = 8f;
    private const float JoinMinWidth = 84f;
    private const int HostColumns = 2;
    private const int SkeletonRows = 2;
    private const int CodeBufferLength = 16;
    private const int DiscSegments = RoomCard.DiscSegments;

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    private readonly GameRoomsStore store;
    private readonly Action<string, string> openRoom;
    private readonly Action refreshNow;
    private readonly PullToRefresh refresh = new();
    private readonly string?[] playerLabels = new string?[OnlineGameArt.Infos.Length];

    private string codeBuffer = string.Empty;
    private string inlineReason = string.Empty;
    private string preferredKind = string.Empty;
    private string pendingKind = string.Empty;
    private GameRoomCardDto[] labeledRooms = Array.Empty<GameRoomCardDto>();
    private string[] roomTitles = Array.Empty<string>();
    private string[] roomSubtitles = Array.Empty<string>();
    private string[] roomMonograms = Array.Empty<string>();
    private bool roomsFailed;
    private int drawnFrame = -1;

    public OnlineHub(GameRoomsStore store, Action<string, string> openRoom)
    {
        this.store = store;
        this.openRoom = openRoom;
        refreshNow = RefreshNow;
    }

    public void Reset()
    {
        preferredKind = string.Empty;
        pendingKind = string.Empty;
        inlineReason = string.Empty;
        codeBuffer = string.Empty;
        roomsFailed = false;
        ResetLabels();
    }

    public void ResetLabels()
    {
        Array.Clear(playerLabels);
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

        pendingKind = string.Empty;
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
        if (store.AccountId.Length == 0)
        {
            DrawSignedOut(body, ui, navigation);
            return;
        }

        RefreshWhenShown();
        refresh.Draw(body, pull, dragging, store.LoadingRooms, ui.MutedInk, refreshNow);
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var left = origin.X;
        var y = DrawHeader(drawList, ui, left, origin.Y, width, Loc.T(L.Games.OnlineMyRooms), scale);
        y = DrawRooms(drawList, ui, left, y, width, scale);
        y = DrawHeader(drawList, ui, left, y + HubMetrics.SectionGap * scale, width,
            Loc.T(L.Games.OnlineJoinHeading), scale);
        y = DrawJoinByCode(drawList, ui, left, y, width, scale);
        if (inlineReason.Length > 0)
        {
            y = DrawNotice(drawList, ui, left, y + Metrics.Space.Md * scale, width,
                Loc.T(GamesOnlineText.ReasonMessage(inlineReason)), FontAwesomeIcon.ExclamationCircle,
                ui.Theme.Danger, 0f, scale);
        }

        y = DrawHeader(drawList, ui, left, y + HubMetrics.SectionGap * scale, width, Loc.T(L.GamesHub.StartRoom),
            scale);
        y = DrawHostGrid(drawList, ui, left, y, width, scale);
        ImGui.SetCursorScreenPos(new Vector2(left, y));
        ImGui.Dummy(new Vector2(width, Metrics.Space.Xl * scale));
    }

    private static void DrawSignedOut(Rect body, AppSkin ui, INavigator navigation)
    {
        var title = Loc.T(L.GamesHub.SignInTitle);
        var hint = Loc.T(L.Games.OnlineSignIn);
        if (!navigation.IsAvailable(SettingsAppId))
        {
            EmptyState.Draw(body, ui, PhoneIcons.Users, title, hint);
            return;
        }

        if (EmptyState.Draw(body, ui, PhoneIcons.Users, title, hint, Loc.T(L.GamesHub.OpenSettings)))
        {
            navigation.Open(SettingsAppId);
        }
    }

    private void RefreshWhenShown()
    {
        var frame = ImGui.GetFrameCount();
        if (frame - drawnFrame > 1)
        {
            store.EnsureFresh();
        }

        drawnFrame = frame;
    }

    private void RefreshNow()
    {
        roomsFailed = false;
        store.RefreshNow();
    }

    private static float DrawHeader(ImDrawListPtr drawList, AppSkin ui, float left, float top, float width,
        string title, float scale) =>
        top + CardSectionHeader.Draw(drawList, new Vector2(left, top), width, title, ui.TitleInk)
            + HubMetrics.HeaderGap * scale;

    private float DrawRooms(ImDrawListPtr drawList, AppSkin ui, float left, float top, float width, float scale)
    {
        var rooms = store.Rooms;
        if (rooms.Length == 0)
        {
            if (roomsFailed && !store.LoadingRooms)
            {
                return DrawFailure(drawList, ui, left, top, width, scale);
            }

            return store.LoadedRooms
                ? DrawNotice(drawList, ui, left, top, width, Loc.T(L.Games.OnlineNoRooms), FontAwesomeIcon.DoorOpen,
                    ui.MutedInk, 0f, scale)
                : DrawSkeleton(drawList, ui, left, top, width, scale);
        }

        RefreshRoomLabels(rooms);
        var height = RoomCardHeight * scale;
        var gap = CardGap * scale;
        var entered = -1;
        var y = top;
        for (var index = 0; index < rooms.Length; index++)
        {
            var rect = new Rect(new Vector2(left, y), new Vector2(left + width, y + height));
            y += height + gap;
            if (!ImGui.IsRectVisible(rect.Min, rect.Max))
            {
                continue;
            }

            ImGui.PushID(rooms[index].RoomId);
            if (RoomCard.Draw(drawList, ui, rect, rooms[index], roomTitles[index], roomSubtitles[index],
                    roomMonograms[index], scale))
            {
                entered = index;
            }

            ImGui.PopID();
        }

        if (entered >= 0)
        {
            inlineReason = string.Empty;
            store.Enter(rooms[entered].RoomId);
            openRoom(rooms[entered].RoomId, rooms[entered].GameKind);
        }

        return y - gap;
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
            roomMonograms = new string[rooms.Length];
        }

        for (var index = 0; index < rooms.Length; index++)
        {
            var room = rooms[index];
            roomTitles[index] = Loc.T(GamesOnlineText.GameName(room.GameKind));
            roomSubtitles[index] = Loc.T(L.GamesHub.RoomOf, room.OwnerName);
            roomMonograms[index] = Initials.Of(room.OwnerName);
        }
    }

    private static float DrawSkeleton(ImDrawListPtr drawList, AppSkin ui, float left, float top, float width,
        float scale)
    {
        var height = RoomCardHeight * scale;
        var gap = CardGap * scale;
        var radius = HubMetrics.CardRadius * scale;
        var pad = CardPad * scale;
        var iconSize = RoomIconSize * scale;
        var line = SkeletonLine * scale;
        var y = top;
        for (var index = 0; index < SkeletonRows; index++)
        {
            var centerY = y + height * 0.5f;
            ui.Card(drawList, new Vector2(left, y), new Vector2(left + width, y + height), radius);
            var iconMin = new Vector2(left + pad, centerY - iconSize * 0.5f);
            Skeleton.Bar(drawList, iconMin, iconMin + new Vector2(iconSize, iconSize), GameIconArt.Radius(iconSize));
            var textLeft = iconMin.X + iconSize + TextGap * scale;
            var textWidth = left + width - pad - textLeft;
            Skeleton.Bar(drawList, new Vector2(textLeft, centerY - line - StatusGap * scale * 0.5f),
                new Vector2(textLeft + textWidth * SkeletonTitleShare, centerY - StatusGap * scale * 0.5f),
                line * 0.5f);
            Skeleton.Bar(drawList, new Vector2(textLeft, centerY + StatusGap * scale * 0.5f),
                new Vector2(textLeft + textWidth * SkeletonSubtitleShare, centerY + StatusGap * scale * 0.5f + line),
                line * 0.5f);
            y += height + gap;
        }

        return y - gap;
    }

    private float DrawFailure(ImDrawListPtr drawList, AppSkin ui, float left, float top, float width, float scale)
    {
        var label = Loc.T(L.Common.Retry);
        var buttonWidth = Button.WidthFor(label, ButtonSize.Small);
        var pad = CardPad * scale;
        var bottom = DrawNotice(drawList, ui, left, top, width, Loc.T(L.Common.LoadFailed),
            FontAwesomeIcon.ExclamationTriangle, ui.MutedInk, buttonWidth + TextGap * scale, scale);
        var buttonHeight = Button.SmallHeight * scale;
        var centerY = (top + bottom) * 0.5f;
        var rect = new Rect(new Vector2(left + width - pad - buttonWidth, centerY - buttonHeight * 0.5f),
            new Vector2(left + width - pad, centerY + buttonHeight * 0.5f));
        if (Button.Draw(drawList, rect, label, ui.Ink, ButtonStyle.Gray, enabled: !store.LoadingRooms, id: RetryId))
        {
            RefreshNow();
        }

        return bottom;
    }

    private static float DrawNotice(ImDrawListPtr drawList, AppSkin ui, float left, float top, float width,
        string message, FontAwesomeIcon glyph, Vector4 tint, float trailing, float scale)
    {
        var pad = CardPad * scale;
        var slot = RoomIconSize * scale;
        var textLeft = left + pad + slot + TextGap * scale;
        var textWidth = MathF.Max(1f, left + width - pad - trailing - textLeft);
        var block = Typography.MeasureWrappedBlock(message, TextStyles.Subheadline, textWidth);
        var height = MathF.Max(RoomCardHeight * scale, block.Y + pad * 2f);
        var max = new Vector2(left + width, top + height);
        ui.Card(drawList, new Vector2(left, top), max, HubMetrics.CardRadius * scale);
        var slotCenter = new Vector2(left + pad + slot * 0.5f, top + height * 0.5f);
        drawList.AddCircleFilled(slotCenter, slot * 0.5f,
            ImGui.GetColorU32(Surfaces.Fill(ui.TitleInk, FillLevel.Tertiary)), DiscSegments);
        ProgressRing.CenterIcon(drawList, slotCenter, glyph, tint, NoticeGlyph * scale);
        Typography.DrawWrappedLeft(new Vector2(textLeft, top + (height - block.Y) * 0.5f), message, ui.MutedInk,
            TextStyles.Subheadline, textWidth);
        return max.Y;
    }

    private float DrawJoinByCode(ImDrawListPtr drawList, AppSkin ui, float left, float top, float width, float scale)
    {
        var height = GlassField.HeightUnits * scale;
        var label = Loc.T(L.Games.OnlineJoin);
        var buttonHeight = Button.RegularHeight * scale;
        var buttonWidth = MathF.Max(JoinMinWidth * scale, Button.WidthFor(label, ButtonSize.Regular));
        var field = new Rect(new Vector2(left, top),
            new Vector2(left + width - buttonWidth - JoinGap * scale, top + height));
        SearchBar.Surface(drawList, field, ui.Ink);
        var submitted = GlassField.Text(field, CodeFieldId, Loc.T(L.Games.OnlineJoinHint), ref codeBuffer,
            ui.Theme, scale, CodeBufferLength, false, ImGuiInputTextFlags.EnterReturnsTrue);
        var trimmed = codeBuffer.AsSpan().Trim();
        var ready = trimmed.Length > 0 && !store.IntentInFlight;
        var buttonTop = top + (height - buttonHeight) * 0.5f;
        var buttonRect = new Rect(new Vector2(field.Max.X + JoinGap * scale, buttonTop),
            new Vector2(left + width, buttonTop + buttonHeight));
        var tapped = Button.Draw(drawList, buttonRect, label, ui.Ink, enabled: ready, id: JoinId);
        if ((tapped || submitted) && ready)
        {
            inlineReason = string.Empty;
            pendingKind = string.Empty;
            store.JoinByCode(trimmed.ToString());
        }

        GamesHubArt.ReportAnchor(JoinId, new Rect(field.Min, buttonRect.Max));
        return top + height;
    }

    private float DrawHostGrid(ImDrawListPtr drawList, AppSkin ui, float left, float top, float width, float scale)
    {
        var busy = store.IntentInFlight;
        if (!busy)
        {
            pendingKind = string.Empty;
        }

        var gap = CardGap * scale;
        var tileWidth = HubMetrics.GridTile(width, HostColumns, gap);
        var tileHeight = HostTileHeightFor(scale);
        var infos = OnlineGameArt.Infos;
        var tapped = -1;
        for (var index = 0; index < infos.Length; index++)
        {
            var min = new Vector2(left + index % HostColumns * (tileWidth + gap),
                top + index / HostColumns * (tileHeight + gap));
            var rect = new Rect(min, new Vector2(min.X + tileWidth, min.Y + tileHeight));
            if (!ImGui.IsRectVisible(rect.Min, rect.Max))
            {
                continue;
            }

            ImGui.PushID(infos[index].HostId);
            if (DrawHostTile(drawList, rect, index, busy, scale))
            {
                tapped = index;
            }

            ImGui.PopID();
        }

        if (tapped >= 0)
        {
            var kind = infos[tapped].Kind;
            inlineReason = string.Empty;
            preferredKind = kind;
            pendingKind = kind;
            store.CreateRoom(kind);
        }

        var rows = (infos.Length + HostColumns - 1) / HostColumns;
        return top + rows * tileHeight + (rows - 1) * gap;
    }

    private static float HostTileHeightFor(float scale)
    {
        var stack = CardPad * 2f * scale + HostIconSize * scale + Metrics.Space.Sm * scale
                    + Typography.LineHeight(TextStyles.Headline) + Typography.LineHeight(TextStyles.Footnote);
        return MathF.Max(HostTileHeight * scale, stack);
    }

    private bool DrawHostTile(ImDrawListPtr drawList, Rect rect, int kindIndex, bool busy, float scale)
    {
        ref readonly var info = ref OnlineGameArt.Infos[kindIndex];
        var pending = busy && string.Equals(info.Kind, pendingKind, StringComparison.Ordinal);
        var hovered = !busy && UiInteract.Hover(rect.Min, rect.Max);
        var card = RoomCard.Pose(rect, hovered, out var hover);
        var firstVertex = drawList.VtxBuffer.Size;
        var radius = HubMetrics.CardRadius * scale;
        var accent = AppAccents.For(info.AccentId);
        Squircle.FillVerticalGradient(drawList, card.Min, card.Max, radius,
            ImGui.GetColorU32(Palette.Lighten(accent, HostLighten) with { W = 1f }),
            ImGui.GetColorU32(Palette.Darken(accent, HostDarken) with { W = 1f }));
        if (hover > HoverFloor)
        {
            Squircle.Fill(drawList, card.Min, card.Max, radius,
                ImGui.GetColorU32(White with { W = HostHoverAlpha * hover }));
        }

        Squircle.Stroke(drawList, card.Min, card.Max, radius, ImGui.GetColorU32(White with { W = HostRimAlpha }),
            Metrics.Stroke.Hairline * scale);
        if (string.Equals(info.Kind, preferredKind, StringComparison.Ordinal))
        {
            Squircle.Stroke(drawList, card.Min, card.Max, radius,
                ImGui.GetColorU32(White with { W = HighlightRest + HighlightPulse * Pulse.Wave() }),
                HighlightStroke * scale);
        }

        var pad = CardPad * scale;
        var iconSize = HostIconSize * scale;
        var iconMin = new Vector2(card.Min.X + pad, card.Min.Y + pad);
        var iconMax = new Vector2(iconMin.X + iconSize, iconMin.Y + iconSize);
        GameIconArt.Draw(drawList, info.AccentId, accent, iconMin, iconMax, IconAppearance.Default, true);
        if (pending)
        {
            var spinner = SpinnerRadius * scale;
            LoadingPulse.Spinner(new Vector2(card.Max.X - pad - spinner, card.Min.Y + pad + spinner), spinner, White,
                1f, drawList);
        }

        var textWidth = MathF.Max(1f, card.Width - pad * 2f);
        var playersTop = card.Max.Y - pad - Typography.LineHeight(TextStyles.Footnote);
        var nameTop = playersTop - Typography.LineHeight(TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(card.Min.X + pad, nameTop),
            Typography.FitText(Loc.T(GamesOnlineText.GameName(info.Kind)), textWidth, TextStyles.Headline), White,
            TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(card.Min.X + pad, playersTop),
            Typography.FitText(PlayersLabel(kindIndex), textWidth, TextStyles.Footnote),
            White with { W = PlayersAlpha }, TextStyles.Footnote);
        if (busy && !pending)
        {
            LayerCompositor.Fade(drawList, firstVertex, BusyAlpha);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return !busy && UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private string PlayersLabel(int kindIndex)
    {
        var label = playerLabels[kindIndex];
        if (label is not null)
        {
            return label;
        }

        var most = OnlineGameArt.Infos[kindIndex].MaxPlayers;
        label = most <= OnlineGameArt.MinPlayers
            ? Loc.Plural(L.GamesHub.PlayerCount, most)
            : Loc.T(L.GamesHub.PlayerRange, GameNumber.Label(OnlineGameArt.MinPlayers), GameNumber.Label(most));
        playerLabels[kindIndex] = label;
        return label;
    }
}
