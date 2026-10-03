using Aetherphone.Apps.Music.Components;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Songs;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const int ChartPreviewCount = 10;
    private const float ChartRankWidth = 26f;
    private const float ChartMenuHitRadius = 15f;
    private const float ChartMenuGlyphScale = 0.8f;
    private const float ChartMenuReserve = 34f;

    private readonly ShelfRail newReleasesRail = new();
    private readonly ShelfRail newTrendingRail = new();
    private readonly ShelfRail[] newGameRails = CreateRails(MusicCatalogShelves.GameShelves.Length);
    private readonly ShelfRail[] genreRails = CreateRails(3);
    private readonly string[] chartRanks = CreateRanks(ChartPreviewCount);
    private int genreRailsIndex = -1;

    private static ShelfRail[] CreateRails(int count)
    {
        var rails = new ShelfRail[count];
        for (var index = 0; index < count; index++)
        {
            rails[index] = new ShelfRail();
        }

        return rails;
    }

    private static string[] CreateRanks(int count)
    {
        var ranks = new string[count];
        for (var index = 0; index < count; index++)
        {
            ranks[index] = (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return ranks;
    }

    private void DrawNew(in PhoneContext context)
    {
        var scale = UiScale.Current;
        var frame = BeginPage(context);
        using (AppSurface.BeginEdgeToEdge(frame.Body))
        {
            DrawHeroCarousel(scale);
            DrawCatalogShelf(newReleasesRail, MusicCatalogShelves.Releases);
            DrawCatalogShelf(newTrendingRail, MusicCatalogShelves.Trending);
            DrawChart(scale);
            var games = MusicCatalogShelves.GameShelves;
            for (var index = 0; index < games.Length; index++)
            {
                DrawCatalogShelf(newGameRails[index], games[index]);
            }

            SectionHeader.Draw(ui, Loc.T(L.Music.New.Genres), false);
            DrawGenreGrid(scale);
            ImGui.Dummy(new Vector2(0f, MusicUi.SectionGap * scale));
        }

        EndPage(in frame, context, Loc.T(L.Music.TabNew));
    }

    private void DrawChart(float scale)
    {
        var chart = MusicCatalogShelves.Chart;
        var chartCatalog = Catalog;
        chartCatalog.Ensure(chart.Key, chart.Request);
        var songs = chartCatalog.Songs(chart.Key);
        var state = chartCatalog.State(chart.Key);
        if (songs.Length == 0 && state != CatalogState.Loading)
        {
            return;
        }

        var title = Loc.T(chart.Title);
        if (SectionHeader.Draw(ui, title, songs.Length > 0))
        {
            Push(MusicRoute.Genre(chart.Key, title));
        }

        if (songs.Length == 0)
        {
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var height = SongRow.Height * scale * 3f;
            var inset = MusicUi.Inset * scale;
            Skeleton.Rows(ImGui.GetWindowDrawList(),
                new Rect(new Vector2(origin.X + inset, origin.Y), new Vector2(origin.X + width - inset, origin.Y + height)),
                SongRow.Height, 0f, scale);
            ImGui.Dummy(new Vector2(width, height));
            return;
        }

        var count = Math.Min(ChartPreviewCount, songs.Length);
        for (var index = 0; index < count; index++)
        {
            var action = DrawChartRow(scale, songs[index], chartRanks[index]);
            if (action == SongRowAction.Play)
            {
                PlayFrom(songs, index, chart.Key, title);
            }
            else if (action == SongRowAction.Menu)
            {
                songMenu.Open(songs[index]);
            }
        }
    }

    private SongRowAction DrawChartRow(float scale, in Song song, string rank)
    {
        var height = SongRow.Height * scale;
        var width = ScrollLayout.StableContentWidth();
        if (!ImGui.IsRectVisible(new Vector2(width, height)))
        {
            ImGui.Dummy(new Vector2(width, height));
            return SongRowAction.None;
        }

        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, height, ui.HoverWash);
        var min = cell.Bounds.Min;
        var max = cell.Bounds.Max;
        var inset = MusicUi.Inset * scale;
        var current = kit.IsCurrent(song);
        var rankWidth = ChartRankWidth * scale;
        var rankHeight = Typography.LineHeight(TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(min.X + inset, min.Y + (height - rankHeight) * 0.5f), rank,
            current ? ui.Accent : ui.MutedInk, TextStyles.Headline);
        var side = ArtworkTile.Side(ArtworkTile.RowArt);
        var artMin = new Vector2(min.X + inset + rankWidth, min.Y + (height - side) * 0.5f);
        ArtworkTile.Draw(drawList, images, artMin, side, song.ThumbnailUrl, song.Title);
        var textLeft = artMin.X + side + Metrics.Space.Md * scale;
        var textWidth = MathF.Max(1f, max.X - inset - ChartMenuReserve * scale - textLeft);
        var titleHeight = Typography.LineHeight(TextStyles.Body);
        var subtitleHeight = Typography.LineHeight(TextStyles.Subheadline);
        var titleTop = min.Y + (height - titleHeight - subtitleHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, titleTop), Typography.FitText(song.Title, textWidth,
            TextStyles.Body), current ? ui.Accent : ui.TitleInk, TextStyles.Body);
        Typography.Draw(drawList, new Vector2(textLeft, titleTop + titleHeight),
            Typography.FitText(song.Author, textWidth, TextStyles.Subheadline), ui.MutedInk, TextStyles.Subheadline);
        var menuCenter = new Vector2(max.X - inset - ChartMenuHitRadius * scale * 0.5f, min.Y + height * 0.5f);
        var menuTapped = ui.IconButton(menuCenter, ChartMenuHitRadius * scale,
            IconGlyph.Of(FontAwesomeIcon.EllipsisH), ui.MutedInk, AppSkin.Transparent, ChartMenuGlyphScale,
            Loc.T(L.Music.MoreOptions));
        var rightClicked = cell.Hovered && ImGui.IsMouseReleased(ImGuiMouseButton.Right);
        FeedCell.End(drawList, cell, ui.Hairline, false);
        FeedCell.Hairline(drawList, textLeft, max.X, max.Y, ui.Hairline);
        if (menuTapped || rightClicked)
        {
            return SongRowAction.Menu;
        }

        return cell.Tapped ? SongRowAction.Play : SongRowAction.None;
    }

    private void DrawGenrePage(in PhoneContext context, int genreIndex)
    {
        var scale = UiScale.Current;
        var genre = MusicCatalogShelves.Genres[genreIndex];
        if (genreRailsIndex != genreIndex)
        {
            genreRailsIndex = genreIndex;
            for (var index = 0; index < genreRails.Length; index++)
            {
                genreRails[index].Reset();
            }
        }

        var title = Loc.T(genre.Title);
        var frame = BeginPage(context);
        using (AppSurface.BeginEdgeToEdge(frame.Body))
        {
            var shelves = genre.Shelves;
            for (var index = 0; index < shelves.Length && index < genreRails.Length; index++)
            {
                DrawCatalogShelf(genreRails[index], shelves[index], index == 0 ? ArtworkTile.LargeCard
                    : ArtworkTile.Standard);
            }

            ImGui.Dummy(new Vector2(0f, MusicUi.SectionGap * scale));
        }

        EndPage(in frame, context, title);
    }
}
