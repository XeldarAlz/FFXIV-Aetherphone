using Aetherphone.Apps.Casino.Live;
using Aetherphone.Apps.Casino.Strip;
using Aetherphone.Apps.Coin;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino;

internal sealed partial class CasinoApp
{
    private const float LiveTickerGap = 12f;

    private readonly LiveTable[] liveTables = new LiveTable[LiveBoard.Capacity];
    private readonly int[] liveShown = new int[LiveBoard.Capacity];
    private readonly CasinoTableRowDto?[] liveViewSources = new CasinoTableRowDto?[LiveBoard.Capacity];
    private readonly LiveTableView[] liveViews = new LiveTableView[LiveBoard.Capacity];
    private readonly LiveRoomCard[] liveRoomCards = new LiveRoomCard[LiveRooms.Length];
    private readonly string[] liveFilterLabels = new string[LiveBoard.Filters.Length];
    private readonly bool[] liveFilterActive = new bool[LiveBoard.Filters.Length];
    private readonly ChipRail liveFilterRail = new();
    private readonly LiveJoinBar liveJoin = new();
    private readonly HouseRoomsSheet holdemRooms =
        new("casino.live.holdem", CasinoGames.Holdem, HoldemRules.TierCount);
    private readonly HouseRoomsSheet blackjackRooms =
        new("casino.live.blackjack", CasinoGames.Blackjack, CasinoRoomIds.BlackjackHouse.Length);
    private readonly HoldemRoomRows holdemRoomRows = new();
    private readonly BlackjackRoomRows blackjackRoomRows = new();
    private readonly LiveChallenges liveChallenges = new();
    private LanguageInfo? liveViewLanguage;
    private CasinoTableRowDto? liveOwnSource;
    private LanguageInfo? liveOwnLanguage;
    private string liveOwnName = string.Empty;
    private LanguageInfo? livePhaseLanguage;
    private int livePhaseLines;
    private int liveDrawnFrame = int.MinValue / 2;
    private LiveFilter liveFilter;

    private void DrawLiveTab(Rect body)
    {
        EnterLiveWhenReturning();
        if (!router.IsTransitioning)
        {
            liveJoin.Consume(casinoTables);
        }

        casinoVenue.EnsureNearby();
        var scale = UiScale.Current;
        using (ImRaii.PushId("casino.live"))
        using (AppSurface.Begin(body))
        {
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var interactive = !router.IsTransitioning && !LiveSheetOpen;
            var cursorY = DrawLiveTicker(drawList, origin, width, scale);
            cursorY = DrawStakeNotice(new Vector2(origin.X, cursorY), width, scale);
            cursorY = liveJoin.Draw(drawList, ui, casinoTables, new Vector2(origin.X, cursorY), width,
                !LiveSheetOpen, scale);
            var pinned = PinnedLiveTable(out var owner);
            cursorY = DrawYourTable(drawList, pinned, owner, new Vector2(origin.X, cursorY), width, interactive, scale);
            cursorY = DrawOnTheClock(drawList, new Vector2(origin.X, cursorY), width, interactive, scale);
            cursorY = DrawChallenges(drawList, new Vector2(origin.X, cursorY), width, interactive, scale);
            cursorY = DrawHouseTables(drawList, new Vector2(origin.X, cursorY), width, interactive, scale);
            cursorY = DrawPlayerTables(drawList, pinned?.TableId ?? string.Empty, new Vector2(origin.X, cursorY), width,
                interactive, scale);
            cursorY = DrawLiveHostCta(drawList, new Vector2(origin.X, cursorY), width, interactive, scale);
            CoinArt.Reserve(origin, width, cursorY + CoinArt.BottomPad * scale);
        }
    }

    private bool LiveSheetOpen => holdemRooms.CapturesPointer || blackjackRooms.CapturesPointer;

