using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Casino.Strip;
using Aetherphone.Apps.Coin;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino;

internal sealed partial class CasinoApp
{
    private const float LiveRowHeight = 84f;
    private const float LiveRowTile = 44f;
    private const float SeatEmptyAlpha = 0.22f;
    private const int SeatDotLimit = 8;

    private readonly LiveRow[] liveRows = new LiveRow[LiveBoard.Capacity];
    private readonly LiveRow[] liveShown = new LiveRow[LiveBoard.Capacity];
    private readonly string[] liveFilterLabels = new string[LiveBoard.Filters.Length];
    private readonly bool[] liveFilterActive = new bool[LiveBoard.Filters.Length];
    private readonly ChipRail liveFilterRail = new();
    private readonly CasinoFriendNames friends = new();
    private LiveFilter liveFilter;

    private void DrawLiveTab(Rect body)
    {
        var scale = UiScale.Current;
        friends.Refresh();
        using (ImRaii.PushId("casino.live"))
        using (AppSurface.Begin(body))
        {
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = DrawStakeNotice(origin, width, scale);
            cursorY = DrawLiveFilters(new Vector2(origin.X, cursorY), width, scale);
            var collected = LiveBoard.Collect(casinoRooms.Rooms, casinoTables.Tables, casinoTables.Listed,
                holdemStore.Tables, casino.Features, liveRows);
            var shown = LiveBoard.Filter(liveRows, collected, liveFilter, friends.Names, liveShown);
            if (shown == 0)
            {
                var empty = liveFilter == LiveFilter.Friends && friends.Count == 0
                    ? Loc.T(L.Strip.LiveNoFriends)
                    : Loc.T(L.Strip.LiveEmptyBody);
                cursorY = CoinArt.DrawPanel(ui, new Vector2(origin.X, cursorY + CardGap * scale), width,
                    FontAwesomeIcon.BroadcastTower, ui.Accent, Loc.T(L.Strip.LiveEmptyTitle), empty, scale);
            }
            else
            {
                cursorY = DrawLiveSection(drawList, new Vector2(origin.X, cursorY), width, LiveRowKind.Room, shown,
                    Loc.T(L.Casino.LiveRoomsHeading), scale);
                cursorY = DrawLiveSection(drawList, new Vector2(origin.X, cursorY), width, LiveRowKind.Table, shown,
                    Loc.T(L.Casino.TablesTitle), scale);
            }

            cursorY = DrawTablesLink(drawList, new Vector2(origin.X, cursorY + CoinArt.SectionGap * scale), width,
                scale);
            CoinArt.Reserve(origin, width, cursorY + CoinArt.BottomPad * scale);
        }
    }

    private float DrawLiveFilters(Vector2 origin, float width, float scale)
    {
        for (var index = 0; index < LiveBoard.Filters.Length; index++)
        {
            liveFilterLabels[index] = Loc.T(LiveBoard.LabelOf(LiveBoard.Filters[index]));
            liveFilterActive[index] = LiveBoard.Filters[index] == liveFilter;
        }

        var height = ChipRail.RowHeight * scale;
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var tapped = liveFilterRail.Draw(row, ui, liveFilterLabels, liveFilterActive);
        if (tapped >= 0)
        {
            liveFilter = LiveBoard.Filters[tapped];
        }

        ImGui.SetCursorScreenPos(new Vector2(origin.X, row.Max.Y));
        return row.Max.Y;
    }

    private float DrawLiveSection(ImDrawListPtr drawList, Vector2 origin, float width, LiveRowKind kind, int shown,
        string title, float scale)
    {
        var count = 0;
        for (var index = 0; index < shown; index++)
        {
            if (liveShown[index].Kind == kind)
            {
                count++;
            }
        }

        if (count == 0)
        {
            return origin.Y;
        }

        var top = SectionTitle(drawList, origin, width, title, scale);
        var rowHeight = LiveRowHeight * scale;
        var min = new Vector2(origin.X, top);
        var max = new Vector2(origin.X + width, top + rowHeight * count);
        ui.Card(drawList, min, max, Metrics.Radius.Grouped * scale);
        var drawn = 0;
        for (var index = 0; index < shown; index++)
        {
            if (liveShown[index].Kind != kind)
            {
                continue;
            }

            using (ImRaii.PushId(index))
            {
                DrawLiveRow(drawList, RowAt(min, max.X, rowHeight, drawn), liveShown[index], drawn > 0, scale);
            }

            drawn++;
        }

        return max.Y;
    }

