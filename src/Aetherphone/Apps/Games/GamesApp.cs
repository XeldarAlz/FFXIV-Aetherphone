using System.Numerics;
using Aetherphone.Apps.Games.Breakout;
using Aetherphone.Apps.Games.BubbleShooter;
using Aetherphone.Apps.Games.Flap;
using Aetherphone.Apps.Games.Flow;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.GemSwap;
using Aetherphone.Apps.Games.Nonogram;
using Aetherphone.Apps.Games.Pairs;
using Aetherphone.Apps.Games.Reversi;
using Aetherphone.Apps.Games.Simon;
using Aetherphone.Apps.Games.Snake;
using Aetherphone.Apps.Games.Solitaire;
using Aetherphone.Apps.Games.Sweeper;
using Aetherphone.Apps.Games.Tetris;
using Aetherphone.Apps.Games.Twenty48;
using Aetherphone.Apps.Games.WaterSort;
using Aetherphone.Apps.Games.Whack;
using Aetherphone.Core;
using Aetherphone.Core.Analytics;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Games;

internal sealed class GamesApp : IPhoneApp
{
    private enum GameRoute : byte
    {
        Launcher,
        Playing,
    }

    private readonly struct Section
    {
        public readonly int Start;
        public readonly int Count;

        public Section(int start, int count)
        {
            Start = start;
            Count = count;
        }
    }

    private const float HeaderHeight = 42f;
    private const float HeroHeight = 168f;
    private const float SectionHeaderHeight = 30f;
    private const float GameRowHeight = 64f;
    private const int FeaturedStep = 5;
    private readonly GameStatsStore stats;
    private readonly IMiniGame[] games;
    private readonly int[] tileOrder;
    private readonly ViewRouter<GameRoute> router;
    private readonly RouterDraw<GameRoute> drawView;
    private readonly Action back;
    private Section[] sections = Array.Empty<Section>();
    private Spring heroScale = new(1f);
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private IMiniGame? currentGame;
    private DateTime gameStartedAt;
    private string? activeGameId;
    private int preGameBestScore;
    private int preGameBestTime;
    private int featuredIndex;
    private float entrance;
    public string Id => "games";
    public string DisplayName => Loc.T(L.Apps.Games);
    public string Glyph => ">";
    public int BadgeCount => 0;

    public GamesApp(GameStatsStore stats)
    {
        this.stats = stats;
        games = new IMiniGame[]
        {
            new SweeperApp(), new PairsApp(), new GemSwapApp(), new TetrisApp(), new Twenty48App(),
            new WaterSortApp(), new BreakoutApp(), new BubbleShooterApp(), new NonogramApp(), new FlowApp(),
            new SolitaireApp(), new SimonApp(), new FlapApp(), new ReversiApp(), new WhackApp(), new SnakeApp(),
        };
        tileOrder = new int[games.Length];
        RebuildLayout();
        router = new ViewRouter<GameRoute>(GameRoute.Launcher, Id);
        drawView = DrawView;
        back = () => router.Pop();
    }

    private void RebuildLayout()
    {
        BuildDisplayOrder();
        BuildSections();
        var day = (int)(DateTime.UtcNow.Ticks / TimeSpan.TicksPerDay);
        featuredIndex = day * FeaturedStep % games.Length;
    }

    private void BuildDisplayOrder()
    {
        var distinct = new string[games.Length];
        var distinctCount = 0;
        for (var index = 0; index < games.Length; index++)
        {
            var genre = games[index].Genre;
            var found = false;
            for (var search = 0; search < distinctCount; search++)
            {
                if (string.Equals(distinct[search], genre, StringComparison.Ordinal))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                distinct[distinctCount++] = genre;
            }
        }

        var cursor = 0;
        for (var section = 0; section < distinctCount; section++)
        {
            for (var index = 0; index < games.Length; index++)
            {
                if (!string.Equals(games[index].Genre, distinct[section], StringComparison.Ordinal))
                {
                    continue;
                }

                tileOrder[cursor++] = index;
            }
        }
    }

