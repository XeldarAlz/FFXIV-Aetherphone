namespace Aetherphone.Apps.Music;

internal enum MusicTab : byte
{
    Home,
    New,
    Radio,
    Library,
    Search,
}

internal enum MusicScreen : byte
{
    Home,
    New,
    Radio,
    Library,
    Search,
    PlaylistDetail,
    ArtistDetail,
    SongList,
    GenreDetail,
    LibrarySongs,
    LibraryArtists,
    LibraryPlaylists,
    Stations,
    StationFilter,
    CommunityStation,
    MyStation,
    StationArtwork,
    LiveDjs,
    LiveDjDetail,
    VenueDetail,
    JamLobby,
    Import,
    Replay,
}

internal enum SongListKind : byte
{
    None,
    Loved,
    Downloaded,
    RecentlyAdded,
    RecentlyPlayed,
    TopSongs,
    Genre,
}

internal readonly record struct MusicRoute(MusicScreen Screen, string Key, string Label, SongListKind List)
{
    public const string CountryFilter = "country";
    public const string LanguageFilter = "language";

    public static readonly MusicRoute HomeRoot = Of(MusicScreen.Home);
    public static readonly MusicRoute NewRoot = Of(MusicScreen.New);
    public static readonly MusicRoute RadioRoot = Of(MusicScreen.Radio);
    public static readonly MusicRoute LibraryRoot = Of(MusicScreen.Library);
    public static readonly MusicRoute SearchRoot = Of(MusicScreen.Search);

    public bool IsRoot => Screen <= MusicScreen.Search;

    public static MusicRoute Of(MusicScreen screen) => new(screen, string.Empty, string.Empty, SongListKind.None);

    public static MusicRoute Root(MusicTab tab) => tab switch
    {
        MusicTab.New => NewRoot,
        MusicTab.Radio => RadioRoot,
        MusicTab.Library => LibraryRoot,
        MusicTab.Search => SearchRoot,
        _ => HomeRoot,
    };

    public static MusicRoute Playlist(string playlistId) =>
        new(MusicScreen.PlaylistDetail, playlistId, string.Empty, SongListKind.None);

    public static MusicRoute Artist(string channelId, string name) =>
        new(MusicScreen.ArtistDetail, channelId, name, SongListKind.None);

    public static MusicRoute Songs(SongListKind list, string key = "", string label = "") =>
        new(MusicScreen.SongList, key, label, list);

    public static MusicRoute Genre(string key, string label) =>
        new(MusicScreen.GenreDetail, key, label, SongListKind.Genre);

    public static MusicRoute CommunityStation(string stationId) =>
        new(MusicScreen.CommunityStation, stationId, string.Empty, SongListKind.None);

    public static MusicRoute StationFilter(bool country) =>
        new(MusicScreen.StationFilter, country ? CountryFilter : LanguageFilter, string.Empty, SongListKind.None);

    public static MusicRoute LiveDj(string key, string name) =>
        new(MusicScreen.LiveDjDetail, key, name, SongListKind.None);

    public static MusicRoute Venue(string key, string name) =>
        new(MusicScreen.VenueDetail, key, name, SongListKind.None);
}
