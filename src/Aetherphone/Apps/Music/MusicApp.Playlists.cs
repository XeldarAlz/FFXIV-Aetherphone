using Aetherphone.Apps.Music.Components;
using Aetherphone.Apps.Music.Library;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Songs;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float PlaylistMenuHitRadius = 15f;
    private const float PlaylistMenuGlyphScale = 0.8f;
    private const float PlaylistMenuReserve = 34f;
    private const int PlaylistRowKey = 1000;
    private const int ShareLimit = 50;
    private const int ShareCharactersPerSong = 12;
    private const string ShareVideosPrefix = "https://www.youtube.com/watch_videos?video_ids=";

    private readonly NavBarButton[] playlistsPageButtons = new NavBarButton[1];
    private PlaylistRecord[] sortedPlaylists = Array.Empty<PlaylistRecord>();
    private CoverArt[] sortedPlaylistArt = Array.Empty<CoverArt>();
    private int sortedPlaylistsVersion = -1;
    private PlaylistSort playlistSort = PlaylistSort.RecentlyUpdated;

    private void DrawLibraryPlaylists(in PhoneContext context)
    {
        EnsureSortedPlaylists();
        var scale = UiScale.Current;
        var frame = BeginPage(context);
        using (AppSurface.BeginEdgeToEdge(frame.Body))
        {
            if (LibraryKit.ActionRow(ui, IconGlyph.Of(FontAwesomeIcon.Plus), Loc.T(L.Music.NewPlaylist)))
            {
                CreatePlaylistAndEdit();
            }

            if (LibraryKit.ActionRow(ui, IconGlyph.Of(FontAwesomeIcon.FileImport),
                    Loc.T(L.Music.Library.ImportFromYoutube)))
            {
                OpenImport(string.Empty);
            }

            for (var index = 0; index < sortedPlaylists.Length; index++)
            {
                DrawPlaylistRow(scale, index);
            }

            if (sortedPlaylists.Length == 0)
            {
                LibraryKit.Gap(Metrics.Space.Xl);
                var width = ScrollLayout.StableContentWidth() - MusicUi.Inset * 2f * scale;
                LibraryKit.CenteredText(Loc.T(L.Music.NoPlaylistsYet), TextStyles.Headline, ui.TitleInk, width);
                LibraryKit.Gap(Metrics.Space.Xs);
                LibraryKit.CenteredText(Loc.T(L.Music.PlaylistEmptySub), TextStyles.Subheadline, ui.MutedInk, width);
            }

            LibraryKit.Gap(LibraryBottomGap);
        }

        playlistsPageButtons[0] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.SortAmountDown),
            Loc.T(L.Music.Library.SortBy));
        if (EndPage(in frame, context, Loc.T(L.Music.LibraryPlaylists), playlistsPageButtons) == 0)
        {
            OpenPlaylistSortMenu();
        }
    }

    private void EnsureSortedPlaylists()
    {
        if (sortedPlaylistsVersion == library.Version)
        {
            return;
        }

        sortedPlaylistsVersion = library.Version;
        sortedPlaylists = LibrarySorting.Playlists(library.Playlists, playlistSort);
        var art = new CoverArt[sortedPlaylists.Length];
        for (var index = 0; index < art.Length; index++)
        {
            art[index] = CoverArt.Of(sortedPlaylists[index]);
        }

        sortedPlaylistArt = art;
    }

    private void SetPlaylistSort(PlaylistSort order)
    {
        playlistSort = order;
        sortedPlaylistsVersion = -1;
    }

    private void DrawPlaylistRow(float scale, int index)
    {
        var height = SongRow.Height * scale;
        var width = ScrollLayout.StableContentWidth();
        if (!ImGui.IsRectVisible(new Vector2(width, height)))
        {
            ImGui.Dummy(new Vector2(width, height));
            return;
        }

        var playlist = sortedPlaylists[index];
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var inset = MusicUi.Inset * scale;
        var menuCenter = new Vector2(origin.X + width - inset - PlaylistMenuHitRadius * scale * 0.5f,
            origin.Y + height * 0.5f);
        var menuRadius = PlaylistMenuHitRadius * scale;
        var overMenu = UiInteract.Hover(menuCenter - new Vector2(menuRadius, menuRadius),
            menuCenter + new Vector2(menuRadius, menuRadius));
        var cell = FeedCell.Begin(drawList, height, ui.HoverWash, !overMenu);
        var side = ArtworkTile.Side(ArtworkTile.RowArt);
        var artMin = new Vector2(cell.Bounds.Min.X + inset, cell.Bounds.Min.Y + (height - side) * 0.5f);
        LibraryArt.DrawCover(drawList, images, wallpaperImages, artMin, side, sortedPlaylistArt[index], playlist.Name);
        var textLeft = artMin.X + side + Metrics.Space.Md * scale;
        DrawRowText(drawList, cell.Bounds, textLeft, cell.Bounds.Max.X - inset - PlaylistMenuReserve * scale,
            playlist.Name, MusicUi.SongCount(playlist.Songs.Count));
        var menuTapped = ui.IconButton(menuCenter, menuRadius, IconGlyph.Of(FontAwesomeIcon.EllipsisH), ui.MutedInk,
            AppSkin.Transparent, PlaylistMenuGlyphScale, Loc.T(L.Music.MoreOptions));
        var secondary = LibraryKit.Secondary(PlaylistRowKey + index, cell.Hovered);
        FeedCell.End(drawList, cell, ui.Hairline, false);
        FeedCell.Hairline(drawList, textLeft, cell.Bounds.Max.X, cell.Bounds.Max.Y, ui.Hairline);
        if (menuTapped || secondary)
        {
            OpenPlaylistMenu(playlist.Id);
            return;
        }

        if (cell.Tapped)
        {
            Push(MusicRoute.Playlist(playlist.Id));
        }
    }

    private void CreatePlaylistAndEdit()
    {
        var id = library.CreatePlaylist(Loc.T(L.Music.NewPlaylist));
        Push(MusicRoute.Playlist(id));
        BeginPlaylistEdit(id, true, false);
    }

    private void PlayPlaylist(string playlistId, bool shuffled)
    {
        var songs = library.PlaylistSongs(playlistId);
        if (songs.Length == 0)
        {
            return;
        }

        var title = PlaylistTitle(playlistId);
        if (shuffled)
        {
            playback.PlaySongsShuffled(songs, playlistId, title);
            return;
        }

        playback.PlaySongs(songs, 0, playlistId, title);
    }

    private void DownloadPlaylist(string playlistId)
    {
        var songs = library.PlaylistSongs(playlistId);
        for (var index = 0; index < songs.Length; index++)
        {
            downloads.Download(songs[index]);
        }
    }

    private void SharePlaylist(string playlistId)
    {
        if (library.FindPlaylist(playlistId) is not { } playlist)
        {
            return;
        }

        var link = playlist.SourceUrl;
        if (link.Length == 0)
        {
            var count = Math.Min(ShareLimit, playlist.Songs.Count);
            if (count == 0)
            {
                return;
            }

            var builder = new System.Text.StringBuilder(ShareVideosPrefix, ShareVideosPrefix.Length + count * ShareCharactersPerSong);
            for (var index = 0; index < count; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                builder.Append(playlist.Songs[index].VideoId);
            }

            link = builder.ToString();
        }

        ImGui.SetClipboardText(link);
        ShellToast.Show(Loc.T(L.Music.LinkCopied));
    }

    private void ConfirmDeletePlaylist(string playlistId)
    {
        if (library.FindPlaylist(playlistId) is not { } playlist)
        {
            return;
        }

        var coverPath = playlist.CoverPath;
        confirm.Ask(new ConfirmRequest
        {
            Title = playlist.Name,
            Message = Loc.T(L.Music.DeletePlaylistConfirm),
            ConfirmLabel = Loc.T(L.Music.DeletePlaylistButton),
            CancelLabel = Loc.T(L.Common.Cancel),
            Sheet = true,
            Confirm = () => DeletePlaylist(playlistId, coverPath),
        });
    }

    private void DeletePlaylist(string playlistId, string coverPath)
    {
        library.DeletePlaylist(playlistId);
        PlaylistCovers.Delete(coverPath);
        if (Router.Current.Screen == MusicScreen.PlaylistDetail &&
            string.Equals(Router.Current.Key, playlistId, StringComparison.Ordinal))
        {
            Router.Pop();
        }
    }
}