    private void DrawLiveSheet()
    {
        if (!routes.OnRoot || routes.Tab != CasinoTab.Live)
        {
            holdemRooms.Close();
            blackjackRooms.Close();
        }

        if (holdemRooms.CapturesPointer)
        {
            holdemRoomRows.Refresh(holdemStore.Tables, !HoldemRules.RoyalOpenTo(HoldemBalance()));
            holdemRooms.Draw(screenArea, ui, Loc.T(L.LivePage.RoomsTitle), Loc.T(L.LivePage.RoomsLine),
                holdemRoomRows.Rows);
        }

        if (blackjackRooms.CapturesPointer)
        {
            blackjackRoomRows.Refresh(casinoTables.Tables, SeatedBlackjackTable(), casino.Ceiling.MaxBet);
            blackjackRooms.Draw(screenArea, ui, Loc.T(L.LivePage.BlackjackRoomsTitle),
                Loc.T(L.LivePage.BlackjackRoomsLine), blackjackRoomRows.Rows, Loc.T(L.Casino.QuickSeatAction),
                !casinoTables.IntentInFlight);
        }

        OpenTable(holdemRooms.TakePick());
        OpenTable(blackjackRooms.TakePick());
        if (blackjackRooms.TakeQuickSeat())
        {
            EnterHouseBlackjack();
        }
    }

    private void EnterLiveWhenReturning()
    {
        var frame = ImGui.GetFrameCount();
        var returning = frame - liveDrawnFrame > 1;
        liveDrawnFrame = frame;
        if (!returning)
        {
            return;
        }

        liveJoin.Enter();
        liveFilterRail.Reset();
        livePhaseLines = 0;
        casinoTables.RefreshNow();
        if (CasinoGameGate.IsOpen(casino.Features, CasinoGames.Holdem))
        {
            holdemStore.RefreshTables();
        }
    }

    private long HoldemBalance() =>
        (casino.State?.Sitting?.Stack ?? 0) + (casino.State?.TableSitting?.Stack ?? 0);

    private float DrawLiveTicker(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var bottom = winsTicker.Draw(drawList, floor.Ticker, floor.TickerVersion, origin, width, scale);
        return bottom > origin.Y ? bottom + LiveTickerGap * scale : origin.Y;
    }

    private CasinoTableRowDto? PinnedLiveTable(out bool owner)
    {
        owner = true;
        var hosted = casinoTables.HostedTableId();
        if (hosted.Length > 0 && LiveRowFor(hosted) is { } own)
        {
            return own;
        }

        owner = false;
        var rack = casino.State?.TableSitting;
        if (rack is null || rack.TableId.Length == 0)
        {
            return null;
        }

        var seat = LiveRowFor(rack.TableId);
        return LiveBoard.IsHostedSeat(seat, rack) ? seat : null;
    }

    private CasinoTableRowDto? LiveRowFor(string tableId) =>
        casinoTables.CardFor(tableId) ?? LiveBoard.Find(holdemStore.Tables, tableId);

    private float DrawYourTable(ImDrawListPtr drawList, CasinoTableRowDto? pinned, bool owner, Vector2 origin,
        float width, bool interactive, float scale)
    {
        if (pinned is null)
        {
            return origin.Y;
        }

        var top = LiveArt.Header(drawList, ui, new Vector2(origin.X, origin.Y + LiveArt.SectionGap * scale), width,
            Loc.T(L.LivePage.YourTable), string.Empty, scale) + LiveArt.HeaderGap * scale;
        if (owner)
        {
            casinoTables.EnsureDoorFresh(pinned.TableId);
        }

        var door = owner ? casinoTables.DoorFor(pinned.TableId) : null;
        var here = LiveBoard.HereOf(pinned, door);
        var knocking = LiveBoard.KnockingOf(door);
        var line = knocking > 0
            ? texts.Counts(L.LivePage.HereKnocking, here, knocking)
            : texts.Count(L.Venue.InRoom, here);
        var action = new LiveRowAction(Loc.T(owner ? L.LivePage.Manage : L.Strip.ResumeAction), true, false,
            "casino.live.own");
        var card = new Rect(new Vector2(origin.X, top), new Vector2(origin.X + width, top + LiveArt.OwnCardHeight(scale)));
        if (!LiveArt.OwnCard(drawList, ui, card, LiveBoard.TableGameOf(pinned.GameKind), LiveOwnName(pinned), line,
                action, interactive, scale))
        {
            return card.Max.Y;
        }

        if (owner)
        {
            OpenDoor(pinned.TableId);
        }
        else
        {
            OpenTable(pinned.TableId);
        }

        return card.Max.Y;
    }

    private string LiveOwnName(CasinoTableRowDto row)
    {
        if (ReferenceEquals(row, liveOwnSource) && ReferenceEquals(liveOwnLanguage, Loc.Current))
        {
            return liveOwnName;
        }

        liveOwnSource = row;
        liveOwnLanguage = Loc.Current;
        liveOwnName = Tables.TableRowViews.NameOf(row, LiveBoard.TableGameOf(row.GameKind));
        return liveOwnName;
    }

