using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Casino.Strip;
using Aetherphone.Apps.Coin;
using Aetherphone.Apps.Games;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino;

internal sealed partial class CasinoApp
{
    private const float RecordRowHeight = 62f;
    private const float RecordTile = 34f;
    private const float CardGap = 12f;
    private const float LiveNowWidth = 168f;
    private const float LiveNowHeight = 104f;
    private const float LiveNowTile = 34f;
    private const float LiveNowPad = 12f;
    private const int LiveNowCapacity = 12;
    private const float RecordsTileHeight = 84f;
    private const float RecordsIcon = 34f;

    internal const int RecentGamesCap = 4;

    internal static readonly string[] LiveRooms = { CasinoGames.Race, CasinoGames.Wheel, CasinoGames.Bingo };

    private readonly LiveNowItem[] liveNow = new LiveNowItem[LiveNowCapacity];
    private readonly int[] recentPicks = new int[RecentGamesCap];
    private readonly StripEntry[] recentEntries = new StripEntry[RecentGamesCap];
    private CasinoRoundHistoryDto[]? recentRounds;
    private int recentCount;
    private readonly TileRail liveNowRail = new();
    private readonly Dictionary<int, string> returnLabels = new();
    private LanguageInfo? returnLanguage;

    private readonly record struct LiveNowItem(string GameId, string TableId, string RoomId, CasinoTableRowDto? Row);

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
        return gameId switch
        {
            CasinoGames.Bingo => texts.Duration(L.Casino.BingoCardsClose, seconds),
            CasinoGames.Race => texts.Duration(L.Strip.RaceBetsClose, seconds),
            _ => texts.Duration(L.Casino.WheelBetsCloseIn, seconds),
        };
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
        CasinoGames.Barkeep => Cabinets.BarkeepCabinet.ReturnTenths,
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

    public PosterInfo Describe(in StripEntry entry)
    {
        var open = CasinoGameGate.IsOpen(casino.Features, entry.GameId);
        var room = RoomOf(entry.GameId);
        var meta = room.Length > 0 ? RoomPhaseLine(entry.GameId, room, out _) : string.Empty;
        if (meta.Length == 0)
        {
            meta = MinimumStakeLine(entry.GameId);
        }

        return new PosterInfo(open, CrowdAt(entry.GameId), meta, ReturnLabel(ReturnTenthsOf(entry.GameId)),
            StripCatalog.WearsSkillBadge(entry.GameId) ? Loc.T(L.Floor.SkillBadge) : string.Empty);
    }

    public ICabinetIdle? IdleFor(string gameId) => machines.IdleFor(gameId);

    private float DrawRecentGames(ImDrawListPtr drawList, Vector2 origin, float width, float delta, float scale)
    {
        RefreshRecentGames();
        if (recentCount == 0)
        {
            return origin.Y;
        }

        var top = origin.Y + FloorSectionGap * scale * 0.5f;
        var bottom = shelves.DrawRecent(drawList, ui, this, new Vector2(origin.X, top), width, stage.Phase, delta,
            !router.IsTransitioning && !LaunchActive, scale, recentEntries.AsSpan(0, recentCount), out var tapped,
            out var source);
        if (tapped.GameId is not null)
        {
            OpenEntry(tapped, source);
        }

        return bottom + CardGap * scale;
    }

    private void RefreshRecentGames()
    {
        var rounds = history.Rounds;
        if (ReferenceEquals(rounds, recentRounds))
        {
            return;
        }

        recentRounds = rounds;
        recentCount = CasinoRecentGames.Collect(rounds, StripCatalog.PlayableIds, recentPicks);
        for (var index = 0; index < recentCount; index++)
        {
            recentEntries[index] = StripCatalog.Playable[recentPicks[index]];
        }
    }

    private float DrawLiveNow(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var count = CollectLiveNow();
        var top = SectionTitle(drawList, origin, width, Loc.T(L.Casino.LiveHeading), scale);
        var cardWidth = LiveNowWidth * scale;
        var cardHeight = LiveNowCardHeight(count, cardWidth - LiveNowPad * 2f * scale, scale);
        var gap = CardGap * scale;
        var pad = Strip.StripShelves.RailPad * scale;
        var contentWidth = Strip.StripShelves.RailContentWidth(count, cardWidth, gap, pad);
        var row = new Rect(new Vector2(origin.X - pad, top - pad), new Vector2(origin.X + width + pad, top + cardHeight + pad));
        liveNowRail.Begin(drawList, "##casino.livenow", row, row, contentWidth);
        var interactive = liveNowRail.TapAllowed;
        for (var index = 0; index < count; index++)
        {
            var left = origin.X + index * (cardWidth + gap) - liveNowRail.Offset;
            if (left > row.Max.X || left + cardWidth < row.Min.X)
            {
                continue;
            }

            var card = new Rect(new Vector2(left, top), new Vector2(left + cardWidth, top + cardHeight));
            using (ImRaii.PushId(index))
            {
                if (DrawLiveNowCard(drawList, liveNow[index], card, interactive, scale, out var pressed))
                {
                    EnterLive(liveNow[index], pressed);
                }
            }
        }

        liveNowRail.End(drawList, row, contentWidth, ui, cardWidth + gap);
        return top + cardHeight + gap;
    }

