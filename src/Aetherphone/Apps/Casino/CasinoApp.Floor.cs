using Aetherphone.Apps.Coin;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino;

internal sealed partial class CasinoApp
{
    private const float RecordRowHeight = 62f;
    private const float RecordTile = 34f;
    private const float CardGap = 12f;

    private static readonly string[] FloorGameIds =
    {
        CasinoGames.Blackjack,
        CasinoGames.Slots,
        CasinoGames.Scratch,
        CasinoGames.Barkeep,
        CasinoGames.Bingo,
        CasinoGames.Wheel,
    };

    private static readonly LocString[] FloorGameNames =
    {
        L.Casino.GameBlackjack,
        L.Casino.GameSlots,
        L.Casino.GameScratch,
        L.Casino.GameBarkeep,
        L.Casino.GameBingo,
        L.Casino.GameWheel,
    };

    private static string RoomOf(string gameId) => gameId switch
    {
        CasinoGames.Bingo => Core.Casino.CasinoRoomIds.BingoHall,
        CasinoGames.Wheel => Core.Casino.CasinoRoomIds.WheelFloor,
        _ => string.Empty,
    };

    private void DrawFloorTabBar(Rect area)
    {
        navTabs[0] = new TabItem(Loc.T(L.Casino.TabLobby), IconGlyph.Of(FontAwesomeIcon.DiceD20));
        navTabs[1] = new TabItem(Loc.T(L.Casino.TabGames), IconGlyph.Of(FontAwesomeIcon.Th),
            AnchorKey: "casino.tab.games");
        navTabs[2] = new TabItem(Loc.T(L.Casino.TabLive), IconGlyph.Of(FontAwesomeIcon.BroadcastTower),
            Badge: LiveHeadcount());
        navTabs[3] = new TabItem(Loc.T(L.Casino.TabCashier), IconGlyph.Of(FontAwesomeIcon.CashRegister));
        var result = bottomNav.Draw(area, ui, navTabs, (int)tab);
        if (result.Tapped < 0 || result.Tapped == (int)tab)
        {
            return;
        }

        tab = (CasinoTab)result.Tapped;
    }

    private int LiveHeadcount()
    {
        var total = casinoTables.SeatedAt(Core.Casino.CasinoWire.BlackjackKind);
        total += casinoRooms.OccupancyOf(Core.Casino.CasinoRoomIds.WheelFloor);
        total += casinoRooms.OccupancyOf(Core.Casino.CasinoRoomIds.BingoHall);
        return total;
    }

    private int CrowdAt(string gameId)
    {
        if (string.Equals(gameId, CasinoGames.Blackjack, StringComparison.Ordinal))
        {
            return casinoTables.SeatedAt(Core.Casino.CasinoWire.BlackjackKind);
        }

        var room = RoomOf(gameId);
        return room.Length > 0 ? casinoRooms.OccupancyOf(room) : 0;
    }

    private bool RoomLive(string roomId)
    {
        return casinoRooms.TryRoomClock(roomId, out _, out var endsAt) && endsAt > 0;
    }

    private bool AnyRoomLive()
    {
        return RoomLive(Core.Casino.CasinoRoomIds.WheelFloor) || RoomLive(Core.Casino.CasinoRoomIds.BingoHall);
    }