    private void BuildSections()
    {
        var count = 0;
        for (var index = 0; index < tileOrder.Length; index++)
        {
            if (index == 0 || !string.Equals(games[tileOrder[index]].Genre, games[tileOrder[index - 1]].Genre,
                    StringComparison.Ordinal))
            {
                count++;
            }
        }

        if (sections.Length != count)
        {
            sections = new Section[count];
        }

        var sectionIndex = 0;
        var start = 0;
        for (var index = 1; index <= tileOrder.Length; index++)
        {
            if (index == tileOrder.Length || !string.Equals(games[tileOrder[index]].Genre,
                    games[tileOrder[index - 1]].Genre, StringComparison.Ordinal))
            {
                sections[sectionIndex++] = new Section(start, index - start);
                start = index;
            }
        }
    }

    public void OnOpened()
    {
        router.Reset();
        RebuildLayout();
        entrance = 0f;
        heroScale.SnapTo(1f);
    }

    public void OnClosed()
    {
        CloseCurrentGame();
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
        router.Draw(context.Content, context.Theme.AppBackground, ImGui.GetIO().DeltaTime, drawView);
        if (!router.IsTransitioning && router.Current == GameRoute.Launcher && currentGame is not null)
        {
            CloseCurrentGame();
        }
    }

    private void DrawView(GameRoute route, Rect area, int depth)
    {
        var context = new PhoneContext(area, theme, navigation);
        if (route == GameRoute.Playing)
        {
            DrawActiveGame(context);
            return;
        }

        DrawLauncher(context);
    }

