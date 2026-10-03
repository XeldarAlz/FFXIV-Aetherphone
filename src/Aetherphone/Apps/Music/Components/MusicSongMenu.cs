using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Playback;
using Aetherphone.Core.Songs;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music.Components;

internal enum SongMenuOutcome : byte
{
    None,
    AddToPlaylist,
    GoToArtist,
}

internal sealed class MusicSongMenu
{
    private const int MaxItems = 10;
    private const string ShareLinkPrefix = "https://music.youtube.com/watch?v=";
    private const string StationContextPrefix = "station.";

    private enum Command : byte
    {
        PlayNext,
        PlayLast,
        AddToPlaylist,
        Love,
        Library,
        Download,
        GoToArtist,
        Share,
        StartStation,
    }

    private readonly ActionSheet sheet = new();
    private readonly ActionSheet.Item[] items = new ActionSheet.Item[MaxItems];
    private readonly Command[] commands = new Command[MaxItems];
    private Song song;

    public Song Song => song;

    public bool IsOpen => sheet.IsOpen;

    public bool CapturesPointer => sheet.CapturesPointer;

    public void Open(in Song target)
    {
        if (target.IsEmpty)
        {
            return;
        }

        song = target;
        sheet.Open();
    }

    public void Close() => sheet.Close();

    public SongMenuOutcome Draw(Rect screen, MusicKit kit)
    {
        if (!sheet.CapturesPointer)
        {
            return SongMenuOutcome.None;
        }

        var count = Build(kit.Library, kit.Downloads);
        var picked = sheet.Draw(screen, ActionSheetStyle.From(kit.Ui), items.AsSpan(0, count),
            Loc.T(L.Common.Cancel), false, song.Title);
        return picked < 0 ? SongMenuOutcome.None : Run(commands[picked], kit);
    }

    private int Build(LibraryStore library, DownloadStore? downloads)
    {
        var count = 0;
        Add(ref count, Command.PlayNext, Loc.T(L.Music.PlayNext), FontAwesomeIcon.Reply);
        Add(ref count, Command.PlayLast, Loc.T(L.Music.PlayLast), FontAwesomeIcon.ListUl);
        Add(ref count, Command.AddToPlaylist, Loc.T(L.Music.AddToPlaylist), FontAwesomeIcon.Plus);
        var loved = library.IsLoved(song.VideoId);
        Add(ref count, Command.Love, Loc.T(loved ? L.Music.Unlove : L.Music.Love), FontAwesomeIcon.Heart);
        var inLibrary = library.InLibrary(song.VideoId);
        Add(ref count, Command.Library, Loc.T(inLibrary ? L.Music.RemoveFromLibrary : L.Music.AddToLibrary),
            inLibrary ? FontAwesomeIcon.Trash : FontAwesomeIcon.PlusCircle, inLibrary);
        var downloaded = downloads is null
            ? library.IsDownloaded(song.VideoId)
            : downloads.StateOf(song.VideoId) is DownloadState.Queued or DownloadState.Downloading or DownloadState.Done;
        Add(ref count, Command.Download, Loc.T(downloaded ? L.Music.RemoveDownload : L.Music.Download),
            downloaded ? FontAwesomeIcon.Times : FontAwesomeIcon.ArrowDown);
        if (song.ChannelId.Length > 0)
        {
            Add(ref count, Command.GoToArtist, Loc.T(L.Music.GoToArtist), FontAwesomeIcon.User);
        }

        Add(ref count, Command.Share, Loc.T(L.Music.ShareSong), FontAwesomeIcon.ShareSquare);
        Add(ref count, Command.StartStation, Loc.T(L.Music.StartStation), FontAwesomeIcon.BroadcastTower);
        return count;
    }

    private void Add(ref int count, Command command, string label, FontAwesomeIcon icon, bool danger = false)
    {
        items[count] = new ActionSheet.Item(label, IconGlyph.Of(icon), danger);
        commands[count] = command;
        count++;
    }

    private SongMenuOutcome Run(Command command, MusicKit kit)
    {
        var library = kit.Library;
        var playback = kit.Playback;
        switch (command)
        {
            case Command.PlayNext:
                playback.PlayNext(song);
                return SongMenuOutcome.None;
            case Command.PlayLast:
                playback.PlayLast(song);
                return SongMenuOutcome.None;
            case Command.AddToPlaylist:
                return SongMenuOutcome.AddToPlaylist;
            case Command.Love:
                library.SetLoved(song, !library.IsLoved(song.VideoId));
                return SongMenuOutcome.None;
            case Command.Library:
                ToggleLibrary(library);
                return SongMenuOutcome.None;
            case Command.Download:
                ToggleDownload(kit);
                return SongMenuOutcome.None;
            case Command.GoToArtist:
                return SongMenuOutcome.GoToArtist;
            case Command.Share:
                ImGui.SetClipboardText(ShareLinkPrefix + song.VideoId);
                ShellToast.Show(Loc.T(L.Music.LinkCopied));
                return SongMenuOutcome.None;
            default:
                StartStation(playback);
                return SongMenuOutcome.None;
        }
    }

    private void ToggleDownload(MusicKit kit)
    {
        if (kit.Downloads is { } downloads)
        {
            downloads.Toggle(song);
            return;
        }

        kit.Library.SetDownloaded(song, !kit.Library.IsDownloaded(song.VideoId));
    }

    private void ToggleLibrary(LibraryStore library)
    {
        if (library.InLibrary(song.VideoId))
        {
            library.RemoveFromLibrary(song.VideoId);
            return;
        }

        library.AddToLibrary(song);
    }

    private void StartStation(PlaybackHub playback)
    {
        playback.SetAutoplay(true);
        playback.PlaySongs(new[] { song }, 0, StationContextPrefix + song.VideoId,
            string.Format(Loc.Culture, Loc.T(L.Music.StationFor), song.Title));
    }
}
