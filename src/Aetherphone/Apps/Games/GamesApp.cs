using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Games;

internal sealed class GamesApp : IPhoneApp
{
    private const int Columns = 2;

    public string Id => "games";
    public string DisplayName => "Games";
    public string Glyph => ">";

    public Vector4 Accent => new(0.32f, 0.78f, 0.50f, 1f);
    public int BadgeCount => 0;

    private readonly IPhoneApp[] games;
    private IPhoneApp? currentGame;
    private readonly GameBackNavigator backNav;
    private PhoneTheme frameTheme = PhoneTheme.Default;

    public GamesApp()
    {
        games = new IPhoneApp[]
        {
            new MinesweeperApp(),
            new MemoryMatchApp(),
            new Match3App(),
            new Twenty48App(),
        };
        backNav = new GameBackNavigator(this);
    }

    public void OnOpened()
    {
    }

    public void OnClosed()
    {
        if (currentGame is not null)
        {
            currentGame.OnClosed();
            currentGame = null;
        }
    }

    public void Draw(in PhoneContext context)
    {
        frameTheme = context.Theme;

        if (currentGame is not null)
        {
            DrawGame(context);
        }
        else
        {
            DrawGameList(context);
        }
    }

    public void Dispose()
    {
        if (currentGame is not null)
        {
            currentGame.OnClosed();
            currentGame = null;
        }
    }

    private void DrawGameList(in PhoneContext context)
    {
        AppHeader.Draw(context, DisplayName);

        var body = GameCommon.LayoutBelowHeader(context.Content);

        using (AppSurface.Begin(body))
        {
            var contentMax = new Vector2(body.Min.X, body.Min.Y) + ImGui.GetContentRegionAvail();
            var surface = new Rect(new Vector2(body.Min.X, body.Min.Y), contentMax);
            var scale = ImGuiHelpers.GlobalScale;

            var pad = 16f * scale;
            var availableWidth = surface.Width - pad * 2f;
            var cardSpacing = 12f * scale;
            var cardWidth = (availableWidth - cardSpacing) / Columns;
            var cardHeight = cardWidth * 1.15f;

            var startX = surface.Min.X + pad;
            var startY = surface.Min.Y + 8f * scale;

            for (var index = 0; index < games.Length; index++)
            {
                var column = index % Columns;
                var row = index / Columns;
                var cardCenter = new Vector2(
                    startX + cardWidth * 0.5f + column * (cardWidth + cardSpacing),
                    startY + cardHeight * 0.5f + row * (cardHeight + cardSpacing));

                if (DrawGameCard(cardCenter, cardWidth, cardHeight, games[index], scale))
                {
                    OpenGame(games[index]);
                }
            }
        }
    }

    private bool DrawGameCard(Vector2 center, float width, float height, IPhoneApp game, float scale)
    {
        var half = new Vector2(width * 0.5f, height * 0.5f);
        var min = center - half;
        var max = center + half;
        var rounding = 16f * scale;

        var hovered = GameCommon.HitTest(min, max);
        var fill = hovered ? Palette.Mix(game.Accent, frameTheme.TextStrong, 0.14f) : game.Accent;

        GameCommon.FillRect(min, max, fill, rounding);

        var artCenter = new Vector2(center.X, center.Y - 6f * scale);
        if (!AppIconArt.TryDraw(game.Id, artCenter, height * 0.7f, frameTheme.TextStrong, fill))
        {
            var glyphSize = Typography.Measure(game.Glyph, 2f);
            var glyphScale = glyphSize.Y > 0f ? height * 0.4f / glyphSize.Y : 1f;
            Typography.DrawCentered(artCenter, game.Glyph, frameTheme.TextStrong, glyphScale);
        }

        Typography.DrawCentered(new Vector2(center.X, max.Y - 18f * scale), game.DisplayName, frameTheme.TextStrong, 1.3f, FontWeight.SemiBold);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void DrawGame(in PhoneContext context)
    {
        var game = currentGame!;
        var gameContext = new PhoneContext(context.Content, frameTheme, backNav);
        game.Draw(gameContext);
    }

    private void OpenGame(IPhoneApp game)
    {
        currentGame = game;
        game.OnOpened();
    }

    internal void CloseCurrentGame()
    {
        if (currentGame is not null)
        {
            currentGame.OnClosed();
            currentGame = null;
        }
    }

    private sealed class GameBackNavigator : INavigator
    {
        private readonly GamesApp owner;

        public bool AtHome => true;

        public GameBackNavigator(GamesApp owner)
        {
            this.owner = owner;
        }

        public void OpenApp(IPhoneApp app)
        {
        }

        public void Open(string appId)
        {
        }

        public void Back()
        {
            owner.CloseCurrentGame();
        }

        public void GoHome()
        {
            owner.CloseCurrentGame();
        }
    }
}
