using Aetherphone.Apps.Coin;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino;

internal sealed partial class CasinoApp
{
    private const float LiveRowHeight = 76f;
    private const float LiveRowTile = 44f;
    private const float HouseRowHeight = 84f;
    private const float HouseRowTile = 40f;
    private const float SitHeight = Button.RegularHeight;
    private const float SeatEmptyAlpha = 0.18f;

    private void DrawLiveTab(Rect body)
    {
        var scale = UiScale.Current;
        using (ImRaii.PushId("casino.live"))
        using (AppSurface.Begin(body))
        {
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = DrawStakeNotice(origin, width, scale);
            var roomsTop = cursorY + CoinArt.HeaderGap * scale +
                           CardSectionHeader.Draw(drawList, new Vector2(origin.X, cursorY), width,
                               Loc.T(L.Casino.LiveRoomsHeading), ui.TitleInk);
            var rowHeight = LiveRowHeight * scale;
            var roomsMin = new Vector2(origin.X, roomsTop);
            var roomsMax = new Vector2(origin.X + width, roomsTop + rowHeight * 2f);
            ui.Card(drawList, roomsMin, roomsMax, Metrics.Radius.Grouped * scale);
            if (DrawLiveRoomRow(drawList, RowAt(roomsMin, roomsMax.X, rowHeight, 0), CasinoGames.Wheel,
                    L.Casino.GameWheel, false, scale))
            {
                OpenGame(CasinoGames.Wheel);
            }

            if (DrawLiveRoomRow(drawList, RowAt(roomsMin, roomsMax.X, rowHeight, 1), CasinoGames.Bingo,
                    L.Casino.GameBingo, true, scale))
            {
                OpenGame(CasinoGames.Bingo);
            }

            var tablesTop = SectionTitle(drawList, new Vector2(origin.X, roomsMax.Y), width,
                Loc.T(L.Casino.LiveTablesHeading), scale);
            cursorY = DrawHouseTables(drawList, new Vector2(origin.X, tablesTop), width, scale);
            cursorY = DrawTablesLink(drawList, new Vector2(origin.X, cursorY + CardGap * scale), width, scale);
            CoinArt.Reserve(origin, width, cursorY + CoinArt.BottomPad * scale);
        }
    }

    private bool DrawLiveRoomRow(ImDrawListPtr drawList, Rect row, string gameId, LocString name, bool hairline,
        float scale)
    {
        var pad = Metrics.Space.Lg * scale;
        var tile = LiveRowTile * scale;
        if (hairline)
        {
            CoinArt.Hairline(drawList, ui, row.Min.X + pad + tile + CoinArt.TextGap * scale, row.Max.X, row.Min.Y);
        }

        var hovered = CoinArt.RowInteraction(drawList, ui, row, scale);
        var tileCenter = new Vector2(row.Min.X + pad + tile * 0.5f, row.Center.Y);
        CasinoArt.GameTile(drawList, gameId, tileCenter, tile);

        var chevronCenter = new Vector2(row.Max.X - pad - 4f * scale, row.Center.Y);
        CasinoArt.Chevron(drawList, chevronCenter, ui.MutedInk);
        var occupancy = CrowdAt(gameId);
        var crowd = texts.Count(L.Casino.LivePlayers, occupancy);
        var crowdSize = Typography.Measure(crowd, TextStyles.Footnote);
        var crowdX = chevronCenter.X - CoinArt.ValueGap * scale - crowdSize.X;
        var live = occupancy > 0;
        Typography.Draw(drawList, new Vector2(crowdX, row.Center.Y - crowdSize.Y * 0.5f), crowd,
            live ? ui.Accent : ui.MutedInk, TextStyles.Footnote);
        CasinoArt.LiveDot(drawList, new Vector2(crowdX - (CasinoArt.LiveDotRadius + 4f) * scale, row.Center.Y), scale,
            live ? ui.Accent : ui.MutedInk, live);

        var phase = RoomPhaseLine(gameId, RoomOf(gameId), out var open);
        var subtitle = phase.Length > 0 ? phase : Loc.T(L.Casino.RoomIdle);
        var textLeft = tileCenter.X + tile * 0.5f + CoinArt.TextGap * scale;
        var textRight = crowdX - (CasinoArt.LiveDotRadius * 2f + 4f + CoinArt.ValueGap) * scale;
        CoinArt.Labels(drawList, textLeft, textRight, row.Center.Y, Loc.T(name), subtitle, ui.TitleInk,
            open ? ui.Accent : ui.MutedInk, scale);
        return UiInteract.Click(row.Min, row.Max, hovered);
    }