    private float DrawOnTheClock(ImDrawListPtr drawList, Vector2 origin, float width, bool interactive, float scale)
    {
        var features = casino.Features;
        var nowMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var count = 0;
        for (var index = 0; index < LiveRooms.Length; index++)
        {
            var gameId = LiveRooms[index];
            if (CasinoGameGate.IsOpen(features, gameId))
            {
                liveRoomCards[count++] = LiveRoomCardOf(gameId, nowMilliseconds);
            }
        }

        if (count == 0)
        {
            return origin.Y;
        }

        var top = LiveArt.Header(drawList, ui, new Vector2(origin.X, origin.Y + LiveArt.SectionGap * scale), width,
            Loc.T(L.LivePage.ClockTitle), Loc.T(L.LivePage.ClockLine), scale) + LiveArt.HeaderGap * scale;
        var gap = LiveArt.RoomGap * scale;
        var cardWidth = (width - gap * (count - 1)) / count;
        if (!ReferenceEquals(livePhaseLanguage, Loc.Current))
        {
            livePhaseLanguage = Loc.Current;
            livePhaseLines = 0;
        }

        var nameHeight = 0f;
        for (var index = 0; index < count; index++)
        {
            nameHeight = MathF.Max(nameHeight, LiveArt.RoomNameHeight(liveRoomCards[index].Name, cardWidth, scale));
            livePhaseLines = Math.Max(livePhaseLines, Math.Max(LiveArt.MinimumPhaseLines,
                LiveArt.PhaseLinesOf(liveRoomCards[index].Phase, cardWidth, scale)));
        }

        var height = LiveArt.RoomCardHeight(nameHeight, livePhaseLines, scale);
        for (var index = 0; index < count; index++)
        {
            var left = origin.X + index * (cardWidth + gap);
            var rect = new Rect(new Vector2(left, top), new Vector2(left + cardWidth, top + height));
            using (ImRaii.PushId(index))
            {
                if (LiveArt.RoomCard(drawList, ui, liveRoomCards[index], rect, nameHeight, livePhaseLines, interactive,
                        scale, out var pressed))
                {
                    OpenGame(liveRoomCards[index].GameId, pressed);
                }
            }
        }

        return top + height;
    }

    private LiveRoomCard LiveRoomCardOf(string gameId, long nowMilliseconds)
    {
        var roomId = RoomOf(gameId);
        var phaseLine = RoomPhaseLine(gameId, roomId, out var open);
        var clocked = phaseLine.Length > 0;
        var ring = 0f;
        if (clocked && casinoRooms.TryRoomClock(roomId, out var phase, out var endsAt))
        {
            ring = LiveBoard.RingFraction(casinoRooms.Room.RemainingMilliseconds(endsAt, nowMilliseconds),
                LiveBoard.WindowSeconds(gameId, phase));
        }

        return new LiveRoomCard(gameId, Loc.T(CasinoGameNames.Of(gameId)),
            clocked ? phaseLine : Loc.T(L.Casino.RoomIdle), texts.Count(L.Venue.InRoom, casinoRooms.OccupancyOf(roomId)),
            ring, open, clocked);
    }

    private float DrawChallenges(ImDrawListPtr drawList, Vector2 origin, float width, bool interactive, float scale)
    {
        if (!casino.HasFeature(CasinoFeatures.Challenges))
        {
            return origin.Y;
        }

        var views = liveChallenges.Refresh(floor.Challenges);
        var now = floor.ServerNowUnix;
        var showing = 0;
        for (var index = 0; index < views.Length; index++)
        {
            if (LiveChallenges.PhaseOf(views[index], now) != ChallengePhase.Over)
            {
                showing++;
            }
        }

        if (showing == 0)
        {
            return origin.Y;
        }

        var top = LiveArt.Header(drawList, ui, new Vector2(origin.X, origin.Y + LiveArt.SectionGap * scale), width,
            Loc.T(L.LivePage.ChallengesTitle), Loc.T(L.LivePage.ChallengesLine), scale) + LiveArt.HeaderGap * scale;
        var gap = LiveArt.RowGap * scale;
        for (var index = 0; index < views.Length; index++)
        {
            var phase = LiveChallenges.PhaseOf(views[index], now);
            if (phase == ChallengePhase.Over)
            {
                continue;
            }

            var running = phase == ChallengePhase.Running;
            var clock = running
                ? texts.Duration(L.LivePage.ChallengeEndsIn, LiveChallenges.SecondsUntil(views[index].EndsAtUnix, now))
                : texts.Duration(L.LivePage.ChallengeStartsIn,
                    LiveChallenges.SecondsUntil(views[index].StartsAtUnix, now));
            var height = ChallengeCard.Height(views[index], running, width, scale);
            var rect = new Rect(new Vector2(origin.X, top), new Vector2(origin.X + width, top + height));
            using (ImRaii.PushId(index))
            {
                if (ChallengeCard.Draw(drawList, ui, rect, views[index], clock, running, interactive, scale))
                {
                    OpenGame(views[index].GameId, rect);
                }
            }

            top = rect.Max.Y + gap;
        }

        return top - gap;
    }

