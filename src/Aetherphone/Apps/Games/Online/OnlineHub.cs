using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Games.Online;

// The friends lobby: host a room, type a code, or walk back into a room you are already in. The
// code IS the door, so this screen owns exactly three verbs and the room screen owns the rest.
internal sealed class OnlineHub
{
    private const float HostCardHeight = 86f;
    private const float MedallionRadius = 27f;
    private const float RoomMedallionRadius = 18f;
    private const float FieldHeight = 40f;
    private const float RoomRowHeight = 64f;
    private const float HostPillHeight = 32f;
    private const float HostPillMinWidth = 70f;
    private const float HeadingHeight = 32f;
    private const int CodeBufferLength = 16;

    private readonly GameRoomsStore store;
    private readonly Action<string, string> openRoom;

    private string codeBuffer = string.Empty;
    private string inlineReason = string.Empty;
    private string preferredKind = string.Empty;
    private string unoHint = string.Empty;
    private GameRoomCardDto[] labeledRooms = Array.Empty<GameRoomCardDto>();
    private string[] roomTitles = Array.Empty<string>();
    private string[] roomSubtitles = Array.Empty<string>();

    public OnlineHub(GameRoomsStore store, Action<string, string> openRoom)
    {
        this.store = store;
        this.openRoom = openRoom;
    }

    public void Enter(string preferredKind)
    {
        this.preferredKind = preferredKind;
        inlineReason = string.Empty;
        codeBuffer = string.Empty;
        unoHint = Loc.T(L.Games.OnlineHostHint,
            OnlineGameArt.MaxPlayers(GameRoomWire.UnoKind).ToString(Loc.Culture));
        labeledRooms = Array.Empty<GameRoomCardDto>();
        store.RefreshNow();
    }

