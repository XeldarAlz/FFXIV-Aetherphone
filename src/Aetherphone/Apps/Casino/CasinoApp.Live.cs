using Aetherphone.Apps.Casino.Strip;
using Aetherphone.Apps.Coin;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino;

internal sealed partial class CasinoApp
{
    private const float LiveCardGap = 10f;

    private readonly LiveRow[] liveRows = new LiveRow[LiveBoard.Capacity];
    private readonly LiveRow[] liveShown = new LiveRow[LiveBoard.Capacity];
    private readonly CasinoTableRowDto?[] liveViewSources = new CasinoTableRowDto?[LiveBoard.Capacity];
    private readonly TableRowView[] liveViews = new TableRowView[LiveBoard.Capacity];
    private readonly string[] liveFilterLabels = new string[LiveBoard.Filters.Length];
    private readonly bool[] liveFilterActive = new bool[LiveBoard.Filters.Length];
    private readonly ChipRail liveFilterRail = new();
    private readonly CasinoFriendNames friends = new();
    private LanguageInfo? liveViewLanguage;
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
                cursorY = DrawLiveHostButton(drawList, new Vector2(origin.X, cursorY + CardGap * scale), width, scale);
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
        var gap = LiveCardGap * scale;
        for (var index = 0; index < shown; index++)
        {
            if (liveShown[index].Kind != kind)
            {
                continue;
            }

            var view = LiveView(index, liveShown[index]);
            var height = TableRow.HeightOf(view) * scale;
            var card = new Rect(new Vector2(origin.X, top), new Vector2(origin.X + width, top + height));
            using (ImRaii.PushId(index))
            {
                if (TableRow.Draw(drawList, card, ui, view, scale))
                {
                    EnterLiveRow(liveShown[index]);
                }
            }

            top = card.Max.Y + gap;
        }

        return top - gap;
    }

    private TableRowView LiveView(int slot, in LiveRow item)
    {
        if (item.Kind == LiveRowKind.Room)
        {
            var phase = RoomPhaseLine(item.GameId, item.RoomId, out var open);
            var crowd = casinoRooms.OccupancyOf(item.RoomId);
            return new TableRowView(Loc.T(CasinoGameNames.Of(item.GameId)),
                phase.Length > 0 ? phase : Loc.T(L.Casino.RoomIdle), texts.Count(L.Casino.LivePlayers, crowd),
                string.Empty, false, false, false, false, CasinoCurrencies.Chips, string.Empty, string.Empty, false,
                false, item.GameId, string.Empty, string.Empty, crowd, 0, VenueRoomKind.None,
                Loc.T(open ? L.Strip.LiveJoin : L.Strip.LiveWatch), open);
        }

        if (!ReferenceEquals(liveViewLanguage, Loc.Current))
        {
            liveViewLanguage = Loc.Current;
            Array.Clear(liveViewSources);
        }

        if (!ReferenceEquals(liveViewSources[slot], item.Table))
        {
            liveViewSources[slot] = item.Table;
            var view = Tables.TableBrowser.ViewOf(item.Table!, casinoTables.AccountId);
            liveViews[slot] = view with { Name = LiveTitle(item) };
        }

        return liveViews[slot];
    }

    private string JoinCodeOf(string tableId) => casinoTables.CardFor(tableId)?.JoinCode ?? string.Empty;

    private float DrawLiveHostButton(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var height = Button.LargeHeight * scale;
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        if (Button.Draw(drawList, rect, Loc.T(L.Tables.HostCta), ui.Ink, ButtonStyle.Prominent,
                id: "casino.live.host"))
        {
            OpenHostSheet();
        }

        return rect.Max.Y;
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
