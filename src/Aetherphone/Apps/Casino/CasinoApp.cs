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

internal sealed partial class CasinoApp : IPhoneApp, INameplateActivitySource
{
    private const int RulesButton = 0;
    private const float StageSurfaceSlack = 16f;

    public string Id => "casino";
    public string DisplayName => Loc.T(L.Apps.Casino);
    public string Glyph => "Sa";
    public int BadgeCount => 0;

    private readonly AethernetSession session;
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
    private readonly Cabinets.SlotsCabinet slots;
    private readonly Cabinets.ScratchCabinet scratch;
    private readonly Cabinets.BarkeepCabinet barkeep;
    private readonly Cabinets.WheelCabinet wheel;
    private readonly Cabinets.BingoCabinet bingo;
    private readonly Cabinets.DailySpinCabinet dailySpin;
    private readonly Originals.OriginalsCabinet originals;
    private readonly Tables.BlackjackTable blackjack;
    private readonly Tables.TableBrowser browser;
    private readonly Tables.TableDoor tableDoor;
    private readonly CasinoStage stage = new();
    private readonly JackpotRail jackpotRail = new();
    private readonly GameRulesSheet rulesSheet = new();
    private readonly TabBar bottomNav = new();
    private readonly TabItem[] navTabs = new TabItem[4];
    private readonly NavBarButton[] navButtons = new NavBarButton[1];
    private readonly PullToRefresh lobbyRefresh = new();
    private readonly CasinoTextCache texts = new();
    private readonly AppSkin ui = new(AppPalettes.Gamba);
    private readonly ViewRouter<CasinoRoute> router;
    private readonly RouterDraw<CasinoRoute> drawView;
    private readonly Action popRoute;
    private readonly Action openLimits;
    private readonly Action refreshFloor;
    private readonly Action<string> openTable;
    private readonly Action<string> openDoorFromRow;

    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private Rect screenArea;
    private CasinoTab tab;
    private string pendingTableId = string.Empty;
    private bool historyLoadFailed;

