using Aetherphone.Apps.Music.Components;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Songs;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const int TabCount = 5;
    private const string NavBarId = "music.nav";

    private static readonly string[] TabIds = ["music.tab.home", "music.tab.new", "music.tab.radio",
        "music.tab.library", "music.tab.search"];

    private readonly ViewRouter<MusicRoute>[] routers;
    private readonly RouterDraw<MusicRoute> drawView;
    private readonly Action popPage;
    private readonly TabBar tabBar = new();
    private readonly TabItem[] tabItems = new TabItem[TabCount];
    private MusicTab tab = MusicTab.Home;
    private int pageDepth = 1;
    private float bottomChrome;

    private ViewRouter<MusicRoute> Router => routers[(int)tab];

    private static ViewRouter<MusicRoute>[] CreateRouters()
    {
        var created = new ViewRouter<MusicRoute>[TabCount];
        for (var index = 0; index < TabCount; index++)
        {
            created[index] = new ViewRouter<MusicRoute>(MusicRoute.Root((MusicTab)index));
        }

        return created;
    }

    private void ResetTabs()
    {
        for (var index = 0; index < routers.Length; index++)
        {
            routers[index].Reset();
        }

        tab = MusicTab.Home;
    }

    private void SelectTab(MusicTab wanted)
    {
        if (wanted == MusicTab.Search)
        {
            focusSearch = true;
        }

        if (tab == wanted)
        {
            Router.Reset();
            return;
        }

        tab = wanted;
    }

    private void ShowOnTab(MusicTab target, in MusicRoute route)
    {
        tab = target;
        var router = Router;
        if (router.Current == route)
        {
            return;
        }

        router.Reset();
        router.Push(route, false);
    }

    private void Push(in MusicRoute route) => Router.Push(route);

    private void PopPage() => Router.Pop();

    private void DrawShell(Rect content, Rect screen, float scale, float delta)
    {
        TickJam();
        var sheetsCapture = songMenu.CapturesPointer || playlistPicker.CapturesPointer || PcMediaCapturesPointer ||
                            JamCapturesPointer || LibraryOverlaysCapture;
        var stage = TabBar.ContentArea(content, scale);
        bottomChrome = TabBar.ContentInset(scale) + MiniPlayerInset(scale, delta);
        using (InputShield.Engage(sheetsCapture || NowPlayingCapturesPointer))
        {
            using (AppSurface.ReserveBottom(bottomChrome))
            {
                ImGui.PushID(TabIds[(int)tab]);
                Router.Draw(content, AppSkin.Transparent, delta, drawView);
                ImGui.PopID();
            }

            DrawMiniPlayer(stage, scale);
            DrawTabBar(content);
        }

        using (InputShield.Engage(sheetsCapture))
        {
            DrawNowPlayingSheet(screen, scale);
        }

        DrawPcMediaSheet(screen, scale);

        DrawSongMenu(screen);
        playlistPicker.Draw(screen, kit);
        DrawJamOverlays(screen);
        DrawLibraryOverlays(screen);
    }

    private void DrawTabBar(Rect area)
    {
        tabItems[0] = new TabItem(Loc.T(L.Music.TabHome), IconGlyph.Of(FontAwesomeIcon.Home),
            AnchorKey: "music.tab.home");
        tabItems[1] = new TabItem(Loc.T(L.Music.TabNew), IconGlyph.Of(FontAwesomeIcon.ThLarge));
        tabItems[2] = new TabItem(Loc.T(L.Music.TabRadio), IconGlyph.Of(FontAwesomeIcon.BroadcastTower),
            Badge: community.FollowedLiveCount, AnchorKey: "music.tab.radio");
        tabItems[3] = new TabItem(Loc.T(L.Music.TabLibrary), IconGlyph.Of(FontAwesomeIcon.LayerGroup),
            AnchorKey: "music.tab.library");
        tabItems[4] = new TabItem(Loc.T(L.Common.Search), IconGlyph.Of(FontAwesomeIcon.Search),
            AnchorKey: "music.tab.search");
        var result = tabBar.Draw(area, ui, tabItems, (int)tab);
        if (result.Tapped < 0)
        {
            return;
        }

        SelectTab((MusicTab)result.Tapped);
    }

    private void DrawSongMenu(Rect screen)
    {
        var outcome = songMenu.Draw(screen, kit);
        var song = songMenu.Song;
        if (outcome == SongMenuOutcome.AddToPlaylist)
        {
            playlistPicker.Open(song);
            return;
        }

        if (outcome == SongMenuOutcome.GoToArtist)
        {
            CloseNowPlaying();
            Push(MusicRoute.Artist(song.ChannelId, song.Author));
        }
    }

    private void DrawView(MusicRoute route, Rect area, int depth)
    {
        ui.Body(area);
        pageDepth = depth;
        var context = new PhoneContext(area, theme, navigation);
        switch (route.Screen)
        {
            case MusicScreen.Home:
                DrawHome(context);
                break;
            case MusicScreen.New:
                DrawNew(context);
                break;
            case MusicScreen.Radio:
                DrawRadio(context);
                break;
            case MusicScreen.Library:
                DrawLibrary(context);
                break;
            case MusicScreen.Search:
                DrawSearch(context);
                break;
            case MusicScreen.PlaylistDetail:
                DrawPlaylistDetail(context, route);
                break;
            case MusicScreen.ArtistDetail:
                DrawArtistDetail(context, route);
                break;
            case MusicScreen.SongList:
                DrawSongList(context, route);
                break;
            case MusicScreen.GenreDetail:
                DrawGenreDetail(context, route);
                break;
            case MusicScreen.LibrarySongs:
                DrawLibrarySongs(context);
                break;
            case MusicScreen.LibraryArtists:
                DrawLibraryArtists(context);
                break;
            case MusicScreen.LibraryPlaylists:
                DrawLibraryPlaylists(context);
                break;
            case MusicScreen.Stations:
                DrawStations(context);
                break;
            case MusicScreen.StationFilter:
                DrawStationFilter(context, route);
                break;
            case MusicScreen.CommunityStation:
                DrawCommunityStation(context, route);
                break;
            case MusicScreen.MyStation:
                DrawMyStation(context);
                break;
            case MusicScreen.StationArtwork:
                DrawStationArtwork(context);
                break;
            case MusicScreen.LiveDjs:
                DrawLiveDjs(context);
                break;
            case MusicScreen.LiveDjDetail:
                DrawLiveDjDetail(context);
                break;
            case MusicScreen.VenueDetail:
                DrawVenueDetail(context);
                break;
            case MusicScreen.JamLobby:
                DrawJamLobby(context);
                break;
            case MusicScreen.Import:
                DrawImport(context);
                break;
            case MusicScreen.Replay:
                DrawReplay(context);
                break;
        }
    }

    private NavBarFrame BeginPage(in PhoneContext context) => AppHeader.BeginLargeTitle(context, pageDepth > 1);

    private int EndPage(in NavBarFrame frame, in PhoneContext context, string title) =>
        EndPage(in frame, context, title, ReadOnlySpan<NavBarButton>.Empty);

    private int EndPage(in NavBarFrame frame, in PhoneContext context, string title,
        ReadOnlySpan<NavBarButton> buttons)
    {
        if (pageDepth <= 1 || !Router.TryGetView(pageDepth - 2, out var previous))
        {
            return AppHeader.EndLargeTitle(in frame, context, NavBarId, title, NavBarStyle.From(ui), buttons);
        }

        return AppHeader.EndLargeTitle(in frame, context, NavBarId, title, NavBarStyle.From(ui), buttons,
            TitleOf(previous), popPage);
    }

    private Rect Unobstructed(Rect body) =>
        new(body.Min, new Vector2(body.Max.X, MathF.Max(body.Min.Y, body.Max.Y - bottomChrome)));

    private string TitleOf(in MusicRoute route)
    {
        return route.Screen switch
        {
            MusicScreen.Home => Loc.T(L.Music.TabHome),
            MusicScreen.New => Loc.T(L.Music.TabNew),
            MusicScreen.Radio => Loc.T(L.Music.TabRadio),
            MusicScreen.Library => Loc.T(L.Music.TabLibrary),
            MusicScreen.Search => Loc.T(L.Common.Search),
            MusicScreen.PlaylistDetail => PlaylistTitle(route.Key),
            MusicScreen.SongList => SongListTitle(route),
            MusicScreen.LibrarySongs => Loc.T(L.Music.LibrarySongs),
            MusicScreen.LibraryArtists => Loc.T(L.Music.LibraryArtists),
            MusicScreen.LibraryPlaylists => Loc.T(L.Music.LibraryPlaylists),
            MusicScreen.Stations => StationsTitle(),
            MusicScreen.StationFilter => Loc.T(route.Key == MusicRoute.CountryFilter
                ? L.Music.FilterCountry
                : L.Music.FilterLanguage),
            MusicScreen.CommunityStation => CommunityStationTitle(route.Key),
            MusicScreen.MyStation => Loc.T(L.Music.MyStation),
            MusicScreen.StationArtwork => Loc.T(L.Music.StationArtwork),
            MusicScreen.LiveDjs => Loc.T(L.Music.LiveDjs),
            MusicScreen.JamLobby => Loc.T(L.Music.Jam.Title),
            MusicScreen.Import => Loc.T(L.Music.ImportPlaylist),
            MusicScreen.Replay => Loc.T(L.Music.Replay.Title),
            _ => route.Label,
        };
    }

    private string PlaylistTitle(string playlistId)
    {
        return library.FindPlaylist(playlistId) is { } playlist ? playlist.Name : Loc.T(L.Music.LibraryPlaylists);
    }

    private string SongListTitle(in MusicRoute route)
    {
        return route.List switch
        {
            SongListKind.Loved => Loc.T(L.Music.LovedSongs),
            SongListKind.Downloaded => Loc.T(L.Music.Downloaded),
            SongListKind.RecentlyAdded => Loc.T(L.Music.RecentlyAdded),
            SongListKind.RecentlyPlayed => Loc.T(L.Music.RecentlyPlayed),
            SongListKind.TopSongs => Loc.T(L.Music.TopSongs),
            _ => route.Label,
        };
    }

    private void PlayFrom(Song[] songs, int index, string contextId, string contextTitle)
    {
        if (index < 0 || index >= songs.Length)
        {
            return;
        }

        if (kit.IsCurrent(songs[index]))
        {
            playback.TogglePlayPause();
            return;
        }

        playback.PlaySongs(songs, index, contextId, contextTitle);
    }

    private void DrawSongRows(Song[] songs, string contextId, string contextTitle)
    {
        for (var index = 0; index < songs.Length; index++)
        {
            var action = SongRow.Draw(kit, songs[index]);
            if (action == SongRowAction.Play)
            {
                PlayFrom(songs, index, contextId, contextTitle);
            }
            else if (action == SongRowAction.Menu)
            {
                songMenu.Open(songs[index]);
            }
        }
    }

    private void ConsumeLaunchRequests()
    {
        ConsumeJamLaunch();
        if (!launcher.TryConsumeStation(out var stationId))
        {
            community.EnsureFresh(false);
            return;
        }

        community.OpenStation(stationId, null);
        openedStationId = stationId;
        community.EnsureFresh(true);
        showAllTracks = false;
        ShowOnTab(MusicTab.Radio, MusicRoute.CommunityStation(stationId));
    }
}
