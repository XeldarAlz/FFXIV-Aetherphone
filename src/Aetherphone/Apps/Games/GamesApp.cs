using Aetherphone.Apps.Games.Beat;
using Aetherphone.Apps.Games.Blade;
using Aetherphone.Apps.Games.Breakout;
using Aetherphone.Apps.Games.BubbleShooter;
using Aetherphone.Apps.Games.CapMan;
using Aetherphone.Apps.Games.Chess;
using Aetherphone.Apps.Games.Coil;
using Aetherphone.Apps.Games.Crawler;
using Aetherphone.Apps.Games.CrystalDrop;
using Aetherphone.Apps.Games.Doom;
using Aetherphone.Apps.Games.Drift;
using Aetherphone.Apps.Games.Flap;
using Aetherphone.Apps.Games.Flow;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.GemSwap;
using Aetherphone.Apps.Games.Gloop;
using Aetherphone.Apps.Games.Hop;
using Aetherphone.Apps.Games.Invaders;
using Aetherphone.Apps.Games.Mahjong;
using Aetherphone.Apps.Games.MiniGolf;
using Aetherphone.Apps.Games.Nonogram;
using Aetherphone.Apps.Games.Online;
using Aetherphone.Apps.Games.Pairs;
using Aetherphone.Apps.Games.Reversi;
using Aetherphone.Apps.Games.Simon;
using Aetherphone.Apps.Games.Skyfall;
using Aetherphone.Apps.Games.Slice;
using Aetherphone.Apps.Games.Snake;
using Aetherphone.Apps.Games.Snip;
using Aetherphone.Apps.Games.Solitaire;
using Aetherphone.Apps.Games.Spiral;
using Aetherphone.Apps.Games.Squadron;
using Aetherphone.Apps.Games.Stack;
using Aetherphone.Apps.Games.Sudoku;
using Aetherphone.Apps.Games.Sweeper;
using Aetherphone.Apps.Games.Swoop;
using Aetherphone.Apps.Games.Tetris;
using Aetherphone.Apps.Games.Trails;
using Aetherphone.Apps.Games.Thrust;
using Aetherphone.Apps.Games.Trailblaze;
using Aetherphone.Apps.Games.Trivia;
using Aetherphone.Apps.Games.Twenty48;
using Aetherphone.Apps.Games.Updraft;
using Aetherphone.Apps.Games.WaterSort;
using Aetherphone.Apps.Games.Whack;
using Aetherphone.Apps.Games.WordRun;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Changelog;
using Aetherphone.Core.Game;
using Aetherphone.Core.Games;
using Aetherphone.Core.Honorific;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Games;

internal sealed partial class GamesApp : IPhoneApp, ITabRouteTarget, INameplateActivitySource
{
    private readonly struct CoinSessionChip
    {
        public readonly string Label;
        public readonly float Fraction;
        public readonly bool Qualified;
        public readonly bool CoolingDown;
        public readonly bool Visible;

        public CoinSessionChip(string label, float fraction, bool qualified, bool coolingDown = false)
        {
            Label = label;
            Fraction = fraction;
            Qualified = qualified;
            CoolingDown = coolingDown;
            Visible = true;
        }
    }

    public const string HomeTabRoute = "games.tab.home";
    public const string TogetherTabRoute = "games.tab.together";
    public const string RecordsTabRoute = "games.tab.records";
    public const string SearchTabRoute = "games.tab.search";
    private const string PlayRoutePrefix = "games.play.";
    private const int TabCount = 4;
    private const float CoinChipRingRadius = 7f;
    private const float CoinChipGap = 5f;
    private const string CoinChipTooltipId = "games.coinChip";
    private const float ResultAppearSpeed = 3.4f;
    private const int FeaturedStep = 5;

    private static readonly string[] TabIds = [HomeTabRoute, TogetherTabRoute, RecordsTabRoute, SearchTabRoute];

