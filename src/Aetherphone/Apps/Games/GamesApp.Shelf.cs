using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Games;

internal sealed partial class GamesApp
{
    private const string ShelfNavId = "games.shelf.nav";
    private const string SearchNavId = "games.search.nav";
    private const int SearchMaxLength = 40;
    private const float BrowseCardHeight = 84f;
    private const float BrowseIconSize = 22f;
    private const float BrowsePad = 14f;
    private const int BrowseColumns = 2;
    private const int BrowseTogether = (int)GameGenre.Friends;
    private const int BrowseAll = BrowseTogether + 1;
    private const int BrowseCount = BrowseAll + 1;

    private static readonly FontAwesomeIcon[] BrowseIcons =
    [
        FontAwesomeIcon.Gamepad, FontAwesomeIcon.Bolt, FontAwesomeIcon.PuzzlePiece, FontAwesomeIcon.Lightbulb,
        FontAwesomeIcon.Flag, FontAwesomeIcon.Chess, FontAwesomeIcon.UserFriends, FontAwesomeIcon.ThLarge,
    ];

    private static readonly string[] BrowseIds =
    [
        "games.browse.arcade", "games.browse.action", "games.browse.puzzle", "games.browse.brain",
        "games.browse.strategy", "games.browse.tabletop", "games.browse.together", "games.browse.all",
    ];

    private bool focusSearch;
    private string searchText = string.Empty;
    private string lastSearchText = string.Empty;

    private ReadOnlySpan<int> ShelfEntries(GamesShelf shelf) => shelf switch
    {
        GamesShelf.Latest => library.Latest,
        GamesShelf.All => library.Ordered,
        _ => library.Genre((GameGenre)shelf),
    };

    private string ShelfTitle(GamesShelf shelf) => shelf switch
    {
        GamesShelf.Latest => Loc.T(L.Games.ShelfLatest),
        GamesShelf.All => Loc.T(L.Games.LibraryHeading),
        _ => Loc.T(GameGenres.Label((GameGenre)shelf)),
    };

    private void DrawShelfPage(in PhoneContext context, GamesShelf shelf)
    {
        var navBar = AppHeader.BeginLargeTitle(context);
        using (ImRaii.PushId("games.shelf"))
        using (AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            entrance = GameJuice.Advance(entrance, frameSeconds, EntranceSpeed);
            var entries = ShelfEntries(shelf);
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var drawList = ImGui.GetWindowDrawList();
            Typography.Draw(drawList, origin, CountLabel(entries.Length), ui.MutedInk, TextStyles.Subheadline);
            var y = origin.Y + Typography.LineHeight(TextStyles.Subheadline) + Metrics.Space.Md * scale;
            var gridTop = y;
            y = DrawGrid(entries, origin.X, y, width, scale);
            GamesHubArt.ReportAnchor("games.shelf", new Rect(new Vector2(origin.X, gridTop),
                new Vector2(origin.X + width, y)));
            FinishPage(origin, width, y, scale);
        }

        AppHeader.EndLargeTitle(in navBar, context, ShelfNavId, ShelfTitle(shelf), NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty, TabTitle(tab), back);
    }

    private void DrawSearch(in PhoneContext context)
    {
        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var drawList = ImGui.GetWindowDrawList();
            var field = new Rect(origin, new Vector2(origin.X + width, origin.Y + GlassField.HeightUnits * scale));
            SearchBar.Surface(drawList, field, ControlInk.From(theme));
            GlassField.Search(drawList, field, "##gamesSearch", Loc.T(L.Games.SearchHint), ref searchText, theme,
                scale, SearchMaxLength, focusSearch);
            focusSearch = false;
            if (!string.Equals(searchText, lastSearchText, StringComparison.Ordinal))
            {
                lastSearchText = searchText;
                entrance = 0f;
            }

            entrance = GameJuice.Advance(entrance, frameSeconds, EntranceSpeed);
            var y = field.Max.Y + GamesHubArt.SectionGap * scale;
            var results = library.Search(searchText);
            if (results.Length > 0)
            {
                GamesHubArt.Section(drawList, ui, origin.X, y, width, CountLabel(results.Length), string.Empty,
                    string.Empty);
                y += GamesHubArt.SectionHeight * scale;
                y = DrawGrid(results, origin.X, y, width, scale);
            }
            else if (searchText.AsSpan().Trim().Length > 0)
            {
                var empty = new Rect(new Vector2(origin.X, y), new Vector2(origin.X + width, navBar.Body.Max.Y));
                if (GamesHubArt.StateScreen(drawList, ui, empty, FontAwesomeIcon.Search, Loc.T(L.Games.SearchEmpty),
                        Loc.T(L.Games.SearchEmptyHint), Loc.T(L.GamesHub.ClearSearch), "games.search.clear"))
                {
                    searchText = string.Empty;
                    focusSearch = true;
                }

                y = empty.Max.Y;
            }
            else
            {
                GamesHubArt.Section(drawList, ui, origin.X, y, width, Loc.T(L.GamesHub.Browse), string.Empty,
                    string.Empty);
                y += GamesHubArt.SectionHeight * scale;
                y = DrawBrowse(origin.X, y, width, scale);
            }

            FinishPage(origin, width, y, scale);
        }