    private void DrawLiveRow(ImDrawListPtr drawList, Rect row, in LiveRow item, bool hairline, float scale)
    {
        var pad = Metrics.Space.Lg * scale;
        var tile = LiveRowTile * scale;
        if (hairline)
        {
            CoinArt.Hairline(drawList, ui, row.Min.X + pad + tile + CoinArt.TextGap * scale, row.Max.X, row.Min.Y);
        }

        var table = item.Table;
        var join = LiveJoins(item);
        var label = join ? Loc.T(L.Strip.LiveJoin) : Loc.T(L.Strip.LiveWatch);
        var buttonHeight = Button.LargeHeight * scale;
        var buttonWidth = MathF.Max(Button.WidthFor(label, ButtonSize.Large), 76f * scale);
        var button = new Rect(new Vector2(row.Max.X - pad - buttonWidth, row.Center.Y - buttonHeight * 0.5f),
            new Vector2(row.Max.X - pad, row.Center.Y + buttonHeight * 0.5f));
        var overButton = UiInteract.Hover(button.Min, button.Max);
        var hovered = !overButton && CoinArt.RowInteraction(drawList, ui, row, scale);
        var tileCenter = new Vector2(row.Min.X + pad + tile * 0.5f, row.Center.Y);
        CasinoArt.GameTile(drawList, item.GameId, tileCenter, tile);
        var textLeft = tileCenter.X + tile * 0.5f + CoinArt.TextGap * scale;
        var textWidth = MathF.Max(1f, button.Min.X - CoinArt.ValueGap * scale - textLeft);
        var headline = Typography.LineHeight(TextStyles.Headline);
        var footnote = Typography.LineHeight(TextStyles.Footnote);
        var top = row.Center.Y - (headline + footnote * 2f) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(LiveTitle(item), textWidth, TextStyles.Headline), ui.TitleInk, TextStyles.Headline);
        top += headline;
        var line = LiveLine(item, out var open);
        Typography.Draw(drawList, new Vector2(textLeft, top), Typography.FitText(line, textWidth, TextStyles.Footnote),
            open ? ui.Accent : ui.BodyInk, TextStyles.Footnote);
        top += footnote;
        DrawLiveOccupancy(drawList, item, new Vector2(textLeft, top), textWidth, scale);

