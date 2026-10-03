using Aetherphone.Apps.Music.Components;
using Aetherphone.Apps.Music.Library;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Songs;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float LibraryRowHeight = 50f;
    private const float LibraryGlyphScale = 0.9f;
    private const float LibraryGlyphBox = 28f;
    private const float LibraryBottomGap = 24f;
    private const int RecentlyPlayedListCount = 100;
    private const int TopSongsCount = 100;
    private const int RecentlyAddedCount = 12;
    private const int RecentGridKey = 2000;
    private const string LibrarySongsContext = "library.songs";

    private static readonly string[] SongListContexts =
    [
        "list.none", "list.loved", "list.downloaded", "list.recentlyAdded", "list.recentlyPlayed", "list.topSongs",
        "list.genre",
    ];

    private readonly Song[][] songListCaches = new Song[SongListContexts.Length][];
    private readonly int[] songListVersions = [-1, -1, -1, -1, -1, -1, -1];
    private readonly NavBarButton[] songsPageButtons = new NavBarButton[1];
    private Song[] librarySongs = Array.Empty<Song>();
    private int librarySongsVersion = -1;
    private SongSort librarySongSort = SongSort.RecentlyAdded;
    private RecentItem[] recentlyAdded = Array.Empty<RecentItem>();
    private Song[] recentlyAddedSongs = Array.Empty<Song>();
    private int recentlyAddedVersion = -1;
    private int downloadedVersion = -1;
    private string downloadedSummary = string.Empty;
    private string downloadingText = string.Empty;
    private string summaryLanguage = string.Empty;

    private readonly struct RecentItem
    {
        public readonly string PlaylistId;
        public readonly int SongIndex;
        public readonly CoverArt Art;
        public readonly string Title;
        public readonly string Subtitle;
        public readonly long AddedUnix;

        public RecentItem(string playlistId, int songIndex, CoverArt art, string title, string subtitle, long addedUnix)
        {
            PlaylistId = playlistId;
            SongIndex = songIndex;
            Art = art;
            Title = title;
            Subtitle = subtitle;
            AddedUnix = addedUnix;
        }

        public bool IsPlaylist => PlaylistId.Length > 0;
    }

    private void DrawLibrary(in PhoneContext context)
    {
        var scale = UiScale.Current;
        EnsureRecentlyAdded();
        var frame = BeginPage(context);
        using (AppSurface.BeginEdgeToEdge(frame.Body))
        {
            if (DrawLibraryLink(scale, FontAwesomeIcon.ListUl, Loc.T(L.Music.LibraryPlaylists),
                    "music.library.playlists"))
            {
                Push(MusicRoute.Of(MusicScreen.LibraryPlaylists));
            }

            if (DrawLibraryLink(scale, FontAwesomeIcon.Microphone, Loc.T(L.Music.LibraryArtists)))
            {
                Push(MusicRoute.Of(MusicScreen.LibraryArtists));
            }

            if (DrawLibraryLink(scale, FontAwesomeIcon.Music, Loc.T(L.Music.LibrarySongs)))
            {
                Push(MusicRoute.Of(MusicScreen.LibrarySongs));
            }

            if (DrawLibraryLink(scale, FontAwesomeIcon.Heart, Loc.T(L.Music.LovedSongs)))
            {
                Push(MusicRoute.Songs(SongListKind.Loved));
            }

            if (DrawLibraryLink(scale, FontAwesomeIcon.ArrowDown, Loc.T(L.Music.Downloaded)))
            {
                Push(MusicRoute.Songs(SongListKind.Downloaded));
            }

            if (DrawLibraryLink(scale, FontAwesomeIcon.ChartBar, Loc.T(L.Music.Replay.Title)))
            {
                Push(MusicRoute.Of(MusicScreen.Replay));
            }

            if (recentlyAdded.Length > 0)
            {
                SectionHeader.Draw(ui, Loc.T(L.Music.RecentlyAdded), false);
                DrawRecentGrid(scale);
            }

            LibraryKit.Gap(LibraryBottomGap);
        }

        EndPage(in frame, context, Loc.T(L.Music.TabLibrary));
    }

    private bool DrawLibraryLink(float scale, FontAwesomeIcon icon, string label, string? anchorKey = null)
    {
        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, LibraryRowHeight * scale, ui.HoverWash);
        if (anchorKey is not null)
        {
            UiAnchors.Report(anchorKey, cell.Bounds);
        }

        var inset = MusicUi.Inset * scale;
        var centerY = cell.Bounds.Min.Y + cell.Bounds.Height * 0.5f;
        var glyphBox = LibraryGlyphBox * scale;
        AppSkin.Icon(drawList, new Vector2(cell.Bounds.Min.X + inset + glyphBox * 0.5f, centerY), IconGlyph.Of(icon),
            ui.Accent, LibraryGlyphScale);
        var textLeft = cell.Bounds.Min.X + inset + glyphBox + Metrics.Space.Md * scale;
        var labelHeight = Typography.LineHeight(TextStyles.Title3);
        Typography.Draw(drawList, new Vector2(textLeft, centerY - labelHeight * 0.5f),
            Typography.FitText(label, cell.Bounds.Max.X - inset * 2f - textLeft, TextStyles.Title3), ui.TitleInk,
            TextStyles.Title3);
        AppSkin.Icon(drawList, new Vector2(cell.Bounds.Max.X - inset, centerY),
            IconGlyph.Of(FontAwesomeIcon.ChevronRight), ui.MutedInk, LibraryGlyphScale * 0.7f);
        FeedCell.End(drawList, cell, ui.Hairline, false);
        FeedCell.Hairline(drawList, textLeft, cell.Bounds.Max.X, cell.Bounds.Max.Y, ui.Hairline);
        return cell.Tapped;
    }

    private void EnsureRecentlyAdded()
    {
        if (recentlyAddedVersion == library.Version)
        {
            return;
        }

        recentlyAddedVersion = library.Version;
        var playlists = library.Playlists;
        var songs = library.Songs;
        var songCount = Math.Min(songs.Count, RecentlyAddedCount);
        var pool = new List<RecentItem>(playlists.Count + songCount);
        var poolSongs = new Song[songCount];
        for (var index = 0; index < songCount; index++)
        {
            var record = songs[index];
            poolSongs[index] = record.ToSong();
            pool.Add(new RecentItem(string.Empty, index, CoverArt.None, record.Title, record.Author,
                record.AddedUnix));
        }

        for (var index = 0; index < playlists.Count; index++)
        {
            var playlist = playlists[index];
            pool.Add(new RecentItem(playlist.Id, -1, CoverArt.Of(playlist), playlist.Name,
                MusicUi.SongCount(playlist.Songs.Count), playlist.CreatedUnix));
        }

        pool.Sort(static (left, right) => right.AddedUnix.CompareTo(left.AddedUnix));
        if (pool.Count > RecentlyAddedCount)
        {
            pool.RemoveRange(RecentlyAddedCount, pool.Count - RecentlyAddedCount);
        }

        recentlyAdded = pool.ToArray();
        recentlyAddedSongs = poolSongs;
    }

    private void DrawRecentGrid(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var inset = MusicUi.Inset * scale;
        var gap = ShelfRail.TileGap * scale;
        var side = MathF.Max(1f, (width - inset * 2f - gap) * 0.5f);
        var cardHeight = ArtworkTile.CardHeight(side);
        var rowPitch = cardHeight + gap;
        var rows = (recentlyAdded.Length + 1) / 2;
        var drawList = ImGui.GetWindowDrawList();
        var clipTop = drawList.GetClipRectMin().Y;
        var clipBottom = drawList.GetClipRectMax().Y;
        for (var index = 0; index < recentlyAdded.Length; index++)
        {
            var column = index % 2;
            var row = index / 2;
            var min = new Vector2(origin.X + inset + column * (side + gap), origin.Y + row * rowPitch);
            var max = new Vector2(min.X + side, min.Y + cardHeight);
            if (max.Y < clipTop || min.Y > clipBottom)
            {
                continue;
            }

            DrawRecentTile(drawList, index, min, max, side);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, MathF.Max(0f, rows * rowPitch - gap)));
    }

    private void DrawRecentTile(ImDrawListPtr drawList, int index, Vector2 min, Vector2 max, float side)
    {
        var item = recentlyAdded[index];
        var hovered = UiInteract.Hover(min, max);
        if (item.IsPlaylist)
        {
            LibraryArt.DrawCover(drawList, images, wallpaperImages, min, side, item.Art, item.Title);
        }
        else
        {
            var song = recentlyAddedSongs[item.SongIndex];
            ArtworkTile.Draw(drawList, images, min, side, song.ThumbnailUrl, song.Title);
        }

        ArtworkTile.DrawPressed(drawList, min, side, hovered);
        var current = !item.IsPlaylist && kit.IsCurrent(recentlyAddedSongs[item.SongIndex]);
        ArtworkTile.DrawCaption(drawList, ui, min, side, item.Title, item.Subtitle, current);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (LibraryKit.Secondary(RecentGridKey + index, hovered))
        {
            OpenRecentMenu(item);
            return;
        }

        if (!UiInteract.Click(min, max, hovered))
        {
            return;
        }

        if (item.IsPlaylist)
        {
            Push(MusicRoute.Playlist(item.PlaylistId));
            return;
        }

        PlayFrom(recentlyAddedSongs, item.SongIndex, SongListContexts[(int)SongListKind.RecentlyAdded],
            Loc.T(L.Music.RecentlyAdded));
    }

    private void OpenRecentMenu(in RecentItem item)
    {
        if (item.IsPlaylist)
        {
            OpenPlaylistMenu(item.PlaylistId);
            return;
        }

        songMenu.Open(recentlyAddedSongs[item.SongIndex]);
    }

    private void DrawLibrarySongs(in PhoneContext context)
    {
        if (librarySongsVersion != library.Version)
        {
            librarySongsVersion = library.Version;
            librarySongs = LibrarySorting.Songs(library.LibrarySongs(), librarySongSort);
        }

        songsPageButtons[0] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.SortAmountDown),
            Loc.T(L.Music.Library.SortBy));
        var pressed = DrawSongListPage(context, librarySongs, Loc.T(L.Music.LibrarySongs), LibrarySongsContext,
            Loc.T(L.Music.LibraryEmptyTitle), Loc.T(L.Music.LibraryEmptySub), songsPageButtons);
        if (pressed == 0)
        {
            OpenSongSortMenu();
        }
    }

    private void SetLibrarySongSort(SongSort order)
    {
        librarySongSort = order;
        librarySongsVersion = -1;
    }

    private void DrawSongList(in PhoneContext context, in MusicRoute route)
    {
        var slot = (int)route.List;
        if (route.List == SongListKind.Downloaded)
        {
            DrawDownloaded(context, route);
            return;
        }

        if (songListVersions[slot] != library.Version)
        {
            songListVersions[slot] = library.Version;
            songListCaches[slot] = LoadSongList(route.List);
        }

        var loved = route.List == SongListKind.Loved;
        DrawSongListPage(context, songListCaches[slot], SongListTitle(route), SongListContexts[slot],
            Loc.T(loved ? L.Music.Library.LovedEmptyTitle : L.Music.LibraryEmptyTitle),
            Loc.T(loved ? L.Music.Library.LovedEmptySub : L.Music.LibraryEmptySub),
            ReadOnlySpan<NavBarButton>.Empty);
    }

    private Song[] LoadSongList(SongListKind kind)
    {
        return kind switch
        {
            SongListKind.Loved => library.LovedSongs(),
            SongListKind.Downloaded => DownloadedSongs(),
            SongListKind.RecentlyAdded => library.LibrarySongs(),
            SongListKind.RecentlyPlayed => library.RecentlyPlayed(RecentlyPlayedListCount),
            SongListKind.TopSongs => library.MostPlayed(TopSongsCount),
            _ => Array.Empty<Song>(),
        };
    }

    private Song[] DownloadedSongs()
    {
        var ids = library.DownloadedIds();
        var found = new List<Song>(ids.Length);
        for (var index = 0; index < ids.Length; index++)
        {
            if (library.FindSong(ids[index]) is { } record)
            {
                found.Add(record.ToSong());
            }
        }

        return found.ToArray();
    }

    private void DrawDownloaded(in PhoneContext context, in MusicRoute route)
    {
        downloads.EnsureReconciled();
        var slot = (int)SongListKind.Downloaded;
        var language = Loc.Current.Code;
        if (songListVersions[slot] != library.Version || downloadedVersion != downloads.Version ||
            !string.Equals(summaryLanguage, language, StringComparison.Ordinal))
        {
            songListVersions[slot] = library.Version;
            downloadedVersion = downloads.Version;
            summaryLanguage = language;
            var songs = DownloadedSongs();
            songListCaches[slot] = songs;
            downloadedSummary = string.Format(Loc.Culture, Loc.T(L.Music.Library.Summary),
                MusicUi.SongCount(songs.Length),
                string.Format(Loc.Culture, Loc.T(L.Photos.SizeMegabytes),
                    LibraryKit.MegabytesText(downloads.TotalBytes)));
            var pending = downloads.PendingCount;
            downloadingText = pending > 0 ? Loc.Plural(L.Music.Library.DownloadingCount, pending) : string.Empty;
        }

        var scale = UiScale.Current;
        var title = SongListTitle(route);
        var list = songListCaches[slot];
        var frame = BeginPage(context);
        if (list.Length == 0 && downloadingText.Length == 0)
        {
            EmptyState.Draw(Unobstructed(frame.Body), ui, FontAwesomeIcon.ArrowDown,
                Loc.T(L.Music.Library.DownloadsEmptyTitle), Loc.T(L.Music.Library.DownloadsEmptySub));
            EndPage(in frame, context, title);
            return;
        }

        using (AppSurface.BeginEdgeToEdge(frame.Body))
        {
            DrawPlayShuffle(list, SongListContexts[slot], title);
            LibraryKit.Gap(Metrics.Space.Md);
            var width = ScrollLayout.StableContentWidth() - MusicUi.Inset * 2f * scale;
            var summary = downloadingText.Length > 0 ? downloadingText : downloadedSummary;
            LibraryKit.CenteredText(summary, TextStyles.Footnote, ui.MutedInk, width);
            LibraryKit.Gap(Metrics.Space.Sm);
            DrawSongRows(list, SongListContexts[slot], title);
            LibraryKit.Gap(LibraryBottomGap);
        }

        EndPage(in frame, context, title);
    }

    private int DrawSongListPage(in PhoneContext context, Song[] songs, string title, string contextId,
        string emptyTitle, string emptySub, ReadOnlySpan<NavBarButton> buttons)
    {
        var frame = BeginPage(context);
        if (songs.Length == 0)
        {
            EmptyState.Draw(Unobstructed(frame.Body), ui, FontAwesomeIcon.Music, emptyTitle, emptySub);
            return EndPage(in frame, context, title, ReadOnlySpan<NavBarButton>.Empty);
        }

        using (AppSurface.BeginEdgeToEdge(frame.Body))
        {
            DrawPlayShuffle(songs, contextId, title);
            LibraryKit.Gap(Metrics.Space.Md);
            DrawSongRows(songs, contextId, title);
            LibraryKit.Gap(LibraryBottomGap);
        }

        return EndPage(in frame, context, title, buttons);
    }

    private void DrawPlayShuffle(Song[] songs, string contextId, string title)
    {
        LibraryKit.Gap(Metrics.Space.Sm);
        var (play, shuffle) = LibraryKit.PlayShuffleRow(ui, Loc.T(L.Music.Library.Play), Loc.T(L.Music.Shuffle),
            songs.Length > 0);
        if (play)
        {
            playback.PlaySongs(songs, 0, contextId, title);
        }
        else if (shuffle)
        {
            playback.PlaySongsShuffled(songs, contextId, title);
        }
    }

    private void DrawRowText(ImDrawListPtr drawList, Rect bounds, float textLeft, float textRight, string title,
        string subtitle)
    {
        var textWidth = MathF.Max(1f, textRight - textLeft);
        var titleHeight = Typography.LineHeight(TextStyles.Body);
        var subtitleHeight = subtitle.Length > 0 ? Typography.LineHeight(TextStyles.Subheadline) : 0f;
        var top = bounds.Min.Y + (bounds.Height - titleHeight - subtitleHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top), Typography.FitText(title, textWidth, TextStyles.Body),
            ui.TitleInk, TextStyles.Body);
        if (subtitle.Length == 0)
        {
            return;
        }

        Typography.Draw(drawList, new Vector2(textLeft, top + titleHeight),
            Typography.FitText(subtitle, textWidth, TextStyles.Subheadline), ui.MutedInk, TextStyles.Subheadline);
    }
}
