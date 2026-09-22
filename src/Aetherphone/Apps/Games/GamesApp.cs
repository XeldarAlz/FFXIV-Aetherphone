using Aetherphone.Apps.Games.Beat;
using Aetherphone.Apps.Games.Blade;
using Aetherphone.Apps.Games.Breakout;
using Aetherphone.Apps.Games.BubbleShooter;
using Aetherphone.Apps.Games.CapMan;
using Aetherphone.Apps.Games.Chess;
using Aetherphone.Apps.Games.CrystalDrop;
using Aetherphone.Apps.Games.Doom;
using Aetherphone.Apps.Games.Flap;
using Aetherphone.Apps.Games.Flow;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.GemSwap;
using Aetherphone.Apps.Games.Hop;
using Aetherphone.Apps.Games.Invaders;
using Aetherphone.Apps.Games.Nonogram;
using Aetherphone.Apps.Games.Online;
using Aetherphone.Apps.Games.Pairs;
using Aetherphone.Apps.Games.Reversi;
using Aetherphone.Apps.Games.Simon;
using Aetherphone.Apps.Games.Skyfall;
using Aetherphone.Apps.Games.Snake;
using Aetherphone.Apps.Games.Solitaire;
using Aetherphone.Apps.Games.Squadron;
using Aetherphone.Apps.Games.Stack;
using Aetherphone.Apps.Games.Sudoku;
using Aetherphone.Apps.Games.Sweeper;
using Aetherphone.Apps.Games.Tetris;
using Aetherphone.Apps.Games.Trivia;
using Aetherphone.Apps.Games.Twenty48;
using Aetherphone.Apps.Games.WaterSort;
using Aetherphone.Apps.Games.Whack;
using Aetherphone.Apps.Games.WordRun;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Games;

internal sealed partial class GamesApp : IPhoneApp
{
    private enum GameRoute : byte
    {
        Launcher,
        Playing,
        OnlineHub,
        OnlineRoom,
    }

    private readonly struct CoinSessionChip
    {
        public readonly string Label;
        public readonly float Fraction;
        public readonly bool Qualified;
        public readonly bool Visible;

        public CoinSessionChip(string label, float fraction, bool qualified)
        {
            Label = label;
            Fraction = fraction;
            Qualified = qualified;
            Visible = true;
        }
    }

    private const float HeaderHeight = 42f;
    private const float LandscapeBackRadius = 16f;
    private const float LandscapeBackInset = 10f;
    private const float CoinChipRingRadius = 7f;
    private const float CoinChipGap = 5f;
    private const float CoinChipReserve = 72f;
    private const string CoinChipTooltipId = "games.coinChip";
    private const float PausedFadeSeconds = 0.12f;
    private const int FeaturedStep = 5;
    private readonly GameStatsStore stats;
    private readonly Core.Coins.CoinStore coins;
    private readonly Core.Coins.CoinGameSessionTracker coinSessions;
    private readonly Windows.Components.CoinFloat coinFloats = new();
    private readonly GameRoomsStore gameRooms;
    private readonly OnlineHub onlineHub;
    private readonly OnlineRoomView onlineRoom;
    private readonly IMiniGame[] games;
    private readonly GamesLibrary library;
    private readonly AppSkin ui = new(AppPalettes.Games);
    private readonly ViewRouter<GameRoute> router;
    private readonly RouterDraw<GameRoute> drawView;
    private readonly Action back;
    private Spring pausedVeil = new(0f);
    private Rect screenRect;
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private IMiniGame? currentGame;
    private int featuredIndex;
    private readonly string[] countLabels;
    private float frameSeconds;
    public string Id => "games";
    public string DisplayName => Loc.T(L.Apps.Games);
    public string Glyph => ">";
    public int BadgeCount => 0;

