using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.GemSwap;
using Aetherphone.Apps.Games.Pairs;
using Aetherphone.Apps.Games.Sweeper;
using Aetherphone.Apps.Games.Twenty48;
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
            var padding = 18f * scale;
            var spacing = 14f * scale;
            var availableWidth = body.Width - padding * 2f;
            var cardWidth = (availableWidth - spacing * (Columns - 1)) / Columns;
            var cardHeight = cardWidth * 1.12f;

            var startX = body.Min.X + padding;
            var startY = body.Min.Y + 12f * scale;

            for (var index = 0; index < games.Length; index++)
            {
                var column = index % Columns;
                var row = index / Columns;
                var cardMin = new Vector2(startX + column * (cardWidth + spacing), startY + row * (cardHeight + spacing));
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

        var target = pressed ? 0.96f : hovered ? 1.04f : 1f;
        var grow = cardScale[index].Step(target, 0.09f, deltaSeconds);

        var center = rect.Center;
        var half = rect.Size * 0.5f * grow;
        var min = center - half;
        var max = center + half;
        var rounding = 22f * scale;

        Elevation.Card(drawList, min, max, rounding, scale, hovered ? 1f : 0.8f);

        var accent = game.Accent;
        var top = GamePalette.Lighten(accent, 0.06f);
        var bottom = GamePalette.Darken(accent, 0.28f);
        drawList.AddRectFilledMultiColor(min, max, ImGui.GetColorU32(top), ImGui.GetColorU32(top), ImGui.GetColorU32(bottom), ImGui.GetColorU32(bottom));
        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(GamePalette.Lighten(accent, 0.3f) with { W = 0.5f }), 1f * scale);

        if (hovered)
        {
            ProgressRing.Glow(center, half.X * 0.7f, GamePalette.Lighten(accent, 0.4f), 0.5f);
        }

        var iconCenter = new Vector2(center.X, min.Y + half.Y * 0.72f);
        var ink = GamePalette.InkOn(accent);
        if (!AppIconArt.TryDraw(game.Id, iconCenter, rect.Height * 0.46f * grow, ink, accent))
        {
            Typography.DrawCentered(iconCenter, game.Title, ink, TextStyles.Title2);
        }

        Typography.DrawCentered(new Vector2(center.X, max.Y - 34f * scale), game.Title, ink, TextStyles.Headline);

        var caption = StatCaption(game.Id);
        if (!string.IsNullOrEmpty(caption))
        {
            Typography.DrawCentered(new Vector2(center.X, max.Y - 16f * scale), caption, ink with { W = 0.75f }, TextStyles.Caption2);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private string StatCaption(string gameId)
    {
        switch (gameId)
        {
            case "2048":
            case "match3":
            {
                var best = stats.Get(gameId).BestScore;
                return best > 0 ? $"{Loc.T(L.Games.Best)} {GameNumber.Label(best)}" : string.Empty;
            }

            case "memory":
                return FormatBestTime(stats.Get("memory").BestTimeSeconds);

            case "minesweeper":
                return FormatBestTime(stats.Get("minesweeper.easy").BestTimeSeconds);

            default:
                return string.Empty;
        }
    }

    private static string FormatBestTime(int seconds)
    {
        if (seconds <= 0)
        {
            return string.Empty;
        }

        return $"{Loc.T(L.Games.Best)} {seconds / 60}:{seconds % 60:D2}";
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
