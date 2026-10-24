using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Casino.Strip;
using Aetherphone.Apps.Coin;
using Aetherphone.Apps.Games;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino;

internal sealed partial class CasinoApp
{
    private const float CardGap = 12f;
    private const int RoomSampleSeconds = 119;
    private const string AllTablesId = "casino.live.all";

    internal static readonly string[] LiveRooms = { CasinoGames.Race, CasinoGames.Wheel, CasinoGames.Bingo };

    private readonly LiveRoomView[] roomViews = new LiveRoomView[LiveRooms.Length];
    private readonly Dictionary<int, string> returnLabels = new();
    private LanguageInfo? returnLanguage;
    private string? skillTopLabel;
    private LanguageInfo? skillTopLanguage;

    private static string RoomOf(string gameId) => gameId switch
    {
        CasinoGames.Bingo => CasinoRoomIds.BingoHall,
        CasinoGames.Wheel => CasinoRoomIds.WheelFloor,
        CasinoGames.Race => CasinoRoomIds.RaceTrack,
        _ => string.Empty,
    };

    private void DrawFloorTabBar(Rect area)
    {
        navTabs[0] = new TabItem(Loc.T(L.Strip.TabFloor), IconGlyph.Of(FontAwesomeIcon.DiceD20));
        navTabs[1] = new TabItem(Loc.T(L.Casino.TabLive), IconGlyph.Of(FontAwesomeIcon.BroadcastTower),
            Badge: LiveHeadcount(), AnchorKey: "casino.tab.live");
        navTabs[2] = new TabItem(Loc.T(L.VenueHub.Tab), IconGlyph.Of(FontAwesomeIcon.Store),
            AnchorKey: "casino.tab.tables");
        navTabs[3] = new TabItem(Loc.T(L.Casino.TabCashier), IconGlyph.Of(FontAwesomeIcon.CashRegister));
        var current = (int)routes.Tab;
        var result = bottomNav.Draw(area, ui, navTabs, current);
        if (result.Tapped < 0 || result.Tapped == current)
        {
            return;
        }

        SelectTab((CasinoTab)result.Tapped);
    }

    private int LiveHeadcount()
    {
        var total = casinoTables.SeatedAt(CasinoWire.BlackjackKind);
        total += casinoRooms.OccupancyOf(CasinoRoomIds.WheelFloor);
        total += casinoRooms.OccupancyOf(CasinoRoomIds.BingoHall);
        if (casino.HasFeature(CasinoFeatures.Race))
        {
            total += casinoRooms.OccupancyOf(CasinoRoomIds.RaceTrack);
        }

        return total;
    }

    private int CrowdAt(string gameId)
    {
        if (string.Equals(gameId, CasinoGames.Blackjack, StringComparison.Ordinal))
        {
            return casinoTables.SeatedAt(CasinoWire.BlackjackKind);
        }

        if (string.Equals(gameId, CasinoGames.Holdem, StringComparison.Ordinal))
        {
            return SeatedIn(holdemStore.Tables);
        }

        var room = RoomOf(gameId);
        return room.Length > 0 ? casinoRooms.OccupancyOf(room) : 0;
    }

    private static int SeatedIn(CasinoTableRowDto[] rows)
    {
        var total = 0;
        for (var index = 0; index < rows.Length; index++)
        {
            total += rows[index].SeatedCount;
        }

        return total;
    }

    private bool RoomLive(string roomId)
    {
        return casinoRooms.TryRoomClock(roomId, out _, out var endsAt) && endsAt > 0;
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
        if (phase != CasinoRoomPhases.Open)
        {
            return texts.Duration(L.Casino.RoomNextIn, seconds);
        }

        open = true;
        return texts.Duration(RoomOpenTemplate(gameId), seconds);
    }

    private static LocString RoomOpenTemplate(string gameId) => gameId switch
    {
        CasinoGames.Bingo => L.Casino.BingoCardsClose,
        CasinoGames.Race => L.Strip.RaceBetsClose,
        _ => L.Casino.WheelBetsCloseIn,
    };