    public GamesApp(GameStatsStore stats, GameData gameData, ITextureProvider textures,
        Core.Coins.CoinStore coins, Core.Coins.CoinGameSessionTracker coinSessions,
        GameRoomsStore gameRooms)
    {
        this.stats = stats;
        this.coins = coins;
        this.coinSessions = coinSessions;
        this.gameRooms = gameRooms;
        onlineHub = new OnlineHub(gameRooms, OpenOnlineRoom);
        onlineRoom = new OnlineRoomView(gameRooms);
        games = new IMiniGame[]
        {
            new SweeperApp(), new PairsApp(), new GemSwapApp(), new TetrisApp(), new Twenty48App(),
            new WaterSortApp(), new BreakoutApp(), new BubbleShooterApp(), new NonogramApp(), new FlowApp(),
            new SolitaireApp(), new SimonApp(), new FlapApp(), new ReversiApp(), new WhackApp(), new SnakeApp(),
            new SudokuApp(), new ChessApp(), new StackApp(), new CrystalDropApp(), new BeatApp(), new BladeApp(),
            new TriviaApp(gameData, textures), new SkyfallApp(), new InvadersApp(), new CapManApp(), new HopApp(), new SquadronApp(), new DoomApp(), new WordRunApp(gameData),
        };
        library = new GamesLibrary(games, stats);
        countLabels = new string[library.Entries.Length + 1];
        RebuildLayout();
        router = new ViewRouter<GameRoute>(GameRoute.Launcher);
        drawView = DrawView;
        back = () => router.Pop();
    }

    private void RebuildLayout()
    {
        featuredIndex = GameStatsStore.TodayIndex * FeaturedStep % games.Length;
        var serverFeatured = coins.Wallet?.FeaturedGameId;
        if (!string.IsNullOrEmpty(serverFeatured))
        {
            for (var index = 0; index < games.Length; index++)
            {
                if (string.Equals(games[index].Id, serverFeatured, StringComparison.Ordinal))
                {
                    featuredIndex = index;
                    break;
                }
            }
        }

        stats.DailyGameId = games[featuredIndex].Id;
        library.Rebuild();
    }

    public void OnOpened()
    {
        router.Reset();
        RebuildLayout();
        ResetLauncher();
    }

