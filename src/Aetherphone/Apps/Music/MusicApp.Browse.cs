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
    private const int SkeletonTileCount = 5;
    private const int GenreColumns = 2;
    private const float GenreTileAspect = 0.56f;
    private const float GenreGlyphScale = 1.9f;
    private const float GenreGlyphAlpha = 0.55f;
    private const float GenreLabelPad = 10f;
    private const float CollectionArtUnits = 200f;
    private const float CollectionPillHeight = 40f;
    private const float SkeletonCaptionWidth = 0.8f;
    private const float SkeletonCaptionShortWidth = 0.55f;
    private const float SkeletonLineUnits = 9f;
    private const float GenrePressedDimAlpha = 0.16f;
    private const string CatalogFolder = "Music";

    private static readonly Vector4 BrowseTileInk = new(1f, 1f, 1f, 1f);

    private readonly Dictionary<string, CatalogRequest> collectionRequests = new(StringComparer.Ordinal);
    private MusicCatalog? catalog;
    private Song[] collectionSongsSource = Array.Empty<Song>();
    private string collectionSubtitle = string.Empty;
    private string collectionSubtitleLanguage = string.Empty;

    private MusicCatalog Catalog => catalog ??= MusicCatalog.Create(
        new DirectoryInfo(Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, CatalogFolder)), songSearch,
        songResolver);

    private bool DrawSongShelf(ShelfRail rail, string title, Song[] songs, CatalogState state, string contextId,
        float units, bool seeAll = true)
    {
        var loading = songs.Length == 0;
        if (loading && state != CatalogState.Loading)
        {
            return false;
        }

        var side = ArtworkTile.Side(units);
        rail.Begin(ui, title, seeAll && !loading, loading ? SkeletonTileCount : songs.Length, side,
            ArtworkTile.CardHeight(side));
        var drawList = ImGui.GetWindowDrawList();
        if (loading)
        {
            for (var index = 0; index < SkeletonTileCount; index++)
            {
                if (rail.Tile(index, out var placeholder))
                {
                    DrawSkeletonTile(drawList, placeholder.Min, side);
                }
            }

            rail.End();
            return false;
        }

        for (var index = 0; index < songs.Length; index++)
        {
            if (!rail.Tile(index, out var tile))
            {
                continue;
            }

            var song = songs[index];
            var hovered = rail.Hover(tile);
            ArtworkTile.Draw(drawList, images, tile.Min, side, song.ThumbnailUrl, song.Title);
            ArtworkTile.DrawPressed(drawList, tile.Min, side, hovered);
            ArtworkTile.DrawCaption(drawList, ui, tile.Min, side, song.Title, song.Author, kit.IsCurrent(song));
            if (hovered && ImGui.IsMouseReleased(ImGuiMouseButton.Right))
            {
                songMenu.Open(song);
            }

            if (rail.Tapped(tile, hovered))
            {
                PlayFrom(songs, index, contextId, title);
            }
        }

        rail.End();
        return rail.SeeAllTapped;
    }

    private void DrawCatalogShelf(ShelfRail rail, in CatalogShelf shelf, float units = ArtworkTile.Standard)
    {
        var shelfCatalog = Catalog;
        shelfCatalog.Ensure(shelf.Key, shelf.Request);
        var title = Loc.T(shelf.Title);
        if (DrawSongShelf(rail, title, shelfCatalog.Songs(shelf.Key), shelfCatalog.State(shelf.Key), shelf.Key,
                units))
        {
            Push(MusicRoute.Genre(shelf.Key, title));
        }
    }

    private static void DrawSkeletonTile(ImDrawListPtr drawList, Vector2 min, float side)
    {
        var scale = UiScale.Current;
        Skeleton.Bar(drawList, min, min + new Vector2(side, side), side * ArtworkTile.TileRadiusFraction);
        var line = SkeletonLineUnits * scale;
        var top = min.Y + side + ArtworkTile.CaptionGap * scale;
        Skeleton.Bar(drawList, new Vector2(min.X, top), new Vector2(min.X + side * SkeletonCaptionWidth, top + line),
            line * 0.5f);
        var second = top + line + Metrics.Space.Xs * scale;
        Skeleton.Bar(drawList, new Vector2(min.X, second),
            new Vector2(min.X + side * SkeletonCaptionShortWidth, second + line), line * 0.5f);
    }

    private void DrawGenreGrid(float scale)
    {
        var genres = MusicCatalogShelves.Genres;
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var inset = MusicUi.Inset * scale;
        var gap = ShelfRail.TileGap * scale;
        var tileWidth = (width - inset * 2f - gap * (GenreColumns - 1)) / GenreColumns;
        var tileHeight = tileWidth * GenreTileAspect;
        var rows = (genres.Length + GenreColumns - 1) / GenreColumns;
        var drawList = ImGui.GetWindowDrawList();
        for (var index = 0; index < genres.Length; index++)
        {
            var column = index % GenreColumns;
            var row = index / GenreColumns;
            var min = new Vector2(origin.X + inset + column * (tileWidth + gap), origin.Y + row * (tileHeight + gap));
            var max = min + new Vector2(tileWidth, tileHeight);
            if (DrawGenreTile(drawList, genres[index], min, max, scale))
            {
                Push(MusicRoute.Genre(genres[index].Key, Loc.T(genres[index].Title)));
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rows * tileHeight + Math.Max(0, rows - 1) * gap));
    }

    private bool DrawGenreTile(ImDrawListPtr drawList, in CatalogGenre genre, Vector2 min, Vector2 max, float scale)
    {
        if (!ImGui.IsRectVisible(min, max))
        {
            return false;
        }

        var radius = (max.Y - min.Y) * ArtworkTile.HeroRadiusFraction;
        var swatch = ArtGradient.From(genre.Hue);
        Squircle.FillVerticalGradient(drawList, min, max, radius, ImGui.GetColorU32(swatch.Top),
            ImGui.GetColorU32(swatch.Bottom));
        var glyphCenter = new Vector2(max.X - (max.Y - min.Y) * 0.3f, max.Y - (max.Y - min.Y) * 0.32f);
        AppSkin.Icon(drawList, glyphCenter, IconGlyph.Of(genre.Icon), new Vector4(1f, 1f, 1f, GenreGlyphAlpha),
            GenreGlyphScale);
        var pad = GenreLabelPad * scale;
        var label = Typography.FitText(Loc.T(genre.Title), max.X - min.X - pad * 2f, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(min.X + pad, min.Y + pad), label, BrowseTileInk, TextStyles.Headline);
        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            var alpha = ImGui.IsMouseDown(ImGuiMouseButton.Left) ? GenrePressedDimAlpha : GenrePressedDimAlpha * 0.5f;
            Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, alpha)));
        }

        return UiInteract.Click(min, max, hovered);
    }

    private void OpenCollection(string key, string label, in CatalogRequest request)
    {
        collectionRequests[key] = request;
        Push(MusicRoute.Genre(key, label));
    }

    private void DrawGenreDetail(in PhoneContext context, in MusicRoute route)
    {
        var genreIndex = MusicCatalogShelves.GenreIndex(route.Key);
        if (genreIndex >= 0)
        {
            DrawGenrePage(context, genreIndex);
            return;
        }

        DrawCollectionPage(context, route);
    }

    private void DrawCollectionPage(in PhoneContext context, in MusicRoute route)
    {
        var key = route.Key;
        var songs = CollectionSongs(key, out var state);
        var title = CollectionTitle(route);
        var scale = UiScale.Current;
        var frame = BeginPage(context);
        if (songs.Length == 0)
        {
            var body = Unobstructed(frame.Body);
            if (state == CatalogState.Loading)
            {
                var inset = MusicUi.Inset * scale;
                Skeleton.Rows(ImGui.GetWindowDrawList(),
                    new Rect(new Vector2(body.Min.X + inset, body.Min.Y + Metrics.Space.Lg * scale),
                        new Vector2(body.Max.X - inset, body.Max.Y)), SongRow.Height, Metrics.Space.Xs, scale);
            }
            else
            {
                EmptyState.Draw(body, ui, FontAwesomeIcon.Music, Loc.T(L.Music.NoResults),
                    Loc.T(L.Music.Home.ShelfEmpty));
            }

            EndPage(in frame, context, title);
            return;
        }

        using (AppSurface.BeginEdgeToEdge(frame.Body))
        {
            DrawCollectionHeader(scale, key, title, songs);
            DrawSongRows(songs, key, title);
            ImGui.Dummy(new Vector2(0f, MusicUi.SectionGap * scale));
        }

        EndPage(in frame, context, title);
    }

    private void DrawCollectionHeader(float scale, string key, string title, Song[] songs)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var side = MathF.Min(ArtworkTile.Side(CollectionArtUnits), width - MusicUi.Inset * 2f * scale);
        var drawList = ImGui.GetWindowDrawList();
        var artMin = new Vector2(origin.X + (width - side) * 0.5f, origin.Y + Metrics.Space.Sm * scale);
        ArtworkTile.Draw(drawList, images, artMin, side, songs[0].ThumbnailUrl, key,
            ArtworkTile.HeroRadiusFraction);
        var subtitle = CollectionSubtitle(songs);
        var subtitleTop = artMin.Y + side + Metrics.Space.Md * scale;
        var fitted = Typography.FitText(subtitle, width - MusicUi.Inset * 2f * scale, TextStyles.Subheadline);
        var subtitleSize = Typography.Measure(fitted, TextStyles.Subheadline);
        Typography.Draw(drawList, new Vector2(origin.X + (width - subtitleSize.X) * 0.5f, subtitleTop), fitted,
            ui.MutedInk, TextStyles.Subheadline);
        var inset = MusicUi.Inset * scale;
        var gap = Metrics.Space.Md * scale;
        var pillTop = subtitleTop + subtitleSize.Y + Metrics.Space.Lg * scale;
        var pillWidth = (width - inset * 2f - gap) * 0.5f;
        var pillHeight = CollectionPillHeight * scale;
        var playRect = new Rect(new Vector2(origin.X + inset, pillTop),
            new Vector2(origin.X + inset + pillWidth, pillTop + pillHeight));
        var shuffleRect = new Rect(new Vector2(playRect.Max.X + gap, pillTop),
            new Vector2(playRect.Max.X + gap + pillWidth, pillTop + pillHeight));
        if (ui.PillButton(playRect, Loc.T(L.Music.Home.Play), true, "music.collection.play"))
        {
            playback.PlaySongs(songs, 0, key, title);
        }

        if (ui.PillButton(shuffleRect, Loc.T(L.Music.Shuffle), false, "music.collection.shuffle"))
        {
            playback.PlaySongsShuffled(songs, key, title);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, pillTop + pillHeight + Metrics.Space.Lg * scale - origin.Y));
    }

    private string CollectionSubtitle(Song[] songs)
    {
        var language = Loc.Current.Code;
        if (ReferenceEquals(songs, collectionSongsSource) &&
            string.Equals(language, collectionSubtitleLanguage, StringComparison.Ordinal))
        {
            return collectionSubtitle;
        }

        collectionSongsSource = songs;
        collectionSubtitleLanguage = language;
        collectionSubtitle = MusicUi.SongCount(songs.Length) + ", " +
                             MusicUi.LongDuration(MusicUi.TotalSeconds(songs));
        return collectionSubtitle;
    }

    private Song[] CollectionSongs(string key, out CatalogState state)
    {
        if (string.Equals(key, MusicCatalogShelves.MixFavourites, StringComparison.Ordinal))
        {
            EnsureHomeData();
            state = CatalogState.Ready;
            return homeFavourites;
        }

        var shelfCatalog = Catalog;
        if (string.Equals(key, MusicCatalogShelves.MixDiscovery, StringComparison.Ordinal))
        {
            EnsureHomeData();
            shelfCatalog.Ensure(key, homeDiscoveryRequest);
        }
        else if (string.Equals(key, MusicCatalogShelves.MixBecause, StringComparison.Ordinal))
        {
            EnsureHomeData();
            shelfCatalog.Ensure(key, homeBecauseRequest);
        }
        else if (MusicCatalogShelves.TryFind(key, out var shelf))
        {
            shelfCatalog.Ensure(key, shelf.Request);
        }
        else if (collectionRequests.TryGetValue(key, out var request))
        {
            shelfCatalog.Ensure(key, request);
        }

        state = shelfCatalog.State(key);
        return shelfCatalog.Songs(key);
    }

    private string CollectionTitle(in MusicRoute route)
    {
        var key = route.Key;
        if (string.Equals(key, MusicCatalogShelves.MixFavourites, StringComparison.Ordinal))
        {
            return Loc.T(L.Music.Home.FavouritesMix);
        }

        if (string.Equals(key, MusicCatalogShelves.MixDiscovery, StringComparison.Ordinal))
        {
            return Loc.T(L.Music.Home.DiscoveryMix);
        }

        if (string.Equals(key, MusicCatalogShelves.MixBecause, StringComparison.Ordinal))
        {
            return BecauseTitle();
        }

        if (MusicCatalogShelves.TryFind(key, out var shelf))
        {
            return Loc.T(shelf.Title);
        }

        var fetched = Catalog.Title(key);
        return fetched.Length > 0 ? fetched : route.Label;
    }
}