        if (Button.Draw(drawList, button, label, ui.Ink, join ? ButtonStyle.Tinted : ButtonStyle.Gray,
                id: "casino.live.go"))
        {
            EnterLiveRow(item);
            return;
        }

        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            EnterLiveRow(item);
        }
    }

    private static bool LiveJoins(in LiveRow item)
    {
        if (item.Kind == LiveRowKind.Room)
        {
            return true;
        }

        var table = item.Table!;
        return VenueKinds.IsVenue(table.GameKind) || (CasinoTableFilters.HasOpenSeat(table) && !table.Paused);
    }

    private string LiveTitle(in LiveRow item)
    {
        if (item.Kind == LiveRowKind.Room)
        {
            return Loc.T(CasinoGameNames.Of(item.GameId));
        }

        var table = item.Table!;
        return table.Kind == CasinoTableKinds.House && table.Name.Length == 0
            && !string.Equals(table.GameKind, HoldemRules.Kind, StringComparison.Ordinal)
            ? Loc.T(TierLabel(table.StakeTier))
            : TableName(table);
    }

    private string LiveLine(in LiveRow item, out bool open)
    {
        open = false;
        if (item.Kind == LiveRowKind.Room)
        {
            var phase = RoomPhaseLine(item.GameId, item.RoomId, out open);
            return phase.Length > 0 ? phase : Loc.T(L.Casino.RoomIdle);
        }

        var table = item.Table!;
        if (VenueKinds.IsVenue(table.GameKind))
        {
            return Loc.T(CasinoGameNames.Of(item.GameId));
        }

        return CasinoCurrencies.Of(table) switch
        {
            CasinoCurrencies.Practice => Loc.T(L.Tables.FilterPractice),
            CasinoCurrencies.Gil => texts.Number(L.Strip.LiveGilStakes, table.MaxBet),
            _ => texts.Compacts(L.Casino.TableStakes, table.MinBet, table.MaxBet),
        };
    }

    private void DrawLiveOccupancy(ImDrawListPtr drawList, in LiveRow item, Vector2 origin, float width, float scale)
    {
        var footnote = Typography.LineHeight(TextStyles.Footnote);
        if (item.Kind == LiveRowKind.Room || VenueKinds.IsVenue(item.Table!.GameKind))
        {
            var crowd = item.Kind == LiveRowKind.Room ? casinoRooms.OccupancyOf(item.RoomId) : item.Table!.Occupancy;
            var live = crowd > 0;
            var dot = new Vector2(origin.X + CasinoArt.LiveDotRadius * scale, origin.Y + footnote * 0.5f);
            CasinoArt.LiveDot(drawList, dot, scale, live ? CasinoColors.LightA : ui.MutedInk, live);
            var left = dot.X + (CasinoArt.LiveDotRadius + 5f) * scale;
            Typography.Draw(drawList, new Vector2(left, origin.Y),
                Typography.FitText(texts.Count(L.Casino.LivePlayers, crowd), MathF.Max(1f, origin.X + width - left),
                    TextStyles.Footnote), live ? ui.TitleInk : ui.BodyInk, TextStyles.Footnote);
            return;
        }

        var table = item.Table!;
        var seatInk = table.SeatedCount > 0 ? CasinoColors.Money : ui.MutedInk;
        var dotsWidth = CasinoArt.SeatDots(drawList, new Vector2(origin.X, origin.Y + footnote * 0.5f),
            table.SeatedCount, Math.Min(table.MaxSeats, SeatDotLimit), seatInk,
            Palette.WithAlpha(ui.TitleInk, SeatEmptyAlpha), scale);
        var seatsLeft = origin.X + dotsWidth + (dotsWidth > 0f ? Metrics.Space.Sm * scale : 0f);
        var watching = CasinoTableFilters.SpectatorsOf(table);
        var seats = watching > 0
            ? texts.Counts(L.Strip.LiveSeatsWatching, table.SeatedCount, watching)
            : texts.Counts(L.Casino.TableSeats, table.SeatedCount, table.MaxSeats);
        Typography.Draw(drawList, new Vector2(seatsLeft, origin.Y),
            Typography.FitText(seats, MathF.Max(1f, origin.X + width - seatsLeft), TextStyles.Footnote), ui.BodyInk,
            TextStyles.Footnote);
    }

    private void EnterLiveRow(in LiveRow item)
    {
        if (item.Kind == LiveRowKind.Room)
        {
            OpenGame(item.GameId);
            return;
        }

        var table = item.Table!;
        if (table.Kind == CasinoTableKinds.Private && !table.Admitted && !casinoTables.Owns(table))
        {
            OpenDoor(table.TableId);
            return;
        }

        OpenTable(table.TableId);
    }

    private float DrawTablesLink(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var rowHeight = RecordRowHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + rowHeight);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        if (DrawRecordRow(drawList, new Rect(origin, max), FontAwesomeIcon.ThList,
                CasinoArt.TintOf(CasinoGames.Blackjack), L.Casino.TablesRow, L.Casino.TablesRowHint, false, scale))
        {
            OpenTables();
        }

        return max.Y;
    }

    private static LocString TierLabel(int tier) => tier switch
    {
        CasinoHouseTiers.Parlour => L.Casino.TierParlour,
        CasinoHouseTiers.Salon => L.Casino.TierSalon,
        CasinoHouseTiers.Vault => L.Blackjack.TierVault,
        _ => L.Casino.TierPit,
    };
}