    private readonly GameStatsStore stats;
    private readonly Configuration configuration;
    private readonly Core.Coins.CoinStore coins;
    private readonly Core.Coins.CoinGameSessionTracker coinSessions;
    private readonly Windows.Components.CoinFloat coinFloats = new();
    private readonly GameRoomsStore gameRooms;
    private readonly LeaderboardStore leaderboard;
    private readonly RemoteImageCache images;
    private readonly LodestoneService lodestone;
    private readonly OnlineHub onlineHub;
    private readonly OnlineRoomView onlineRoom;
    private readonly IMiniGame[] games;
    private readonly GamesLibrary library;
    private readonly AppSkin ui = new(AppPalettes.Games);
    private readonly ViewRouter<GamesRoute> router;
    private readonly RouterDraw<GamesRoute> drawView;
    private readonly Action back;
    private readonly TabBar tabBar = new();
    private readonly TabItem[] tabItems = new TabItem[TabCount];
    private readonly string[] countLabels;
    private readonly GameSession session;
    private readonly HudModel hud = new();
    private readonly StageHud stageHud = new();
    private readonly StageBackdrop backdrop = new();
    private readonly ScreenFx fx;
    private readonly StageIntro intro = new();
    private readonly StagePause pause = new();
    private readonly StageChrome chrome = new();
    private readonly LevelSelect levelSelect = new();
    private readonly StageHandoff handoff = new();
    private Rect screenRect;
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private IMiniGame? currentGame;
    private GamesTab tab;
    private string pendingRoute = string.Empty;
    private LanguageInfo? countLanguage;
    private int featuredIndex;
    private int leaderboardVersion = -1;
    private float frameSeconds;
    private float resultProgress;
    private RankText resultRank = new();
    private LabelSlot levelClearedLabel;
    public string Id => "games";
    public string DisplayName => Loc.T(L.Apps.Games);
    public string Glyph => ">";
    public int BadgeCount => configuration.HasUnseenFeaturePin(NewFeaturePins.Games) ? 1 : 0;
    public bool BadgeAsDot => true;

    public GamesApp(GameStatsStore stats, GameData gameData, ITextureProvider textures,
        Core.Coins.CoinStore coins, Core.Coins.CoinGameSessionTracker coinSessions,
        GameRoomsStore gameRooms, Configuration configuration, LeaderboardStore leaderboard, RemoteImageCache images,
        LodestoneService lodestone)
    {
        this.stats = stats;
        this.configuration = configuration;
        this.coins = coins;
        this.coinSessions = coinSessions;
        this.gameRooms = gameRooms;
        this.leaderboard = leaderboard;
        this.images = images;
        this.lodestone = lodestone;
        refreshLeaderboard = RefreshLeaderboardNow;
        session = new GameSession(stats, leaderboard, leaderboard);
        fx = new ScreenFx(backdrop);
        onlineHub = new OnlineHub(gameRooms, OpenOnlineRoom);
        onlineRoom = new OnlineRoomView(gameRooms);
        games = new IMiniGame[]
        {
            new SweeperApp(),
            new PairsApp(),
            new GemSwapApp(),
            new TetrisApp(),
            new Twenty48App(),
            new WaterSortApp(),
            new BreakoutApp(),
            new BubbleShooterApp(),
            new NonogramApp(),
            new FlowApp(),
            new SolitaireApp(),
            new SimonApp(),
            new FlapApp(),
            new ReversiApp(),
            new WhackApp(),
            new SnakeApp(),
            new SudokuApp(),
            new ChessApp(),
            new StackApp(),
            new CrystalDropApp(),
            new BeatApp(),
            new BladeApp(),
            new TriviaApp(gameData, textures),
            new SkyfallApp(),
            new InvadersApp(),
            new CapManApp(),
            new HopApp(),
            new SquadronApp(),
            new DoomApp(),
            new WordRunApp(gameData),
            new CoilApp(),
            new UpdraftApp(),
            new SwoopApp(),
            new SliceApp(),
            new SpiralApp(),
            new MahjongApp(),
            new GloopApp(),
            new DriftApp(),
            new CrawlerApp(),
            new TrailsApp(),
            new TrailblazeApp(),
            new ThrustApp(),
            new SnipApp(),
            new MiniGolfApp(),
        };
        library = new GamesLibrary(games, stats, leaderboard);
        countLabels = new string[library.Entries.Length + 1];
        RebuildLayout();
        router = new ViewRouter<GamesRoute>(GamesRoute.Root);
        drawView = DrawView;
        back = () => router.Pop();
    }

    public IMiniGame DailyGame => games[FeaturedIndex()];

    public GamesLibrary Library => library;

    public static string PlayRoute(string gameId) => PlayRoutePrefix + gameId;

