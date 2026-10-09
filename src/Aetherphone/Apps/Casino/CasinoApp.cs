using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Conduct;
using Aetherphone.Core.Honorific;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino;

internal sealed partial class CasinoApp : IPhoneApp, ITabRouteTarget, INameplateActivitySource, Strip.IStripFloor
{
    private const int RulesButton = 0;
    private const float StageSurfaceSlack = 16f;
    private const float NotOpenCardWidth = 300f;

    public string Id => "casino";
    public string DisplayName => Loc.T(L.Apps.Casino);
    public string Glyph => "Sa";
    public int BadgeCount => configuration.HasUnseenFeaturePin(Core.Changelog.NewFeaturePins.Casino) ? 1 : 0;

    private readonly AethernetSession session;
    private readonly Core.Casino.CasinoFloorStore floor;
    private readonly Configuration configuration;
    private readonly Core.Casino.CasinoPreferences preferences;
    private readonly Strip.StripHero stripHero = new();
    private readonly Strip.StripCarousel heroCards = new();
    private readonly Strip.StripShelves shelves = new();
    private readonly Strip.WinsTicker winsTicker = new();
    private readonly Strip.StripIntro intro = new();
    private readonly CoinStore coins;
    private readonly Core.Casino.CasinoStore casino;
    private readonly Core.Casino.CasinoPlayStore casinoPlay;
    private readonly Core.Casino.CasinoHistoryStore history;
    private readonly Core.Casino.CasinoRoomsStore casinoRooms;
    private readonly Core.Casino.CasinoTablesStore casinoTables;
    private readonly Core.Casino.CasinoSpinStore casinoSpin;
    private readonly Core.Casino.CasinoLauncher launcher;
    private readonly ConfirmService confirm;
    private readonly ConductGateService conduct;
    private readonly CashierDrawer cashier;
    private readonly CashierBonusShelf bonusShelf;
    private readonly CashierCashOut cashierCashOut;
    private readonly CashierClubCard clubCard = new();
    private readonly Machines.MachineCabinet machines;
    private readonly Cabinets.ScratchCabinet scratch;
    private readonly Cabinets.BarkeepCabinet barkeep;
    private readonly Cabinets.WheelCabinet wheel;
    private readonly Cabinets.BingoCabinet bingo;
    private readonly Cabinets.DailySpinCabinet dailySpin;
    private readonly Originals.OriginalsCabinet originals;
    private readonly Race.RaceCabinet race;
    private readonly Plinko.PlinkoCabinet plinko;
    private readonly Tables.BlackjackTable blackjack;
    private readonly Core.Casino.HoldemStore holdemStore;
    private readonly Tables.HoldemTable holdem;
    private readonly Tables.HoldemPit holdemPit;
    private readonly Tables.TableBrowser browser;
    private readonly Tables.BlackjackPit pit;
    private readonly Tables.TableHandsCard tableHands;
    private readonly Tables.HostSheet hostSheet;
    private readonly Tables.TableLedger playerLedger;
    private readonly Tables.TableDoor tableDoor;
    private readonly Core.Casino.CasinoVenueStore casinoVenue;
    private readonly Venue.VenueCabinet venueRoom;
    private readonly Venue.BroadcastView broadcast;
    private readonly Venue.TournamentOverlay tournament = new();
    private readonly Venue.VenueTableSheet venueSheet;
    private readonly Venue.TradeSyncPrompt tradePrompt;
    private readonly Venue.NearbyTablesCard nearbyTables;
    private readonly Action<Core.Aethernet.Contracts.CasinoTableRowDto> openNearbyRow;
    private readonly CasinoStage stage = new();
    private readonly GameRulesSheet rulesSheet = new();
    private readonly TabBar bottomNav = new();
    private readonly TabItem[] navTabs = new TabItem[4];
    private readonly NavBarButton[] navButtons = new NavBarButton[1];
    private readonly PullToRefresh lobbyRefresh = new();
    private readonly CasinoTextCache texts = new();
    private readonly AppSkin ui = new(AppPalettes.Gamba);
    private readonly ViewRouter<CasinoRoute> router;
    private readonly CasinoNavigation routes;
    private readonly RouterDraw<CasinoRoute> drawView;
    private readonly Action popRoute;
    private readonly Action openLimits;
    private readonly Action refreshFloor;
    private readonly Action<string> openTable;
    private readonly Action<string> openDoorFromRow;

    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private Rect screenArea;
    private string pendingTableId = string.Empty;
    private bool historyLoadFailed;

