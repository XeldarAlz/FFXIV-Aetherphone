using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino;

internal sealed partial class CasinoApp
{
    private const float LiveRowHeight = 84f;
    private const float TableRowHeight = 76f;
    private const float LiveRowGap = 8f;


    private void DrawLiveTab(Rect body)
    {
        var scale = UiScale.Current;
        using var surface = AppSurface.Begin(body);

        DrawStakeNotice(scale);

        ui.SectionHeading(Loc.T(L.Casino.LiveRoomsHeading), 4f);
        DrawLiveRoomRow(CasinoGames.Wheel, Core.Casino.CasinoRoomIds.WheelFloor, L.Casino.GameWheel, scale);
        ImGui.Dummy(new Vector2(0f, LiveRowGap * scale));
        DrawLiveRoomRow(CasinoGames.Bingo, Core.Casino.CasinoRoomIds.BingoHall, L.Casino.GameBingo, scale);

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        ui.SectionHeading(Loc.T(L.Casino.LiveTablesHeading), 4f);
        DrawHouseTables(scale);

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        if (DrawNavRow(FontAwesomeIcon.ThList, L.Casino.TablesRow, L.Casino.TablesRowHint, scale))
        {
            OpenTables();
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
    }

    private void DrawLiveRoomRow(string gameId, string roomId, LocString name, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var height = LiveRowHeight * scale;
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var rounding = Metrics.Radius.Card * scale;
        var hovered = UiInteract.Hover(row.Min, row.Max);

        ui.Card(drawList, row.Min, row.Max, rounding);
        if (hovered)
        {
            Squircle.Fill(drawList, row.Min, row.Max, rounding, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var occupancy = casinoRooms.OccupancyOf(roomId);
        var open = casinoRooms.TryRoomClock(roomId, out var phase, out var endsAtUnixMs) && endsAtUnixMs > 0;
        var inset = 14f * scale;

        var glyphCenter = new Vector2(row.Min.X + 38f * scale, row.Center.Y);
        drawList.AddCircleFilled(glyphCenter, 20f * scale, ImGui.GetColorU32(ui.Palette.FieldSurface), 40);
        CasinoGlyphs.Draw(drawList, gameId, glyphCenter, 11f * scale, ImGui.GetColorU32(ui.TitleInk),
            ImGui.GetColorU32(ui.Palette.FieldSurface));

        var textLeft = row.Min.X + 68f * scale;
        var title = Typography.FitText(Loc.T(name), width - 140f * scale, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 14f * scale), title, ui.TitleInk,
            TextStyles.SubheadlineEmphasized);

        var phaseLine = open ? RoomPhaseLine(gameId, roomId) : Loc.T(L.Casino.RoomIdle);
        var fitted = Typography.FitText(phaseLine, width - 140f * scale, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 36f * scale), fitted,
            open ? ui.Accent : ui.MutedInk, TextStyles.Footnote);

        var stakeLine = MinimumStakeLine(gameId);
        Typography.Draw(drawList, new Vector2(textLeft, row.Max.Y - 24f * scale), stakeLine, ui.MutedInk,
            TextStyles.Caption2);

        var headcount = Loc.T(L.Casino.LivePlayers, Apps.Games.Framework.GameNumber.Label(occupancy));
        var headSize = Typography.Measure(headcount, TextStyles.Caption1);
        Typography.Draw(drawList, new Vector2(row.Max.X - inset - headSize.X, row.Min.Y + 16f * scale), headcount,
            occupancy > 0 ? ui.Accent : ui.MutedInk, TextStyles.Caption1);

        AppSkin.Icon(drawList, new Vector2(row.Max.X - inset - 4f * scale, row.Center.Y + 12f * scale),
            IconGlyph.Of(FontAwesomeIcon.ChevronRight), ui.MutedInk, 0.8f);

        var clicked = UiInteract.Click(row.Min, row.Max, hovered);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        if (clicked)
        {
            OpenGame(gameId);
        }
    }

    private void DrawHouseTables(float scale)
    {
        var tables = casinoTables.Tables;
        var tierOrder = Core.Casino.CasinoHouseTiers.All;
        var drawn = 0;
        for (var order = 0; order < tierOrder.Length; order++)
        {
            var tier = tierOrder[order];
            for (var index = 0; index < tables.Length; index++)
            {
                var table = tables[index];
                if (table.Kind != Core.Casino.CasinoTableKinds.House || table.StakeTier != tier)
                {
                    continue;
                }

                if (drawn > 0)
                {
                    ImGui.Dummy(new Vector2(0f, LiveRowGap * scale));
                }

                DrawHouseTableRow(table, scale);
                drawn++;
            }
        }

        if (drawn > 0)
        {
            return;
        }

        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var hint = casinoTables.Loaded ? Loc.T(L.Casino.NoHouseTables) : Loc.T(L.Common.Loading);
        var block = Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, width);
        Typography.DrawWrappedLeft(origin, hint, ui.MutedInk, TextStyles.Footnote, width);
        ImGui.Dummy(new Vector2(width, block.Y));
    }

    private void DrawHouseTableRow(Core.Aethernet.Contracts.CasinoTableRowDto table, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var height = TableRowHeight * scale;
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var rounding = Metrics.Radius.Card * scale;
        var hovered = UiInteract.Hover(row.Min, row.Max);

        ui.Card(drawList, row.Min, row.Max, rounding);
        if (hovered)
        {
            Squircle.Fill(drawList, row.Min, row.Max, rounding, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var inset = 14f * scale;
        var tierName = Typography.FitText(Loc.T(TierLabel(table.StakeTier)), width * 0.55f,
            TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(row.Min.X + inset, row.Min.Y + 13f * scale), tierName, ui.TitleInk,
            TextStyles.SubheadlineEmphasized);

        var bets = Loc.T(L.Casino.TableStakes, NumberText.Group(table.MinBet),
            NumberText.Group(table.MaxBet));
        Typography.Draw(drawList, new Vector2(row.Min.X + inset, row.Min.Y + 34f * scale),
            Typography.FitText(bets, width * 0.6f, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);

        var seats = Loc.T(L.Casino.TableSeats, Apps.Games.Framework.GameNumber.Label(table.SeatedCount),
            Apps.Games.Framework.GameNumber.Label(table.MaxSeats));
        Typography.Draw(drawList, new Vector2(row.Min.X + inset, row.Max.Y - 22f * scale), seats,
            table.SeatedCount > 0 ? ui.Accent : ui.MutedInk, TextStyles.Caption1);

        var full = table.MaxSeats > 0 && table.SeatedCount >= table.MaxSeats;
        var pillLabel = full ? Loc.T(L.Casino.TableFullBadge) : Loc.T(L.Casino.TableSit);
        var pillHeight = 32f * scale;
        var pillMax = new Vector2(row.Max.X - inset, row.Center.Y + pillHeight * 0.5f);
        var pillMin = new Vector2(pillMax.X - 84f * scale, row.Center.Y - pillHeight * 0.5f);
        if (AppSkin.PillButton(new Rect(pillMin, pillMax), pillLabel, !full, !full && table.Admitted, theme))
        {
            OpenTable(table.TableId);
        }

        var clicked = UiInteract.Click(row.Min, row.Max, hovered);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        if (clicked)
        {
            OpenTable(table.TableId);
        }
    }

    private static LocString TierLabel(int tier) => tier switch
    {
        Core.Casino.CasinoHouseTiers.Parlour => L.Casino.TierParlour,
        Core.Casino.CasinoHouseTiers.Salon => L.Casino.TierSalon,
        _ => L.Casino.TierPit,
    };
}