    public void Draw(in PhoneContext context, Action back, AppSkin ui)
    {
        var scale = UiScale.Current;
        var content = context.Content;
        DrawHeader(content, back, ui, scale);
        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + AppHeader.Height * scale), content.Max);
        Consume();
        store.EnsureFresh();
        using var surface = AppSurface.BeginEdgeToEdge(body);
        if (store.AccountId.Length == 0)
        {
            DrawNotice(ui, scale, Loc.T(L.Games.OnlineSignIn));
            return;
        }

        var kinds = OnlineGameArt.Kinds;
        for (var index = 0; index < kinds.Length; index++)
        {
            DrawHostCard(ui, scale, kinds[index]);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        DrawJoinByCode(ui, scale);
        if (inlineReason.Length > 0)
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
            DrawNotice(ui, scale, Loc.T(GamesOnlineText.ReasonMessage(inlineReason)));
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        DrawHeading(ui, scale, Loc.T(L.Games.OnlineMyRooms));
        DrawRooms(ui, scale);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
    }

    private void DrawHeader(Rect content, Action back, AppSkin ui, float scale)
    {
        if (AppHeader.DrawBack(content, scale, "games.online.back", ui.HeaderInk))
        {
            back();
        }

        var rowCenterY = content.Min.Y + AppHeader.Height * scale * 0.5f;
        var title = Typography.FitText(Loc.T(L.Games.OnlineTitle), MathF.Max(1f, content.Width - 112f * scale), 1.3f,
            FontWeight.Bold);
        Typography.DrawCentered(ImGui.GetWindowDrawList(), new Vector2(content.Center.X, rowCenterY), title,
            ui.TitleInk, 1.3f, FontWeight.Bold);
        if (store.LoadingRooms)
        {
            LoadingPulse.Spinner(new Vector2(content.Max.X - 22f * scale, rowCenterY), 8f * scale, ui.Accent);
        }
    }

    private void Consume()
    {
        var answer = store.TakeRoomAnswer();
        if (answer is null)
        {
            return;
        }

        if (answer.Intent is GameRoomIntent.Created or GameRoomIntent.Joined)
        {
            if (answer.Granted && answer.Room is not null)
            {
                inlineReason = string.Empty;
                codeBuffer = string.Empty;
                store.Enter(answer.Room.RoomId);
                openRoom(answer.Room.RoomId, answer.Room.GameKind);
                return;
            }

            inlineReason = answer.Reason;
        }
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

        return string.Equals(kind, GameRoomWire.ConnectFourKind, StringComparison.Ordinal)
            ? Loc.T(L.Games.OnlineConnectFourHostHint)
            : unoHint;
    }

    private void DrawHostCard(AppSkin ui, float scale, string kind)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var inset = FeedCell.PadX * scale;
        var drawList = ImGui.GetWindowDrawList();
        var height = HostCardHeight * scale;
        var row = new Rect(new Vector2(origin.X + inset, origin.Y),
            new Vector2(origin.X + width - inset, origin.Y + height));
        var rounding = Metrics.Radius.Card * scale;
        var accent = OnlineGameArt.Accent(kind);
        var pillLabel = Loc.T(L.Games.OnlineHostShort);
        var pillHeight = HostPillHeight * scale;
        var pillWidth = MathF.Max(AppSkin.PillWidthFor(pillLabel, pillHeight), HostPillMinWidth * scale);
        var pillCenter = new Vector2(row.Max.X - 14f * scale - pillWidth * 0.5f, row.Center.Y);
        var pillHalf = new Vector2(pillWidth, pillHeight) * 0.5f;
        var pillHovered = UiInteract.Hover(pillCenter - pillHalf, pillCenter + pillHalf);
        var hovered = !pillHovered && UiInteract.Hover(row.Min, row.Max);
        ui.Card(drawList, row.Min, row.Max, rounding, hovered || pillHovered);
        var radius = MedallionRadius * scale;
        var medallion = new Vector2(row.Min.X + 16f * scale + radius, row.Center.Y);
        drawList.PushClipRect(row.Min, row.Max, true);
        drawList.AddCircleFilled(medallion, radius * 2.6f, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.10f)), 48);
        drawList.PopClipRect();
        if (hovered)
        {
            Squircle.Fill(drawList, row.Min, row.Max, rounding, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (string.Equals(kind, preferredKind, StringComparison.Ordinal))
        {
            Squircle.Stroke(drawList, row.Min, row.Max, rounding,
                ImGui.GetColorU32(Palette.WithAlpha(accent, 0.30f + 0.35f * Pulse.Wave())), 1.5f * scale);
        }

        if (hovered || pillHovered)
        {
            ProgressRing.Glow(medallion, radius * 1.1f, accent, 0.4f);
        }

        drawList.AddCircleFilled(medallion, radius, ImGui.GetColorU32(GamePalette.Darken(accent, 0.32f)), 48);
        drawList.AddCircle(medallion, radius, ImGui.GetColorU32(GamePalette.Lighten(accent, 0.35f) with { W = 0.55f }),
            48, 1.2f * scale);
        OnlineGameArt.Draw(drawList, kind, medallion, radius * 1.3f, scale);

        var textLeft = medallion.X + radius + 14f * scale;
        var textWidth = MathF.Max(1f, pillCenter.X - pillHalf.X - 10f * scale - textLeft);
        Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 18f * scale),
            Typography.FitText(Loc.T(GamesOnlineText.GameName(kind)), textWidth, TextStyles.Headline), ui.TitleInk,
            TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 42f * scale),
            Typography.FitText(HostHint(kind), textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);

        var pillClicked = GameHud.Button(pillCenter, new Vector2(pillWidth, pillHeight), pillLabel,
            store.IntentInFlight ? ui.MutedInk : accent, ui.Theme);
        var clicked = pillClicked || UiInteract.Click(row.Min, row.Max, hovered);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        if (clicked && !store.IntentInFlight)
        {
            inlineReason = string.Empty;
            store.CreateRoom(kind);
        }
    }

    private void DrawJoinByCode(AppSkin ui, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var inset = FeedCell.PadX * scale;
        var drawList = ImGui.GetWindowDrawList();
        Typography.Draw(drawList, new Vector2(origin.X + inset, origin.Y), Loc.T(L.Games.OnlineJoinHeading),
            ui.MutedInk, TextStyles.FootnoteEmphasized);
        var fieldTop = origin.Y + 20f * scale;
        var pillWidth = 92f * scale;
        var fieldMin = new Vector2(origin.X + inset, fieldTop);
        var fieldMax = new Vector2(origin.X + width - inset - pillWidth - 8f * scale,
            fieldTop + FieldHeight * scale);
        Squircle.Fill(drawList, fieldMin, fieldMax, Metrics.Radius.Field * scale, ImGui.GetColorU32(ui.FieldSurface));
        ImGui.SetCursorScreenPos(new Vector2(fieldMin.X + 10f * scale,
            (fieldMin.Y + fieldMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(fieldMax.X - fieldMin.X - 20f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
        {
            ImGui.InputTextWithHint("##gameRoomCode", Loc.T(L.Games.OnlineJoinHint), ref codeBuffer,
                CodeBufferLength);
        }

        var trimmed = codeBuffer.AsSpan().Trim();
        var ready = trimmed.Length > 0 && !store.IntentInFlight;
        var pillCenter = new Vector2(origin.X + width - inset - pillWidth * 0.5f,
            fieldTop + FieldHeight * scale * 0.5f);
        if (GameHud.Button(pillCenter, new Vector2(pillWidth, FieldHeight * scale), Loc.T(L.Games.OnlineJoin),
                ready ? ui.Accent : ui.MutedInk, ui.Theme) && ready)
        {
            inlineReason = string.Empty;
            store.JoinByCode(trimmed.ToString());
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 20f * scale + FieldHeight * scale));
    }

    private static void DrawHeading(AppSkin ui, float scale, string label)
    {
        var origin = ImGui.GetCursorScreenPos();
        var inset = FeedCell.PadX * scale;
        var width = ScrollLayout.StableContentWidth();
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(origin.X + inset, origin.Y),
            Typography.FitText(label, width - inset * 2f, TextStyles.Title3), ui.TitleInk, TextStyles.Title3);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(1f, HeadingHeight * scale));
    }

    private void DrawRooms(AppSkin ui, float scale)
    {
        var rooms = store.Rooms;
        if (rooms.Length == 0)
        {
            DrawNotice(ui, scale, Loc.T(store.LoadedRooms ? L.Games.OnlineNoRooms : L.Games.OnlineLoading));
            return;
        }

        RefreshRoomLabels(rooms);
        for (var index = 0; index < rooms.Length; index++)
        {
            if (DrawRoomRow(ui, scale, rooms[index], roomTitles[index], roomSubtitles[index]))
            {
                inlineReason = string.Empty;
                store.Enter(rooms[index].RoomId);
                openRoom(rooms[index].RoomId, rooms[index].GameKind);
            }
        }
    }

    private void RefreshRoomLabels(GameRoomCardDto[] rooms)
    {
        if (ReferenceEquals(rooms, labeledRooms))
        {
            return;
        }

        labeledRooms = rooms;
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

    private static bool DrawRoomRow(AppSkin ui, float scale, GameRoomCardDto room, string title, string subtitle)
    {
        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, RoomRowHeight * scale, ui.HoverWash);
        var row = cell.Bounds;
        var inset = FeedCell.PadX * scale;

        var accent = OnlineGameArt.Accent(room.GameKind);
        var radius = RoomMedallionRadius * scale;
        var medallion = new Vector2(row.Min.X + inset + radius, row.Center.Y);
        drawList.AddCircleFilled(medallion, radius, ImGui.GetColorU32(GamePalette.Darken(accent, 0.32f)), 36);
        drawList.AddCircle(medallion, radius, ImGui.GetColorU32(GamePalette.Lighten(accent, 0.35f) with { W = 0.5f }),
            36, 1f * scale);
        OnlineGameArt.Draw(drawList, room.GameKind, medallion, radius * 1.3f, scale);

        var textLeft = medallion.X + radius + 12f * scale;
        var textWidth = MathF.Max(1f, row.Max.X - inset - textLeft);
        Typography.Draw(drawList, new Vector2(textLeft, row.Center.Y - 17f * scale),
            Typography.FitText(title, textWidth, TextStyles.Headline), ui.TitleInk, TextStyles.Headline);
        var subtitleLeft = textLeft;
        var subtitleY = row.Center.Y + 2f * scale;
        if (room.Phase == GameRoomWire.PhasePlaying)
        {
            var lineHeight = Typography.LineHeight(TextStyles.Footnote);
            LivePill.DrawLamp(drawList, new Vector2(textLeft + 5f * scale, subtitleY + lineHeight * 0.5f), accent,
                (float)ImGui.GetTime(), scale);
            subtitleLeft += 16f * scale;
        }

        Typography.Draw(drawList, new Vector2(subtitleLeft, subtitleY),
            Typography.FitText(subtitle, MathF.Max(1f, textWidth - (subtitleLeft - textLeft)), TextStyles.Footnote),
            ui.MutedInk, TextStyles.Footnote);
        FeedCell.End(drawList, cell, ui.Hairline);
        return cell.Tapped;
    }

    private static void DrawNotice(AppSkin ui, float scale, string message)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var inset = FeedCell.PadX * scale;
        var drawList = ImGui.GetWindowDrawList();
        var pad = 14f * scale;
        var textWidth = width - inset * 2f - pad * 2f;
        var block = Typography.MeasureWrappedBlock(message, TextStyles.Footnote, textWidth);
        var height = block.Y + pad * 2f;
        var min = new Vector2(origin.X + inset, origin.Y);
        var max = new Vector2(origin.X + width - inset, origin.Y + height);
        ui.Card(drawList, min, max, Metrics.Radius.Card * scale);
        Typography.DrawWrappedLeft(new Vector2(min.X + pad, min.Y + pad), message, ui.MutedInk,
            TextStyles.Footnote, textWidth);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }
}