    public CasinoApp(AethernetSession session, CoinStore coins, Core.Casino.CasinoStore casino,
        Core.Casino.CasinoPlayStore casinoPlay, Core.Casino.CasinoHistoryStore history,
        Core.Casino.CasinoRoomsStore casinoRooms, Core.Casino.CasinoTablesStore casinoTables,
        Core.Casino.CasinoSpinStore casinoSpin, Core.Casino.CasinoTurnNotifier casinoTurns,
        Core.Casino.CasinoLauncher launcher, Core.Games.GameStatsStore gameStats, ConfirmService confirm,
        ConductGateService conduct, Core.Media.RemoteImageCache remoteImages,
        Core.Lodestone.LodestoneService lodestone, Core.Casino.HoldemStore holdemStore,
        Core.Casino.CasinoVenueStore casinoVenue, Core.Casino.CasinoTradeSync tradeSync,
        Core.Report.ReportService report, Core.Casino.CasinoFloorStore floor, Configuration configuration)
    {
        this.floor = floor;
        this.configuration = configuration;
        fameView = new Strip.FameView(floor);
        stage.Feed = floor;
        this.session = session;
        this.coins = coins;
        this.casino = casino;
        this.casinoPlay = casinoPlay;
        this.history = history;
        this.casinoRooms = casinoRooms;
        this.casinoTables = casinoTables;
        this.casinoSpin = casinoSpin;
        this.launcher = launcher;
        this.confirm = confirm;
        this.conduct = conduct;
        bonusShelf = new CashierBonusShelf(casino);
        cashierCashOut = new CashierCashOut(casino, confirm);
        cashier = new CashierDrawer(casino, coins, confirm, bonusShelf, cashierCashOut);
        preferences = new Core.Casino.CasinoPreferences(configuration);
        stage.Preferences = preferences;
        machines = new Machines.MachineCabinet(casino, casinoPlay, confirm, OpenCashier, preferences);
        scratch = new Cabinets.ScratchCabinet(casino, casinoPlay, OpenCashier);
        barkeep = new Cabinets.BarkeepCabinet(casino, casinoPlay, gameStats, OpenCashier);
        wheel = new Cabinets.WheelCabinet(casino, casinoRooms, OpenCashier, PopRoute);
        bingo = new Cabinets.BingoCabinet(casino, casinoRooms, OpenCashier, PopRoute);
        dailySpin = new Cabinets.DailySpinCabinet(casinoSpin);
        originals = new Originals.OriginalsCabinet(casino, casinoPlay.Originals, OpenCashier);
        race = new Race.RaceCabinet(casino, casinoRooms, OpenCashier, PopRoute);
        plinko = new Plinko.PlinkoCabinet(casino, casinoPlay.Plinko, OpenCashier, preferences);
        blackjack = new Tables.BlackjackTable(casino, casinoRooms, casinoTables, history, casinoTurns, remoteImages,
            lodestone, OpenCashier, PopRoute, OpenLedger);
        playerLedger = new Tables.TableLedger(casinoTables, confirm);
        openTable = OpenTable;
        openDoorFromRow = OpenDoor;
        this.casinoVenue = casinoVenue;
        venueRoom = new Venue.VenueCabinet(casinoVenue, PopRoute);
        broadcast = new Venue.BroadcastView(casinoRooms);
        venueSheet = new Venue.VenueTableSheet(casinoVenue, tradeSync, report, JoinCodeOf);
        tradePrompt = new Venue.TradeSyncPrompt(tradeSync);
        nearbyTables = new Venue.NearbyTablesCard(casinoVenue);
        openNearbyRow = row => OpenTable(row.TableId);
        this.holdemStore = holdemStore;
        holdem = new Tables.HoldemTable(casino, casinoRooms, casinoTables, holdemStore, casinoTurns, remoteImages,
            lodestone, OpenCashier, PopRoute);
        holdemPit = new Tables.HoldemPit(holdemStore, casino, openTable, openDoorFromRow, OpenHoldemHostSheet);
        browser = new Tables.TableBrowser(casinoTables, casino, openTable, openDoorFromRow, OpenHostSheet,
            nearbyTables);
        tableHands = new Tables.TableHandsCard(history);
        pit = new Tables.BlackjackPit(casinoTables, casino, openTable, openDoorFromRow, OpenTables, OpenHostSheet);
        hostSheet = new Tables.HostSheet(casinoTables, casino, new Venue.VenueHostOptions(casinoVenue));
        tableDoor = new Tables.TableDoor(casinoTables, confirm, openTable,
            new Venue.TournamentDoorCard(casinoVenue));
        router = new ViewRouter<CasinoRoute>(CasinoRoute.Floor);
        routes = new CasinoNavigation(router);
        drawView = DrawView;
        popRoute = PopRoute;
        openLimits = OpenLimits;
        refreshFloor = RefreshFloor;
    }

    public bool TryNameplateActivity(NameplateStatus status, out NameplateValues values)
    {
        values = NameplateValues.Empty;
        switch (status)
        {
            case NameplateStatus.Gamba:
                values = NameplateValues.Empty with { Game = Loc.T(PlayingName(router.Current)) };
                return true;
            case NameplateStatus.SlotsWin when machines.TryRecentResult(out var won) && won > 0:
                values = NameplateValues.Empty with { Chips = NameplateTitleService.ChipCount(won) };
                return true;
            case NameplateStatus.SlotsLoss when machines.TryRecentResult(out var lost) && lost < 0:
                values = NameplateValues.Empty with { Chips = NameplateTitleService.ChipCount(-lost) };
                return true;
            default:
                return false;
        }
    }

    public void OpenTab(string tab) => launcher.RequestGame(tab);

    public void OnOpened()
    {
        configuration.MarkFeaturePinSeen(Core.Changelog.NewFeaturePins.Casino);
        intro.Reset();
        routes.Reset();
        cashier.Close();
        bonusShelf.Reset();
        machines.Reset();
        scratch.Reset();
        barkeep.Reset();
        wheel.Reset();
        bingo.Reset();
        dailySpin.Reset();
        originals.Reset();
        race.Reset();
        plinko.Reset();
        blackjack.Reset();
        holdem.Reset();
        venueRoom.Reset();
        broadcast.Reset();
        tournament.Reset();
        venueSheet.Close();
        browser.Reset();
        tableDoor.Reset();
        pendingTableId = string.Empty;
        rulesSheet.Close();
        stage.ResetSession();
        ResetLimitsEditor();
        ResetLobby();
        jackpotRoll.Snap(casino.Jackpot);
        historyLoadFailed = false;
        ResetLaunch();
        clubSheet.Close();
        heroCards.Reset();
        shelves.Reset();
        winsTicker.Reset();
        RefreshFloor();
        floor.Watch();
        casinoPlay.RecoverPendingRound();
        ConsumeLaunch();
    }

    public void OnClosed()
    {
        router.Reset();
        cashier.Close();
        bonusShelf.Reset();
        machines.Reset();
        scratch.Reset();
        barkeep.Reset();
        wheel.Reset();
        bingo.Reset();
        dailySpin.Reset();
        originals.Reset();
        race.Reset();
        AppLandscape.Release(Id);
        plinko.Reset();
        blackjack.Reset();
        holdem.Reset();
        venueRoom.Reset();
        broadcast.Reset();
        tournament.Reset();
        venueSheet.Close();
        browser.Reset();
        tableDoor.Reset();
        pendingTableId = string.Empty;
        rulesSheet.Close();
        stage.ResetSession();
        ResetLimitsEditor();
        ResetLaunch();
        floor.Unwatch();
    }

    private void RefreshFloor()
    {
        lobbyHistoryFailed = false;
        history.Invalidate();
        coins.RefreshNow();
        casino.RefreshNow();
        casinoRooms.RefreshNow();
        casinoTables.RefreshNow();
        casinoSpin.RefreshNow();
        floor.RefreshMissionsNow();
    }

