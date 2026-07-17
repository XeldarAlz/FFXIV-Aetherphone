using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Analytics;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Net;
using Aetherphone.Core.Playback;
using Aetherphone.Core.Radio;
using Aetherphone.Core.Songs;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp : IPhoneApp
{
    private enum View : byte
    {
        Home,
        Stations,
        Search,
        CountryFilter,
        LanguageFilter,
    }

    private const float TopBarHeight = 46f;
    private const float SearchBarHeight = 50f;
    private const float MiniHeight = 56f;
    private const float MiniMargin = 8f;
    private const float MiniSmoothTime = 0.18f;
    private const float SheetSmoothTime = 0.22f;
    private const float ArtSmoothTime = 0.20f;
    private const int RecentTiles = 6;
    private const int FeaturedTiles = 4;

    private static readonly string[] FeaturedSeeds =
    {
        "final fantasy xiv soundtrack",
        "final fantasy xiv battle theme ost",
        "final fantasy xiv endwalker soundtrack",
        "final fantasy xiv shadowbringers soundtrack",
        "final fantasy xiv dawntrail soundtrack",
        "final fantasy xiv city theme music",
    };

    private static readonly string[] FeaturedPlayIds =
    {
        "music.card0", "music.card1", "music.card2", "music.card3",
    };

    public string Id => "music";
    public bool IsAvailable => flags.MusicEnabled;
    public Vector4 Accent => AppAccents.For(Id);
    public string DisplayName => Loc.T(L.Apps.Music);
    public string Glyph => "M";
    public int BadgeCount => 0;
    private readonly RadioService radio;
    private readonly SongSearchService songSearch;
    private readonly PlaybackHub playback;
    private readonly SongHistory history;
    private readonly MediaCache media;
    private readonly HttpService http;
    private readonly FeatureFlags flags;
    private readonly IAnalyticsService analytics;
    private readonly ArtworkCache artwork;
    private readonly ViewRouter<View> router;
    private readonly RouterDraw<View> drawView;
    private readonly AppSkin ui = new(AppPalettes.Music);
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private int categoryIndex = -1;
    private RadioStation[] stations = Array.Empty<RadioStation>();
    private volatile bool loading;
    private CancellationTokenSource? fetch;
    private string radioSearchDraft = string.Empty;
    private string radioQuery = string.Empty;
    private bool focusRadioSearch;
    private int stationOffset;
    private volatile bool stationHasMore;
    private volatile bool loadingMore;
    private readonly DropdownMenu radioSortMenu = new();
    private RadioOrder radioOrder = RadioOrder.Popular;
    private string radioCountryCode = string.Empty;
    private string radioCountryName = string.Empty;
    private string radioLanguage = string.Empty;
    private string radioLanguageName = string.Empty;
    private RadioFacet[] radioCountries = Array.Empty<RadioFacet>();
    private RadioFacet[] radioLanguages = Array.Empty<RadioFacet>();
    private volatile bool facetsLoading;
    private CancellationTokenSource? facetFetch;
    private string facetSearchDraft = string.Empty;
    private Song[] results = Array.Empty<Song>();
    private volatile bool searching;
    private bool hasSearched;
    private CancellationTokenSource? search;
    private string searchDraft = string.Empty;
    private string lastSearchQuery = string.Empty;
    private SongSearchScope searchScope = SongSearchScope.Songs;
    private bool focusSearch;
    private Song[] featured = Array.Empty<Song>();
    private volatile bool featuredLoading;
    private bool featuredRequested;
    private int featuredIndex = -1;
    private CancellationTokenSource? featuredFetch;
    private string lastRecordedVideoId = string.Empty;
    private string playSource = string.Empty;
    private bool nowPlayingOpen;
    private Spring miniPresence;
    private Spring sheetPresence;
    private Spring artBreath;
    private float clock;

    public MusicApp(RadioService radio, SongSearchService songSearch, PlaybackHub playback, SongHistory history,
        MediaCache media, HttpService http, ITextureProvider textures, FeatureFlags flags,
        IAnalyticsService analytics)
    {
        this.radio = radio;
        this.songSearch = songSearch;
        this.playback = playback;
        this.history = history;
        this.media = media;
        this.http = http;
        this.flags = flags;
        this.analytics = analytics;
        artwork = new ArtworkCache(textures);
        router = new ViewRouter<View>(View.Home, Id);
        drawView = DrawView;
        artBreath = new Spring(1f);
    }

    public void OnOpened()
    {
        router.Reset();
        nowPlayingOpen = false;
        sheetPresence.SnapTo(0f);
        miniPresence.SnapTo(playback.IsActive ? 1f : 0f);
        featuredIndex = (featuredIndex + 1) % FeaturedSeeds.Length;
        featuredRequested = false;
        featuredLoading = false;
        featured = Array.Empty<Song>();
        featuredFetch?.Cancel();
    }

    public void OnClosed()
    {
        router.Reset();
        nowPlayingOpen = false;
        sheetPresence.SnapTo(0f);
    }

    public void Draw(in PhoneContext context)
    {
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        clock += delta;
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        radioSortMenu.Gate();
        CaptureRecent();
        if (!playback.IsActive)
        {
            nowPlayingOpen = false;
            sheetPresence.SnapTo(0f);
        }

        var scale = ImGuiHelpers.GlobalScale;
        var content = context.Content;
        miniPresence.Step(playback.IsActive && !nowPlayingOpen ? 1f : 0f, MiniSmoothTime, delta);
        sheetPresence.Step(nowPlayingOpen ? 1f : 0f, SheetSmoothTime, delta);
        var sheetValue = Math.Clamp(sheetPresence.Value, 0f, 1f);

        var screen = SceneChrome.ScreenFrom(content, theme, scale);
        ui.Backdrop(screen);
        using (InputShield.Engage(sheetValue > 0.15f))
        {
            router.Draw(content, AppSkin.Transparent, delta, drawView);
        }

        DrawMiniPlayer(content, scale);
        DrawNowPlayingSheet(content, scale, sheetValue, delta);
    }

    private void DrawView(View view, Rect area, int depth)
    {
        ui.Body(area);
        var context = new PhoneContext(area, theme, navigation);
        switch (view)
        {
            case View.Stations:
                DrawStations(context);
                break;
            case View.Search:
                DrawSearch(context);
                break;
            case View.CountryFilter:
                DrawFacetPicker(context, true);
                break;
            case View.LanguageFilter:
                DrawFacetPicker(context, false);
                break;
            default:
                DrawHome(context);
                break;
        }
    }

    private void DrawTopBar(in PhoneContext context, string title, Action? onBack)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var content = context.Content;
        var rowCenterY = content.Min.Y + TopBarHeight * scale * 0.5f;
        var textLeft = content.Min.X + (onBack is null ? 16f * scale : 38f * scale);
        var fitted = Typography.FitText(title, content.Max.X - 16f * scale - textLeft, TextStyles.Title2);
        var titleSize = Typography.Measure(fitted, TextStyles.Title2);
        Typography.Draw(new Vector2(textLeft, rowCenterY - titleSize.Y * 0.5f), fitted, ui.TitleInk,
            TextStyles.Title2);
        if (onBack is null)
        {
            return;
        }

        var hitMin = content.Min;
        var hitMax = new Vector2(content.Min.X + 40f * scale, content.Min.Y + TopBarHeight * scale);
        var hovered = UiInteract.Hover(hitMin, hitMax);
        var clicked = BackButton.Draw("music.back", new Vector2(content.Min.X + 18f * scale, rowCenterY), 15f * scale,
            ui.TitleInk, hovered, scale);
        if (clicked)
        {
            onBack();
        }
    }

    private MediaResult Thumb(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return default;
        }

        return media.GetOrRequest(url, token => http.GetBytesAsync(new Uri(url), token));
    }

    private void DrawCover(ImDrawListPtr drawList, Vector2 min, Vector2 max, string url, string fallbackName,
        float rounding)
    {
        MusicRenderer.Cover(drawList, min, max, Thumb(url).Texture, fallbackName, rounding);
    }

    private Rect ScrollBody(Rect content, float scale)
    {
        var top = content.Min.Y + TopBarHeight * scale;
        return new Rect(new Vector2(content.Min.X, top), new Vector2(content.Max.X, BodyBottom(content, scale)));
    }

    private Rect SearchBarRect(Rect content, float scale)
    {
        var top = content.Min.Y + TopBarHeight * scale;
        return new Rect(new Vector2(content.Min.X, top), new Vector2(content.Max.X, top + SearchBarHeight * scale));
    }

    private float BodyBottom(Rect content, float scale)
    {
        var inset = (MiniHeight + MiniMargin * 2f) * scale * Math.Clamp(miniPresence.Value, 0f, 1f);
        return content.Max.Y - inset;
    }

    private string Greeting()
    {
        var hour = DateTime.Now.Hour;
        if (hour is >= 5 and < 12)
        {
            return Loc.T(L.Music.GoodMorning);
        }

        return hour is >= 12 and < 18 ? Loc.T(L.Music.GoodAfternoon) : Loc.T(L.Music.GoodEvening);
    }

    private void OpenSearch()
    {
        focusSearch = true;
        router.Push(View.Search);
    }

    private void OpenCategory(int index)
    {
        categoryIndex = index;
        radioQuery = string.Empty;
        radioSearchDraft = string.Empty;
        router.Push(View.Stations);
        BeginFetch(RadioService.Categories[index].Tags);
    }

    private void OpenRadioSearch()
    {
        fetch?.Cancel();
        categoryIndex = -1;
        radioQuery = string.Empty;
        radioSearchDraft = string.Empty;
        stations = Array.Empty<RadioStation>();
        loading = false;
        ResetPaging();
        focusRadioSearch = true;
        router.Push(View.Stations);
        if (!CurrentRadioFilter().IsDefault)
        {
            BeginRadioSearch(string.Empty);
        }
    }

    private void OpenNowPlaying()
    {
        if (nowPlayingOpen || !playback.IsActive)
        {
            return;
        }

        nowPlayingOpen = true;
        analytics.Track(AnalyticsEvents.ScreenView(Id, "NowPlaying"));
    }

    private void CloseNowPlaying() => nowPlayingOpen = false;

    private RadioFilter CurrentRadioFilter()
    {
        return new RadioFilter(radioCountryCode, radioLanguage, radioOrder);
    }

    private void BeginFetch(string[] tags)
    {
        fetch?.Cancel();
        fetch?.Dispose();
        fetch = new CancellationTokenSource();
        var token = fetch.Token;
        loading = true;
        stations = Array.Empty<RadioStation>();
        ResetPaging();
        _ = LoadCategoryPageAsync(tags, CurrentRadioFilter(), 0, token);
    }

    private void BeginRadioSearch(string query)
    {
        var trimmed = query.Trim();
        var filter = CurrentRadioFilter();
        if (trimmed.Length == 0 && filter.IsDefault)
        {
            return;
        }

        fetch?.Cancel();
        fetch?.Dispose();
        fetch = new CancellationTokenSource();
        var token = fetch.Token;
        loading = true;
        categoryIndex = -1;
        radioQuery = trimmed;
        stations = Array.Empty<RadioStation>();
        ResetPaging();
        _ = LoadSearchPageAsync(radioQuery, filter, 0, token);
    }

    private void RefetchRadio()
    {
        if (categoryIndex >= 0)
        {
            BeginFetch(RadioService.Categories[categoryIndex].Tags);
            return;
        }

        if (radioQuery.Length > 0 || !CurrentRadioFilter().IsDefault)
        {
            BeginRadioSearch(radioQuery);
            return;
        }

        fetch?.Cancel();
        stations = Array.Empty<RadioStation>();
        loading = false;
        ResetPaging();
    }

    private void LoadMoreStations()
    {
        if (loading || loadingMore || !stationHasMore || fetch is null)
        {
            return;
        }

        var token = fetch.Token;
        var filter = CurrentRadioFilter();
        var offset = stationOffset + RadioService.PageSize;
        loadingMore = true;
        if (categoryIndex >= 0)
        {
            _ = LoadCategoryPageAsync(RadioService.Categories[categoryIndex].Tags, filter, offset, token);
        }
        else if (radioQuery.Length > 0 || !filter.IsDefault)
        {
            _ = LoadSearchPageAsync(radioQuery, filter, offset, token);
        }
        else
        {
            loadingMore = false;
        }
    }

    private async Task LoadCategoryPageAsync(string[] tags, RadioFilter filter, int offset, CancellationToken token)
    {
        var page = await radio.FetchStationsAsync(tags, filter, offset, token).ConfigureAwait(false);
        if (token.IsCancellationRequested)
        {
            return;
        }

        ApplyPage(page, offset);
    }

    private async Task LoadSearchPageAsync(string query, RadioFilter filter, int offset, CancellationToken token)
    {
        var page = await radio.SearchStationsAsync(query, filter, offset, token).ConfigureAwait(false);
        if (token.IsCancellationRequested)
        {
            return;
        }

        ApplyPage(page, offset);
    }

    private void EnsureRadioFacets()
    {
        if ((radioCountries.Length > 0 && radioLanguages.Length > 0) || facetsLoading)
        {
            return;
        }

        facetsLoading = true;
        facetFetch?.Cancel();
        facetFetch?.Dispose();
        facetFetch = new CancellationTokenSource();
        _ = LoadFacetsAsync(facetFetch.Token);
    }

    private async Task LoadFacetsAsync(CancellationToken token)
    {
        var countries = await radio.FetchCountriesAsync(token).ConfigureAwait(false);
        var languages = await radio.FetchLanguagesAsync(token).ConfigureAwait(false);
        if (token.IsCancellationRequested)
        {
            return;
        }

        radioCountries = countries;
        radioLanguages = languages;
        facetsLoading = false;
    }

    private void OpenFacetPicker(View view)
    {
        facetSearchDraft = string.Empty;
        EnsureRadioFacets();
        router.Push(view);
    }

    private void ApplyPage(RadioPage page, int offset)
    {
        var basis = offset == 0 ? Array.Empty<RadioStation>() : stations;
        stations = AppendDedup(basis, page.Stations);
        stationOffset = offset;
        stationHasMore = page.HasMore;
        loading = false;
        loadingMore = false;
    }

    private void ResetPaging()
    {
        stationOffset = 0;
        stationHasMore = false;
        loadingMore = false;
    }

    private static RadioStation[] AppendDedup(RadioStation[] existing, RadioStation[] incoming)
    {
        if (incoming.Length == 0)
        {
            return existing;
        }

        var seen = new HashSet<string>(existing.Length + incoming.Length, StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < existing.Length; index++)
        {
            seen.Add(existing[index].StreamUrl);
        }

        var list = new List<RadioStation>(existing.Length + incoming.Length);
        list.AddRange(existing);
        for (var index = 0; index < incoming.Length; index++)
        {
            if (seen.Add(incoming[index].StreamUrl))
            {
                list.Add(incoming[index]);
            }
        }

        return list.ToArray();
    }

    private void BeginSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        search?.Cancel();
        search?.Dispose();
        search = new CancellationTokenSource();
        var token = search.Token;
        searching = true;
        hasSearched = true;
        lastSearchQuery = query;
        results = Array.Empty<Song>();
        _ = SearchAsync(query, token);
    }

    private void SetSearchScope(SongSearchScope scope)
    {
        if (searchScope == scope)
        {
            return;
        }

        searchScope = scope;
        if (!string.IsNullOrWhiteSpace(lastSearchQuery))
        {
            BeginSearch(lastSearchQuery);
        }
    }

    private async Task SearchAsync(string query, CancellationToken token)
    {
        var found = await songSearch.SearchAsync(query, searchScope, token).ConfigureAwait(false);
        if (token.IsCancellationRequested)
        {
            return;
        }

        results = found;
        searching = false;
    }

    private void PlaySong(Song[] list, int index, string source)
    {
        playSource = source;
        playback.PlaySongs(list, index);
    }

    private void PlayStation(int index)
    {
        playSource = string.IsNullOrEmpty(radioQuery) ? CategoryTitle() : Loc.T(L.Music.SourceRadioSearch);
        radio.ReportClick(stations[index].Uuid);
        playback.PlayStations(stations, index);
    }

    private void CaptureRecent()
    {
        var songs = playback.Songs;
        if (songs.State == SongPlaybackState.Stopped)
        {
            lastRecordedVideoId = string.Empty;
            return;
        }

        if (songs.State != SongPlaybackState.Playing)
        {
            return;
        }

        var videoId = songs.CurrentVideoId;
        if (string.IsNullOrEmpty(videoId) || string.Equals(videoId, lastRecordedVideoId, StringComparison.Ordinal))
        {
            return;
        }

        lastRecordedVideoId = videoId;
        history.Record(new Song(videoId, songs.CurrentTitle, songs.CurrentAuthor, songs.CurrentThumbnail,
            (int)songs.Duration));
    }

    private void EnsureFeatured()
    {
        if (featuredRequested)
        {
            return;
        }

        var seed = FeaturedSeeds[featuredIndex < 0 ? 0 : featuredIndex % FeaturedSeeds.Length];
        featuredRequested = true;
        featuredLoading = true;
        featuredFetch?.Dispose();
        featuredFetch = new CancellationTokenSource();
        _ = FetchFeaturedAsync(seed, featuredFetch.Token);
    }

    private async Task FetchFeaturedAsync(string query, CancellationToken token)
    {
        var found = await songSearch.SearchAsync(query, SongSearchScope.Songs, token).ConfigureAwait(false);
        if (token.IsCancellationRequested)
        {
            return;
        }

        var take = Math.Min(FeaturedTiles, found.Length);
        var top = new Song[take];
        Array.Copy(found, top, take);
        featured = top;
        featuredLoading = false;
    }

    private void GoToHome()
    {
        fetch?.Cancel();
        search?.Cancel();
        loading = false;
        searching = false;
        router.Pop();
    }

    private bool IsCurrentStation(RadioStation station)
    {
        return playback.RadioActive && playback.Radio.CurrentStation == station.Name;
    }

    private bool IsCurrentSong(Song song)
    {
        return playback.SongActive && playback.Songs.CurrentVideoId == song.VideoId;
    }

    private string CategoryTitle()
    {
        return categoryIndex >= 0
            ? CatalogLabels.RadioCategory(RadioService.Categories[categoryIndex].Display)
            : DisplayName;
    }

    private string StationsTitle()
    {
        if (categoryIndex >= 0)
        {
            return CategoryTitle();
        }

        return string.IsNullOrEmpty(radioQuery) ? Loc.T(L.Common.Search) : radioQuery;
    }

    private static readonly Dictionary<(string, int), string> SongSubtitleCache = new();

    private static readonly Dictionary<(string, int, string), string> StationSubtitleCache = new();

    private static readonly Dictionary<int, string> TimeCache = new();

    private static string FormatTime(int totalSeconds)
    {
        if (totalSeconds < 0)
        {
            totalSeconds = 0;
        }

        if (TimeCache.TryGetValue(totalSeconds, out var cached))
        {
            return cached;
        }

        var formatted = TimeText.Duration(totalSeconds);
        TimeCache[totalSeconds] = formatted;
        return formatted;
    }

    private static string SongRowSubtitle(Song song)
    {
        var key = (song.Author, song.DurationSeconds);
        if (SongSubtitleCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var subtitle = string.IsNullOrEmpty(song.Author)
            ? FormatTime(song.DurationSeconds)
            : $"{song.Author} · {FormatTime(song.DurationSeconds)}";
        SongSubtitleCache[key] = subtitle;
        return subtitle;
    }

    private static string StationSubtitle(RadioStation station)
    {
        var key = (Loc.Current.Code, station.Bitrate, station.Country);
        if (StationSubtitleCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var bitrate = station.Bitrate > 0 ? $"{station.Bitrate}kbps" : Loc.T(L.Music.LiveLower);
        var subtitle = string.IsNullOrEmpty(station.Country) ? bitrate : $"{bitrate} · {station.Country}";
        StationSubtitleCache[key] = subtitle;
        return subtitle;
    }

    public void Dispose()
    {
        fetch?.Cancel();
        fetch?.Dispose();
        search?.Cancel();
        search?.Dispose();
        featuredFetch?.Cancel();
        featuredFetch?.Dispose();
        facetFetch?.Cancel();
        facetFetch?.Dispose();
        artwork.Dispose();
        flags.Dispose();
    }
}