    public bool TryNameplateActivity(NameplateStatus status, out NameplateValues values)
    {
        values = NameplateValues.Empty;
        if (status != NameplateStatus.Games)
        {
            return false;
        }

        var title = PlayingTitle();
        if (title.Length == 0)
        {
            return false;
        }

        values = NameplateValues.Empty with { Game = title };
        return true;
    }

    private string PlayingTitle()
    {
        if (currentGame is not null)
        {
            return currentGame.Title;
        }

        if (router.Current.Screen != GamesScreen.OnlineRoom || gameRooms.Room.State is not { } room)
        {
            return string.Empty;
        }

        return Loc.T(GamesOnlineText.GameName(room.Snapshot.GameKind));
    }

    public void OpenTab(string tab) => pendingRoute = tab ?? string.Empty;

    private void RebuildLayout()
    {
        featuredIndex = FeaturedIndex();
        stats.DailyGameId = games[featuredIndex].Id;
        library.Rebuild();
    }

    private int FeaturedIndex()
    {
        var serverFeatured = coins.Wallet?.FeaturedGameId;
        if (!string.IsNullOrEmpty(serverFeatured))
        {
            for (var index = 0; index < games.Length; index++)
            {
                if (string.Equals(games[index].Id, serverFeatured, StringComparison.Ordinal))
                {
                    return index;
                }
            }
        }

        return GameStatsStore.TodayIndex * FeaturedStep % games.Length;
    }

    public void OnOpened()
    {
        configuration.MarkFeaturePinSeen(NewFeaturePins.Games);
        router.Reset();
        tab = GamesTab.Home;
        RebuildLayout();
        ResetLauncher();
        onlineHub.Reset();
        leaderboard.EnsureMyRanksFresh();
    }

    public void OnClosed()
    {
        CloseCurrentGame();
        gameRooms.Exit();
        AppLandscape.Release(Id);
        router.Reset();
        pendingRoute = string.Empty;
    }