    private void ConsumeLaunch()
    {
        if (!launcher.TryConsume(out var launch))
        {
            return;
        }

        routes.Reset();
        if (launch.Kind == Core.Casino.CasinoLaunchKind.Table && launch.TableId.Length > 0)
        {
            OpenTable(launch.TableId);
            return;
        }

        if (launch.Kind == Core.Casino.CasinoLaunchKind.Game && launch.GameId.Length > 0)
        {
            OpenGame(launch.GameId);
        }
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;

        var scale = UiScale.Current;
        var screen = SceneChrome.ScreenFrom(context.Content, theme, scale);
        ui.Backdrop(screen);

        if (!session.IsSignedIn)
        {
            TourHolds.Hold(Id);
            DrawSignedOut(context);
            return;
        }

        if (casino.State is null)
        {
            TourHolds.Hold(Id);
        }
        else
        {
            TourHolds.Release(Id);
        }

        coins.EnsureFresh();
        casino.EnsureFresh();
        casinoRooms.EnsureFresh();
        casinoTables.EnsureFresh();
        casinoSpin.EnsureFresh();
        ConsumeTableAnswers();
        if (launcher.HasPending && !router.IsTransitioning)
        {
            ConsumeLaunch();
        }

        screenArea = context.Content;
        barkeep.Tick();
        bonusShelf.Update(MathF.Min(ImGui.GetIO().DeltaTime, Core.Animation.TransitionTiming.MaxFrameSeconds), scale);
        cashier.Gate();
        machines.Gate();
        scratch.Gate();
        wheel.Gate();
        bingo.Gate();
        originals.Gate();
        race.Gate();
        plinko.Gate();
        holdem.Gate();
        rulesSheet.Gate();
        venueRoom.Gate();
        venueSheet.Gate();
        tradePrompt.Gate();
        stage.Gate();
        SyncRaceLandscape();
        floor.EnsureFresh();
        ConsumeFloorNotes();
        ConsumeMissionNotes();
        clubSheet.Gate();
        var introShowing = IntroShowing;
        if (introShowing)
        {
            Strip.StripIntro.Gate();
            TourHolds.Hold(Id);
        }
        if (!DrawLaunchLayer(context.Content))
        {
            router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        }

        machines.DrawOverlay(screenArea, ui);
        scratch.DrawOverlay(screenArea, ui);
        wheel.DrawOverlay(screenArea, ui);
        bingo.DrawOverlay(screenArea, ui);
        originals.DrawOverlay(screenArea, ui);
        race.DrawOverlay(screenArea, ui);
        plinko.DrawOverlay(screenArea, ui);
        holdem.DrawOverlay(screenArea, ui);
        if (IsStage(router.Current))
        {
            stage.DrawOverlays(screenArea, ui);
            HandleStageRequests();
        }

        venueRoom.DrawOverlay(screenArea, ui);
        venueSheet.Draw(screenArea, ui);
        HandleVenueSheet();
        rulesSheet.Draw(screenArea, ui);
        clubSheet.Draw(screenArea, ui);
        cashier.Draw(screenArea, ui, openLimits);
        tradePrompt.Draw(screenArea, ui);
        if (introShowing && intro.Draw(screenArea, ui, casino.Rate, casino.Cashier?.DailyNetCashOutCoins is > 0
                ? casino.Cashier.DailyNetCashOutCoins
                : Core.Casino.CasinoCashier.DailyNetCashOutCoinsFallback,
                MathF.Min(ImGui.GetIO().DeltaTime, Core.Animation.TransitionTiming.MaxFrameSeconds))
            == Strip.StripIntroResult.Finished)
        {
            preferences.MarkIntroSeen();
        }
        if (rulesSheet.TakePlayRequest() && !PlayingGame(rulesSheet.GameId))
        {
            OpenGame(rulesSheet.GameId);
        }
    }

    private bool IntroShowing => casino.State is not null && !preferences.IntroSeen && routes.OnRoot
                                 && routes.Tab == CasinoTab.Floor && !router.IsTransitioning;

    private bool PlayingGame(string gameId)
    {
        var current = router.Current;
        return IsStage(current) && string.Equals(StageSpecFor(current).GameId, gameId, StringComparison.Ordinal);
    }

    private void DrawSignedOut(in PhoneContext context)
    {
        ui.Body(context.Content);
        var navBar = AppHeader.BeginLargeTitle(context, false);
        Coin.CoinArt.StateScreen(ImGui.GetWindowDrawList(), ui, navBar.Body, FontAwesomeIcon.UserLock,
            Loc.T(L.Casino.SignInTitle), Loc.T(L.Casino.SignInHint), string.Empty, 0, UiScale.Current);
        AppHeader.EndLargeTitle(in navBar, context, "casino.signedout.nav", DisplayName, NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty);
    }

    private void DrawView(CasinoRoute route, Rect area, int depth)
    {
        if (IsStage(route))
        {
            DrawStage(route, area);
            return;
        }

        ui.Body(area);
        var context = new PhoneContext(area, theme, navigation);
        if (route.Screen == CasinoScreen.Floor)
        {
            DrawRoot(context, area);
            return;
        }

        DrawPushedPage(context, route, depth);
    }

    private static bool IsStage(CasinoRoute route) =>
        route.Screen is CasinoScreen.Cabinet or CasinoScreen.Table or CasinoScreen.DailySpin
            or CasinoScreen.VenueRoom or CasinoScreen.Broadcast;

    private void DrawStage(CasinoRoute route, Rect area)
    {
        var scale = UiScale.Current;
        var spec = StageSpecFor(route);
        using (AppSurface.BeginEdgeToEdge(area, true))
        {
            ImGui.Dummy(new Vector2(area.Width, MathF.Max(1f, area.Height - StageSurfaceSlack * scale)));
            var frame = stage.Begin(area, spec, StageBalance(route), casino.Ceiling);
            DrawStageWorld(route, frame);
            var action = stage.End(ui);
            if (action == CasinoStageAction.Back)
            {
                PopRoute();
            }
            else if (action == CasinoStageAction.Cashier)
            {
                OpenCashier();
            }
        }
    }