    private float DrawHouseTables(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var tables = casinoTables.Tables;
        var tierOrder = Core.Casino.CasinoHouseTiers.All;
        var count = 0;
        for (var order = 0; order < tierOrder.Length; order++)
        {
            for (var index = 0; index < tables.Length; index++)
            {
                if (tables[index].Kind == Core.Casino.CasinoTableKinds.House && tables[index].StakeTier == tierOrder[order])
                {
                    count++;
                }
            }
        }

        if (count == 0)
        {
            var hint = casinoTables.Loaded ? Loc.T(L.Casino.NoHouseTables) : Loc.T(L.Casino.TablesLoading);
            return CoinArt.DrawPanel(ui, origin, width, FontAwesomeIcon.Couch, CasinoArt.TintOf(CasinoGames.Blackjack),
                Loc.T(L.Casino.LiveTablesHeading), hint, scale);
        }

        var rowHeight = HouseRowHeight * scale;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + rowHeight * count);
        ui.Card(drawList, min, max, Metrics.Radius.Grouped * scale);
        var drawn = 0;
        for (var order = 0; order < tierOrder.Length; order++)
        {
            for (var index = 0; index < tables.Length; index++)
            {
                var table = tables[index];
                if (table.Kind != Core.Casino.CasinoTableKinds.House || table.StakeTier != tierOrder[order])
                {
                    continue;
                }

                using (ImRaii.PushId(index))
                {
                    DrawHouseTableRow(drawList, RowAt(min, max.X, rowHeight, drawn), table, drawn > 0, scale);
                }

                drawn++;
            }
        }

        return max.Y;
    }

    private void DrawHouseTableRow(ImDrawListPtr drawList, Rect row, Core.Aethernet.Contracts.CasinoTableRowDto table,
        bool hairline, float scale)
    {
        var pad = Metrics.Space.Lg * scale;
        var tile = HouseRowTile * scale;
        if (hairline)
        {
            CoinArt.Hairline(drawList, ui, row.Min.X + pad + tile + CoinArt.TextGap * scale, row.Max.X, row.Min.Y);
        }

        var full = table.MaxSeats > 0 && table.SeatedCount >= table.MaxSeats;
        var canSit = !full && table.Admitted;
        var sitLabel = full ? Loc.T(L.Casino.TableFullBadge) : Loc.T(L.Casino.TableSit);
        var sitHeight = SitHeight * scale;
        var sitWidth = Button.WidthFor(sitLabel, ButtonSize.Regular);
        var sitRect = new Rect(new Vector2(row.Max.X - pad - sitWidth, row.Center.Y - sitHeight * 0.5f),
            new Vector2(row.Max.X - pad, row.Center.Y + sitHeight * 0.5f));
        var overSit = UiInteract.Hover(sitRect.Min, sitRect.Max);
        var hovered = !overSit && CoinArt.RowInteraction(drawList, ui, row, scale);

        var tileCenter = new Vector2(row.Min.X + pad + tile * 0.5f, row.Center.Y);
        CasinoArt.GameTile(drawList, CasinoGames.Blackjack, tileCenter, tile);
        var textLeft = tileCenter.X + tile * 0.5f + CoinArt.TextGap * scale;
        var textWidth = MathF.Max(1f, sitRect.Min.X - CoinArt.ValueGap * scale - textLeft);
        var headline = Typography.LineHeight(TextStyles.Headline);
        var footnote = Typography.LineHeight(TextStyles.Footnote);
        var top = row.Center.Y - (headline + footnote * 2f) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(Loc.T(TierLabel(table.StakeTier)), textWidth, TextStyles.Headline), ui.TitleInk,
            TextStyles.Headline);
        top += headline;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(texts.Numbers(L.Casino.TableStakes, table.MinBet, table.MaxBet), textWidth,
                TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        top += footnote;
        var seatInk = table.SeatedCount > 0 ? ui.Accent : ui.MutedInk;
        var dotsWidth = CasinoArt.SeatDots(drawList, new Vector2(textLeft, top + footnote * 0.5f), table.SeatedCount,
            Math.Min(table.MaxSeats, 8), seatInk, Palette.WithAlpha(ui.TitleInk, SeatEmptyAlpha), scale);
        var seatsLeft = textLeft + dotsWidth + (dotsWidth > 0f ? Metrics.Space.Sm * scale : 0f);
        Typography.Draw(drawList, new Vector2(seatsLeft, top),
            Typography.FitText(texts.Counts(L.Casino.TableSeats, table.SeatedCount, table.MaxSeats),
                MathF.Max(1f, textLeft + textWidth - seatsLeft), TextStyles.Footnote), seatInk, TextStyles.Footnote);

        if (Button.Draw(drawList, sitRect, sitLabel, ui.Ink, canSit ? ButtonStyle.Tinted : ButtonStyle.Gray,
                enabled: canSit, id: "casino.live.sit"))
        {
            OpenTable(table.TableId);
            return;
        }

        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            OpenTable(table.TableId);
        }
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
        Core.Casino.CasinoHouseTiers.Parlour => L.Casino.TierParlour,
        Core.Casino.CasinoHouseTiers.Salon => L.Casino.TierSalon,
        _ => L.Casino.TierPit,
    };
}
