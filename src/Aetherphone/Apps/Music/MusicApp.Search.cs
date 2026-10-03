using Aetherphone.Apps.Music.Components;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Radio;
using Aetherphone.Core.Songs;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float SearchFieldHeight = 44f;
    private const int SearchQueryLimit = 120;
    private const int SearchScopeCount = 6;
    private const int SearchCacheLimit = 16;
    private const long SearchCacheSeconds = 30 * 60;
    private const float LibraryDebounceSeconds = 0.2f;
    private const string SearchContext = "search";
    private const string SearchLibraryContext = "search.library";

    private readonly ChipRail searchScopeRail = new();
    private readonly string[] searchScopeLabels = new string[SearchScopeCount];
    private readonly bool[] searchScopeActive = new bool[SearchScopeCount];
    private readonly Dictionary<string, SearchBundle> searchCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<CommunityStationDto> communityMatches = new();
    private readonly List<Song> libraryMatchBuffer = new();
    private readonly List<PlaylistRecord> libraryPlaylistBuffer = new();
    private readonly HashSet<string> libraryMatchSeen = new(StringComparer.Ordinal);
    private SearchBundle? searchBundle;
    private SearchScope searchScope = SearchScope.Top;
    private CancellationTokenSource? searchFetch;
    private Song[] libraryMatches = Array.Empty<Song>();
    private PlaylistRecord[] libraryPlaylistMatches = Array.Empty<PlaylistRecord>();
    private CommunityStationDto[]? communityMatchSource;
    private string communityMatchQuery = string.Empty;
    private string searchScopeLanguage = string.Empty;
    private string searchDraft = string.Empty;
    private string searchDraftSeen = string.Empty;
    private string lastSearchQuery = string.Empty;
    private float searchDraftChangedAt;
    private int libraryMatchVersion = -1;
    private bool libraryPending;
    private bool focusSearch;

    private Func<string, bool>? PastedLinkHandler { get; set; }

    private enum SearchScope : byte
    {
        Top,
        Songs,
        Playlists,
        Artists,
        Radio,
        Library,
    }

    private readonly record struct ArtistHit(string ChannelId, string Name, string ThumbnailUrl, int Count);

    private sealed class SearchBundle
    {
        public SearchBundle(string query, long createdUnix)
        {
            Query = query;
            CreatedUnix = createdUnix;
        }

        public string Query { get; }

        public long CreatedUnix { get; }

        public Song[] Songs { get; set; } = Array.Empty<Song>();

        public ArtistHit[] Artists { get; set; } = Array.Empty<ArtistHit>();

        public SongPlaylistHit[] Playlists { get; set; } = Array.Empty<SongPlaylistHit>();

        public RadioStation[] Stations { get; set; } = Array.Empty<RadioStation>();

        public int TopArtist { get; set; } = -1;

        public volatile bool SongsDone;

        public volatile bool PlaylistsDone;

        public volatile bool StationsDone;

        public bool Complete => SongsDone && PlaylistsDone && StationsDone;
    }

    private void OpenSearchFor(string query)
    {
        searchDraft = query;
        searchDraftSeen = query;
        searchScope = SearchScope.Top;
        BeginSearch(query);
        tab = MusicTab.Search;
        Router.Reset();
    }

    private void DrawSearch(in PhoneContext context)
    {
        var scale = UiScale.Current;
        community.EnsureFresh(false);
        var frame = BeginPage(context);
        var body = frame.Body;
        var resultsTop = DrawSearchChrome(body, scale);
        TrackSearchDraft();
        var results = new Rect(new Vector2(body.Min.X, resultsTop), body.Max);
        using (AppSurface.BeginEdgeToEdge(results))
        {
            if (string.IsNullOrWhiteSpace(searchDraft))
            {
                DrawSearchHome(scale);
            }
            else
            {
                DrawSearchResults(scale, Unobstructed(results));
            }
        }

        EndPage(in frame, context, Loc.T(L.Common.Search));
    }

    private float DrawSearchChrome(Rect body, float scale)
    {
        var inset = MusicUi.Inset * scale;
        var fieldTop = body.Min.Y + Metrics.Space.Xs * scale;
        var bar = new Rect(new Vector2(body.Min.X + inset, fieldTop),
            new Vector2(body.Max.X - inset, fieldTop + SearchFieldHeight * scale));
        var focus = focusSearch;
        focusSearch = false;
        SearchField.Draw(bar, "##musicSearch", Loc.T(L.Music.Search.FieldHint), ref searchDraft, ui.Palette,
            SearchQueryLimit, focus);
        var submitted = ImGui.IsItemDeactivated() &&
                        (ImGui.IsKeyPressed(ImGuiKey.Enter) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter));
        if (submitted && !string.IsNullOrWhiteSpace(searchDraft) && !TryHandlePastedText(searchDraft))
        {
            BeginSearch(searchDraft);
        }

        var bottom = bar.Max.Y;
        if (string.IsNullOrWhiteSpace(searchDraft))
        {
            return bottom + Metrics.Space.Xs * scale;
        }

        EnsureScopeLabels();
        for (var index = 0; index < SearchScopeCount; index++)
        {
            searchScopeActive[index] = index == (int)searchScope;
        }

        var railTop = bottom + Metrics.Space.Xs * scale;
        var row = new Rect(new Vector2(body.Min.X + inset, railTop),
            new Vector2(body.Max.X - inset, railTop + ChipRail.RowHeight * scale));
        UiAnchors.Report("music.search.scopes", row);
        var tapped = searchScopeRail.Draw(row, ui, searchScopeLabels, searchScopeActive);
        if (tapped >= 0)
        {
            searchScope = (SearchScope)tapped;
            libraryPending = true;
        }

        return row.Max.Y + Metrics.Space.Sm * scale;
    }

    private void EnsureScopeLabels()
    {
        var language = Loc.Current.Code;
        if (string.Equals(language, searchScopeLanguage, StringComparison.Ordinal))
        {
            return;
        }

        searchScopeLanguage = language;
        searchScopeLabels[(int)SearchScope.Top] = Loc.T(L.Music.Search.ScopeTop);
        searchScopeLabels[(int)SearchScope.Songs] = Loc.T(L.Music.Search.ScopeSongs);
        searchScopeLabels[(int)SearchScope.Playlists] = Loc.T(L.Music.Search.ScopePlaylists);
        searchScopeLabels[(int)SearchScope.Artists] = Loc.T(L.Music.Search.ScopeArtists);
        searchScopeLabels[(int)SearchScope.Radio] = Loc.T(L.Music.Search.ScopeRadio);
        searchScopeLabels[(int)SearchScope.Library] = Loc.T(L.Music.Search.ScopeLibrary);
    }

    private void TrackSearchDraft()
    {
        if (!string.Equals(searchDraft, searchDraftSeen, StringComparison.Ordinal))
        {
            searchDraftSeen = searchDraft;
            searchDraftChangedAt = clock;
            libraryPending = true;
            if (TryHandlePastedText(searchDraft))
            {
                return;
            }
        }

        if (libraryMatchVersion != library.Version)
        {
            libraryPending = true;
        }

        if (!libraryPending || searchScope != SearchScope.Library ||
            clock - searchDraftChangedAt < LibraryDebounceSeconds)
        {
            return;
        }

        MatchLibrary(searchDraft.Trim());
    }

    private bool TryHandlePastedText(string text)
    {
        if (PastedLinkHandler is not { } handler || !LooksLikeYouTubeLink(text))
        {
            return false;
        }

        if (!handler(text.Trim()))
        {
            return false;
        }

        ImGuiP.ClearActiveID();
        searchDraft = string.Empty;
        searchDraftSeen = string.Empty;
        return true;
    }

    private static bool LooksLikeYouTubeLink(string text)
    {
        return text.Contains("youtube.com/", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase);
    }

    private void BeginSearch(string query)
    {
        var trimmed = query.Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        lastSearchQuery = trimmed;
        library.RecordSearch(trimmed);
        MatchLibrary(trimmed);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (searchBundle is { } previous && !previous.Complete)
        {
            searchCache.Remove(previous.Query);
        }

        searchFetch?.Cancel();
        searchFetch?.Dispose();
        searchFetch = null;
        if (searchCache.TryGetValue(trimmed, out var cached) && now - cached.CreatedUnix < SearchCacheSeconds)
        {
            searchBundle = cached;
            return;
        }

        if (searchCache.Count >= SearchCacheLimit)
        {
            searchCache.Clear();
        }

        var bundle = new SearchBundle(trimmed, now);
        searchCache[trimmed] = bundle;
        searchBundle = bundle;
        searchFetch = new CancellationTokenSource();
        var token = searchFetch.Token;
        _ = Task.Run(() => SearchSongsAsync(bundle, token));
        _ = Task.Run(() => SearchPlaylistsAsync(bundle, token));
        _ = Task.Run(() => SearchStationsAsync(bundle, token));
    }

    private async Task SearchSongsAsync(SearchBundle bundle, CancellationToken token)
    {
        var found = await songSearch.SearchAsync(bundle.Query, SongSearchScope.Songs, token).ConfigureAwait(false);
        if (token.IsCancellationRequested)
        {
            return;
        }

        var artists = GroupArtists(found);
        bundle.Artists = artists;
        bundle.TopArtist = PickTopArtist(artists, bundle.Query);
        bundle.Songs = found;
        bundle.SongsDone = true;
    }

    private async Task SearchPlaylistsAsync(SearchBundle bundle, CancellationToken token)
    {
        var found = await songSearch.SearchPlaylistsAsync(bundle.Query, token).ConfigureAwait(false);
        if (token.IsCancellationRequested)
        {
            return;
        }

        bundle.Playlists = found;
        bundle.PlaylistsDone = true;
    }

    private async Task SearchStationsAsync(SearchBundle bundle, CancellationToken token)
    {
        try
        {
            var page = await radio.SearchByNameAsync(bundle.Query, RadioFilter.Default, 0, token)
                .ConfigureAwait(false);
            if (token.IsCancellationRequested)
            {
                return;
            }

            bundle.Stations = page.Stations ?? Array.Empty<RadioStation>();
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "Music radio search failed");
        }

        bundle.StationsDone = true;
    }

    private static ArtistHit[] GroupArtists(Song[] songs)
    {
        var order = new List<string>();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var firsts = new Dictionary<string, Song>(StringComparer.Ordinal);
        for (var index = 0; index < songs.Length; index++)
        {
            var song = songs[index];
            if (song.ChannelId.Length == 0 || song.Author.Length == 0)
            {
                continue;
            }

            if (counts.TryGetValue(song.ChannelId, out var count))
            {
                counts[song.ChannelId] = count + 1;
                continue;
            }

            counts[song.ChannelId] = 1;
            firsts[song.ChannelId] = song;
            order.Add(song.ChannelId);
        }

        var artists = new ArtistHit[order.Count];
        for (var index = 0; index < order.Count; index++)
        {
            var first = firsts[order[index]];
            artists[index] = new ArtistHit(first.ChannelId, first.Author, first.ThumbnailUrl, counts[order[index]]);
        }

        Array.Sort(artists, static (left, right) => right.Count.CompareTo(left.Count));
        return artists;
    }

    private static int PickTopArtist(ArtistHit[] artists, string query)
    {
        for (var index = 0; index < artists.Length; index++)
        {
            var name = artists[index].Name;
            if (string.Equals(name, query, StringComparison.OrdinalIgnoreCase) ||
                (artists[index].Count > 1 && name.Contains(query, StringComparison.OrdinalIgnoreCase)))
            {
                return index;
            }
        }

        return -1;
    }

    private void MatchLibrary(string query)
    {
        libraryPending = false;
        libraryMatchVersion = library.Version;
        libraryMatchBuffer.Clear();
        libraryPlaylistBuffer.Clear();
        libraryMatchSeen.Clear();
        if (query.Length == 0)
        {
            libraryMatches = Array.Empty<Song>();
            libraryPlaylistMatches = Array.Empty<PlaylistRecord>();
            return;
        }

        var songs = library.Songs;
        for (var index = 0; index < songs.Count; index++)
        {
            AddLibraryMatch(songs[index].ToSong(), query);
        }

        var loved = library.LovedSongs();
        for (var index = 0; index < loved.Length; index++)
        {
            AddLibraryMatch(loved[index], query);
        }

        var recent = library.RecentlyPlayed(LibraryStore.PlayHistoryCapacity);
        for (var index = 0; index < recent.Length; index++)
        {
            AddLibraryMatch(recent[index], query);
        }

        var playlists = library.Playlists;
        for (var index = 0; index < playlists.Count; index++)
        {
            if (playlists[index].Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                libraryPlaylistBuffer.Add(playlists[index]);
            }
        }

        libraryMatches = libraryMatchBuffer.ToArray();
        libraryPlaylistMatches = libraryPlaylistBuffer.ToArray();
    }

    private void AddLibraryMatch(in Song song, string query)
    {
        if (song.IsEmpty || libraryMatchSeen.Contains(song.VideoId))
        {
            return;
        }

        if (!song.Title.Contains(query, StringComparison.OrdinalIgnoreCase) &&
            !song.Author.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        libraryMatchSeen.Add(song.VideoId);
        libraryMatchBuffer.Add(song);
    }

    private void MatchCommunityStations(string query)
    {
        var stations = community.Stations;
        if (ReferenceEquals(stations, communityMatchSource) &&
            string.Equals(query, communityMatchQuery, StringComparison.Ordinal))
        {
            return;
        }

        communityMatchSource = stations;
        communityMatchQuery = query;
        communityMatches.Clear();
        if (query.Length == 0)
        {
            return;
        }

        for (var index = 0; index < stations.Length; index++)
        {
            if (MatchesQuery(stations[index], query))
            {
                communityMatches.Add(stations[index]);
            }
        }
    }

    private static bool MatchesQuery(CommunityStationDto station, string query)
    {
        if (station.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || station.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        for (var index = 0; index < station.Tags.Length; index++)
        {
            if (station.Tags[index].Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void DisposeSearch()
    {
        searchFetch?.Cancel();
        searchFetch?.Dispose();
        catalog?.Dispose();
    }
}
