using System.Numerics;
using Aetherphone.Apps.Games.Breakout;
using Aetherphone.Apps.Games.BubbleShooter;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.GemSwap;
using Aetherphone.Apps.Games.Pairs;
using Aetherphone.Apps.Games.Sweeper;
using Aetherphone.Apps.Games.Twenty48;
using Aetherphone.Apps.Games.WaterSort;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Games;

internal sealed class GamesApp : IPhoneApp
{
    private const int Columns = 2;

    private const float HeaderHeight = 42f;

    private readonly GameStatsStore stats;

    private readonly IMiniGame[] games;

    private readonly Spring[] cardScale;

    private IMiniGame? currentGame;

    public string Id => "games";

    public string DisplayName => Loc.T(L.Apps.Games);

    public string Glyph => ">";

    public Vector4 Accent => new(0.32f, 0.78f, 0.50f, 1f);

    public int BadgeCount => 0;

    public GamesApp(GameStatsStore stats)
    {
        this.stats = stats;
        games = new IMiniGame[]
        {
            new SweeperApp(),
            new PairsApp(),
            new GemSwapApp(),
            new Twenty48App(),
            new WaterSortApp(),
            new BreakoutApp(),
            new BubbleShooterApp(),
        };

        cardScale = new Spring[games.Length];
        for (var index = 0; index < cardScale.Length; index++)
        {
            cardScale[index] = new Spring(1f);
        }
    }

    public void OnOpened()
    {
    }

    public void OnClosed()
    {
        CloseCurrentGame();
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
        if (currentGame is not null)
        {
            DrawActiveGame(context);
        }
        else
        {
            DrawLauncher(context);
        }
    }

    private void DrawActiveGame(in PhoneContext context)
    {
        var game = currentGame!;
        AppHeader.Draw(context, game.Title, CloseCurrentGame);

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
            var sidePadding = 16f * scale;
            var topPadding = 10f * scale;
            var bottomPadding = 14f * scale;
            var spacing = 14f * scale;
            var rowCount = (games.Length + Columns - 1) / Columns;

            var availableWidth = body.Width - sidePadding * 2f;
            var cardWidth = (availableWidth - spacing * (Columns - 1)) / Columns;

            var availableHeight = body.Height - topPadding - bottomPadding;
            var cardHeight = (availableHeight - spacing * (rowCount - 1)) / rowCount;
            cardHeight = MathF.Min(cardHeight, cardWidth * 1.34f);

            var gridHeight = cardHeight * rowCount + spacing * (rowCount - 1);
            var startX = body.Min.X + sidePadding;
            var startY = body.Min.Y + topPadding + MathF.Max(0f, (availableHeight - gridHeight) * 0.5f);

            for (var index = 0; index < games.Length; index++)
            {
                var column = index % Columns;
                var row = index / Columns;
                var tilesInRow = Math.Min(Columns, games.Length - row * Columns);
                var rowOffset = (Columns - tilesInRow) * (cardWidth + spacing) * 0.5f;
                var cardMin = new Vector2(startX + rowOffset + column * (cardWidth + spacing), startY + row * (cardHeight + spacing));
                var cardRect = new Rect(cardMin, cardMin + new Vector2(cardWidth, cardHeight));

                if (DrawCard(cardRect, games[index], index, deltaSeconds, context.Theme, scale))
                {
                    OpenGame(games[index]);
                }
            }
        }
    }

    private bool DrawCard(Rect rect, IMiniGame game, int index, float deltaSeconds, PhoneTheme theme, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);

        var target = pressed ? 0.965f : hovered ? 1.035f : 1f;
        var grow = cardScale[index].Step(target, 0.085f, deltaSeconds);

        var center = rect.Center;
        var half = rect.Size * 0.5f * grow;
        var min = center - half;
        var max = center + half;
        var height = max.Y - min.Y;
        var rounding = 26f * scale;
        var inset = rounding;

        var accent = game.Accent;
        var baseColor = GamePalette.Darken(accent, 0.16f);

        Elevation.Floating(drawList, min, max, rounding, scale, hovered ? 1f : 0.7f);
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(baseColor));

        var clear = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0f));
        var gloss = ImGui.GetColorU32(GamePalette.Lighten(accent, 0.36f) with { W = 0.55f });
        drawList.AddRectFilledMultiColor(
            new Vector2(min.X + inset, min.Y + 1f * scale),
            new Vector2(max.X - inset, min.Y + height * 0.52f),
            gloss, gloss, clear, clear);

        var scrim = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.48f));
        drawList.AddRectFilledMultiColor(
            new Vector2(min.X + inset, min.Y + height * 0.54f),
            new Vector2(max.X - inset, max.Y - 1f * scale),
            clear, clear, scrim, scrim);

        var iconCenter = new Vector2(center.X, min.Y + height * 0.40f);
        if (hovered)
        {
            ProgressRing.Glow(iconCenter, height * 0.24f, GamePalette.Lighten(accent, 0.45f), 0.6f);
        }

        var ink = new Vector4(0.99f, 0.99f, 1f, 1f);
        if (!AppIconArt.TryDraw(game.Id, iconCenter, height * 0.46f * grow, ink, baseColor))
        {
            Typography.DrawCentered(iconCenter, game.Title, ink, TextStyles.Title1);
        }

        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(GamePalette.Lighten(accent, 0.4f) with { W = 0.42f }), 1f * scale);
        drawList.AddLine(
            new Vector2(min.X + inset, min.Y + 1.5f * scale),
            new Vector2(max.X - inset, min.Y + 1.5f * scale),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.2f)), 1f * scale);

        Typography.DrawCentered(new Vector2(center.X, max.Y - height * 0.235f), game.Title, ink, TextStyles.Headline);
        Typography.DrawCentered(new Vector2(center.X, max.Y - height * 0.115f), game.Genre.ToUpperInvariant(), new Vector4(1f, 1f, 1f, 0.68f), TextStyles.Caption2);

        var best = StatValue(game.Id);
        if (!string.IsNullOrEmpty(best))
        {
            DrawBestChip(drawList, new Vector2(max.X - 9f * scale, min.Y + 9f * scale), best, scale);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
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
            {
                var best = stats.Get(gameId).BestScore;
                return best > 0 ? GameNumber.Label(best) : string.Empty;
            }

            case "watersort":
            {
                var bestLevel = stats.Get(gameId).BestScore;
                return bestLevel > 0 ? $"{Loc.T(L.Games.Level)} {GameNumber.Label(bestLevel)}" : string.Empty;
            }

            case "memory":
                return FormatTime(stats.Get("memory").BestTimeSeconds);

            case "minesweeper":
                return FormatTime(stats.Get("minesweeper.easy").BestTimeSeconds);

            default:
                return string.Empty;
        }
    }

    private static string FormatTime(int seconds)
    {
        if (seconds <= 0)
        {
            return string.Empty;
        }

        return $"{seconds / 60}:{seconds % 60:D2}";
    }

    private void OpenGame(IMiniGame game)
    {
        currentGame = game;
        game.Open();
    }

    private void CloseCurrentGame()
    {
        if (currentGame is null)
        {
            return;
        }

        currentGame.Close();
        currentGame = null;
    }
}