    public void OnClosed()
    {
        CloseCurrentGame();
        gameRooms.Exit();
        AppLandscape.Release(Id);
        router.Reset();
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
        screenRect = SceneChrome.ScreenFrom(context.Content, theme, UiScale.Current);
        if (router.IsTransitioning || router.Current != GameRoute.Playing)
        {
            ui.Backdrop(screenRect);
            GameScene.Ambient(ImGui.GetWindowDrawList(), screenRect, games[featuredIndex].Accent);
        }

        var appArea = SceneChrome.AppAreaFrom(context.Content, theme, UiScale.Current);
        router.Draw(appArea, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        if (!router.IsTransitioning && router.Current == GameRoute.Launcher && currentGame is not null)
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

    private void DrawView(GameRoute route, Rect area, int depth)
    {
        var context = new PhoneContext(area, theme, navigation);
        if (route == GameRoute.Playing)
        {
            ImGui.GetWindowDrawList().AddRectFilled(area.Min, area.Max, ImGui.GetColorU32(theme.AppBackground));
            DrawActiveGame(context);
            return;
        }

        if (route == GameRoute.OnlineHub)
        {
            PaintViewBackdrop(area);
            onlineHub.Draw(context, back, ui);
            return;
        }

        if (route == GameRoute.OnlineRoom)
        {
            ImGui.GetWindowDrawList().AddRectFilled(area.Min, area.Max, ImGui.GetColorU32(theme.AppBackground));
            SyncOnlineRoomLandscape();
            onlineRoom.Draw(context, LeaveOnlineRoom, ui,
                AppLandscape.Held(Id) && context.Content.IsLandscape());
            return;
        }

        PaintViewBackdrop(area);
        DrawLauncher(area);
    }

    private void PaintViewBackdrop(Rect area)
    {
        ui.Body(area);
        GameScene.Ambient(ImGui.GetWindowDrawList(), screenRect, games[featuredIndex].Accent);
    }

    private void OpenOnlineHub(string preferredKind)
    {
        onlineHub.Enter(preferredKind);
        router.Push(GameRoute.OnlineHub);
    }

    private void OpenOnlineRoom(string roomId, string gameKind)
    {
        stats.MarkPlayed(GamesLibrary.OnlineEntryId(gameKind));
        onlineRoom.Enter();
        router.Push(GameRoute.OnlineRoom);
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
        var scale = UiScale.Current;
        var content = context.Content;
        var landscape = game.WantsLandscape && AppLandscape.Held(Id) && content.IsLandscape();
        Rect body;
        if (landscape)
        {
            body = content;
        }
        else
        {
            var chip = BuildCoinSessionChip();
            if (chip.Visible)
            {
                AppHeader.Draw(context, "games.header.title", game.Title, CoinChipReserve * scale, back);
                DrawCoinSessionChip(chip, content, context.Theme, scale);
            }
            else
            {
                AppHeader.Draw(context, game.Title, back);
            }

            body = new Rect(new Vector2(content.Min.X, content.Min.Y + HeaderHeight * scale), content.Max);
        }

        using (AppSurface.Begin(body))
        {
            var attentive = GameFocus.Active;
            var frameSeconds = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
            game.Draw(new GameContext(body, context.Theme, stats, attentive ? frameSeconds : 0f));
            pausedVeil.Step(!attentive && game.RunsOnAClock ? 1f : 0f, PausedFadeSeconds, frameSeconds);
            DrawPausedVeil(body, context.Theme);
            if (landscape)
            {
                DrawLandscapeBack(body, context.Theme, scale);
            }
        }
    }

    private void DrawLandscapeBack(Rect body, PhoneTheme theme, float scale)
    {
        var radius = LandscapeBackRadius * scale;
        var center = body.Min + new Vector2(radius + LandscapeBackInset * scale, radius + LandscapeBackInset * scale);
        if (GameHud.LandscapeBack(center, radius, theme))
        {
            back();
        }
    }

    private CoinSessionChip BuildCoinSessionChip()
    {
        var seconds = coinSessions.OpenSessionSeconds;
        if (seconds < 0)
        {
            return default;
        }

        var wallet = coins.Wallet;
        if (wallet is not null && RuleExhausted(wallet, "game.session") && RuleExhausted(wallet, "game.deep"))
        {
            return default;
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

    private static void DrawCoinSessionChip(in CoinSessionChip chip, Rect content, PhoneTheme theme, float scale)
    {
        var accent = AppAccents.For("coin");
        var ringRadius = CoinChipRingRadius * scale;
        var thickness = Metrics.Stroke.Ring * scale;
        var rowCenterY = content.Min.Y + HeaderHeight * scale * 0.5f;
        var right = content.Max.X - Metrics.Space.Md * scale;
        var textSize = chip.Label.Length > 0 ? Typography.Measure(chip.Label, TextStyles.Caption1) : Vector2.Zero;
        var labelSpan = chip.Label.Length > 0 ? textSize.X + CoinChipGap * scale : 0f;
        var ringCenter = new Vector2(right - labelSpan - ringRadius, rowCenterY);
        ProgressRing.Track(ringCenter, ringRadius, thickness, Palette.WithAlpha(accent, 0.28f));
        ProgressRing.Fill(ringCenter, ringRadius, thickness, chip.Fraction, accent);
        if (chip.Qualified)
        {
            ProgressRing.CenterIcon(ImGui.GetWindowDrawList(), ringCenter, FontAwesomeIcon.Check, accent,
                ringRadius * 1.05f);
        }
        else
        {
            CurrencyGlyph.Draw(ImGui.GetWindowDrawList(), CurrencyKind.Coins, ringCenter, ringRadius * 1.1f);
        }

        var hoverHalfHeight = HeaderHeight * scale * 0.35f;
        var hoverRect = new Rect(new Vector2(ringCenter.X - ringRadius - thickness, rowCenterY - hoverHalfHeight),
            new Vector2(right, rowCenterY + hoverHalfHeight));
        HoverTooltip.Show(CoinChipTooltipId, hoverRect, CoinChipHint(chip));
        if (chip.Label.Length == 0)
        {
            return;
        }

        Typography.DrawCentered(new Vector2(right - textSize.X * 0.5f, rowCenterY), chip.Label, theme.TextStrong,
            TextStyles.Caption1);
    }

    private static string CoinChipHint(in CoinSessionChip chip)
    {
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

    private void DrawPausedVeil(Rect body, PhoneTheme theme)
    {
        var alpha = Math.Clamp(pausedVeil.Value, 0f, 1f);
        if (alpha <= 0.01f)
        {
            return;
        }

        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(body.Min, body.Max,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.72f * alpha)));
        var center = body.Center;
        Typography.DrawCentered(drawList, new Vector2(center.X, center.Y - 12f * scale), Loc.T(L.Games.Paused),
            new Vector4(1f, 1f, 1f, alpha), TextStyles.Title2);
        Typography.DrawWrappedCentered(drawList, new Vector2(center.X, center.Y + 14f * scale),
            Loc.T(L.Games.PausedHint), new Vector4(1f, 1f, 1f, 0.7f * alpha), TextStyles.Subheadline,
            MathF.Min(body.Width - 48f * scale, 260f * scale));
    }

    private void OpenGame(IMiniGame game)
    {
        currentGame = game;
        game.Open();
        coinSessions.GameOpened(game.Id);
        stats.MarkPlayed(game.Id);
        if (game.WantsLandscape)
        {
            AppLandscape.Request(Id);
        }

        router.Push(GameRoute.Playing);
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