    private float DrawHouseTables(ImDrawListPtr drawList, Vector2 origin, float width, bool interactive, float scale)
    {
        var features = casino.Features;
        var blackjackOpen = CasinoGameGate.RoomOpen(features, CasinoWire.BlackjackKind);
        var holdemOpen = CasinoGameGate.IsOpen(features, CasinoGames.Holdem);
        if (!blackjackOpen && !holdemOpen)
        {
            return origin.Y;
        }

        var top = LiveArt.Header(drawList, ui, new Vector2(origin.X, origin.Y + LiveArt.SectionGap * scale), width,
            Loc.T(L.LivePage.HouseTitle), Loc.T(L.LivePage.HouseLine), scale) + LiveArt.HeaderGap * scale;
        var floorRow = HouseBlackjack.Of(casinoTables.Tables);
        var blackjackLine = HouseBlackjackStakes(L.LivePage.BlackjackLine, floorRow);
        var holdemLine = texts.Count(L.LivePage.HoldemLine, HoldemRules.TierCount);
        var rejoins = CasinoRoomIds.IsBlackjackHouse(SeatedBlackjackTable());
        var blackjackAction = rejoins
            ? new LiveRowAction(Loc.T(L.Strip.ResumeAction), true, false, "casino.live.rejoin")
            : new LiveRowAction(Loc.T(L.LivePage.Pick), false, true, "casino.live.pickBlackjack");
        var pick = new LiveRowAction(Loc.T(L.LivePage.Pick), false, true, "casino.live.pick");
        var blackjackHeight = blackjackOpen ? LiveArt.HouseRowHeight(blackjackLine, width, blackjackAction, scale) : 0f;
        var holdemHeight = holdemOpen ? LiveArt.HouseRowHeight(holdemLine, width, pick, scale) : 0f;
        var max = new Vector2(origin.X + width, top + blackjackHeight + holdemHeight);
        CoinArt.Card(drawList, ui, new Vector2(origin.X, top), max, scale);
        var rowTop = top;
        if (blackjackOpen)
        {
            var row = new Rect(new Vector2(origin.X, rowTop), new Vector2(max.X, rowTop + blackjackHeight));
            if (LiveArt.HouseRow(drawList, ui, row, CasinoGames.Blackjack, Loc.T(L.Casino.GameBlackjack),
                    blackjackLine, texts.Count(L.Casino.LivePlayers, floorRow.Seated), blackjackAction, false,
                    interactive, scale))
            {
                OpenHouseBlackjack(rejoins);
            }

            rowTop = row.Max.Y;
        }

        if (holdemOpen)
        {
            var row = new Rect(new Vector2(origin.X, rowTop), new Vector2(max.X, rowTop + holdemHeight));
            if (LiveArt.HouseRow(drawList, ui, row, CasinoGames.Holdem, Loc.T(L.Casino.GameHoldem), holdemLine,
                    texts.Count(L.Casino.LivePlayers, LiveBoard.HouseHoldemSeated(holdemStore.Tables)), pick,
                    blackjackOpen, interactive, scale))
            {
                holdemRooms.Open();
            }
        }

        return max.Y;
    }