    private int CollectLiveNow()
    {
        var count = 0;
        var features = casino.Features;
        for (var index = 0; index < LiveRooms.Length; index++)
        {
            var gameId = LiveRooms[index];
            if (!CasinoGameGate.IsOpen(features, gameId))
            {
                continue;
            }

            liveNow[count++] = new LiveNowItem(gameId, string.Empty, RoomOf(gameId), null);
        }

        count = CollectTables(casinoTables.Listed, count, features);
        return CollectTables(holdemStore.Tables, count, features);
    }

    private int CollectTables(CasinoTableRowDto[] rows, int count, CasinoFeatureSet features)
    {
        for (var index = 0; index < rows.Length && count < LiveNowCapacity; index++)
        {
            var row = rows[index];
            if (CasinoTableFilters.PlayersOf(row) <= 0 || row.Paused || !CasinoGameGate.RoomOpen(features, row.GameKind)
                || ContainsTable(count, row.TableId))
            {
                continue;
            }

            var gameId = string.Equals(row.GameKind, HoldemRules.Kind, StringComparison.Ordinal)
                ? CasinoGames.Holdem
                : VenueKinds.IsVenue(row.GameKind)
                    ? Venue.VenueCabinet.GameIdOf(VenueKinds.Of(row.GameKind))
                    : CasinoGames.Blackjack;
            liveNow[count++] = new LiveNowItem(gameId, row.TableId, string.Empty, row);
        }

        return count;
    }

