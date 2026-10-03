using Aetherphone.Apps.Music.Library;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const int MaxLibraryActions = 8;

    private enum LibraryMenu : byte
    {
        None,
        PlaylistSort,
        SongSort,
        Playlist,
    }

    private enum PlaylistCommand : byte
    {
        Play,
        Shuffle,
        Rename,
        Download,
        Refresh,
        Share,
        Delete,
    }

    private readonly ActionSheet libraryActions = new();
    private readonly ActionSheet.Item[] libraryActionItems = new ActionSheet.Item[MaxLibraryActions];
    private readonly PlaylistCommand[] playlistCommands = new PlaylistCommand[MaxLibraryActions];
    private LibraryMenu libraryMenu = LibraryMenu.None;
    private string menuPlaylistId = string.Empty;
    private AddSongsSheet? addSongsSheet;
    private int seenDownloadFailures = -1;

    private AddSongsSheet AddSongsPicker => addSongsSheet ??= new AddSongsSheet(songSearch);

    private bool LibraryOverlaysCapture =>
        libraryActions.CapturesPointer || (addSongsSheet?.CapturesPointer ?? false);

    private void OpenPlaylistMenu(string playlistId)
    {
        menuPlaylistId = playlistId;
        libraryMenu = LibraryMenu.Playlist;
        libraryActions.Open();
    }

    private void OpenPlaylistSortMenu()
    {
        libraryMenu = LibraryMenu.PlaylistSort;
        libraryActions.Open();
    }

    private void OpenSongSortMenu()
    {
        libraryMenu = LibraryMenu.SongSort;
        libraryActions.Open();
    }

    private void DrawLibraryOverlays(Rect screen)
    {
        PumpPlaylistEdit();
        PumpCoverWork();
        PumpRefreshWork();
        PumpDownloadFailures();
        addSongsSheet?.Draw(screen, kit);
        if (!libraryActions.CapturesPointer)
        {
            return;
        }

        var count = BuildLibraryActions(out var title);
        var picked = libraryActions.Draw(screen, ActionSheetStyle.From(ui), libraryActionItems.AsSpan(0, count),
            Loc.T(L.Common.Cancel), false, title);
        if (picked < 0)
        {
            return;
        }

        RunLibraryAction(picked);
    }

    private int BuildLibraryActions(out string title)
    {
        switch (libraryMenu)
        {
            case LibraryMenu.PlaylistSort:
                title = Loc.T(L.Music.Library.SortBy);
                libraryActionItems[0] = SortItem(L.Music.Library.SortRecentlyUpdated,
                    playlistSort == PlaylistSort.RecentlyUpdated);
                libraryActionItems[1] = SortItem(L.Music.Library.SortTitle, playlistSort == PlaylistSort.Title);
                libraryActionItems[2] = SortItem(L.Music.RecentlyAdded, playlistSort == PlaylistSort.RecentlyAdded);
                return 3;
            case LibraryMenu.SongSort:
                title = Loc.T(L.Music.Library.SortBy);
                libraryActionItems[0] = SortItem(L.Music.RecentlyAdded, librarySongSort == SongSort.RecentlyAdded);
                libraryActionItems[1] = SortItem(L.Music.Library.SortTitle, librarySongSort == SongSort.Title);
                libraryActionItems[2] = SortItem(L.Music.Library.SortArtist, librarySongSort == SongSort.Artist);
                return 3;
            case LibraryMenu.Playlist:
                return BuildPlaylistActions(out title);
            default:
                title = string.Empty;
                return 0;
        }
    }

    private static ActionSheet.Item SortItem(LocString label, bool selected) =>
        new(Loc.T(label), string.Empty, false, selected, true);

    private int BuildPlaylistActions(out string title)
    {
        if (library.FindPlaylist(menuPlaylistId) is not { } playlist)
        {
            title = string.Empty;
            return 0;
        }

        title = playlist.Name;
        var count = 0;
        var hasSongs = playlist.Songs.Count > 0;
        if (hasSongs)
        {
            AddPlaylistAction(ref count, PlaylistCommand.Play, Loc.T(L.Music.Library.Play), FontAwesomeIcon.Play);
            AddPlaylistAction(ref count, PlaylistCommand.Shuffle, Loc.T(L.Music.Shuffle), FontAwesomeIcon.Random);
        }

        AddPlaylistAction(ref count, PlaylistCommand.Rename, Loc.T(L.Music.RenamePlaylist), FontAwesomeIcon.Pen);
        if (hasSongs)
        {
            AddPlaylistAction(ref count, PlaylistCommand.Download, Loc.T(L.Music.Download),
                FontAwesomeIcon.ArrowDown);
            AddPlaylistAction(ref count, PlaylistCommand.Share, Loc.T(L.Music.Library.SharePlaylist),
                FontAwesomeIcon.ShareSquare);
        }

        if (playlist.SourceUrl.Length > 0)
        {
            AddPlaylistAction(ref count, PlaylistCommand.Refresh, Loc.T(L.Common.Refresh), FontAwesomeIcon.Sync);
        }

        AddPlaylistAction(ref count, PlaylistCommand.Delete, Loc.T(L.Music.DeletePlaylist), FontAwesomeIcon.Trash,
            true);
        return count;
    }

    private void AddPlaylistAction(ref int count, PlaylistCommand command, string label, FontAwesomeIcon icon,
        bool danger = false)
    {
        libraryActionItems[count] = new ActionSheet.Item(label, IconGlyph.Of(icon), danger);
        playlistCommands[count] = command;
        count++;
    }

    private void RunLibraryAction(int picked)
    {
        switch (libraryMenu)
        {
            case LibraryMenu.PlaylistSort:
                SetPlaylistSort((PlaylistSort)picked);
                return;
            case LibraryMenu.SongSort:
                SetLibrarySongSort((SongSort)picked);
                return;
            case LibraryMenu.Playlist:
                RunPlaylistCommand(playlistCommands[picked]);
                return;
        }
    }

    private void RunPlaylistCommand(PlaylistCommand command)
    {
        var playlistId = menuPlaylistId;
        switch (command)
        {
            case PlaylistCommand.Play:
                PlayPlaylist(playlistId, false);
                return;
            case PlaylistCommand.Shuffle:
                PlayPlaylist(playlistId, true);
                return;
            case PlaylistCommand.Rename:
                OpenPlaylistForRename(playlistId);
                return;
            case PlaylistCommand.Download:
                DownloadPlaylist(playlistId);
                return;
            case PlaylistCommand.Share:
                SharePlaylist(playlistId);
                return;
            case PlaylistCommand.Refresh:
                if (library.FindPlaylist(playlistId) is { } playlist)
                {
                    StartPlaylistRefresh(playlist);
                }

                return;
            default:
                ConfirmDeletePlaylist(playlistId);
                return;
        }
    }

    private void OpenPlaylistForRename(string playlistId)
    {
        var current = Router.Current;
        if (current.Screen != MusicScreen.PlaylistDetail ||
            !string.Equals(current.Key, playlistId, StringComparison.Ordinal))
        {
            Push(MusicRoute.Playlist(playlistId));
        }

        BeginPlaylistEdit(playlistId, true, false);
    }

    private void PumpDownloadFailures()
    {
        var failures = downloads.Failures;
        if (failures == seenDownloadFailures)
        {
            return;
        }

        var first = seenDownloadFailures < 0;
        seenDownloadFailures = failures;
        if (!first)
        {
            ShellToast.Show(Loc.T(L.Music.Library.DownloadFailed));
        }
    }

    private void DisposeLibrary()
    {
        importer?.Dispose();
        refreshImporter?.Dispose();
        addSongsSheet?.Dispose();
        artistTopFetch?.Cancel();
        artistTopFetch?.Dispose();
        artistTopFetch = null;
    }
}
