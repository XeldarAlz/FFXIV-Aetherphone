using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games;

internal sealed partial class GamesApp
{
    private const string HomeNavId = "games.home.nav";
    private const float HeroHeight = 196f;
    private const float ShelfTileWidth = 108f;
    private const float GridMinTileWidth = 104f;
    private const float TileGap = 10f;
    private const float FriendsCardHeight = 88f;
    private const float EntranceSpeed = 1.6f;
    private const int MinColumns = 3;
    private const int MaxColumns = 6;
    private const int ShelfGenreCount = (int)GameGenre.Friends;

    private static readonly string[] GenreRailIds =
    [
        "##games.rail.arcade", "##games.rail.action", "##games.rail.puzzle", "##games.rail.brain",
        "##games.rail.strategy", "##games.rail.tabletop",
    ];

    private static readonly string[] GenreSeeAllIds =
    [
        "games.seeAll.arcade", "games.seeAll.action", "games.seeAll.puzzle", "games.seeAll.brain",
        "games.seeAll.strategy", "games.seeAll.tabletop",
    ];

    private readonly TileRail latestRail = new();
    private readonly TileRail recentRail = new();
    private readonly TileRail[] genreRails =
        [new TileRail(), new TileRail(), new TileRail(), new TileRail(), new TileRail(), new TileRail()];
    private string dailyEyebrow = string.Empty;
    private string roomsLabel = string.Empty;
    private int roomsLabelCount = -1;
    private Spring heroScale = new(1f);
    private float entrance;

    private void ResetHome()
    {
        heroScale.SnapTo(1f);
        entrance = 0f;
        latestRail.Reset();
        recentRail.Reset();
        for (var index = 0; index < genreRails.Length; index++)
        {
            genreRails[index].Reset();
        }

        roomsLabelCount = -1;
        dailyEyebrow = Loc.Culture.TextInfo.ToUpper(Loc.T(L.Games.Daily));
    }

    private void DrawHome(in PhoneContext context)
    {
        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (var surface = AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            entrance = GameJuice.Advance(entrance, frameSeconds, EntranceSpeed);
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var left = origin.X;
            var y = origin.Y;
            var featured = games[featuredIndex];
            var heroRect = new Rect(new Vector2(left, y), new Vector2(left + width, y + HeroHeight * scale));
            GamesHubArt.ReportAnchor("games.featured", heroRect);
            if (DrawHero(heroRect, featured, Easing.EaseOutCubic(entrance), scale))
            {
                OpenGame(featured);
            }

            y = heroRect.Max.Y + GamesHubArt.SectionGap * scale;
            var recent = library.Recent;
            if (recent.Length > 0)
            {
                GamesHubArt.Section(ImGui.GetWindowDrawList(), ui, left, y, width,
                    Loc.T(L.GamesHub.ContinuePlaying), string.Empty, string.Empty);
                y += GamesHubArt.SectionHeight * scale;
                y = DrawRail(recent, recentRail, "##games.rail.recent", left, y, width, scale);
            }

            y = DrawFriendsRow(left, y, width, scale);
            var latest = library.Latest;
            if (latest.Length > 0)
            {
                y = DrawShelfSection(Loc.T(L.Games.ShelfLatest), GamesShelf.Latest, "games.seeAll.latest", latest,
                    latestRail, "##games.rail.latest", left, y, width, scale);
            }

            for (var genre = 0; genre < ShelfGenreCount; genre++)
            {
                var entries = library.Genre((GameGenre)genre);
                if (entries.Length == 0)
                {
                    continue;
                }

                y = DrawShelfSection(Loc.T(GameGenres.Label((GameGenre)genre)), (GamesShelf)genre,
                    GenreSeeAllIds[genre], entries, genreRails[genre], GenreRailIds[genre], left, y, width, scale);
            }

            FinishPage(origin, width, y, scale);
            if (AnyRailSwiping())
            {
                surface.CancelDrag();
            }
        }

        AppHeader.EndLargeTitle(in navBar, context, HomeNavId, DisplayName, NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty);
    }

    private bool AnyRailSwiping()
    {
        if (latestRail.Swiping || recentRail.Swiping)
        {
            return true;
        }

        for (var index = 0; index < genreRails.Length; index++)
        {
            if (genreRails[index].Swiping)
            {
                return true;
            }
        }

        return false;
    }