    private float DrawPlayerTables(ImDrawListPtr drawList, string pinnedTableId, Vector2 origin, float width,
        bool interactive, float scale)
    {
        var collected = LiveBoard.Collect(casinoTables.Listed, holdemStore.Tables, casinoVenue.Nearby,
            casino.Features, pinnedTableId, casinoTables.AccountId, liveTables);
        var top = LiveArt.Header(drawList, ui, new Vector2(origin.X, origin.Y + LiveArt.SectionGap * scale), width,
            Loc.T(L.LivePage.PlayerTitle), Loc.T(L.LivePage.PlayerLine), scale) + LiveArt.HeaderGap * scale;
        if (LiveBoard.ShowsRail(collected))
        {
            top = DrawLiveFilters(new Rect(new Vector2(origin.X, top),
                new Vector2(origin.X + width, top + ChipRail.RowHeight * scale))) + LiveArt.RowGap * scale;
        }
        else
        {
            liveFilter = LiveFilter.All;
        }

        var shown = LiveBoard.Filter(liveTables, collected, liveFilter, liveShown);
        if (shown == 0)
        {
            var empty = casinoTables.Loaded
                ? Loc.T(LiveBoard.EmptyOf(liveFilter))
                : liveJoin.DirectoryFailed
                    ? Loc.T(CasinoReasons.MessageFor(CasinoReasons.Unreachable))
                    : Loc.T(L.Casino.TablesLoading);
            LiveArt.Empty(drawList, ui, new Vector2(origin.X, top), width, empty, scale);
            return top + LiveArt.EmptyHeight(empty, width, scale);
        }

        RefreshLiveViews(collected);
        var height = LiveArt.TableRowHeight(scale);
        var gap = LiveArt.RowGap * scale;
        for (var index = 0; index < shown; index++)
        {
            var slot = liveShown[index];
            var table = liveTables[slot];
            var rect = new Rect(new Vector2(origin.X, top), new Vector2(origin.X + width, top + height));
            top = rect.Max.Y + gap;
            if (!ImGui.IsRectVisible(rect.Min, rect.Max))
            {
                continue;
            }

            using (ImRaii.PushId(table.Row.TableId))
            {
                if (LiveArt.TableRow(drawList, ui, rect, table.GameId, liveViews[slot], table.Here, interactive,
                        scale))
                {
                    OpenTable(table.Row.TableId);
                }
            }
        }

        return top - gap;
    }

    private float DrawLiveFilters(Rect row)
    {
        for (var index = 0; index < LiveBoard.Filters.Length; index++)
        {
            liveFilterLabels[index] = Loc.T(LiveBoard.LabelOf(LiveBoard.Filters[index]));
            liveFilterActive[index] = LiveBoard.Filters[index] == liveFilter;
        }

        var tapped = liveFilterRail.Draw(row, ui, liveFilterLabels, liveFilterActive);
        if (tapped >= 0)
        {
            liveFilter = LiveBoard.Filters[tapped];
        }

        return row.Max.Y;
    }

    private void RefreshLiveViews(int count)
    {
        if (!ReferenceEquals(liveViewLanguage, Loc.Current))
        {
            liveViewLanguage = Loc.Current;
            Array.Clear(liveViewSources);
        }

        for (var slot = 0; slot < count; slot++)
        {
            var row = liveTables[slot].Row;
            if (ReferenceEquals(liveViewSources[slot], row))
            {
                continue;
            }

            liveViewSources[slot] = row;
            liveViews[slot] = LiveBoard.ViewOf(row);
        }
    }

    private float DrawLiveHostCta(ImDrawListPtr drawList, Vector2 origin, float width, bool interactive, float scale)
    {
        var top = origin.Y + LiveArt.SectionGap * scale;
        var line = Loc.T(L.LivePage.HostLine);
        if (LiveArt.HostCta(drawList, ui, new Vector2(origin.X, top), width, Loc.T(L.LivePage.HostTitle), line,
                interactive, scale))
        {
            SelectTab(CasinoTab.Host);
        }

        return top + LiveArt.CtaHeight(line, width, scale);
    }

    private string JoinCodeOf(string tableId) => casinoTables.CardFor(tableId)?.JoinCode ?? string.Empty;

    private string SeatedBlackjackTable() =>
        CasinoWire.SittingFor(casino.State, CasinoWire.BlackjackKind)?.TableId ?? string.Empty;

    private string HouseBlackjackStakes(LocString template, in HouseBlackjackFloor floorRow) =>
        texts.Compacts(template, floorRow.MinBet, floorRow.TopBetFor(casino.Ceiling.MaxBet));

    private void OpenHouseBlackjack(bool rejoins)
    {
        if (rejoins)
        {
            OpenTable(SeatedBlackjackTable());
            return;
        }

        blackjackRooms.Open();
    }

    private void EnterHouseBlackjack()
    {
        var seatedAt = SeatedBlackjackTable();
        if (CasinoRoomIds.IsBlackjackHouse(seatedAt))
        {
            OpenTable(seatedAt);
            return;
        }

        casinoTables.QuickSeat(CasinoStakeTiers.Any);
    }
}
