using Aetherphone.Apps.Music.Components;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Radio;
using Aetherphone.Core.Songs;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const int RecentSearchShown = 8;
    private const int TopSongsPreview = 4;
    private const int TopStationsPreview = 3;
    private const int TopLibraryPreview = 3;
    private const int SearchSkeletonRows = 6;
    private const float SearchRecentRowHeight = 46f;
    private const float SearchRecentGlyphScale = 0.8f;
    private const float SearchRecentGlyphBox = 24f;
    private const float TopResultHeight = 112f;
    private const float TopResultArt = 88f;
    private const float ArtistTileUnits = 100f;
    private const float SearchHintHeight = 36f;
    private const float SearchStationRowHeight = 58f;

    private readonly ShelfRail searchArtistsRail = new();
    private readonly ShelfRail searchPlaylistsRail = new();
    private SearchBundle? searchRailsBundle;

    private void DrawSearchHome(float scale)
    {
        var recent = library.RecentSearches;
        if (recent.Count > 0)
        {
            if (DrawRecentHeader(scale))
            {
                library.ClearRecentSearches();
            }

            var shown = Math.Min(RecentSearchShown, recent.Count);
            for (var index = 0; index < shown && index < recent.Count; index++)
            {
                var query = recent[index];
                if (DrawRecentRow(scale, query))
                {
                    searchDraft = query;
                    searchDraftSeen = query;
                    BeginSearch(query);
                    break;
                }
            }
        }

        SectionHeader.Draw(ui, Loc.T(L.Music.Search.Browse), false);
        DrawGenreGrid(scale);
        ImGui.Dummy(new Vector2(0f, MusicUi.SectionGap * scale));
    }

    private bool DrawRecentHeader(float scale)
    {
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var inset = MusicUi.Inset * scale;
        var drawList = ImGui.GetWindowDrawList();
        var clear = Loc.T(L.Music.Search.Clear);
        var clearSize = Typography.Measure(clear, TextStyles.Body);
        var titleHeight = Typography.LineHeight(TextStyles.Title3);
        var title = Typography.FitText(Loc.T(L.Music.Search.Recent),
            MathF.Max(1f, width - inset * 3f - clearSize.X), TextStyles.Title3);
        Typography.Draw(drawList, new Vector2(origin.X + inset, origin.Y), title, ui.Palette.HeadingInk,
            TextStyles.Title3);
        var clearMin = new Vector2(origin.X + width - inset - clearSize.X, origin.Y + (titleHeight - clearSize.Y) * 0.5f);
        var clearMax = clearMin + clearSize;
        var hovered = UiInteract.Hover(clearMin, clearMax);
        Typography.Draw(drawList, clearMin, clear, hovered ? ui.MutedInk : ui.Accent, TextStyles.Body);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, titleHeight + Metrics.Space.Sm * scale));
        return UiInteract.Click(clearMin, clearMax, hovered);
    }

    private bool DrawRecentRow(float scale, string query)
    {
        var drawList = ImGui.GetWindowDrawList();
        var height = SearchRecentRowHeight * scale;
        var cell = FeedCell.Begin(drawList, height, ui.HoverWash);
        var inset = MusicUi.Inset * scale;
        var centerY = cell.Bounds.Min.Y + height * 0.5f;
        var glyphBox = SearchRecentGlyphBox * scale;
        AppSkin.Icon(drawList, new Vector2(cell.Bounds.Min.X + inset + glyphBox * 0.5f, centerY),
            IconGlyph.Of(FontAwesomeIcon.History), ui.MutedInk, SearchRecentGlyphScale);
        var textLeft = cell.Bounds.Min.X + inset + glyphBox + Metrics.Space.Sm * scale;
        var lineHeight = Typography.LineHeight(TextStyles.Body);
        Typography.Draw(drawList, new Vector2(textLeft, centerY - lineHeight * 0.5f),
            Typography.FitText(query, MathF.Max(1f, cell.Bounds.Max.X - inset - textLeft), TextStyles.Body),
            ui.Accent, TextStyles.Body);
        FeedCell.End(drawList, cell, ui.Hairline, false);
        FeedCell.Hairline(drawList, textLeft, cell.Bounds.Max.X, cell.Bounds.Max.Y, ui.Hairline);
        return cell.Tapped;
    }

    private void DrawSearchResults(float scale, Rect placeholder)
    {
        if (searchScope == SearchScope.Library)
        {
            DrawLibraryResults(scale, placeholder, int.MaxValue);
            return;
        }

        if (!DraftMatches(lastSearchQuery))
        {
            DrawPressEnterHint(scale);
        }

        if (searchBundle is not { } bundle)
        {
            return;
        }

        if (!ReferenceEquals(bundle, searchRailsBundle))
        {
            searchRailsBundle = bundle;
            searchArtistsRail.Reset();
            searchPlaylistsRail.Reset();
        }

        MatchCommunityStations(bundle.Query);
        switch (searchScope)
        {
            case SearchScope.Songs:
                DrawSongResults(scale, placeholder, bundle);
                break;
            case SearchScope.Playlists:
                DrawPlaylistResults(scale, placeholder, bundle);
                break;
            case SearchScope.Artists:
                DrawArtistResults(scale, placeholder, bundle);
                break;
            case SearchScope.Radio:
                DrawRadioResults(scale, placeholder, bundle, int.MaxValue);
                break;
            default:
                DrawTopResults(scale, placeholder, bundle);
                break;
        }

        ImGui.Dummy(new Vector2(0f, MusicUi.SectionGap * scale));
    }

    private bool DraftMatches(string query)
    {
        return searchDraft.AsSpan().Trim().Equals(query.AsSpan(), StringComparison.OrdinalIgnoreCase);
    }

    private void DrawPressEnterHint(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var height = SearchHintHeight * scale;
        var text = Typography.FitText(Loc.T(L.Music.Search.PressEnter), width - MusicUi.Inset * 2f * scale,
            TextStyles.Footnote);
        var size = Typography.Measure(text, TextStyles.Footnote);
        Typography.Draw(ImGui.GetWindowDrawList(),
            new Vector2(origin.X + (width - size.X) * 0.5f, origin.Y + (height - size.Y) * 0.5f), text, ui.MutedInk,
            TextStyles.Footnote);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawSearchSkeleton(float scale, int rows)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var inset = MusicUi.Inset * scale;
        var height = rows * SongRow.Height * scale;
        Skeleton.Rows(ImGui.GetWindowDrawList(),
            new Rect(new Vector2(origin.X + inset, origin.Y), new Vector2(origin.X + width - inset, origin.Y + height)),
            SongRow.Height, 0f, scale);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawNoResults(Rect placeholder)
    {
        var top = ImGui.GetCursorScreenPos().Y;
        var area = new Rect(new Vector2(placeholder.Min.X, top),
            new Vector2(placeholder.Max.X, MathF.Max(top + 1f, placeholder.Max.Y)));
        EmptyState.Draw(area, ui, FontAwesomeIcon.Search, Loc.T(L.Music.NoResults), Loc.T(L.Music.NoResultsSub));
    }

    private void DrawTopResults(float scale, Rect placeholder, SearchBundle bundle)
    {
        if (!bundle.SongsDone)
        {
            DrawSearchSkeleton(scale, SearchSkeletonRows);
            return;
        }

        var any = DrawTopResultCard(scale, bundle);
        any |= DrawSongPreview(bundle);
        any |= DrawArtistRail(bundle);
        any |= DrawPlaylistRail(bundle);
        any |= DrawRadioResults(scale, placeholder, bundle, TopStationsPreview);
        any |= DrawLibraryResults(scale, placeholder, TopLibraryPreview);
        if (!any && bundle.Complete)
        {
            DrawNoResults(placeholder);
        }
    }

    private bool DrawTopResultCard(float scale, SearchBundle bundle)
    {
        var artistIndex = bundle.TopArtist;
        var songs = bundle.Songs;
        if (artistIndex < 0 && songs.Length == 0)
        {
            return false;
        }

        SectionHeader.Draw(ui, Loc.T(L.Music.Search.TopResult), false, MusicUi.Inset, Metrics.Space.Sm);
        var isArtist = artistIndex >= 0 && artistIndex < bundle.Artists.Length;
        var artist = isArtist ? bundle.Artists[artistIndex] : default;
        var song = isArtist ? default : songs[0];
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var inset = MusicUi.Inset * scale;
        var min = new Vector2(origin.X + inset, origin.Y);
        var max = new Vector2(origin.X + width - inset, origin.Y + TopResultHeight * scale);
        var drawList = ImGui.GetWindowDrawList();
        var radius = Metrics.Radius.Card * scale;
        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(ui.Palette.CardFill));
        var pad = Metrics.Space.Md * scale;
        var side = TopResultArt * scale;
        var artMin = new Vector2(min.X + pad, min.Y + (max.Y - min.Y - side) * 0.5f);
        var name = isArtist ? artist.Name : song.Title;
        if (isArtist)
        {
            DrawSearchRoundArt(drawList, artMin, side, artist.ThumbnailUrl, artist.Name);
        }
        else
        {
            ArtworkTile.Draw(drawList, images, artMin, side, song.ThumbnailUrl, song.Title);
        }

        var textLeft = artMin.X + side + pad;
        var textWidth = MathF.Max(1f, max.X - pad - textLeft);
        var nameHeight = Typography.LineHeight(TextStyles.Title3);
        var lineHeight = Typography.LineHeight(TextStyles.Subheadline);
        var textTop = min.Y + (max.Y - min.Y - nameHeight - lineHeight * 2f) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop), Typography.FitText(name, textWidth, TextStyles.Title3),
            kit.IsCurrent(song) ? ui.Accent : ui.TitleInk, TextStyles.Title3);
        var kind = Loc.T(isArtist ? L.Music.Search.KindArtist : L.Music.Search.KindSong);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + nameHeight),
            Typography.FitText(kind, textWidth, TextStyles.Subheadline), ui.MutedInk, TextStyles.Subheadline);
        var detail = isArtist ? MusicUi.SongCount(artist.Count) : song.Author;
        Typography.Draw(drawList, new Vector2(textLeft, textTop + nameHeight + lineHeight),
            Typography.FitText(detail, textWidth, TextStyles.Subheadline), ui.MutedInk, TextStyles.Subheadline);
        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(ui.HoverWash));
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, max.Y - min.Y));
        if (!isArtist && hovered && ImGui.IsMouseReleased(ImGuiMouseButton.Right))
        {
            songMenu.Open(song);
        }

        if (!UiInteract.Click(min, max, hovered))
        {
            return true;
        }

        if (isArtist)
        {
            Push(MusicRoute.Artist(artist.ChannelId, artist.Name));
        }
        else
        {
            PlayFrom(songs, 0, SearchContext, Loc.T(L.Music.SourceSearch));
        }

        return true;
    }

    private bool DrawSongPreview(SearchBundle bundle)
    {
        var songs = bundle.Songs;
        if (songs.Length == 0)
        {
            return false;
        }

        if (SectionHeader.Draw(ui, Loc.T(L.Music.Search.ScopeSongs), songs.Length > TopSongsPreview))
        {
            searchScope = SearchScope.Songs;
        }

        var count = Math.Min(TopSongsPreview, songs.Length);
        var title = Loc.T(L.Music.SourceSearch);
        for (var index = 0; index < count; index++)
        {
            var action = SongRow.Draw(kit, songs[index]);
            if (action == SongRowAction.Play)
            {
                PlayFrom(songs, index, SearchContext, title);
            }
            else if (action == SongRowAction.Menu)
            {
                songMenu.Open(songs[index]);
            }
        }

        return true;
    }

    private bool DrawArtistRail(SearchBundle bundle)
    {
        var artists = bundle.Artists;
        if (artists.Length == 0)
        {
            return false;
        }

        var side = ArtworkTile.Side(ArtistTileUnits);
        searchArtistsRail.Begin(ui, Loc.T(L.Music.Search.ScopeArtists), true, artists.Length, side,
            ArtworkTile.CardHeight(side));
        var drawList = ImGui.GetWindowDrawList();
        var tapped = -1;
        for (var index = 0; index < artists.Length; index++)
        {
            if (!searchArtistsRail.Tile(index, out var tile))
            {
                continue;
            }

            var artist = artists[index];
            var hovered = searchArtistsRail.Hover(tile);
            DrawSearchRoundArt(drawList, tile.Min, side, artist.ThumbnailUrl, artist.Name);
            if (hovered)
            {
                drawList.AddCircleFilled(tile.Min + new Vector2(side, side) * 0.5f, side * 0.5f,
                    ImGui.GetColorU32(ui.HoverWash), 48);
            }

            ArtworkTile.DrawCaption(drawList, ui, tile.Min, side, artist.Name, MusicUi.SongCount(artist.Count));
            if (searchArtistsRail.Tapped(tile, hovered))
            {
                tapped = index;
            }
        }

        searchArtistsRail.End();
        if (searchArtistsRail.SeeAllTapped)
        {
            searchScope = SearchScope.Artists;
        }

        if (tapped >= 0)
        {
            Push(MusicRoute.Artist(artists[tapped].ChannelId, artists[tapped].Name));
        }

        return true;
    }

    private bool DrawPlaylistRail(SearchBundle bundle)
    {
        var playlists = bundle.Playlists;
        if (!bundle.PlaylistsDone || playlists.Length == 0)
        {
            return false;
        }

        var side = ArtworkTile.Side(ArtworkTile.Standard);
        searchPlaylistsRail.Begin(ui, Loc.T(L.Music.Search.ScopePlaylists), true, playlists.Length, side,
            ArtworkTile.CardHeight(side));
        var drawList = ImGui.GetWindowDrawList();
        var tapped = -1;
        for (var index = 0; index < playlists.Length; index++)
        {
            if (!searchPlaylistsRail.Tile(index, out var tile))
            {
                continue;
            }

            var playlist = playlists[index];
            var hovered = searchPlaylistsRail.Hover(tile);
            ArtworkTile.Draw(drawList, images, tile.Min, side, playlist.ThumbnailUrl, playlist.Title);
            ArtworkTile.DrawPressed(drawList, tile.Min, side, hovered);
            ArtworkTile.DrawCaption(drawList, ui, tile.Min, side, playlist.Title, playlist.Author);
            if (searchPlaylistsRail.Tapped(tile, hovered))
            {
                tapped = index;
            }
        }

        searchPlaylistsRail.End();
        if (searchPlaylistsRail.SeeAllTapped)
        {
            searchScope = SearchScope.Playlists;
        }

        if (tapped >= 0)
        {
            OpenSearchPlaylist(playlists[tapped]);
        }

        return true;
    }

    private void OpenSearchPlaylist(in SongPlaylistHit playlist)
    {
        var key = MusicCatalogShelves.PlaylistKey(playlist.PlaylistId);
        OpenCollection(key, playlist.Title, CatalogRequest.Playlist(MusicCatalogShelves.PlaylistUrl(key)));
    }

    private void DrawSongResults(float scale, Rect placeholder, SearchBundle bundle)
    {
        if (!bundle.SongsDone)
        {
            DrawSearchSkeleton(scale, SearchSkeletonRows);
            return;
        }

        if (bundle.Songs.Length == 0)
        {
            DrawNoResults(placeholder);
            return;
        }

        DrawSongRows(bundle.Songs, SearchContext, Loc.T(L.Music.SourceSearch));
    }

    private void DrawPlaylistResults(float scale, Rect placeholder, SearchBundle bundle)
    {
        if (!bundle.PlaylistsDone)
        {
            DrawSearchSkeleton(scale, SearchSkeletonRows);
            return;
        }

        var playlists = bundle.Playlists;
        if (playlists.Length == 0)
        {
            DrawNoResults(placeholder);
            return;
        }

        for (var index = 0; index < playlists.Length; index++)
        {
            var playlist = playlists[index];
            if (DrawSearchRow(scale, playlist.ThumbnailUrl, playlist.Title, playlist.Author, false))
            {
                OpenSearchPlaylist(playlist);
            }
        }
    }

    private void DrawArtistResults(float scale, Rect placeholder, SearchBundle bundle)
    {
        if (!bundle.SongsDone)
        {
            DrawSearchSkeleton(scale, SearchSkeletonRows);
            return;
        }

        var artists = bundle.Artists;
        if (artists.Length == 0)
        {
            DrawNoResults(placeholder);
            return;
        }

        for (var index = 0; index < artists.Length; index++)
        {
            var artist = artists[index];
            if (DrawSearchRow(scale, artist.ThumbnailUrl, artist.Name, MusicUi.SongCount(artist.Count), true))
            {
                Push(MusicRoute.Artist(artist.ChannelId, artist.Name));
            }
        }
    }

    private bool DrawRadioResults(float scale, Rect placeholder, SearchBundle bundle, int limit)
    {
        var any = false;
        var full = limit == int.MaxValue;
        var stations = bundle.StationsDone ? bundle.Stations : Array.Empty<RadioStation>();
        if (communityMatches.Count > 0 || stations.Length > 0)
        {
            SectionHeader.Draw(ui, Loc.T(L.Music.Search.Stations), false);
        }

        var communityCount = Math.Min(limit, communityMatches.Count);
        for (var index = 0; index < communityCount; index++)
        {
            DrawCommunityRow(scale, communityMatches[index], FeedCell.PadX);
            any = true;
        }

        var worldCount = Math.Min(limit, stations.Length);
        for (var index = 0; index < worldCount; index++)
        {
            if (DrawStationResultRow(scale, stations[index]))
            {
                radio.ReportClick(stations[index].Uuid);
                playback.PlayStations(stations, index);
            }

            any = true;
        }

        if (!full)
        {
            return any;
        }

        if (!bundle.StationsDone)
        {
            DrawSearchSkeleton(scale, SearchSkeletonRows);
            return true;
        }

        if (!any)
        {
            DrawNoResults(placeholder);
        }

        return any;
    }

    private bool DrawLibraryResults(float scale, Rect placeholder, int limit)
    {
        var full = limit == int.MaxValue;
        var songCount = Math.Min(limit, libraryMatches.Length);
        var playlistCount = Math.Min(limit, libraryPlaylistMatches.Length);
        if (songCount == 0 && playlistCount == 0)
        {
            if (full && !libraryPending)
            {
                DrawNoResults(placeholder);
            }

            return false;
        }

        if (!full)
        {
            SectionHeader.Draw(ui, Loc.T(L.Music.Search.FromLibrary), false);
        }

        var title = Loc.T(L.Music.Search.FromLibrary);
        for (var index = 0; index < songCount; index++)
        {
            var action = SongRow.Draw(kit, libraryMatches[index]);
            if (action == SongRowAction.Play)
            {
                PlayFrom(libraryMatches, index, SearchLibraryContext, title);
            }
            else if (action == SongRowAction.Menu)
            {
                songMenu.Open(libraryMatches[index]);
            }
        }

        for (var index = 0; index < playlistCount; index++)
        {
            var playlist = libraryPlaylistMatches[index];
            var cover = playlist.Songs.Count > 0 ? playlist.Songs[0].ThumbnailUrl : string.Empty;
            if (DrawSearchRow(scale, cover, playlist.Name, MusicUi.SongCount(playlist.Songs.Count), false))
            {
                Push(MusicRoute.Playlist(playlist.Id));
            }
        }

        if (full)
        {
            ImGui.Dummy(new Vector2(0f, MusicUi.SectionGap * scale));
        }

        return true;
    }

    private bool DrawSearchRow(float scale, string url, string title, string subtitle, bool round)
    {
        var height = SongRow.Height * scale;
        var width = ScrollLayout.StableContentWidth();
        if (!ImGui.IsRectVisible(new Vector2(width, height)))
        {
            ImGui.Dummy(new Vector2(width, height));
            return false;
        }

        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, height, ui.HoverWash);
        var inset = MusicUi.Inset * scale;
        var side = ArtworkTile.Side(ArtworkTile.RowArt);
        var artMin = new Vector2(cell.Bounds.Min.X + inset, cell.Bounds.Min.Y + (height - side) * 0.5f);
        if (round)
        {
            DrawSearchRoundArt(drawList, artMin, side, url, title);
        }
        else
        {
            ArtworkTile.Draw(drawList, images, artMin, side, url, title);
        }

        var textLeft = artMin.X + side + Metrics.Space.Md * scale;
        DrawSearchRowText(drawList, cell.Bounds, textLeft, inset, title, subtitle, ui.TitleInk);
        FeedCell.End(drawList, cell, ui.Hairline, false);
        FeedCell.Hairline(drawList, textLeft, cell.Bounds.Max.X, cell.Bounds.Max.Y, ui.Hairline);
        return cell.Tapped;
    }

    private bool DrawStationResultRow(float scale, in RadioStation station)
    {
        var height = SearchStationRowHeight * scale;
        var width = ScrollLayout.StableContentWidth();
        if (!ImGui.IsRectVisible(new Vector2(width, height)))
        {
            ImGui.Dummy(new Vector2(width, height));
            return false;
        }

        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, height, ui.HoverWash);
        var inset = MusicUi.Inset * scale;
        var side = ArtworkTile.Side(ArtworkTile.RowArt);
        var artMin = new Vector2(cell.Bounds.Min.X + inset, cell.Bounds.Min.Y + (height - side) * 0.5f);
        var artMax = artMin + new Vector2(side, side);
        var rounding = side * ArtworkTile.TileRadiusFraction;
        var texture = string.IsNullOrEmpty(station.ArtworkUrl) ? null : images.Sized(station.ArtworkUrl, side);
        if (texture is not null)
        {
            var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
            drawList.AddImageRounded(texture.Handle, artMin, artMax, uv0, uv1, 0xFFFFFFFFu, rounding,
                ImDrawFlags.RoundCornersAll);
        }
        else
        {
            drawList.AddImageRounded(artwork.HandleForName(station.Name), artMin, artMax, Vector2.Zero, Vector2.One,
                0xFFFFFFFFu, rounding, ImDrawFlags.RoundCornersAll);
        }

        var current = playback.RadioActive && playback.Radio.CurrentStation == station.Name;
        var textLeft = artMax.X + Metrics.Space.Md * scale;
        DrawSearchRowText(drawList, cell.Bounds, textLeft, inset, station.Name, station.Country,
            current ? ui.Accent : ui.TitleInk);
        FeedCell.End(drawList, cell, ui.Hairline, false);
        FeedCell.Hairline(drawList, textLeft, cell.Bounds.Max.X, cell.Bounds.Max.Y, ui.Hairline);
        if (!cell.Tapped)
        {
            return false;
        }

        if (current)
        {
            playback.TogglePlayPause();
            return false;
        }

        return true;
    }

    private void DrawSearchRowText(ImDrawListPtr drawList, Rect bounds, float textLeft, float inset, string title,
        string subtitle, Vector4 titleInk)
    {
        var textWidth = MathF.Max(1f, bounds.Max.X - inset - textLeft);
        var titleHeight = Typography.LineHeight(TextStyles.Body);
        var subtitleHeight = subtitle.Length > 0 ? Typography.LineHeight(TextStyles.Subheadline) : 0f;
        var top = bounds.Min.Y + (bounds.Height - titleHeight - subtitleHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top), Typography.FitText(title, textWidth, TextStyles.Body),
            titleInk, TextStyles.Body);
        if (subtitle.Length == 0)
        {
            return;
        }

        Typography.Draw(drawList, new Vector2(textLeft, top + titleHeight),
            Typography.FitText(subtitle, textWidth, TextStyles.Subheadline), ui.MutedInk, TextStyles.Subheadline);
    }

    private void DrawSearchRoundArt(ImDrawListPtr drawList, Vector2 min, float side, string url, string seed)
    {
        var center = min + new Vector2(side, side) * 0.5f;
        var texture = string.IsNullOrEmpty(url) ? null : images.Sized(url, side);
        if (texture is null)
        {
            ArtGradient.DrawDisc(drawList, center, side * 0.5f, ArtGradient.FromName(seed), 1f);
            return;
        }

        var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
        drawList.AddImageRounded(texture.Handle, min, min + new Vector2(side, side), uv0, uv1, 0xFFFFFFFFu,
            side * 0.5f, ImDrawFlags.RoundCornersAll);
    }
}