    private bool ContainsTable(int count, string tableId)
    {
        for (var index = 0; index < count; index++)
        {
            if (string.Equals(liveNow[index].TableId, tableId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private bool DrawLiveNowCard(ImDrawListPtr drawList, in LiveNowItem item, Rect card, bool interactive, float scale,
        out Rect pressed)
    {
        var hovered = CasinoArt.PressCard(ImGui.GetID("card"), card.Min, card.Max, out var pressedMin,
            out var pressedMax, interactive);
        pressed = new Rect(pressedMin, pressedMax);
        var radius = Metrics.Radius.Grouped * scale;
        ui.Card(drawList, pressed.Min, pressed.Max, radius);
        Squircle.Stroke(drawList, pressed.Min, pressed.Max, radius,
            ImGui.GetColorU32(CasinoArt.TintOf(item.GameId) with { W = 0.45f }), MathF.Max(1f, scale));
        var pad = LiveNowPad * scale;
        var tile = LiveNowTile * scale;
        var tileCenter = new Vector2(pressed.Min.X + pad + tile * 0.5f, pressed.Min.Y + pad + tile * 0.5f);
        CasinoArt.GameTile(drawList, item.GameId, tileCenter, tile);
        var live = item.Row is not null || RoomLive(item.RoomId);
        if (live)
        {
            DrawLiveNowCrowd(drawList, item, pressed, tileCenter, tile, pad, scale);
        }

        var textWidth = card.Width - pad * 2f;
        var left = pressed.Min.X + pad;
        var nameTop = tileCenter.Y + tile * 0.5f + Metrics.Space.Sm * scale;
        var lineTop = Tables.PitText.Draw(drawList, LiveNowName(item), TextStyles.Headline, ui.TitleInk, left, nameTop,
            textWidth, false);
        Tables.PitText.Draw(drawList, LiveNowLine(item, live), TextStyles.Footnote, ui.BodyInk, left, lineTop, textWidth,
            false);
        return interactive && UiInteract.Click(card.Min, card.Max, hovered);
    }

    private float LiveNowCardHeight(int count, float textWidth, float scale)
    {
        var height = LiveNowHeight * scale;
        var chrome = (LiveNowPad * 2f + LiveNowTile) * scale + Metrics.Space.Sm * scale;
        for (var index = 0; index < count; index++)
        {
            ref readonly var item = ref liveNow[index];
            var live = item.Row is not null || RoomLive(item.RoomId);
            height = MathF.Max(height, chrome
                + Tables.PitText.Height(LiveNowName(item), TextStyles.Headline, textWidth)
                + Tables.PitText.Height(LiveNowLine(item, live), TextStyles.Footnote, textWidth));
        }

        return height;
    }

    private string LiveNowName(in LiveNowItem item) =>
        item.Row is { } table ? TableName(table) : Loc.T(CasinoGameNames.Of(item.GameId));

    private string LiveNowLine(in LiveNowItem item, bool live)
    {
        if (item.Row is { } stakes)
        {
            return texts.Compacts(L.Casino.TableStakes, stakes.MinBet, stakes.MaxBet);
        }

        return live ? RoomPhaseLine(item.GameId, item.RoomId, out _) : Loc.T(L.Casino.RoomIdle);
    }

    private void DrawLiveNowCrowd(ImDrawListPtr drawList, in LiveNowItem item, Rect pressed, Vector2 tileCenter,
        float tile, float pad, float scale)
    {
        var crowd = item.Row is { } row ? CasinoTableFilters.PlayersOf(row) : casinoRooms.OccupancyOf(item.RoomId);
        var crowdText = texts.Count(L.Casino.LivePlayers, crowd);
        var crowdStyle = TextStyles.FootnoteEmphasized;
        var crowdLeft = tileCenter.X + tile * 0.5f + Metrics.Space.Sm * scale;
        var dotCenter = new Vector2(crowdLeft + CasinoArt.LiveDotRadius * scale, tileCenter.Y);
        CasinoArt.LiveDot(drawList, dotCenter, scale, CasinoColors.LightA, true);
        var crowdX = dotCenter.X + (CasinoArt.LiveDotRadius + 5f) * scale;
        Typography.Draw(drawList, new Vector2(crowdX, tileCenter.Y - Typography.LineHeight(crowdStyle) * 0.5f),
            Typography.FitText(crowdText, MathF.Max(1f, pressed.Max.X - pad - crowdX), crowdStyle),
            CasinoColors.InkTitle, crowdStyle);
    }

    private void EnterLive(in LiveNowItem item, Rect source)
    {
        if (item.TableId.Length > 0)
        {
            OpenTable(item.TableId);
            return;
        }

        OpenGame(item.GameId, source);
    }

    internal static float RecordsTileHeightFor(int lines, float lineHeight, float scale) =>
        MathF.Max(RecordsTileHeight * scale,
            Metrics.Space.Md * scale * 2f + RecordsIcon * scale + Metrics.Space.Sm * scale
            + Math.Max(1, lines) * lineHeight);

    internal static float RecordsLabelWidth(float tileWidth, float scale) =>
        MathF.Max(1f, tileWidth - Metrics.Space.Sm * 2f * scale);

    private static LocString RecordTitle(int index) => index switch
    {
        0 => L.Casino.HistoryRow,
        1 => L.Casino.FairnessRow,
        _ => L.Casino.LimitsRow,
    };

    private float DrawRecordsRow(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var top = SectionTitle(drawList, origin, width, Loc.T(L.Casino.RecordsHeading), scale);
        var gap = CardGap * scale;
        var tileWidth = (width - gap * 2f) / 3f;
        var labelWidth = RecordsLabelWidth(tileWidth, scale);
        var lines = 1;
        for (var index = 0; index < 3; index++)
        {
            lines = Math.Max(lines, Typography.WrapText(Loc.T(RecordTitle(index)), TextStyles.SubheadlineEmphasized,
                labelWidth).Length);
        }

        var height = RecordsTileHeightFor(lines, Typography.LineHeight(TextStyles.SubheadlineEmphasized), scale);
        UiAnchors.Report("casino.records", new Rect(new Vector2(origin.X, top), new Vector2(origin.X + width, top + height)));
        for (var index = 0; index < 3; index++)
        {
            var left = origin.X + index * (tileWidth + gap);
            var tile = new Rect(new Vector2(left, top), new Vector2(left + tileWidth, top + height));
            var (icon, tint) = index switch
            {
                0 => (FontAwesomeIcon.Receipt, AccentRing.Indigo),
                1 => (FontAwesomeIcon.ShieldAlt, AccentRing.Green),
                _ => (FontAwesomeIcon.HandHoldingHeart, AccentRing.Rose),
            };
            if (!DrawRecordTile(drawList, tile, icon, tint, RecordTitle(index), index, scale))
            {
                continue;
            }

            switch (index)
            {
                case 0:
                    OpenHistory();
                    break;
                case 1:
                    OpenFairness();
                    break;
                default:
                    OpenLimits();
                    break;
            }
        }

        return top + height;
    }

    private bool DrawRecordTile(ImDrawListPtr drawList, Rect tile, FontAwesomeIcon icon, Vector4 tint, LocString title,
        int index, float scale)
    {
        var hovered = CasinoArt.PressCard(ImGui.GetID($"record{index}"), tile.Min, tile.Max, out var min, out var max);
        ui.Card(drawList, min, max, Metrics.Radius.Grouped * scale);
        var iconSize = RecordsIcon * scale;
        var iconCenter = new Vector2(tile.Center.X, tile.Min.Y + Metrics.Space.Md * scale + iconSize * 0.5f);
        CasinoArt.IconTileAt(drawList, iconCenter, iconSize, tint, icon);
        var labelTop = iconCenter.Y + iconSize * 0.5f + Metrics.Space.Sm * scale;
        var style = TextStyles.SubheadlineEmphasized;
        var lineHeight = Typography.LineHeight(style);
        var lines = Typography.WrapText(Loc.T(title), style, RecordsLabelWidth(tile.Width, scale));
        for (var line = 0; line < lines.Length; line++)
        {
            Typography.DrawCentered(drawList, new Vector2(tile.Center.X, labelTop + lineHeight * (line + 0.5f)),
                lines[line], ui.TitleInk, style);
        }

        return UiInteract.Click(tile.Min, tile.Max, hovered);
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
            Loc.T(hint), ui.TitleInk, ui.BodyInk, scale);
        return UiInteract.Click(row.Min, row.Max, hovered);
    }

    private static string ClientGameId(string wireKind) => CasinoRecentGames.ClientGameId(wireKind);
}
