using System.Numerics;
using Aetherphone.Core;
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
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp : IPhoneApp
{
    private enum View : byte
    {
        Browse,
        Stations,
        Search,
        RadioNowPlaying,
        SongNowPlaying,
    }

    private const float MiniHeight = 58f;
    private const float TileHeight = 92f;
    private const float SearchBarHeight = 50f;
    private const int RecentTiles = 4;
    private const int FeaturedTiles = 4;

    private readonly struct FeaturedSeed
    {
        public readonly string Title;
        public readonly string Query;

        public FeaturedSeed(string title, string query)
        {
            Title = title;
            Query = query;
        }
    }

    private static readonly FeaturedSeed[] FeaturedSeeds =
    {
        new("FFXIV soundtrack", "final fantasy xiv soundtrack"),
        new("FFXIV battle themes", "final fantasy xiv battle theme ost"),
        new("Endwalker OST", "final fantasy xiv endwalker soundtrack"),
        new("Shadowbringers OST", "final fantasy xiv shadowbringers soundtrack"),
        new("Dawntrail OST", "final fantasy xiv dawntrail soundtrack"),
        new("FFXIV city themes", "final fantasy xiv city theme music"),
    };

    public string Id => "music";
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
    private readonly ArtworkCache artwork;
    private readonly ViewRouter<View> router;
    private readonly RouterDraw<View> drawView;
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private int categoryIndex = -1;
    private RadioStation[] stations = Array.Empty<RadioStation>();
    private volatile bool loading;
    private CancellationTokenSource? fetch;
    private Song[] results = Array.Empty<Song>();
    private volatile bool searching;
    private bool hasSearched;
    private CancellationTokenSource? search;
    private string searchDraft = string.Empty;
    private Song[] featured = Array.Empty<Song>();
    private volatile bool featuredLoading;
    private bool featuredRequested;
    private int featuredIndex = -1;
    private string featuredTitle = "Featured";
    private CancellationTokenSource? featuredFetch;
    private string lastRecordedVideoId = string.Empty;
    private float clock;

    public MusicApp(RadioService radio, SongSearchService songSearch, PlaybackHub playback, SongHistory history,
        MediaCache media, HttpService http, ITextureProvider textures)
    {
        this.radio = radio;
        this.songSearch = songSearch;
        this.playback = playback;
        this.history = history;
        this.media = media;
        this.http = http;
        artwork = new ArtworkCache(textures);
        router = new ViewRouter<View>(View.Browse, Id);
        drawView = DrawView;
    }

    public void OnOpened()
    {
        router.Reset();
        featuredIndex = (featuredIndex + 1) % FeaturedSeeds.Length;
        featuredRequested = false;
        featuredLoading = false;
        featured = Array.Empty<Song>();
        featuredFetch?.Cancel();
    }

    public void OnClosed()
    {
        router.Reset();
    }

    public void Draw(in PhoneContext context)
    {
        clock += MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        theme = context.Theme;
        navigation = context.Navigation;
        CaptureRecent();
        var current = router.Current;
        if ((current == View.RadioNowPlaying && !playback.RadioActive) ||
            (current == View.SongNowPlaying && !playback.SongActive))
        {
            router.Pop(false);
        }

        MusicArt.Backdrop(context);
        router.Draw(context.Content, default, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(View view, Rect area, int depth)
    {
        var context = new PhoneContext(area, theme, navigation);
        switch (view)
        {
            case View.Stations:
                DrawStations(context);
                break;
            case View.Search:
                DrawSearch(context);
                break;
            case View.RadioNowPlaying:
                DrawRadioNowPlaying(context);
                break;
            case View.SongNowPlaying:
                DrawSongNowPlaying(context);
                break;
            default:
                DrawBrowse(context);
                break;
        }
    }

    private bool DrawSearchBar(Rect bar, PhoneTheme theme)
    {
        var submitted = SearchField.DrawSubmit(bar, "##songSearch", Loc.T(L.Music.SearchSongs), ref searchDraft,
            theme, 120, 2f);
        return submitted && !string.IsNullOrWhiteSpace(searchDraft);
    }

    private void DrawThumb(ImDrawListPtr dl, Vector2 min, Vector2 max, string url, string fallbackName, float rounding)
    {
        MusicArt.Thumb(dl, min, max, Thumb(url).Texture, fallbackName, rounding);
    }

    private MediaResult Thumb(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return default;
        }

        return media.GetOrRequest(url, token => http.GetBytesAsync(new Uri(url), token));
    }

    private Rect ScrollBody(Rect content, float scale)
    {
        var top = content.Min.Y + AppHeader.Height * scale;
        return new Rect(new Vector2(content.Min.X, top), new Vector2(content.Max.X, BodyBottom(content, scale)));
    }

    private Rect SearchBarRect(Rect content, float scale)
    {
        var top = content.Min.Y + AppHeader.Height * scale;
        return new Rect(new Vector2(content.Min.X, top), new Vector2(content.Max.X, top + SearchBarHeight * scale));
    }

    private float BodyBottom(Rect content, float scale)
    {
        return content.Max.Y - (playback.IsActive ? MiniHeight * scale + 4f * scale : 0f);
    }

    private void OpenCategory(int index)
    {
        categoryIndex = index;
        router.Push(View.Stations);
        BeginFetch(RadioService.Categories[index].Tag);
    }

    private void BeginFetch(string tag)
    {
        fetch?.Cancel();
        fetch?.Dispose();
        fetch = new CancellationTokenSource();
        var token = fetch.Token;
        loading = true;
        stations = Array.Empty<RadioStation>();
        _ = FetchAsync(tag, token);
    }

    private async Task FetchAsync(string tag, CancellationToken token)
    {
        var result = await radio.FetchStationsAsync(tag, token).ConfigureAwait(false);
        if (token.IsCancellationRequested)
        {
            return;
        }

        stations = result;
        loading = false;
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
        results = Array.Empty<Song>();
        _ = SearchAsync(query, token);
    }

    private async Task SearchAsync(string query, CancellationToken token)
    {
        var found = await songSearch.SearchAsync(query, token).ConfigureAwait(false);
        if (token.IsCancellationRequested)
        {
            return;
        }

        results = found;
        searching = false;
    }

    private void PlaySong(Song[] list, int index)
    {
        playback.PlaySongs(list, index);
        router.Push(View.SongNowPlaying);
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
        featuredTitle = seed.Title;
        featuredRequested = true;
        featuredLoading = true;
        featuredFetch?.Dispose();
        featuredFetch = new CancellationTokenSource();
        _ = FetchFeaturedAsync(seed.Query, featuredFetch.Token);
    }

    private async Task FetchFeaturedAsync(string query, CancellationToken token)
    {
        var found = await songSearch.SearchAsync(query, token).ConfigureAwait(false);
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

    private void GoToBrowse()
    {
        fetch?.Cancel();
        search?.Cancel();
        loading = false;
        searching = false;
        router.Pop();
    }

    private void GoToReturnView() => router.Pop();

    private bool IsCurrentStation(RadioStation station)
    {
        return playback.RadioActive && playback.Radio.CurrentStation == station.Name;
    }

    private bool IsCurrentSong(Song song)
    {
        return playback.SongActive && playback.Songs.CurrentVideoId == song.VideoId;
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

        var formatted = TimeText.MinutesSeconds(totalSeconds);
        TimeCache[totalSeconds] = formatted;
        return formatted;
    }

    private static readonly Dictionary<(string, int), string> TruncateCache = new();

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max)
        {
            return value ?? string.Empty;
        }

        var key = (value, max);
        if (TruncateCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var result = value.Substring(0, max - 1) + "…";
        TruncateCache[key] = result;
        return result;
    }

    public void Dispose()
    {
        fetch?.Cancel();
        fetch?.Dispose();
        search?.Cancel();
        search?.Dispose();
        featuredFetch?.Cancel();
        featuredFetch?.Dispose();
        artwork.Dispose();
    }
}