        AppHeader.EndLargeTitle(in navBar, context, SearchNavId, Loc.T(L.Common.Search), NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty);
    }

    private float DrawBrowse(float left, float y, float width, float scale)
    {
        var gap = TileGap * scale;
        var cardWidth = (width - gap * (BrowseColumns - 1)) / BrowseColumns;
        var cardHeight = BrowseCardHeight * scale;
        var top = y;
        var tapped = -1;
        var shown = 0;
        for (var index = 0; index < BrowseCount; index++)
        {
            if (BrowseCountOf(index) == 0)
            {
                continue;
            }

            var column = shown % BrowseColumns;
            var row = shown / BrowseColumns;
            shown++;
            var min = new Vector2(left + column * (cardWidth + gap), y + row * (cardHeight + gap));
            if (DrawBrowseCard(new Rect(min, min + new Vector2(cardWidth, cardHeight)), index, scale))
            {
                tapped = index;
            }
        }

        var rows = (shown + BrowseColumns - 1) / BrowseColumns;
        var bottom = y + rows * (cardHeight + gap) - gap;
        GamesHubArt.ReportAnchor("games.browse", new Rect(new Vector2(left, top), new Vector2(left + width, bottom)));
        if (tapped == BrowseTogether)
        {
            OpenOnlineHub(string.Empty);
        }
        else if (tapped == BrowseAll)
        {
            router.Push(GamesRoute.ShelfOf(GamesShelf.All));
        }
        else if (tapped >= 0)
        {
            router.Push(GamesRoute.ShelfOf((GamesShelf)tapped));
        }

        return bottom;
    }

    private bool DrawBrowseCard(Rect rect, int index, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(BrowseIds[index], pressed, Motion.PressScaleCard);
        var half = rect.Size * 0.5f * press;
        var min = rect.Center - half;
        var max = rect.Center + half;
        var rounding = Metrics.Radius.Widget * scale;
        var accent = BrowseAccent(index);
        Squircle.FillVerticalGradient(drawList, min, max, rounding,
            ImGui.GetColorU32(GamePalette.Lighten(accent, hovered ? 0.20f : 0.12f)),
            ImGui.GetColorU32(GamePalette.Darken(accent, 0.38f)));
        drawList.PushClipRect(min, max, true);
        var watermark = new Vector2(max.X - BrowsePad * scale, max.Y - BrowsePad * 0.5f * scale);
        ProgressRing.CenterIcon(drawList, watermark, BrowseIcons[index], new Vector4(1f, 1f, 1f, 0.16f),
            (max.Y - min.Y) * 0.7f);
        drawList.PopClipRect();
        var pad = BrowsePad * scale;
        ProgressRing.CenterIcon(drawList,
            new Vector2(min.X + pad + BrowseIconSize * scale * 0.5f, min.Y + pad + BrowseIconSize * scale * 0.5f),
            BrowseIcons[index], HeroInk, BrowseIconSize * scale);
        var textWidth = MathF.Max(1f, max.X - min.X - pad * 2f);
        var subtitleHeight = Typography.LineHeight(TextStyles.Footnote);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var subtitleY = max.Y - pad - subtitleHeight;
        Typography.Draw(drawList, new Vector2(min.X + pad, subtitleY - titleHeight),
            Typography.FitText(BrowseTitle(index), textWidth, TextStyles.Headline), HeroInk, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(min.X + pad, subtitleY),
            Typography.FitText(CountLabel(BrowseCountOf(index)), textWidth, TextStyles.Footnote),
            HeroInk with { W = 0.78f }, TextStyles.Footnote);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private Vector4 BrowseAccent(int index)
    {
        if (index == BrowseTogether)
        {
            return library.Accent(library.Genre(GameGenre.Friends)[0]);
        }

        if (index == BrowseAll)
        {
            return ui.Accent;
        }

        var entries = library.Genre((GameGenre)index);
        return entries.Length > 0 ? library.Accent(entries[0]) : ui.Accent;
    }

    private string BrowseTitle(int index)
    {
        if (index == BrowseTogether)
        {
            return Loc.T(L.Games.OnlineTitle);
        }

        return index == BrowseAll ? Loc.T(L.Games.LibraryHeading) : Loc.T(GameGenres.Label((GameGenre)index));
    }

    private int BrowseCountOf(int index)
    {
        if (index == BrowseTogether)
        {
            return library.Genre(GameGenre.Friends).Length;
        }

        return index == BrowseAll ? library.Ordered.Length : library.Genre((GameGenre)index).Length;
    }
}