    public void Dispose()
    {
        for (var index = 0; index < games.Length; index++)
        {
            games[index].Dispose();
        }
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        frameSeconds = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        library.EnsureLanguage();
        SyncCountLabels();
        SyncLeaderboardVersion();
        screenRect = SceneChrome.ScreenFrom(context.Content, theme, UiScale.Current);
        ConsumePendingRoute();
        if (!router.IsTransitioning && router.Current.Screen is GamesScreen.Root or GamesScreen.Shelf)
        {
            onlineHub.Consume();
        }

        if (router.IsTransitioning || router.Current.Screen != GamesScreen.Playing)
        {
            ui.Backdrop(screenRect);
        }

        var appArea = SceneChrome.AppAreaFrom(context.Content, theme, UiScale.Current);
        router.Draw(appArea, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        if (!router.IsTransitioning && router.Current.Screen is GamesScreen.Root or GamesScreen.Shelf
            && currentGame is not null)
        {
            CloseCurrentGame();
        }

        var award = coinSessions.TakeAward(out _);
        if (award is not null)
        {
            var anchor = new Vector2(context.Content.Center.X, context.Content.Min.Y + 96f * UiScale.Current);
            if (award.Granted && award.Amount > 0)
            {
                coinFloats.Spawn(Loc.T(L.Coin.CheckInReward, NumberText.Group(award.Amount)), anchor);
                coins.AbsorbLocalAward(award.Balance);
            }
            else if (award.Reason == "too_short")
            {
                coinFloats.Spawn(Loc.T(L.Coin.SessionTooShort), anchor, true);
            }
        }

        coinFloats.Draw(ImGui.GetWindowDrawList(), Core.Apps.AppAccents.For("coin"), theme.TextMuted,
            ImGui.GetIO().DeltaTime);
    }

    private void SyncCountLabels()
    {
        if (ReferenceEquals(countLanguage, Loc.Current))
        {
            return;
        }

        countLanguage = Loc.Current;
        Array.Clear(countLabels);
        roomsLabelCount = -1;
        dailyEyebrow = Loc.Culture.TextInfo.ToUpper(Loc.T(L.Games.Daily));
        recordsText.Reset();
        streakText.Reset();
        recordCountText.Reset();
        onlineHub.ResetLabels();
    }

    private void SyncLeaderboardVersion()
    {
        var version = leaderboard.Version;
        if (version == leaderboardVersion)
        {
            return;
        }

        leaderboardVersion = version;
        library.RefreshRanks();
        if (currentGame is not null)
        {
            session.RefreshRank();
        }
    }

    private void ConsumePendingRoute()
    {
        if (pendingRoute.Length == 0 || router.IsTransitioning)
        {
            return;
        }

        var route = pendingRoute;
        pendingRoute = string.Empty;
        if (router.Current.Screen is GamesScreen.OnlineRoom or GamesScreen.Playing)
        {
            return;
        }

        if (route.StartsWith(PlayRoutePrefix, StringComparison.Ordinal))
        {
            var game = FindGame(route.AsSpan(PlayRoutePrefix.Length));
            if (game is null)
            {
                return;
            }

            router.Reset();
            tab = GamesTab.Home;
            OpenGame(game, false);
            return;
        }

        for (var index = 0; index < TabIds.Length; index++)
        {
            if (string.Equals(TabIds[index], route, StringComparison.Ordinal))
            {
                router.Reset();
                SelectTab((GamesTab)index);
                return;
            }
        }
    }

    private IMiniGame? FindGame(ReadOnlySpan<char> id)
    {
        for (var index = 0; index < games.Length; index++)
        {
            if (id.SequenceEqual(games[index].Id))
            {
                return games[index];
            }
        }

        return null;
    }

    private void DrawView(GamesRoute route, Rect area, int depth)
    {
        var context = new PhoneContext(area, theme, navigation);
        switch (route.Screen)
        {
            case GamesScreen.Playing:
                ImGui.GetWindowDrawList().AddRectFilled(area.Min, area.Max, ImGui.GetColorU32(theme.AppBackground));
                DrawActiveGame(context);
                return;
            case GamesScreen.OnlineRoom:
                ImGui.GetWindowDrawList().AddRectFilled(area.Min, area.Max, ImGui.GetColorU32(theme.AppBackground));
                SyncOnlineRoomLandscape();
                onlineRoom.Draw(context, LeaveOnlineRoom, ui,
                    AppLandscape.Held(Id) && context.Content.IsLandscape(), TabTitle(GamesTab.Together));
                return;
            case GamesScreen.Shelf:
                PaintViewBackdrop(area);
                DrawShelfPage(context, route.Shelf);
                return;
            case GamesScreen.Leaderboard:
                PaintViewBackdrop(area, leaderboardAccent);
                DrawLeaderboard(context);
                return;
            default:
                PaintViewBackdrop(area);
                DrawRoot(context, area);
                return;
        }
    }

    private void PaintViewBackdrop(Rect area) => PaintViewBackdrop(area, games[featuredIndex].Accent);

    private void PaintViewBackdrop(Rect area, Vector4 accent)
    {
        ui.Body(area);
        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(area.Min, area.Max, true);
        GameScene.Ambient(drawList, area, accent);
        drawList.PopClipRect();
    }

    private void DrawRoot(in PhoneContext context, Rect area)
    {
        using (TabBar.ReserveContent(UiScale.Current))
        {
            ImGui.PushID(TabIds[(int)tab]);
            switch (tab)
            {
                case GamesTab.Together:
                    DrawTogether(context);
                    break;
                case GamesTab.Records:
                    DrawRecords(context);
                    break;
                case GamesTab.Search:
                    DrawSearch(context);
                    break;
                default:
                    DrawHome(context);
                    break;
            }

            ImGui.PopID();
        }

        DrawTabBar(area);
    }

    private void DrawTabBar(Rect area)
    {
        tabItems[(int)GamesTab.Home] = new TabItem(Loc.T(L.GamesHub.TabHome), IconGlyph.Of(FontAwesomeIcon.Gamepad),
            AnchorKey: HomeTabRoute);
        tabItems[(int)GamesTab.Together] = new TabItem(Loc.T(L.Games.OnlineTitle),
            IconGlyph.Of(FontAwesomeIcon.UserFriends), AnchorKey: TogetherTabRoute);
        tabItems[(int)GamesTab.Records] = new TabItem(Loc.T(L.GamesHub.TabRecords),
            IconGlyph.Of(FontAwesomeIcon.Trophy), AnchorKey: RecordsTabRoute);
        tabItems[(int)GamesTab.Search] = new TabItem(Loc.T(L.Common.Search), IconGlyph.Of(FontAwesomeIcon.Search),
            AnchorKey: SearchTabRoute);
        var result = tabBar.Draw(area, ui, tabItems, (int)tab);
        if (result.Tapped < 0)
        {
            return;
        }

        SelectTab((GamesTab)result.Tapped);
    }

    private void SelectTab(GamesTab wanted)
    {
        if (wanted == GamesTab.Search)
        {
            focusSearch = true;
        }

        if (wanted == GamesTab.Together)
        {
            gameRooms.EnsureFresh();
        }

        if (wanted == tab)
        {
            return;
        }

        tab = wanted;
        entrance = 0f;
    }

    private string TabTitle(GamesTab target) => target switch
    {
        GamesTab.Together => Loc.T(L.Games.OnlineTitle),
        GamesTab.Records => Loc.T(L.GamesHub.TabRecords),
        GamesTab.Search => Loc.T(L.Common.Search),
        _ => DisplayName,
    };

    private void OpenOnlineHub(string preferredKind)
    {
        onlineHub.Highlight(preferredKind);
        if (router.Depth > 1)
        {
            router.Reset();
        }

        SelectTab(GamesTab.Together);
    }

    private void OpenOnlineRoom(string roomId, string gameKind)
    {
        stats.MarkPlayed(GamesLibrary.OnlineEntryId(gameKind));
        onlineRoom.Enter();
        router.Push(GamesRoute.OnlineRoom);
    }

    private void LeaveOnlineRoom()
    {
        AppLandscape.Release(Id);
        gameRooms.Exit();
        router.Pop();
        library.Rebuild();
    }

    private void SyncOnlineRoomLandscape()
    {
        if (onlineRoom.WantsLandscape)
        {
            AppLandscape.Request(Id);
            return;
        }

        AppLandscape.Release(Id);
    }

    private void DrawActiveGame(in PhoneContext context)
    {
        var game = currentGame!;
        var spec = game.Spec;
        var scale = UiScale.Current;
        var full = context.Content;
        var landscape = spec.Landscape && AppLandscape.Held(Id) && full.IsLandscape();
        var accent = game.Accent;
        using (AppSurface.BeginEdgeToEdge(full))
        {
            var drawList = ImGui.GetWindowDrawList();
            var rawSeconds = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
            var attentive = GameFocus.Active;
            backdrop.Update(rawSeconds, full, ImGui.GetMousePos(), UiInteract.Hover(full.Min, full.Max));
            backdrop.Draw(drawList, full, accent, scale);
            fx.Update(rawSeconds);
            var safe = StageLayout.Safe(full, spec.Hud, scale);
            DrawStageGame(drawList, game, full, safe, accent, context.Theme, rawSeconds, attentive);
            fx.Draw(drawList, full, accent);
            DrawStageOverlays(drawList, game, full, accent, context.Theme, rawSeconds, scale);
            chrome.BeginFrame();
            DrawChrome(drawList, full, context.Theme, landscape, scale);
        }
    }

    private void DrawStageGame(ImDrawListPtr drawList, IMiniGame game, Rect full, Rect safe, Vector4 accent,
        PhoneTheme theme, float rawSeconds, bool attentive)
    {
        var spec = game.Spec;
        if (spec.Clocked && !attentive && session.State == StageFlow.Playing)
        {
            session.Pause();
        }

        session.Tick(attentive ? rawSeconds : 0f);
        if (session.State == StageFlow.Countdown && session.CountdownStepChanged)
        {
            GameSfx.CountdownTick();
        }

        var playing = session.State == StageFlow.Playing && attentive && !session.HandoffPending;
        var gameSeconds = playing ? rawSeconds * fx.TimeScale : 0f;
        hud.Clear();
        var gameContext = new GameContext(full, safe, theme, gameSeconds, rawSeconds, session, hud, fx, backdrop,
            chrome);
        if (session.State == StageFlow.Intro)
        {
            game.DrawIdle(gameContext);
            return;
        }

        if (session.HandoffPending)
        {
            return;
        }

        game.Draw(gameContext);
        stageHud.Draw(drawList, hud, full, spec.Hud, session.Kind, accent, theme, rawSeconds, session.BeatingBest, fx);
    }

    private void DrawStageOverlays(ImDrawListPtr drawList, IMiniGame game, Rect full, Vector4 accent,
        PhoneTheme theme, float rawSeconds, float scale)
    {
        var spec = game.Spec;
        var safe = StageLayout.Safe(full, spec.Hud, scale);
        switch (session.State)
        {
            case StageFlow.Intro:
            {
                var gameContext = new GameContext(full, safe, theme, 0f, rawSeconds, session, hud, fx, backdrop,
                    chrome);
                var action = levelSelect.IsOpen ? IntroAction.None : intro.Draw(drawList, gameContext, accent, scale);
                if (action == IntroAction.Play)
                {
                    StartRun(game);
                    break;
                }

                if (action == IntroAction.Leaderboard)
                {
                    OpenLeaderboard(game, session.LeaderboardStatId, game.Title);
                }
                else if (action == IntroAction.Levels)
                {
                    levelSelect.Open();
                }

                var chosen = levelSelect.Draw(drawList, gameContext, accent, scale);
                if (chosen > 0 && session.SelectLevel(chosen))
                {
                    StartRun(game);
                }

                break;
            }
            case StageFlow.Countdown:
            {
                var step = session.CountdownStep;
                var label = step > 0 ? GameNumber.Label(step) : Loc.Upper(Loc.T(L.Stage.Go));
                GameBanner.Draw(drawList, full.Center, label, accent, theme, session.CountdownStepProgress,
                    TextStyles.LargeTitle);
                break;
            }
            case StageFlow.Result:
            {
                resultProgress = MathF.Min(1f, resultProgress + rawSeconds * ResultAppearSpeed);
                var result = BuildStageResult(spec, accent);
                var resultAction = GameOverlay.DrawStage(full, theme, accent, resultProgress, result);
                if (resultAction == ResultAction.Primary)
                {
                    session.AdvanceLevel();
                    StartRun(game);
                }
                else if (resultAction == ResultAction.Secondary)
                {
                    StartRun(game);
                }
                else if (resultAction == ResultAction.Leaderboard)
                {
                    OpenLeaderboard(game, session.LeaderboardStatId, game.Title);
                }

                break;
            }
            default:
                break;
        }

        handoff.Draw(drawList, full, session, rawSeconds, scale);
        var pauseAction = pause.Draw(drawList, full, theme, accent, rawSeconds, session.State == StageFlow.Paused,
            scale);
        switch (pauseAction)
        {
            case PauseAction.Resume:
                session.Resume();
                break;
            case PauseAction.Restart:
                StartRun(game);
                break;
            case PauseAction.Leaderboard:
                OpenLeaderboard(game, session.LeaderboardStatId, game.Title);
                break;
            case PauseAction.Quit:
                game.OnQuit(session);
                back();
                break;
            default:
                break;
        }
    }

    private StageResult BuildStageResult(in GameSpec spec, Vector4 accent)
    {
        var outcome = session.Outcome;
        var kind = outcome.Kind;
        var lost = !outcome.Won && !outcome.IsDraw;
        var title = outcome.IsDraw
            ? Loc.T(L.Games.Draw)
            : kind == ScoreKind.Score || (kind == ScoreKind.Level && lost)
                ? Loc.T(L.Games.GameOver)
                : outcome.Won ? Loc.T(L.Games.YouWin) : Loc.T(L.Games.Lose);
        var hidePrimary = kind == ScoreKind.Time && lost;
        var label = hidePrimary
            ? string.Empty
            : kind switch
            {
                ScoreKind.Time => Loc.T(L.Games.Time),
                ScoreKind.Level => Loc.T(L.Games.Level),
                ScoreKind.Streak => Loc.T(L.Games.Streak),
                _ => Loc.T(L.Games.Score),
            };
        var value = hidePrimary ? string.Empty : StageHud.ValueLabel(session.ResultValue, kind);
        var continueLabel = Loc.T(outcome.ContinueLabel ?? L.Games.PlayAgain);
        var secondaryLabel = string.Empty;
        var stars = session.HasLevels ? session.LevelStars : GameOutcome.NoStars;
        if (stars >= 0)
        {
            title = stars > 0 ? levelClearedLabel.Get(L.Stage.LevelCleared, session.Level) : Loc.T(L.Games.GameOver);
            if (kind == ScoreKind.Level)
            {
                label = Loc.T(L.Stage.TotalStars);
            }
        }

        if (session.HasLevels)
        {
            continueLabel = Loc.T(session.CanAdvance ? L.Stage.NextLevel : L.Stage.Retry);
            secondaryLabel = session.CanAdvance ? Loc.T(L.Stage.Retry) : string.Empty;
        }

        if (session.Unranked)
        {
            return BuildUnrankedResult(outcome, accent, continueLabel, secondaryLabel);
        }

        resultRank.Refresh(session.Rank);
        return new StageResult(title, accent, label, value, session.NewBest && !outcome.QuietBest, continueLabel,
            resultRank.ResultLine, resultRank.FriendsLine, session.Rank.State == RankState.Uploading,
            GameOverlay.IsTopTen(session.Rank), outcome, secondaryLabel, stars);
    }

    private static StageResult BuildUnrankedResult(in GameOutcome outcome, Vector4 accent, string continueLabel,
        string secondaryLabel)
    {
        var title = outcome.IsDraw
            ? Loc.T(L.Games.Draw)
            : outcome.Won ? Loc.T(L.Games.YouWin) : Loc.T(L.Games.GameOver);
        var titleColor = accent;
        if (outcome.HasWinner)
        {
            title = GameSeats.WinLine(outcome.WinnerSeat);
            titleColor = GameSeats.Color(outcome.WinnerSeat);
        }

        return new StageResult(title, titleColor, string.Empty, string.Empty, false, continueLabel, string.Empty,
            string.Empty, false, false, outcome, secondaryLabel, GameOutcome.NoStars);
    }

    private void DrawChrome(ImDrawListPtr drawList, Rect full, PhoneTheme theme, bool landscape, float scale)
    {
        var radius = StageLayout.ChipRadius * scale;
        if (chrome.BackChip(drawList, StageLayout.BackChipCenter(full, scale), radius, theme, scale))
        {
            if (levelSelect.IsOpen)
            {
                levelSelect.Close();
            }
            else
            {
                back();
            }
        }

        if (landscape)
        {
            return;
        }

        var pauseCenter = StageLayout.PauseChipCenter(full, scale);
        var showPause = session.State is StageFlow.Playing or StageFlow.Countdown or StageFlow.Paused;
        var rightEdge = full.Max.X - Metrics.Space.Md * scale;
        if (showPause)
        {
            if (chrome.PauseChip(drawList, pauseCenter, radius, theme, scale, session.State == StageFlow.Paused))
            {
                TogglePause();
            }

            rightEdge = pauseCenter.X - radius - StageLayout.CoinChipGap * scale;
        }

        var chip = BuildCoinSessionChip();
        if (chip.Visible)
        {
            chrome.RecordCoinChip(DrawCoinSessionChip(chip, rightEdge, pauseCenter.Y, theme, scale));
        }
    }

    private void TogglePause()
    {
        switch (session.State)
        {
            case StageFlow.Playing:
                session.Pause();
                break;
            case StageFlow.Paused:
                session.Resume();
                break;
            default:
                break;
        }
    }

    private void StartRun(IMiniGame game)
    {
        session.Play();
        fx.Clear();
        hud.Clear();
        stageHud.Reset();
        pause.Reset();
        levelSelect.Reset();
        handoff.Reset();
        resultProgress = 0f;
        game.Start(session.Start);
    }

    private CoinSessionChip BuildCoinSessionChip()
    {
        var wallet = coins.Wallet;
        if (wallet is not null && RuleExhausted(wallet, "game.session") && RuleExhausted(wallet, "game.deep"))
        {
            return default;
        }

        var seconds = coinSessions.OpenSessionSeconds;
        if (seconds < 0)
        {
            var cooldown = coinSessions.CooldownSeconds;
            return cooldown > 0
                ? new CoinSessionChip(TimeText.Duration(cooldown), coinSessions.CooldownProgress, false, true)
                : default;
        }

        var minSeconds = coinSessions.OpenMinSeconds;
        if (seconds < minSeconds)
        {
            return new CoinSessionChip(TimeText.Duration(minSeconds - seconds), seconds / (float)minSeconds, false);
        }

        var deepSeconds = coinSessions.OpenDeepSeconds;
        if (seconds < deepSeconds)
        {
            return new CoinSessionChip(TimeText.Duration(deepSeconds - seconds),
                (seconds - minSeconds) / (float)(deepSeconds - minSeconds), true);
        }

        return new CoinSessionChip(string.Empty, 1f, true);
    }

    private static Rect DrawCoinSessionChip(in CoinSessionChip chip, float right, float rowCenterY, PhoneTheme theme,
        float scale)
    {
        var accent = chip.CoolingDown ? theme.TextMuted : AppAccents.For("coin");
        var ringRadius = CoinChipRingRadius * scale;
        var thickness = Metrics.Stroke.Ring * scale;
        var textSize = chip.Label.Length > 0 ? Typography.Measure(chip.Label, TextStyles.Caption1) : Vector2.Zero;
        var labelSpan = chip.Label.Length > 0 ? textSize.X + CoinChipGap * scale : 0f;
        var ringCenter = new Vector2(right - labelSpan - ringRadius, rowCenterY);
        ProgressRing.Track(ringCenter, ringRadius, thickness, Palette.WithAlpha(accent, 0.28f));
        ProgressRing.Fill(ringCenter, ringRadius, thickness, chip.Fraction, accent);
        if (chip.CoolingDown)
        {
            ProgressRing.CenterIcon(ImGui.GetWindowDrawList(), ringCenter, FontAwesomeIcon.HourglassHalf, accent,
                ringRadius * 0.95f);
        }
        else if (chip.Qualified)
        {
            ProgressRing.CenterIcon(ImGui.GetWindowDrawList(), ringCenter, FontAwesomeIcon.Check, accent,
                ringRadius * 1.05f);
        }
        else
        {
            CurrencyGlyph.Draw(ImGui.GetWindowDrawList(), CurrencyKind.Coins, ringCenter, ringRadius * 1.1f);
        }

        var hoverHalfHeight = StageLayout.ChromeBand * scale * 0.35f;
        var hoverRect = new Rect(new Vector2(ringCenter.X - ringRadius - thickness, rowCenterY - hoverHalfHeight),
            new Vector2(right, rowCenterY + hoverHalfHeight));
        HoverTooltip.Show(CoinChipTooltipId, hoverRect, CoinChipHint(chip));
        if (chip.Label.Length == 0)
        {
            return hoverRect;
        }

        Typography.DrawCentered(ImGui.GetWindowDrawList(), new Vector2(right - textSize.X * 0.5f, rowCenterY),
            chip.Label, chip.CoolingDown ? theme.TextMuted : theme.TextStrong, TextStyles.Caption1);
        return hoverRect;
    }

    private static string CoinChipHint(in CoinSessionChip chip)
    {
        if (chip.CoolingDown)
        {
            return Loc.T(L.Games.CoinTimerCooldownHint);
        }

        if (!chip.Qualified)
        {
            return Loc.T(L.Games.CoinTimerHint);
        }

        return chip.Label.Length > 0 ? Loc.T(L.Games.CoinTimerDeepHint) : Loc.T(L.Games.CoinTimerDoneHint);
    }

    private static bool RuleExhausted(CoinWalletDto wallet, string ruleId)
    {
        for (var index = 0; index < wallet.Rules.Length; index++)
        {
            ref readonly var rule = ref wallet.Rules[index];
            if (string.Equals(rule.RuleId, ruleId, StringComparison.Ordinal))
            {
                return rule.PeriodCap > 0 && rule.EarnedThisPeriod >= rule.PeriodCap;
            }
        }

        return false;
    }

    private void OpenGame(IMiniGame game, bool animate = true)
    {
        currentGame = game;
        var spec = game.Spec;
        var daily = string.Equals(spec.Id, stats.DailyGameId, StringComparison.Ordinal);
        var seed = daily ? GameSeed.Daily(spec.Id, GameStatsStore.TodayIndex) : GameSeed.Fresh();
        session.Begin(spec, new GameStart(stats.LastMode(spec.Id), seed, daily));
        backdrop.Set(spec.Backdrop);
        fx.Clear();
        hud.Clear();
        stageHud.Reset();
        intro.Begin(spec);
        pause.Reset();
        levelSelect.Reset();
        handoff.Reset();
        resultProgress = 0f;
        coinSessions.GameOpened(spec.Id);
        stats.MarkPlayed(spec.Id);
        if (spec.Landscape)
        {
            AppLandscape.Request(Id);
        }

        router.Push(GamesRoute.Playing, animate);
    }

    private void CloseCurrentGame()
    {
        AppLandscape.Release(Id);
        if (currentGame is null)
        {
            return;
        }

        currentGame.Close();
        currentGame = null;
        coinSessions.GameClosed();
        library.Rebuild();
    }
}