    private string RoomPhaseLine(string gameId, string roomId, out bool open)
    {
        open = false;
        if (!casinoRooms.TryRoomClock(roomId, out var phase, out var endsAtUnixMs) || endsAtUnixMs <= 0)
        {
            return string.Empty;
        }

        var remaining = casinoRooms.Room.RemainingMilliseconds(endsAtUnixMs,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        if (remaining <= 0)
        {
            return string.Empty;
        }

        var seconds = (int)((remaining + 999) / 1000);
        if (phase != Core.Casino.CasinoRoomPhases.Open)
        {
            return texts.Duration(L.Casino.RoomNextIn, seconds);
        }

        open = true;
        return string.Equals(gameId, CasinoGames.Bingo, StringComparison.Ordinal)
            ? texts.Duration(L.Casino.BingoCardsClose, seconds)
            : texts.Duration(L.Casino.WheelBetsCloseIn, seconds);
    }

    private string MinimumStakeLine(string gameId)
    {
        var minimum = MinimumStakeOf(gameId);
        return minimum > 0 ? texts.Number(L.Casino.MinimumStake, minimum) : string.Empty;
    }

    private static long MinimumStakeOf(string gameId) => gameId switch
    {
        CasinoGames.Slots => Core.Casino.SlotsRules.MinStake,
        CasinoGames.Wheel => Core.Casino.WheelRules.MinStakePerSpot,
        CasinoGames.Bingo => Core.Casino.BingoRules.CardPrice,
        CasinoGames.Scratch => Core.Casino.ScratchRules.Prices[0],
        CasinoGames.Barkeep => Core.Casino.BarkeepRules.EntryChips,
        CasinoGames.Blackjack => Core.Casino.BlackjackRules.HouseFloor,
        _ => 0,
    };

    private float DrawStakeNotice(Vector2 origin, float width, float scale)
    {
        var state = casino.State;
        if (state is null || (!state.StakesPaused && !state.Draining))
        {
            return origin.Y;
        }

        var title = state.StakesPaused ? Loc.T(L.Casino.PausedTitle) : Loc.T(L.Casino.DrainingTitle);
        var hint = state.StakesPaused ? Loc.T(L.Casino.PausedHint) : Loc.T(L.Casino.DrainingHint);
        var bottom = CoinArt.DrawPanel(ui, origin, width, state.StakesPaused ? FontAwesomeIcon.Pause
            : FontAwesomeIcon.DoorClosed, AccentRing.Orange, title, hint, scale);
        return bottom + CardGap * scale;
    }

    private float SectionTitle(ImDrawListPtr drawList, Vector2 origin, float width, string title, float scale)
    {
        var top = origin.Y + CoinArt.SectionGap * scale;
        return top + CardSectionHeader.Draw(drawList, new Vector2(origin.X, top), width, title, ui.TitleInk) +
               CoinArt.HeaderGap * scale;
    }

    private float DrawRecordsCard(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var top = SectionTitle(drawList, origin, width, Loc.T(L.Casino.RecordsHeading), scale);
        var rowHeight = RecordRowHeight * scale;
        var min = new Vector2(origin.X, top);
        var max = new Vector2(origin.X + width, top + rowHeight * 3f);
        ui.Card(drawList, min, max, Metrics.Radius.Grouped * scale);
        if (DrawRecordRow(drawList, RowAt(min, max.X, rowHeight, 0), FontAwesomeIcon.Receipt, AccentRing.Indigo,
                L.Casino.HistoryRow, L.Casino.HistoryRowHint, false, scale))
        {
            OpenHistory();
        }

        if (DrawRecordRow(drawList, RowAt(min, max.X, rowHeight, 1), FontAwesomeIcon.ShieldAlt, AccentRing.Green,
                L.Casino.FairnessRow, L.Casino.FairnessRowHint, true, scale))
        {
            OpenFairness();
        }

        if (DrawRecordRow(drawList, RowAt(min, max.X, rowHeight, 2), FontAwesomeIcon.HandHoldingHeart,
                AccentRing.Rose, L.Casino.LimitsRow, L.Casino.LimitsRowHint, true, scale))
        {
            OpenLimits();
        }

        return max.Y;
    }

    private static Rect RowAt(Vector2 min, float right, float rowHeight, int index)
    {
        var top = min.Y + index * rowHeight;
        return new Rect(new Vector2(min.X, top), new Vector2(right, top + rowHeight));
    }

    private bool DrawRecordRow(ImDrawListPtr drawList, Rect row, FontAwesomeIcon icon, Vector4 tint, LocString title,
        LocString hint, bool hairline, float scale)
    {
        var pad = Metrics.Space.Lg * scale;
        if (hairline)
        {
            CoinArt.Hairline(drawList, ui, row.Min.X + pad + (RecordTile + CoinArt.TextGap) * scale, row.Max.X,
                row.Min.Y);
        }

        var hovered = CoinArt.RowInteraction(drawList, ui, row, scale);
        var tile = RecordTile * scale;
        var tileCenter = new Vector2(row.Min.X + pad + tile * 0.5f, row.Center.Y);
        CasinoArt.IconTileAt(drawList, tileCenter, tile, tint, icon);
        var chevronCenter = new Vector2(row.Max.X - pad - 4f * scale, row.Center.Y);
        CasinoArt.Chevron(drawList, chevronCenter, ui.MutedInk);
        var textLeft = tileCenter.X + tile * 0.5f + CoinArt.TextGap * scale;
        CoinArt.Labels(drawList, textLeft, chevronCenter.X - CoinArt.ValueGap * scale, row.Center.Y, Loc.T(title),
            Loc.T(hint), ui.TitleInk, ui.MutedInk, scale);
        return UiInteract.Click(row.Min, row.Max, hovered);
    }

    private void AskCashOut(Core.Aethernet.Contracts.CasinoSittingDto sitting)
    {
        cashierCashOut.Ask(CashOutSplit.Of(casino.State));
    }

    private static string ClientGameId(string wireKind) => CasinoRecentGames.ClientGameId(wireKind);
}