    private bool RouteOpen(CasinoRoute route)
    {
        var features = casino.Features;
        return route.Screen switch
        {
            CasinoScreen.Cabinet => CasinoGameGate.IsOpen(features, route.GameId),
            CasinoScreen.Table when IsHoldem(route) => CasinoGameGate.IsOpen(features, CasinoGames.Holdem),
            CasinoScreen.VenueRoom => CasinoGameGate.IsOpen(features, route.GameId),
            _ => true,
        };
    }

    private void DrawNotOpen(in CasinoStageFrame frame)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var safe = frame.Layout.Safe;
        var width = MathF.Min(safe.Width, NotOpenCardWidth * scale);
        var title = Loc.T(L.Strip.NotOpenYet);
        var body = Loc.T(L.Strip.NotOpenHint);
        var height = CasinoNotice.Height(CasinoNoticeKind.Card, title, body, width, scale);
        CasinoNotice.Draw(drawList, ui, CasinoNoticeKind.Card, title, body, safe.Center.X - width * 0.5f,
            safe.Center.Y - height * 0.5f, width, scale);
        if (stage.PrimaryAction(Loc.T(L.Strip.BackToFloor), true, ui.Ink))
        {
            PopRoute();
        }
    }

    private void DrawStageWorld(CasinoRoute route, in CasinoStageFrame frame)
    {
        var body = frame.Body;
        if (!RouteOpen(route))
        {
            DrawNotOpen(frame);
            return;
        }

        switch (route.Screen)
        {
            case CasinoScreen.Cabinet when Machines.MachineCabinet.Owns(route.GameId):
                machines.Draw(stage, frame, ui);
                break;
            case CasinoScreen.Cabinet when string.Equals(route.GameId, CasinoGames.Scratch, StringComparison.Ordinal):
                scratch.Draw(stage, frame, ui);
                break;
            case CasinoScreen.Cabinet when string.Equals(route.GameId, CasinoGames.Barkeep, StringComparison.Ordinal):
                barkeep.Draw(stage, frame, ui);
                break;
            case CasinoScreen.Cabinet when string.Equals(route.GameId, CasinoGames.Wheel, StringComparison.Ordinal):
                wheel.Draw(stage, frame, ui);
                break;
            case CasinoScreen.Cabinet when string.Equals(route.GameId, CasinoGames.Bingo, StringComparison.Ordinal):
                bingo.Draw(stage, frame, ui);
                break;
            case CasinoScreen.Cabinet when Originals.OriginalsCabinet.Owns(route.GameId):
                originals.Draw(stage, frame, ui);
                break;
            case CasinoScreen.Cabinet when string.Equals(route.GameId, CasinoGames.Race, StringComparison.Ordinal):
                race.Draw(stage, frame, ui, RaceLandscape());
                break;
            case CasinoScreen.Cabinet when string.Equals(route.GameId, CasinoGames.Plinko, StringComparison.Ordinal):
                plinko.Draw(stage, frame, ui);
                break;
            case CasinoScreen.Table when IsHoldem(route):
                holdem.Draw(stage, frame, ui);
                break;
            case CasinoScreen.Table:
                blackjack.Draw(stage, frame, ui);
                tournament.Draw(stage, frame, casinoRooms.Room.State?.Blackjack, casinoVenue.AccountId);
                break;
            case CasinoScreen.VenueRoom:
                venueRoom.Draw(stage, frame, ui);
                break;
            case CasinoScreen.Broadcast:
                broadcast.Draw(frame, ui);
                break;
            case CasinoScreen.DailySpin:
                dailySpin.Draw(stage, frame, ui);
                break;
            default:
                EmptyState.Draw(body, ui, FontAwesomeIcon.Hammer, Loc.T(L.Casino.CabinetSoonTitle),
                    Loc.T(L.Casino.CabinetSoonHint));
                break;
        }
    }

    private static bool IsHoldem(CasinoRoute route) =>
        string.Equals(route.GameId, CasinoGames.Holdem, StringComparison.Ordinal);

    private CasinoStageSpec StageSpecFor(CasinoRoute route)
    {
        if (route.Screen == CasinoScreen.Table && IsHoldem(route))
        {
            return new CasinoStageSpec(CasinoGames.Holdem, L.Casino.GameHoldem, Backdrop.Strip, Room: true,
                DeckHeight: holdem.DeckHeight, Practice: holdem.Practice, LampPool: 1f, Extra: L.Venue.TableSheet);
        }

        if (route.Screen == CasinoScreen.Table)
        {
            return new CasinoStageSpec(CasinoGames.Blackjack, L.Casino.GameBlackjack, Backdrop.Felt,
                DeckHeight: blackjack.DeckHeight,
                Practice: blackjack.Currency == Core.Casino.CasinoCurrencies.Practice,
                ReturnTenths: Core.Casino.BlackjackRules.ReturnTenths, Extra: L.Venue.TableSheet);
        }

        if (route.Screen == CasinoScreen.VenueRoom)
        {
            return venueRoom.Spec();
        }

        if (route.Screen == CasinoScreen.Broadcast)
        {
            return broadcast.Spec();
        }

        if (route.Screen == CasinoScreen.DailySpin)
        {
            return new CasinoStageSpec(CasinoGames.DailySpin, L.Casino.GameDailySpin, Backdrop.Strip);
        }

        return route.GameId switch
        {
            CasinoGames.Scratch => new CasinoStageSpec(route.GameId, L.Casino.GameScratch, Backdrop.Strip,
                DeckHeight: Cabinets.ScratchCabinet.DeckHeight, BetsRail: true, InstantAvailable: true,
                ReturnTenths: Core.Casino.ScratchRules.ReturnTenths(scratch.CurrentTier), Extra: L.Casino.ScratchOdds),
            CasinoGames.Barkeep => new CasinoStageSpec(route.GameId, L.Casino.GameBarkeep, Backdrop.Strip,
                DeckHeight: Cabinets.BarkeepCabinet.DeckHeight, Practice: barkeep.Practicing, BetsRail: true,
                ReturnTenths: Cabinets.BarkeepCabinet.ReturnTenths, Warmth: 1f),
            CasinoGames.Bingo => new CasinoStageSpec(route.GameId, L.Casino.GameBingo, Backdrop.Arena, Room: true,
                DeckHeight: Cabinets.BingoCabinet.DeckHeight, ReturnTenths: Core.Casino.BingoRules.ReturnTenths,
                Extra: bingo.DaubLabel),
            CasinoGames.Wheel => new CasinoStageSpec(route.GameId, L.Casino.GameWheel, Backdrop.Strip, Room: true,
                DeckHeight: Cabinets.WheelCabinet.DeckHeight),
            _ when Originals.OriginalsCabinet.Owns(route.GameId) => originals.SpecFor(route.GameId),
            CasinoGames.Race => race.Spec(RaceLandscape()),
            CasinoGames.Plinko => plinko.Spec,
            _ when Machines.MachineCabinet.Owns(route.GameId) => machines.SpecFor(route.GameId),
            _ => new CasinoStageSpec(route.GameId, GameName(route.GameId), Backdrop.Strip),
        };
    }

    private long StageBalance(CasinoRoute route)
    {
        var state = casino.State;
        if (route.Screen == CasinoScreen.Table && IsHoldem(route) && holdem.HeroStack >= 0)
        {
            return holdem.HeroStack;
        }

        if (route.Screen == CasinoScreen.Table && !IsHoldem(route) && state?.TableSitting is { } rack
            && !Core.Casino.CasinoCurrencies.SeatBanked(blackjack.Currency))
        {
            return rack.Stack;
        }

        if (route.Screen == CasinoScreen.Cabinet
            && string.Equals(route.GameId, CasinoGames.Plinko, StringComparison.Ordinal))
        {
            return plinko.DisplayStack();
        }

        var stack = state?.Sitting?.Stack ?? 0;
        return route.Screen == CasinoScreen.Cabinet && Machines.MachineCabinet.Owns(route.GameId)
            ? machines.DisplayStack(stack)
            : stack;
    }

    private void HandleStageRequests()
    {
        var round = stage.TakeRoundRequest();
        if (round.Length > 0)
        {
            history.Invalidate();
            router.Push(new CasinoRoute(CasinoScreen.RoundDetail, router.Current.GameId, round));
        }

        var current = router.Current;
        switch (stage.TakeInfoRequest())
        {
            case CasinoInfoRequest.Rules:
                rulesSheet.Open(StageSpecFor(current).GameId);
                break;
            case CasinoInfoRequest.Extra when Machines.MachineCabinet.Owns(current.GameId):
                machines.OpenPayTable();
                break;
            case CasinoInfoRequest.Extra when string.Equals(current.GameId, CasinoGames.Scratch,
                StringComparison.Ordinal):
                scratch.OpenOdds();
                break;
            case CasinoInfoRequest.Extra when string.Equals(current.GameId, CasinoGames.Bingo,
                StringComparison.Ordinal):
                bingo.ToggleDaub();
                break;
            case CasinoInfoRequest.Extra when current.Screen is CasinoScreen.Table or CasinoScreen.VenueRoom:
                OpenVenueSheet(current);
                break;
            case CasinoInfoRequest.Fairness:
                OpenFairness();
                break;
        }
    }

    private void DrawPushedPage(in PhoneContext context, CasinoRoute route, int depth)
    {
        var navBar = AppHeader.BeginLargeTitle(context);
        switch (route.Screen)
        {
            case CasinoScreen.Tables when IsHoldem(route):
                holdemPit.Draw(navBar.Body, ui);
                break;
            case CasinoScreen.Tables:
                browser.Draw(navBar.Body, ui);
                break;
            case CasinoScreen.Pit:
                pit.Draw(navBar.Body, ui);
                break;
            case CasinoScreen.TableDoor:
                tableDoor.Draw(navBar.Body, ui);
                break;
            case CasinoScreen.HostTable:
                hostSheet.Draw(navBar.Body, ui);
                break;
            case CasinoScreen.TableLedger:
                DrawPlayerLedger(navBar.Body, route.TableId);
                break;
            case CasinoScreen.Limits:
                DrawLimits(navBar.Body);
                break;
            case CasinoScreen.History:
                DrawHistory(navBar.Body);
                break;
            case CasinoScreen.Fairness:
                DrawFairness(navBar.Body);
                break;
            case CasinoScreen.RoundDetail:
                DrawRoundDetail(navBar.Body, route.RoundId);
                break;
            case CasinoScreen.Fame:
                fameView.Draw(navBar.Body, ui);
                break;
        }

        AppHeader.EndLargeTitle(in navBar, context, "casino.page.nav", RouteTitle(route), NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty, BackTitle(depth), popRoute);
    }

    private void DrawRoot(in PhoneContext context, Rect area)
    {
        var scale = UiScale.Current;
        using (TabBar.ReserveContent(scale))
        {
            var navBar = AppHeader.BeginLargeTitle(context, false);
            switch (routes.Tab)
            {
                case CasinoTab.Tables:
                    browser.Draw(navBar.Body, ui);
                    break;
                case CasinoTab.Live:
                    DrawLiveTab(navBar.Body);
                    break;
                case CasinoTab.Cashier:
                    DrawCashierTab(navBar.Body);
                    break;
                default:
                    DrawLobbyTab(navBar.Body);
                    break;
            }

            navButtons[RulesButton] = new NavBarButton(PhoneIcons.ShieldCheck, Loc.T(L.Conduct.Eyebrow));
            var pressed = AppHeader.EndLargeTitle(in navBar, context, "casino.root.nav", TabTitle(routes.Tab),
                NavBarStyle.From(ui), navButtons);
            if (pressed == RulesButton)
            {
                conduct.ShowRules(Id);
            }
        }

        DrawFloorTabBar(area);
    }

    private string TabTitle(CasinoTab target) => target switch
    {
        CasinoTab.Tables => Loc.T(L.Casino.TablesTitle),
        CasinoTab.Live => Loc.T(L.Casino.TabLive),
        CasinoTab.Cashier => Loc.T(L.Casino.Cashier),
        _ => DisplayName,
    };

    private string RouteTitle(CasinoRoute route) => route.Screen switch
    {
        CasinoScreen.Tables or CasinoScreen.Table when IsHoldem(route) => Loc.T(L.Casino.GameHoldem),
        CasinoScreen.Tables => Loc.T(L.Casino.TablesTitle),
        CasinoScreen.Pit => Loc.T(L.Blackjack.PitTitle),
        CasinoScreen.TableDoor => Loc.T(L.Casino.DoorTitle),
        CasinoScreen.HostTable => Loc.T(L.Tables.HostTitle),
        CasinoScreen.TableLedger => Loc.T(L.Tables.LedgerHeading),
        CasinoScreen.Table => Loc.T(L.Casino.GameBlackjack),
        CasinoScreen.VenueRoom => Loc.T(Venue.VenueCabinet.NameOf(venueRoom.Kind)),
        CasinoScreen.Broadcast => Loc.T(L.Venue.BroadcastTitle),
        CasinoScreen.Limits => Loc.T(L.Casino.LimitsRow),
        CasinoScreen.History => Loc.T(L.Casino.HistoryRow),
        CasinoScreen.Fairness => Loc.T(L.Casino.FairnessRow),
        CasinoScreen.RoundDetail => Loc.T(L.Casino.RoundDetailTitle),
        CasinoScreen.Fame => Loc.T(L.Club.FameTitle),
        CasinoScreen.DailySpin => Loc.T(L.Casino.GameDailySpin),
        CasinoScreen.Cabinet => Loc.T(GameName(route.GameId)),
        _ => TabTitle(routes.Tab),
    };

    private string BackTitle(int depth)
    {
        return router.TryGetView(depth - 2, out var previous) ? RouteTitle(previous) : TabTitle(routes.Tab);
    }

    private void OpenCashier()
    {
        cashier.Open();
    }

    private void OpenLimits()
    {
        if (router.Current.Screen != CasinoScreen.Limits)
        {
            limitsSeeded = false;
            router.Push(new CasinoRoute(CasinoScreen.Limits));
        }
    }

    private void OpenHistory()
    {
        historyLoadFailed = false;
        history.Invalidate();
        router.Push(new CasinoRoute(CasinoScreen.History));
    }

    private void OpenFairness()
    {
        router.Push(new CasinoRoute(CasinoScreen.Fairness));
    }

    private void PopRoute()
    {
        if (TryDismissLaunch())
        {
            return;
        }

        PopNow(true);
    }

    private void PopNow(bool animate)
    {
        machines.ClosePayTable();
        scratch.CloseOdds();
        var leaving = router.Current;
        if (IsStage(leaving))
        {
            stage.Reset();
            floor.RefreshMissionsNow();
        }

        ResetCabinetOf(leaving);
        router.Pop(animate);
    }

    private void ResetCabinetOf(CasinoRoute route)
    {
        if (route.Screen == CasinoScreen.DailySpin)
        {
            dailySpin.Reset();
            return;
        }

        if (route.Screen == CasinoScreen.Table && IsHoldem(route))
        {
            holdem.Exit();
            return;
        }

        if (route.Screen == CasinoScreen.Table)
        {
            blackjack.Exit();
            tournament.Reset();
            return;
        }

        if (route.Screen == CasinoScreen.VenueRoom)
        {
            venueRoom.Reset();
            return;
        }

        if (route.Screen == CasinoScreen.Broadcast)
        {
            broadcast.Reset();
            return;
        }

        if (route.Screen == CasinoScreen.TableLedger)
        {
            playerLedger.Reset();
            return;
        }

        if (route.Screen == CasinoScreen.TableDoor)
        {
            tableDoor.Reset();
            return;
        }

        if (route.Screen != CasinoScreen.Cabinet)
        {
            return;
        }

        if (string.Equals(route.GameId, CasinoGames.Wheel, StringComparison.Ordinal))
        {
            wheel.Reset();
            return;
        }

        if (string.Equals(route.GameId, CasinoGames.Bingo, StringComparison.Ordinal))
        {
            bingo.Reset();
            return;
        }

        if (Originals.OriginalsCabinet.Owns(route.GameId))
        {
            originals.Reset();
            return;
        }

        if (Machines.MachineCabinet.Owns(route.GameId))
        {
            machines.Reset();
            return;
        }

        if (string.Equals(route.GameId, CasinoGames.Plinko, StringComparison.Ordinal))
        {
            plinko.Reset();
            return;
        }

        if (string.Equals(route.GameId, CasinoGames.Race, StringComparison.Ordinal))
        {
            race.Reset();
        }
    }

    private bool RaceLandscape() => AppLandscape.Held(Id) && screenArea.IsLandscape();

    private void SyncRaceLandscape()
    {
        var current = router.Current;
        if (current.Screen == CasinoScreen.Cabinet && race.WantsLandscape
            && string.Equals(current.GameId, CasinoGames.Race, StringComparison.Ordinal))
        {
            AppLandscape.Request(Id);
            return;
        }

        AppLandscape.Release(Id);
    }

    private void OpenTables()
    {
        if (routes.OnRoot)
        {
            SelectTab(CasinoTab.Tables);
            return;
        }

        browser.Enter();
        routes.Push(new CasinoRoute(CasinoScreen.Tables, CasinoGames.Blackjack));
    }

    private void SelectTab(CasinoTab next)
    {
        if (next == routes.Tab && routes.OnRoot)
        {
            return;
        }

        if (!routes.OnRoot)
        {
            stage.Reset();
            ResetCabinetOf(router.Current);
        }

        if (next == CasinoTab.Tables)
        {
            browser.Enter();
        }

        routes.Select(next);
    }

    private void LeaveClosedTable()
    {
        var closed = casinoTables.TakeClosedTable();
        if (closed.Length == 0)
        {
            return;
        }

        while (routes.Holds(closed))
        {
            PopNow(true);
        }
    }

    private void ToastQuickSeatRefusal()
    {
        var reason = casinoTables.TakeQuickSeatRefusal();
        if (reason.Length > 0)
        {
            ShellToast.Show(Loc.T(Core.Casino.CasinoReasons.MessageFor(reason)));
        }
    }

    private void OpenPit()
    {
        if (router.Current.Screen == CasinoScreen.Pit)
        {
            return;
        }

        pit.Enter();
        router.Push(new CasinoRoute(CasinoScreen.Pit, CasinoGames.Blackjack));
    }

    private void OpenLedger(string tableId)
    {
        if (tableId.Length == 0 || router.Current.Screen == CasinoScreen.TableLedger)
        {
            return;
        }

        playerLedger.Enter(tableId);
        casinoTables.RefreshCard(tableId);
        router.Push(new CasinoRoute(CasinoScreen.TableLedger, CasinoGames.Blackjack, string.Empty, tableId));
    }

    private void DrawPlayerLedger(Rect body, string tableId)
    {
        var scale = UiScale.Current;
        using (AppSurface.Begin(body))
        {
            playerLedger.Draw(ui, casinoTables.AccountId, casinoTables.CardFor(tableId)?.OwnerUserId ?? string.Empty,
                scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private void OpenHostSheet()
    {
        if (router.Current.Screen == CasinoScreen.HostTable)
        {
            return;
        }

        hostSheet.Enter();
        router.Push(new CasinoRoute(CasinoScreen.HostTable, CasinoGames.Blackjack));
    }

    private void OpenVenueHostSheet(Core.Casino.VenueRoomKind kind)
    {
        if (router.Current.Screen == CasinoScreen.HostTable)
        {
            return;
        }

        hostSheet.EnterVenue(kind);
        routes.Push(new CasinoRoute(CasinoScreen.HostTable, Venue.VenueCabinet.GameIdOf(kind)));
    }

    private void OpenHoldemHostSheet()
    {
        if (router.Current.Screen == CasinoScreen.HostTable)
        {
            return;
        }

        hostSheet.Enter(Core.Casino.HoldemRules.Kind);
        router.Push(new CasinoRoute(CasinoScreen.HostTable, CasinoGames.Holdem));
    }

    private void OpenHoldemPit()
    {
        if (router.Current.Screen == CasinoScreen.Tables && IsHoldem(router.Current))
        {
            return;
        }

        holdemPit.Enter();
        router.Push(new CasinoRoute(CasinoScreen.Tables, CasinoGames.Holdem));
    }

    private bool IsHoldemTable(string tableId)
    {
        if (Core.Casino.HoldemRules.IsHouseRoom(tableId))
        {
            return true;
        }

        var card = casinoTables.CardFor(tableId);
        if (card is not null)
        {
            return string.Equals(card.GameKind, Core.Casino.HoldemRules.Kind, StringComparison.Ordinal);
        }

        return KindIn(holdemStore.Tables, tableId) || KindIn(casinoTables.Listed, tableId);
    }

    private static bool KindIn(Core.Aethernet.Contracts.CasinoTableRowDto[] rows, string tableId)
    {
        for (var index = 0; index < rows.Length; index++)
        {
            if (string.Equals(rows[index].TableId, tableId, StringComparison.Ordinal))
            {
                return string.Equals(rows[index].GameKind, Core.Casino.HoldemRules.Kind, StringComparison.Ordinal);
            }
        }

        return false;
    }

    private void OpenHoldemTable(string tableId)
    {
        var current = router.Current;
        if (current.Screen == CasinoScreen.Table && IsHoldem(current)
            && string.Equals(current.TableId, tableId, StringComparison.Ordinal))
        {
            return;
        }

        holdem.Enter(tableId);
        router.Push(new CasinoRoute(CasinoScreen.Table, CasinoGames.Holdem, string.Empty, tableId));
    }

    private void OpenTable(string tableId)
    {
        if (tableId.Length == 0)
        {
            return;
        }

        if (IsHoldemTable(tableId))
        {
            OpenHoldemTable(tableId);
            return;
        }

        var current = router.Current;
        if (current.Screen == CasinoScreen.Table
            && string.Equals(current.TableId, tableId, StringComparison.Ordinal))
        {
            return;
        }

        var roomKind = Core.Casino.VenueKinds.Of(casinoTables.CardFor(tableId)?.GameKind ?? string.Empty);
        if (roomKind != Core.Casino.VenueRoomKind.None)
        {
            OpenVenueRoom(tableId, roomKind);
            return;
        }

        blackjack.Enter(tableId);
        router.Push(new CasinoRoute(CasinoScreen.Table, CasinoGames.Blackjack, string.Empty, tableId));
    }

    private void OpenVenueRoom(string tableId, Core.Casino.VenueRoomKind roomKind)
    {
        var current = router.Current;
        if (current.Screen == CasinoScreen.VenueRoom
            && string.Equals(current.TableId, tableId, StringComparison.Ordinal))
        {
            return;
        }

        venueRoom.Enter(tableId, roomKind);
        router.Push(new CasinoRoute(CasinoScreen.VenueRoom, Venue.VenueCabinet.GameIdOf(roomKind), string.Empty,
            tableId));
    }

    private void OpenVenueSheet(CasinoRoute current)
    {
        var tableId = current.TableId;
        var card = casinoTables.CardFor(tableId);
        var holdemTable = current.Screen == CasinoScreen.Table && IsHoldem(current);
        var gil = current.Screen == CasinoScreen.VenueRoom
            ? venueRoom.Currency == Core.Casino.CasinoCurrencies.Gil
            : !holdemTable && blackjack.Currency == Core.Casino.CasinoCurrencies.Gil;
        var spectating = holdemTable ? holdem.HeroStack < 0 : !SeatedAtTable(tableId);
        var canBroadcast = current.Screen == CasinoScreen.Table && spectating
            && (card?.Config?.Spectators ?? true);
        venueSheet.Open(new Venue.VenueSheetTarget(tableId, gil, canBroadcast));
    }

    private bool SeatedAtTable(string tableId)
    {
        var board = casinoRooms.Room.State?.Blackjack;
        var me = casinoVenue.AccountId;
        if (board?.Seats is null || me.Length == 0
            || !string.Equals(casinoRooms.Room.RoomId, tableId, StringComparison.Ordinal))
        {
            return false;
        }

        for (var index = 0; index < board.Seats.Length; index++)
        {
            if (string.Equals(board.Seats[index].UserId, me, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void HandleVenueSheet()
    {
        if (venueSheet.TakeRequest() != Venue.VenueSheetRequest.Broadcast)
        {
            return;
        }

        var current = router.Current;
        if (current.Screen == CasinoScreen.Table && current.TableId.Length > 0)
        {
            OpenBroadcast(current.TableId,
                IsHoldem(current) ? Venue.BroadcastGame.Holdem : Venue.BroadcastGame.Blackjack);
        }
    }

    private float DrawNearbyTables(ImDrawListPtr drawList, float top, float left, float width, float scale)
    {
        var bottom = nearbyTables.Draw(drawList, ui, new Vector2(left, top), width, openNearbyRow);
        return bottom > top ? bottom + CardGap * scale : top;
    }

    internal void OpenBroadcast(string tableId, Venue.BroadcastGame game)
    {
        if (tableId.Length == 0 || router.Current.Screen == CasinoScreen.Broadcast)
        {
            return;
        }

        broadcast.Enter(tableId, game);
        router.Push(new CasinoRoute(CasinoScreen.Broadcast,
            game == Venue.BroadcastGame.Holdem ? CasinoGames.Holdem : CasinoGames.Blackjack, string.Empty, tableId));
    }

    private void OpenDoor(string tableId)
    {
        OpenDoor(tableId, string.Empty);
    }

    private void OpenDoor(string tableId, string inviteToken)
    {
        if (tableId.Length == 0)
        {
            return;
        }

        tableDoor.Enter(tableId, inviteToken);
        if (router.Current.Screen == CasinoScreen.TableDoor)
        {
            return;
        }

        router.Push(new CasinoRoute(CasinoScreen.TableDoor, CasinoGames.Blackjack, string.Empty, tableId));
    }

    private void ConsumeTableAnswers()
    {
        LeaveClosedTable();
        ToastQuickSeatRefusal();
        var quick = casinoTables.TakeQuickSeat();
        if (quick is not null)
        {
            pendingTableId = quick.RoomId;

            if (!SeatedAt(Core.Casino.CasinoWire.BlackjackKind) && !casino.HasChips)
            {
                cashier.Open(quick.SuggestedBuyIn);
            }
        }

        var hosted = casinoTables.TakeHostedTable();
        if (hosted is not null)
        {
            if (router.Current.Screen == CasinoScreen.HostTable)
            {
                router.Pop();
            }

            OpenDoor(hosted.TableId, hosted.InviteToken);
        }

        var resolved = casinoTables.TakeResolvedTable();
        if (resolved is not null)
        {
            if (casinoTables.Owns(resolved))
            {
                OpenDoor(resolved.TableId, resolved.InviteToken);
            }
            else if (string.Equals(resolved.GameKind, Core.Casino.HoldemRules.Kind, StringComparison.Ordinal))
            {
                OpenHoldemTable(resolved.TableId);
            }
            else
            {
                OpenTable(resolved.TableId);
            }
        }

        if (pendingTableId.Length == 0
            || (!casino.HasChips && !SeatedAt(Core.Casino.CasinoWire.BlackjackKind)))
        {
            return;
        }

        var target = pendingTableId;
        pendingTableId = string.Empty;
        cashier.Close();
        OpenTable(target);
    }

    private void OpenDailySpin(Rect source)
    {
        if (router.Current.Screen == CasinoScreen.DailySpin)
        {
            return;
        }

        dailySpin.Enter();
        PushStage(new CasinoRoute(CasinoScreen.DailySpin), source);
    }

    internal void OpenGame(string gameId) => OpenGame(gameId, default);

    private void OpenGame(string gameId, Rect source)
    {
        if (string.Equals(gameId, CasinoGames.DailySpin, StringComparison.Ordinal))
        {
            OpenDailySpin(source);
            return;
        }

        if (!CasinoGameGate.IsOpen(casino.Features, gameId))
        {
            PushStage(new CasinoRoute(CasinoScreen.Cabinet, gameId), source);
            return;
        }

        if (string.Equals(gameId, CasinoGames.Blackjack, StringComparison.Ordinal))
        {
            OpenPit();
            return;
        }

        if (string.Equals(gameId, CasinoGames.Holdem, StringComparison.Ordinal))
        {
            OpenHoldemPit();
            return;
        }

        if (!casino.HasChips && !string.Equals(gameId, CasinoGames.Barkeep, StringComparison.Ordinal))
        {
            cashier.Open();
            return;
        }

        if (Machines.MachineCabinet.Owns(gameId))
        {
            machines.Enter(gameId);
        }
        else if (string.Equals(gameId, CasinoGames.Scratch, StringComparison.Ordinal))
        {
            scratch.Enter();
        }
        else if (string.Equals(gameId, CasinoGames.Barkeep, StringComparison.Ordinal))
        {
            barkeep.Enter();
        }
        else if (string.Equals(gameId, CasinoGames.Wheel, StringComparison.Ordinal))
        {
            wheel.Enter();
        }
        else if (string.Equals(gameId, CasinoGames.Bingo, StringComparison.Ordinal))
        {
            bingo.Enter();
        }
        else if (Originals.OriginalsCabinet.Owns(gameId))
        {
            originals.Enter(gameId);
        }
        else if (string.Equals(gameId, CasinoGames.Race, StringComparison.Ordinal))
        {
            race.Enter();
        }
        else if (string.Equals(gameId, CasinoGames.Plinko, StringComparison.Ordinal))
        {
            plinko.Enter();
        }

        PushStage(new CasinoRoute(CasinoScreen.Cabinet, gameId), source);
    }

    private void PushStage(CasinoRoute route, Rect source)
    {
        var morph = source.Width > 0f && source.Height > 0f && !router.IsTransitioning;
        router.Push(route, !morph);
        if (morph)
        {
            BeginLaunch(source);
        }
    }

    private bool SeatedAt(string wireKind)
    {
        return Core.Casino.CasinoWire.SittingFor(casino.State, wireKind) is not null;
    }

    private static LocString PlayingName(CasinoRoute route) => route.Screen switch
    {
        CasinoScreen.Cabinet => GameName(route.GameId),
        CasinoScreen.Table when string.Equals(route.GameId, CasinoGames.Holdem, StringComparison.Ordinal) =>
            L.Casino.GameHoldem,
        CasinoScreen.Table or CasinoScreen.TableDoor or CasinoScreen.Pit or CasinoScreen.Broadcast =>
            L.Casino.GameBlackjack,
        CasinoScreen.VenueRoom => L.Venue.VenueRooms,
        CasinoScreen.DailySpin => L.Casino.GameDailySpin,
        _ => L.Apps.Casino,
    };

    private static LocString GameName(string gameId) => CasinoGameNames.Of(gameId);

    public void Dispose()
    {
    }
}