    private void DrawActiveGame(in PhoneContext context)
    {
        var game = currentGame!;
        AppHeader.Draw(context, game.Title, back);
        var scale = ImGuiHelpers.GlobalScale;
        var content = context.Content;
        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + HeaderHeight * scale), content.Max);
        using (AppSurface.Begin(body))
        {
            var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
            game.Draw(new GameContext(body, context.Theme, stats, deltaSeconds));
        }
    }

    private void DrawLauncher(in PhoneContext context)
    {
        AppHeader.Draw(context, DisplayName);
        var scale = ImGuiHelpers.GlobalScale;
        var content = context.Content;
        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + HeaderHeight * scale), content.Max);
        using (AppSurface.Begin(body))
        {
            var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
            entrance = GameJuice.Advance(entrance, deltaSeconds, 1.6f);
            var drawList = ImGui.GetWindowDrawList();
            var featured = games[featuredIndex];
            GameScene.Ambient(drawList, body, featured.Accent);
            var origin = ImGui.GetCursorScreenPos();
            var availableWidth = ImGui.GetContentRegionAvail().X;
            var y = origin.Y + Metrics.Space.Xxs * scale;
            var heroRect = new Rect(new Vector2(origin.X, y),
                new Vector2(origin.X + availableWidth, y + HeroHeight * scale));
            UiAnchors.Report("games.featured", heroRect);
            if (DrawHero(heroRect, featured, Easing.EaseOutCubic(entrance), deltaSeconds, scale))
            {
                OpenGame(featured);
            }

            y = heroRect.Max.Y;
            for (var sectionIndex = 0; sectionIndex < sections.Length; sectionIndex++)
            {
                var section = sections[sectionIndex];
                y += Metrics.Space.Xl * scale;
                Typography.Draw(new Vector2(origin.X + Metrics.Space.Xxs * scale, y),
                    games[tileOrder[section.Start]].Genre, theme.TextStrong, TextStyles.Title3);
                y += SectionHeaderHeight * scale;
                ImGui.SetCursorScreenPos(new Vector2(origin.X, y));
                if (sectionIndex == 0)
                {
                    UiAnchors.Report("games.library", new Rect(new Vector2(origin.X, y),
                        new Vector2(origin.X + availableWidth, y + section.Count * GameRowHeight * scale)));
                }

                var card = GroupCard.Begin(theme, section.Count, GameRowHeight);
                for (var item = 0; item < section.Count; item++)
                {
                    var gameIndex = tileOrder[section.Start + item];
                    if (DrawGameRow(card.NextRow(), games[gameIndex], scale))
                    {
                        OpenGame(games[gameIndex]);
                    }
                }

                card.End();
                y += section.Count * GameRowHeight * scale;
            }

            ImGui.SetCursorScreenPos(new Vector2(origin.X, y));
            ImGui.Dummy(new Vector2(availableWidth, Metrics.Space.Lg * scale));
        }
    }

    private bool DrawGameRow(Rect row, IMiniGame game, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hitMin = new Vector2(row.Min.X - Metrics.Space.Lg * scale, row.Min.Y);
        var hitMax = new Vector2(row.Max.X + Metrics.Space.Lg * scale, row.Max.Y);
        var hovered = ImGui.IsMouseHoveringRect(hitMin, hitMax);
        if (hovered)
        {
            drawList.AddRectFilled(hitMin, hitMax, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.045f)),
                Metrics.Radius.Md * scale);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var accent = game.Accent;
        var iconSize = 42f * scale;
        var iconCenter = new Vector2(row.Min.X + iconSize * 0.5f, row.Center.Y);
        var iconHalf = new Vector2(iconSize * 0.5f, iconSize * 0.5f);
        var iconMin = iconCenter - iconHalf;
        var iconMax = iconCenter + iconHalf;
        var iconRounding = iconSize * Metrics.Radius.TileFactor;
        Squircle.FillVerticalGradient(drawList, iconMin, iconMax, iconRounding,
            ImGui.GetColorU32(GamePalette.Lighten(accent, 0.16f)),
            ImGui.GetColorU32(GamePalette.Darken(accent, 0.18f)));
        Squircle.Stroke(drawList, iconMin, iconMax, iconRounding,
            ImGui.GetColorU32(GamePalette.Lighten(accent, 0.4f) with { W = 0.35f }), Metrics.Stroke.Hairline * scale);
        var ink = new Vector4(0.99f, 0.99f, 1f, 1f);
        if (!AppIconArt.TryDraw(game.Id, iconCenter, iconSize * 0.62f, ink, accent))
        {
            Typography.DrawCentered(iconCenter, game.Title, ink, TextStyles.Caption2);
        }

        var textX = iconMax.X + Metrics.Space.Md * scale;
        Typography.Draw(new Vector2(textX, row.Center.Y - 17f * scale), game.Title, theme.TextStrong,
            TextStyles.Headline);
        var best = StatValue(game.Id);
        var subtitle = string.IsNullOrEmpty(best) ? game.Genre : $"{Loc.T(L.Games.Best)} · {best}";
        Typography.Draw(new Vector2(textX, row.Center.Y + 2f * scale), subtitle, theme.TextMuted, TextStyles.Footnote);
        DrawPlayPill(drawList, row, accent, scale);
        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static void DrawPlayPill(ImDrawListPtr drawList, Rect row, Vector4 accent, float scale)
    {
        var label = Loc.T(L.Games.Play);
        var labelSize = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        var pillWidth = MathF.Max(labelSize.X + 24f * scale, 54f * scale);
        var pillHeight = 26f * scale;
        var pillMax = new Vector2(row.Max.X, row.Center.Y + pillHeight * 0.5f);
        var pillMin = new Vector2(row.Max.X - pillWidth, row.Center.Y - pillHeight * 0.5f);
        var pillHovered = ImGui.IsMouseHoveringRect(pillMin, pillMax);
        Squircle.Fill(drawList, pillMin, pillMax, pillHeight * 0.5f,
            ImGui.GetColorU32(accent with { W = pillHovered ? 0.32f : 0.18f }));
        Typography.DrawCentered((pillMin + pillMax) * 0.5f, label, GamePalette.Lighten(accent, 0.38f),
            TextStyles.FootnoteEmphasized);
    }

    private bool DrawHero(Rect rect, IMiniGame game, float phase, float deltaSeconds, float scale)
    {
        if (phase <= 0f)
        {
            return false;
        }

        var drawList = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var target = pressed ? 0.975f :
            hovered ? 1.012f : 1f;
        var grow = heroScale.Step(target, 0.085f, deltaSeconds) * (0.94f + 0.06f * Easing.EaseOutBack(phase));
        var lift = (1f - Easing.EaseOutCubic(phase)) * 18f * scale;
        var center = rect.Center + new Vector2(0f, lift);
        var half = rect.Size * 0.5f * grow;
        var min = center - half;
        var max = center + half;
        var height = max.Y - min.Y;
        var rounding = 28f * scale;
        var accent = game.Accent;
        Elevation.Floating(drawList, min, max, rounding, scale, phase * (hovered ? 1f : 0.8f));
        var topTone = ImGui.GetColorU32(GamePalette.Lighten(accent, 0.34f));
        var bottomTone = ImGui.GetColorU32(GamePalette.Darken(accent, 0.48f));
        Squircle.FillVerticalGradient(drawList, min, max, rounding, topTone, bottomTone);
        drawList.PushClipRect(min, max, true);
        DrawHeroGlow(drawList, min, max, accent);
        DrawSheen(drawList, min, max, Pulse.Phase(5600.0), 0.05f, scale);
        drawList.PopClipRect();
        var iconCenter = new Vector2(min.X + height * 0.40f,
            center.Y + MathF.Sin((float)ImGui.GetTime() * 1.6f) * 3f * scale);
        var iconSize = height * 0.52f;
        drawList.AddCircleFilled(iconCenter + new Vector2(0f, iconSize * 0.10f), iconSize * 0.52f,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.22f)));
        ProgressRing.Glow(iconCenter, iconSize * 0.5f, GamePalette.Lighten(accent, 0.45f), hovered ? 0.9f : 0.55f);
        var ink = new Vector4(0.99f, 0.99f, 1f, 1f);
        if (!AppIconArt.TryDraw(game.Id, iconCenter, iconSize, ink, GamePalette.Darken(accent, 0.16f)))
        {
            Typography.DrawCentered(iconCenter, game.Title, ink, TextStyles.Title1);
        }

        var textX = min.X + height * 0.72f;
        Typography.Draw(new Vector2(textX, center.Y - 34f * scale),
            Loc.Culture.TextInfo.ToUpper(Loc.T(L.Games.Featured)),
            GamePalette.Lighten(accent, 0.62f), TextStyles.Caption2);
        Typography.Draw(new Vector2(textX, center.Y - 18f * scale), game.Title, ink, TextStyles.Title2);
        Typography.Draw(new Vector2(textX, center.Y + 8f * scale), game.Genre, ink with { W = 0.72f },
            TextStyles.Footnote);
        var playCenter = new Vector2(textX + 34f * scale, center.Y + 38f * scale);
        var playClicked = GameHud.Button(playCenter, new Vector2(68f * scale, 28f * scale), Loc.T(L.Games.Play),
            new Vector4(0.97f, 0.97f, 0.99f, 1f), theme);
        Squircle.Stroke(drawList, min, max, rounding,
            ImGui.GetColorU32(GamePalette.Lighten(accent, 0.4f) with { W = 0.42f }), 1f * scale);
        drawList.AddLine(new Vector2(min.X + rounding, min.Y + 1.5f * scale),
            new Vector2(max.X - rounding, min.Y + 1.5f * scale), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.25f)),
            1f * scale);
        var best = StatValue(game.Id);
        if (!string.IsNullOrEmpty(best))
        {
            DrawBestChip(drawList, new Vector2(max.X - 11f * scale, min.Y + 11f * scale), best, scale);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (playClicked)
        {
            return true;
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static void DrawHeroGlow(ImDrawListPtr drawList, Vector2 min, Vector2 max, Vector4 accent)
    {
        var size = max - min;
        var topLeft = new Vector2(min.X + size.X * 0.16f, min.Y + size.Y * 0.14f);
        var bottomRight = new Vector2(min.X + size.X * 0.86f, min.Y + size.Y * 0.95f);
        for (var layer = 3; layer >= 1; layer--)
        {
            var alphaScale = (4 - layer) * 0.34f;
            drawList.AddCircleFilled(topLeft, size.Y * (0.24f + layer * 0.16f),
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.045f * alphaScale)));
            drawList.AddCircleFilled(bottomRight, size.Y * (0.30f + layer * 0.20f),
                ImGui.GetColorU32(GamePalette.Lighten(accent, 0.55f) with { W = 0.06f * alphaScale }));
        }
    }

    private static void DrawSheen(ImDrawListPtr drawList, Vector2 min, Vector2 max, float sweep, float alpha,
        float scale)
    {
        var width = max.X - min.X;
        var band = 26f * scale;
        var sweepX = min.X + (width + band * 4f) * sweep - band * 2f;
        var skew = 18f * scale;
        drawList.AddQuadFilled(new Vector2(sweepX - band * 0.5f, max.Y), new Vector2(sweepX + skew - band * 0.5f, min.Y),
            new Vector2(sweepX + skew + band * 0.5f, min.Y), new Vector2(sweepX + band * 0.5f, max.Y),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)));
    }

    private static void DrawBestChip(ImDrawListPtr drawList, Vector2 topRight, string text, float scale)
    {
        var textSize = Typography.Measure(text, TextStyles.Caption1);
        var chipWidth = textSize.X + 14f * scale;
        var chipHeight = 18f * scale;
        var min = new Vector2(topRight.X - chipWidth, topRight.Y);
        var max = new Vector2(topRight.X, topRight.Y + chipHeight);
        Material.Frosted(drawList, min, max, chipHeight * 0.5f, scale);
        Typography.DrawCentered((min + max) * 0.5f, text, new Vector4(0.97f, 0.97f, 0.99f, 1f), TextStyles.Caption1);
    }

    private string StatValue(string gameId)
    {
        switch (gameId)
        {
            case "2048":
            case "match3":
            case "breakout":
            case "bubbles":
            case "simon":
            case "flap":
            case "whack":
            case "snake":
            case "tetris":
            {
                var best = stats.Get(gameId).BestScore;
                return best > 0 ? GameNumber.Label(best) : string.Empty;
            }
            case "watersort":
            case "flow":
            {
                var bestLevel = stats.Get(gameId).BestScore;
                return bestLevel > 0 ? $"{Loc.T(L.Games.Level)} {GameNumber.Label(bestLevel)}" : string.Empty;
            }
            case "memory":
            {
                var bestSeconds = stats.Get("memory").BestTimeSeconds;
                return bestSeconds > 0 ? TimeText.MinutesSeconds(bestSeconds) : string.Empty;
            }
            case "solitaire":
            {
                var bestSeconds = stats.Get("solitaire").BestTimeSeconds;
                return bestSeconds > 0 ? TimeText.MinutesSeconds(bestSeconds) : string.Empty;
            }
            case "minesweeper":
            {
                var bestSeconds = stats.Get("minesweeper.easy").BestTimeSeconds;
                return bestSeconds > 0 ? TimeText.MinutesSeconds(bestSeconds) : string.Empty;
            }
            case "nonogram":
            {
                var bestSeconds = stats.Get("nonogram.easy").BestTimeSeconds;
                return bestSeconds > 0 ? TimeText.MinutesSeconds(bestSeconds) : string.Empty;
            }
            case "reversi":
            {
                var wins = stats.Get("reversi").Streak;
                return wins > 0 ? GameNumber.Label(wins) : string.Empty;
            }
            default:
                return string.Empty;
        }
    }

    private void OpenGame(IMiniGame game)
    {
        currentGame = game;
        var gameStats = stats.Get(game.Id);
        preGameBestScore = gameStats.BestScore;
        preGameBestTime = gameStats.BestTimeSeconds;
        gameStartedAt = DateTime.UtcNow;
        activeGameId = game.Id;
        game.Open();
        Plugin.Analytics.Track(AnalyticsEvents.GameStarted(game.Id));
        router.Push(GameRoute.Playing);
    }

    private void CloseCurrentGame()
    {
        if (currentGame is null)
        {
            return;
        }

        currentGame.Close();

        if (activeGameId is not null)
        {
            var durationMs = (DateTime.UtcNow - gameStartedAt).TotalMilliseconds;
            var finalStats = stats.Get(activeGameId);
            var improved = finalStats.BestScore > preGameBestScore
                || (finalStats.BestTimeSeconds > 0 && (preGameBestTime == 0 || finalStats.BestTimeSeconds < preGameBestTime));
            Plugin.Analytics.Track(AnalyticsEvents.GameEnded(activeGameId, finalStats.BestScore, durationMs, improved));
            activeGameId = null;
        }

        currentGame = null;
    }
}