    private float DrawShelfSection(string title, GamesShelf shelf, string seeAllId, ReadOnlySpan<int> entries,
        TileRail rail, string railId, float left, float y, float width, float scale)
    {
        if (GamesHubArt.Section(ImGui.GetWindowDrawList(), ui, left, y, width, title, Loc.T(L.GamesHub.SeeAll),
                seeAllId))
        {
            router.Push(GamesRoute.ShelfOf(shelf));
        }

        y += GamesHubArt.SectionHeight * scale;
        return DrawRail(entries, rail, railId, left, y, width, scale);
    }

    private float DrawFriendsRow(float left, float y, float width, float scale)
    {
        var layout = MeasureFriendsCard(width, scale);
        var rect = new Rect(new Vector2(left, y), new Vector2(left + width, y + layout.Height));
        GamesHubArt.ReportAnchor("games.friends", rect);
        if (DrawFriendsCard(rect, layout, scale))
        {
            OpenOnlineHub(string.Empty);
        }

        return rect.Max.Y + GamesHubArt.SectionGap * scale;
    }

    private float DrawRail(ReadOnlySpan<int> entries, TileRail rail, string railId, float left, float y,
        float width, float scale)
    {
        var tileWidth = ShelfTileWidth * scale;
        var gap = TileGap * scale;
        var tileHeight = TileHeight(tileWidth, scale);
        var bleed = Metrics.Space.Lg * scale;
        var row = new Rect(new Vector2(left - bleed, y),
            new Vector2(left + width + bleed, y + tileHeight + Metrics.Space.Sm * scale));
        var contentWidth = bleed * 2f + entries.Length * (tileWidth + gap) - gap;
        var drawList = ImGui.GetWindowDrawList();
        rail.Begin(drawList, railId, row, new Rect(new Vector2(left, row.Min.Y), new Vector2(left + width, row.Max.Y)),
            contentWidth);
        var interactive = rail.TapAllowed;
        var x = left - rail.Offset;
        var activate = -1;
        for (var index = 0; index < entries.Length; index++)
        {
            var rect = new Rect(new Vector2(x, y), new Vector2(x + tileWidth, y + tileHeight));
            if (rect.Max.X >= row.Min.X && rect.Min.X <= row.Max.X
                && DrawTile(rect, entries[index], GameJuice.Stagger(entrance, index, entries.Length), interactive))
            {
                activate = entries[index];
            }

            x += tileWidth + gap;
        }

        rail.End(drawList, row, contentWidth, ui);
        if (activate >= 0)
        {
            Activate(activate);
        }

        return row.Max.Y + GamesHubArt.SectionGap * scale - Metrics.Space.Sm * scale;
    }

    private float DrawGrid(ReadOnlySpan<int> entries, float left, float y, float width, float scale)
    {
        if (entries.Length == 0)
        {
            return y;
        }

        var gap = TileGap * scale;
        var columns = Math.Clamp((int)((width + gap) / (GridMinTileWidth * scale + gap)), MinColumns, MaxColumns);
        var tileWidth = (width - gap * (columns - 1)) / columns;
        var tileHeight = TileHeight(tileWidth, scale);
        var drawList = ImGui.GetWindowDrawList();
        var clipMin = drawList.GetClipRectMin();
        var clipMax = drawList.GetClipRectMax();
        var activate = -1;
        for (var index = 0; index < entries.Length; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var min = new Vector2(left + column * (tileWidth + gap), y + row * (tileHeight + gap));
            var rect = new Rect(min, min + new Vector2(tileWidth, tileHeight));
            if (rect.Max.Y < clipMin.Y || rect.Min.Y > clipMax.Y)
            {
                continue;
            }

            if (DrawTile(rect, entries[index], GameJuice.Stagger(entrance, index, entries.Length), true))
            {
                activate = entries[index];
            }
        }

        if (activate >= 0)
        {
            Activate(activate);
        }

        var rows = (entries.Length + columns - 1) / columns;
        return y + rows * (tileHeight + gap) - gap;
    }

    private string RoomsLabel(int count)
    {
        if (count != roomsLabelCount)
        {
            roomsLabel = Loc.Plural(L.Games.OnlineRoomsOpen, count);
            roomsLabelCount = count;
        }

        return roomsLabel;
    }
}