    private float RoomCountdown(string gameId, string roomId)
    {
        if (!casinoRooms.TryRoomClock(roomId, out var phase, out var endsAtUnixMs) || endsAtUnixMs <= 0)
        {
            return 0f;
        }

        var remaining = casinoRooms.Room.RemainingMilliseconds(endsAtUnixMs,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        return LiveBoard.RingFraction(remaining, LiveBoard.WindowSeconds(gameId, phase));
    }

    private string MinimumStakeLine(string gameId)
    {
        var minimum = MinimumStakeOf(gameId);
        return minimum > 0 ? texts.Compact(L.Strip.FromChips, minimum) : string.Empty;
    }

    internal static long MinimumStakeOf(string gameId) => gameId switch
    {
        CasinoGames.Slots or CasinoGames.SlotsBird or CasinoGames.SlotsCascade or CasinoGames.SlotsMoogle =>
            SlotsRules.MinStake,
        CasinoGames.Mines or CasinoGames.Dice or CasinoGames.Limbo or CasinoGames.Keno or CasinoGames.HiLo =>
            OriginalsRules.MinBet,
        CasinoGames.Plinko => PlinkoRules.MinBet,
        CasinoGames.Race => RaceRules.MinBet,
        CasinoGames.Wheel => WheelRules.MinStakePerSpot,
        CasinoGames.Bingo => BingoRules.CardPrice,
        CasinoGames.Scratch => ScratchRules.Prices[0],
        CasinoGames.Barkeep => BarkeepRules.EntryChips,
        CasinoGames.Blackjack => BlackjackRules.MinBet,
        CasinoGames.Holdem => HoldemRules.BigBlindFor(0),
        CasinoGames.DealerHoldem => DealerHoldemRules.MinAnte,
        _ => 0,
    };

    internal static int ReturnTenthsOf(string gameId) => gameId switch
    {
        CasinoGames.SlotsBird or CasinoGames.SlotsCascade or CasinoGames.SlotsMoogle =>
            (SlotsMachines.For(gameId).ReturnBasisPoints + 5) / 10,
        CasinoGames.Mines or CasinoGames.Dice or CasinoGames.Limbo or CasinoGames.Keno or CasinoGames.HiLo =>
            OriginalsRules.ReturnTenths,
        CasinoGames.Plinko => PlinkoRules.ReturnTenths(PlinkoRules.DefaultRows, PlinkoRules.DefaultRisk),
        CasinoGames.Race => RaceRules.ReturnTenths,
        CasinoGames.Wheel => WheelRules.ReturnBasisPointsFor(0) / 10,
        CasinoGames.Bingo => BingoRules.ReturnTenths,
        CasinoGames.Scratch => ScratchRules.ReturnBasisPoints / 10,
        CasinoGames.Blackjack => BlackjackRules.ReturnTenths,
        CasinoGames.DealerHoldem => DealerHoldemRules.ReturnTenths,
        _ => 0,
    };

    private string ReturnLabel(int tenths)
    {
        if (tenths <= 0)
        {
            return string.Empty;
        }

        if (!ReferenceEquals(returnLanguage, Loc.Current))
        {
            returnLanguage = Loc.Current;
            returnLabels.Clear();
        }

        if (returnLabels.TryGetValue(tenths, out var cached))
        {
            return cached;
        }

        var label = Loc.T(L.Strip.PaysBackShort, (tenths / 10m).ToString("0.#", Loc.Culture));
        returnLabels[tenths] = label;
        return label;
    }

    private string SkillTopLabel()
    {
        if (skillTopLabel is not null && ReferenceEquals(skillTopLanguage, Loc.Current))
        {
            return skillTopLabel;
        }

        skillTopLanguage = Loc.Current;
        skillTopLabel = Loc.T(L.FloorPage.SkillPaysUpTo,
            (BarkeepRules.TopPayTenths / 10m).ToString("0.#", Loc.Culture));
        return skillTopLabel;
    }

    private string PosterReturn(string gameId) => gameId switch
    {
        CasinoGames.Holdem => Loc.T(L.FloorPage.PlayerVersusPlayer),
        CasinoGames.Barkeep => SkillTopLabel(),
        _ => ReturnLabel(ReturnTenthsOf(gameId)),
    };

    public PosterInfo Describe(in StripEntry entry)
    {
        var open = CasinoGameGate.IsOpen(casino.Features, entry.GameId);
        var room = RoomOf(entry.GameId);
        var meta = room.Length > 0 ? RoomPhaseLine(entry.GameId, room, out _) : string.Empty;
        if (meta.Length == 0)
        {
            meta = MinimumStakeLine(entry.GameId);
        }

        return new PosterInfo(open, CrowdAt(entry.GameId), meta, PosterReturn(entry.GameId),
            StripCatalog.WearsSkillBadge(entry.GameId) ? Loc.T(L.Floor.SkillBadge) : string.Empty);
    }

    public ICabinetIdle? IdleFor(string gameId) => machines.IdleFor(gameId);

    private float DrawLiveNow(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var gap = StripShelves.GridGap * scale;
        var count = OpenLiveRooms();
        if (count == 0)
        {
            return origin.Y;
        }

        var cardWidth = StripGrid.CellWidth(width, count, gap);
        CollectRoomViews(FloorRoomCard.TextWidth(cardWidth, scale), out var nameBlock, out var lineBlock);
        var top = FloorHeader.DrawWithLink(drawList, ui, origin, width, Loc.T(L.FloorPage.LiveTitle),
            Loc.T(L.FloorPage.AllTables), AllTablesId, scale, out var allTables) + FloorHeaderGap * scale;
        if (allTables)
        {
            SelectTab(CasinoTab.Live);
        }

        var height = FloorRoomCard.Height(nameBlock, lineBlock, scale);
        for (var index = 0; index < count; index++)
        {
            var left = origin.X + index * (cardWidth + gap);
            var card = new Rect(new Vector2(left, top), new Vector2(left + cardWidth, top + height));
            using (UiScope.PushId(index))
            {
                if (FloorRoomCard.Draw(drawList, ui, roomViews[index], card, scale, out var pressed))
                {
                    OpenGame(roomViews[index].GameId, pressed);
                }
            }
        }

        return top + height + FloorBlockGap * scale;
    }

    private int OpenLiveRooms()
    {
        var count = 0;
        var features = casino.Features;
        for (var index = 0; index < LiveRooms.Length; index++)
        {
            if (CasinoGameGate.IsOpen(features, LiveRooms[index]))
            {
                count++;
            }
        }

        return count;
    }

    private void CollectRoomViews(float textWidth, out float nameBlock, out float lineBlock)
    {
        nameBlock = 0f;
        var lines = 1;
        var count = 0;
        var features = casino.Features;
        for (var index = 0; index < LiveRooms.Length; index++)
        {
            var gameId = LiveRooms[index];
            if (!CasinoGameGate.IsOpen(features, gameId))
            {
                continue;
            }

            var roomId = RoomOf(gameId);
            var live = RoomLive(roomId);
            var line = live ? RoomPhaseLine(gameId, roomId, out _) : string.Empty;
            if (line.Length == 0)
            {
                live = false;
                line = Loc.T(L.Casino.RoomIdle);
            }

            var name = TileLabel.Fit(Loc.T(CasinoGameNames.Of(gameId)), textWidth, FloorRoomCard.NameLines,
                TextStyles.Headline, TextStyles.SubheadlineEmphasized);
            nameBlock = MathF.Max(nameBlock, TileLabel.Height(name));
            lines = Math.Max(lines, Math.Max(RoomLineCount(gameId, textWidth),
                Typography.WrapText(line, TextStyles.Footnote, textWidth).Length));
            roomViews[count++] = new LiveRoomView(gameId, name, line, casinoRooms.OccupancyOf(roomId), live,
                live ? RoomCountdown(gameId, roomId) : 0f);
        }

        lineBlock = FloorRoomCard.LineBlock(lines, Typography.LineHeight(TextStyles.Footnote));
    }

    private int RoomLineCount(string gameId, float textWidth)
    {
        var open = Typography.WrapText(texts.Duration(RoomOpenTemplate(gameId), RoomSampleSeconds),
            TextStyles.Footnote, textWidth).Length;
        var next = Typography.WrapText(texts.Duration(L.Casino.RoomNextIn, RoomSampleSeconds), TextStyles.Footnote,
            textWidth).Length;
        var idle = Typography.WrapText(Loc.T(L.Casino.RoomIdle), TextStyles.Footnote, textWidth).Length;
        return Math.Max(open, Math.Max(next, idle));
    }

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

    private static Rect RowAt(Vector2 min, float right, float rowHeight, int index)
    {
        var top = min.Y + index * rowHeight;
        return new Rect(new Vector2(min.X, top), new Vector2(right, top + rowHeight));
    }

    private static string ClientGameId(string wireKind) => CasinoRecentGames.ClientGameId(wireKind);
}