    public CasinoApp(AethernetSession session, CoinStore coins, Core.Casino.CasinoStore casino,
        Core.Casino.CasinoPlayStore casinoPlay, Core.Casino.CasinoHistoryStore history,
        Core.Casino.CasinoRoomsStore casinoRooms, Core.Casino.CasinoTablesStore casinoTables,
        Core.Casino.CasinoSpinStore casinoSpin, Core.Casino.CasinoTurnNotifier casinoTurns,
        Core.Casino.CasinoLauncher launcher, Core.Games.GameStatsStore gameStats, ConfirmService confirm,
        ConductGateService conduct, Core.Media.RemoteImageCache remoteImages,
        Core.Lodestone.LodestoneService lodestone)
    {
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
        cashier = new CashierDrawer(casino, coins, confirm);
        slots = new Cabinets.SlotsCabinet(casino, casinoPlay, OpenCashier);
        scratch = new Cabinets.ScratchCabinet(casino, casinoPlay, OpenCashier);
        barkeep = new Cabinets.BarkeepCabinet(casino, casinoPlay, gameStats, OpenCashier);
        wheel = new Cabinets.WheelCabinet(casino, casinoRooms, OpenCashier, PopRoute);
        bingo = new Cabinets.BingoCabinet(casino, casinoRooms, OpenCashier, PopRoute);
        dailySpin = new Cabinets.DailySpinCabinet(casinoSpin);
        originals = new Originals.OriginalsCabinet(casino, casinoPlay.Originals, OpenCashier);
        blackjack = new Tables.BlackjackTable(casino, casinoRooms, casinoTables, casinoTurns, remoteImages,
            lodestone, OpenCashier, PopRoute);
        openTable = OpenTable;
        openDoorFromRow = OpenDoor;
        browser = new Tables.TableBrowser(casinoTables, openTable, openDoorFromRow);
        tableDoor = new Tables.TableDoor(casinoTables, confirm, openTable);
        router = new ViewRouter<CasinoRoute>(CasinoRoute.Floor);
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
            case NameplateStatus.SlotsWin when slots.TryRecentResult(out var won) && won > 0:
                values = NameplateValues.Empty with { Chips = NameplateTitleService.ChipCount(won) };
                return true;
            case NameplateStatus.SlotsLoss when slots.TryRecentResult(out var lost) && lost < 0:
                values = NameplateValues.Empty with { Chips = NameplateTitleService.ChipCount(-lost) };
                return true;
            default:
                return false;
        }
    }

    public void OnOpened()
    {
        router.Reset();
        cashier.Close();
        slots.Reset();
        scratch.Reset();
        barkeep.Reset();
        wheel.Reset();
        bingo.Reset();
        dailySpin.Reset();
        originals.Reset();
        blackjack.Reset();
        browser.Reset();
        tableDoor.Reset();
        pendingTableId = string.Empty;
        tab = CasinoTab.Lobby;
        rulesSheet.Close();
        stage.ResetSession();
        ResetLimitsEditor();
        ResetLobby();
        jackpotRail.Snap(Core.Casino.CasinoChipLots.CoinsFor(casino.Jackpot));
        historyLoadFailed = false;
        RefreshFloor();
        casinoPlay.RecoverPendingRound();
        ConsumeLaunch();
    }

    public void OnClosed()
    {
        router.Reset();
        cashier.Close();
        slots.Reset();
        scratch.Reset();
        barkeep.Reset();
        wheel.Reset();
        bingo.Reset();
        dailySpin.Reset();
        originals.Reset();
        blackjack.Reset();
        browser.Reset();
        tableDoor.Reset();
        pendingTableId = string.Empty;
        rulesSheet.Close();
        stage.ResetSession();
        ResetLimitsEditor();
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
    }

    private void ConsumeLaunch()
    {
        if (!launcher.TryConsume(out var launch))
        {
            return;
        }

        if (launch.Kind == Core.Casino.CasinoLaunchKind.Table && launch.TableId.Length > 0)
        {
            OpenTable(launch.TableId);
            return;
        }

        if (launch.Kind == Core.Casino.CasinoLaunchKind.Tables)
        {
            OpenTables();
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
        screenArea = context.Content;
        barkeep.Tick();
        cashier.Gate();
        slots.Gate();
        scratch.Gate();
        wheel.Gate();
        originals.Gate();
        rulesSheet.Gate();
        stage.Gate();
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        slots.DrawOverlay(screenArea, ui);
        scratch.DrawOverlay(screenArea, ui);
        wheel.DrawOverlay(screenArea, ui);
        originals.DrawOverlay(screenArea, ui);
        if (IsStage(router.Current))
        {
            stage.DrawOverlays(screenArea, ui);
            HandleStageRequests();
        }

        rulesSheet.Draw(screenArea, ui);
        cashier.Draw(screenArea, ui, openLimits);
        if (rulesSheet.TakePlayRequest() && !PlayingGame(rulesSheet.GameId))
        {
            OpenGame(rulesSheet.GameId);
        }
    }

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
        route.Screen is CasinoScreen.Cabinet or CasinoScreen.Table or CasinoScreen.DailySpin;

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

    private void DrawStageWorld(CasinoRoute route, in CasinoStageFrame frame)
    {
        var body = frame.Body;
        switch (route.Screen)
        {
            case CasinoScreen.Cabinet when string.Equals(route.GameId, CasinoGames.Slots, StringComparison.Ordinal):
                slots.Draw(body, ui);
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
                bingo.Draw(body, ui);
                break;
            case CasinoScreen.Cabinet when Originals.OriginalsCabinet.Owns(route.GameId):
                originals.Draw(stage, frame, ui);
                break;
            case CasinoScreen.Table:
                blackjack.Draw(body, ui);
                break;
            case CasinoScreen.DailySpin:
                dailySpin.Draw(body, ui);
                break;
            default:
                EmptyState.Draw(body, ui, FontAwesomeIcon.Hammer, Loc.T(L.Casino.CabinetSoonTitle),
                    Loc.T(L.Casino.CabinetSoonHint));
                break;
        }
    }

    private CasinoStageSpec StageSpecFor(CasinoRoute route)
    {
        if (route.Screen == CasinoScreen.Table)
        {
            return new CasinoStageSpec(CasinoGames.Blackjack, L.Casino.GameBlackjack, Backdrop.Felt, LampPool: 1f);
        }

        if (route.Screen == CasinoScreen.DailySpin)
        {
            return new CasinoStageSpec(CasinoGames.DailySpin, L.Casino.GameDailySpin, Backdrop.Strip);
        }

        return route.GameId switch
        {
            CasinoGames.Slots => new CasinoStageSpec(route.GameId, L.Casino.GameSlots, Backdrop.Strip,
                Extra: L.Casino.SlotsPays),
            CasinoGames.Scratch => new CasinoStageSpec(route.GameId, L.Casino.GameScratch, Backdrop.Strip,
                DeckHeight: Cabinets.ScratchCabinet.DeckHeight, BetsRail: true, InstantAvailable: true,
                ReturnTenths: Core.Casino.ScratchRules.ReturnTenths(scratch.CurrentTier), Extra: L.Casino.ScratchOdds),
            CasinoGames.Barkeep => new CasinoStageSpec(route.GameId, L.Casino.GameBarkeep, Backdrop.Strip,
                DeckHeight: Cabinets.BarkeepCabinet.DeckHeight, Practice: barkeep.Practicing, BetsRail: true,
                ReturnTenths: Cabinets.BarkeepCabinet.ReturnTenths, Warmth: 1f),
            CasinoGames.Bingo => new CasinoStageSpec(route.GameId, L.Casino.GameBingo, Backdrop.Arena),
            CasinoGames.Wheel => new CasinoStageSpec(route.GameId, L.Casino.GameWheel, Backdrop.Strip, Room: true,
                DeckHeight: Cabinets.WheelCabinet.DeckHeight),
            _ when Originals.OriginalsCabinet.Owns(route.GameId) => originals.SpecFor(route.GameId),
            _ => new CasinoStageSpec(route.GameId, GameName(route.GameId), Backdrop.Strip),
        };
    }

    private long StageBalance(CasinoRoute route)
    {
        var state = casino.State;
        if (route.Screen == CasinoScreen.Table && state?.TableSitting is { } rack)
        {
            return rack.Stack;
        }

        return state?.Sitting?.Stack ?? 0;
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
            case CasinoInfoRequest.Extra when string.Equals(current.GameId, CasinoGames.Slots,
                StringComparison.Ordinal):
                slots.OpenPayTable();
                break;
            case CasinoInfoRequest.Extra when string.Equals(current.GameId, CasinoGames.Scratch,
                StringComparison.Ordinal):
                scratch.OpenOdds();
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
            case CasinoScreen.Tables:
                browser.Draw(navBar.Body, ui);
                break;
            case CasinoScreen.TableDoor:
                tableDoor.Draw(navBar.Body, ui);
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
            switch (tab)
            {
                case CasinoTab.Games:
                    DrawGamesTab(navBar.Body);
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
            var pressed = AppHeader.EndLargeTitle(in navBar, context, "casino.root.nav", TabTitle(tab),
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
        CasinoTab.Games => Loc.T(L.Casino.GamesHeading),
        CasinoTab.Live => Loc.T(L.Casino.TabLive),
        CasinoTab.Cashier => Loc.T(L.Casino.Cashier),
        _ => DisplayName,
    };

    private string RouteTitle(CasinoRoute route) => route.Screen switch
    {
        CasinoScreen.Tables => Loc.T(L.Casino.TablesTitle),
        CasinoScreen.TableDoor => Loc.T(L.Casino.DoorTitle),
        CasinoScreen.Table => Loc.T(L.Casino.GameBlackjack),
        CasinoScreen.Limits => Loc.T(L.Casino.LimitsRow),
        CasinoScreen.History => Loc.T(L.Casino.HistoryRow),
        CasinoScreen.Fairness => Loc.T(L.Casino.FairnessRow),
        CasinoScreen.RoundDetail => Loc.T(L.Casino.RoundDetailTitle),
        CasinoScreen.DailySpin => Loc.T(L.Casino.GameDailySpin),
        CasinoScreen.Cabinet => Loc.T(GameName(route.GameId)),
        _ => TabTitle(tab),
    };

    private string BackTitle(int depth)
    {
        return router.TryGetView(depth - 2, out var previous) ? RouteTitle(previous) : TabTitle(tab);
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
        slots.ClosePayTable();
        scratch.CloseOdds();
        if (IsStage(router.Current))
        {
            stage.Reset();
        }

        ResetCabinetOf(router.Current);
        router.Pop();
    }

    private void ResetCabinetOf(CasinoRoute route)
    {
        if (route.Screen == CasinoScreen.DailySpin)
        {
            dailySpin.Reset();
            return;
        }

        if (route.Screen == CasinoScreen.Table)
        {
            blackjack.Exit();
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
        }
    }

    private void OpenTables()
    {
        if (router.Current.Screen == CasinoScreen.Tables)
        {
            return;
        }

        browser.Enter();
        router.Push(new CasinoRoute(CasinoScreen.Tables, CasinoGames.Blackjack));
    }

    private void OpenTable(string tableId)
    {
        if (tableId.Length == 0)
        {
            return;
        }

        var current = router.Current;
        if (current.Screen == CasinoScreen.Table
            && string.Equals(current.TableId, tableId, StringComparison.Ordinal))
        {
            return;
        }

        blackjack.Enter(tableId);
        router.Push(new CasinoRoute(CasinoScreen.Table, CasinoGames.Blackjack, string.Empty, tableId));
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
            OpenDoor(hosted.TableId, hosted.InviteToken);
        }

        var resolved = casinoTables.TakeResolvedTable();
        if (resolved is not null)
        {
            if (casinoTables.Owns(resolved))
            {
                OpenDoor(resolved.TableId, resolved.InviteToken);
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

    private void OpenDailySpin()
    {
        if (router.Current.Screen == CasinoScreen.DailySpin)
        {
            return;
        }

        dailySpin.Enter();
        router.Push(new CasinoRoute(CasinoScreen.DailySpin));
    }

    internal void OpenGame(string gameId)
    {
        if (string.Equals(gameId, CasinoGames.Blackjack, StringComparison.Ordinal))
        {
            casinoTables.QuickSeat(Core.Casino.CasinoStakeTiers.Any);
            return;
        }

        if (!casino.HasChips && !string.Equals(gameId, CasinoGames.Barkeep, StringComparison.Ordinal))
        {
            cashier.Open();
            return;
        }

        if (string.Equals(gameId, CasinoGames.Slots, StringComparison.Ordinal))
        {
            slots.Enter();
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

        router.Push(new CasinoRoute(CasinoScreen.Cabinet, gameId));
    }

    private bool SeatedAt(string wireKind)
    {
        return Core.Casino.CasinoWire.SittingFor(casino.State, wireKind) is not null;
    }

    private static LocString PlayingName(CasinoRoute route) => route.Screen switch
    {
        CasinoScreen.Cabinet => GameName(route.GameId),
        CasinoScreen.Table or CasinoScreen.TableDoor => L.Casino.GameBlackjack,
        CasinoScreen.DailySpin => L.Casino.GameDailySpin,
        _ => L.Apps.Casino,
    };

    private static LocString GameName(string gameId) => gameId switch
    {
        CasinoGames.Blackjack => L.Casino.GameBlackjack,
        CasinoGames.Holdem => L.Casino.GameHoldem,
        CasinoGames.Slots => L.Casino.GameSlots,
        CasinoGames.Scratch => L.Casino.GameScratch,
        CasinoGames.Bingo => L.Casino.GameBingo,
        CasinoGames.Wheel => L.Casino.GameWheel,
        CasinoGames.Barkeep => L.Casino.GameBarkeep,
        CasinoGames.DailySpin => L.Casino.GameDailySpin,
        CasinoGames.Mines => L.Originals.GameMines,
        CasinoGames.Dice => L.Originals.GameDice,
        CasinoGames.Limbo => L.Originals.GameLimbo,
        CasinoGames.Keno => L.Originals.GameKeno,
        CasinoGames.HiLo => L.Originals.GameHiLo,
        _ => L.Apps.Casino,
    };

    public void Dispose()
    {
    }
}
