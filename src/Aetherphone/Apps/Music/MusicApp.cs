using Aetherphone.Apps.Music.Components;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Game;
using Aetherphone.Core.Jam;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Lyrics;
using Aetherphone.Core.Media;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Playback;
using Aetherphone.Core.Radio;
using Aetherphone.Core.Report;
using Aetherphone.Core.Rolladeck;
using Aetherphone.Core.Songs;
using Aetherphone.Core.SystemMedia;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp : IResumableApp
{
    private const float MaxFrameSeconds = 0.1f;

    public string Id => "music";
    public Vector4 Accent => AppAccents.For(Id);
    public string DisplayName => Loc.T(L.Apps.Music);
    public string Glyph => "M";
    public int BadgeCount => socialNotifications.UnseenCount(Id);
    public bool HasBadge => true;
    public bool BadgeAsDot => true;

    private readonly RadioService radio;
    private readonly SongSearchService songSearch;
    private readonly SongLinkResolver songResolver;
    private readonly PlaybackHub playback;
    private readonly LibraryStore library;
    private readonly DownloadStore downloads;
    private readonly AethernetApi aethernet;
    private readonly RadioLauncher launcher;
    private readonly CommunityRadioService community;
    private readonly SocialNotificationService socialNotifications;
    private readonly ReportService report;
    private readonly PhotoLibrary photoLibrary;
    private readonly WallpaperImageCache wallpaperImages;
    private readonly ConfirmService confirm;
    private readonly Configuration configuration;
    private readonly ArtworkCache artwork;
    private readonly RolladeckService rolladeck;
    private readonly RemoteImageCache images;
    private readonly LodestoneService lodestone;
    private readonly GameData gameData;
    private readonly LyricsService lyrics;
    private readonly WindowsMediaSessions windowsMedia;
    private readonly AppSkin ui = new(AppPalettes.Music);
    private readonly MusicKit kit;
    private readonly MusicSongMenu songMenu = new();
    private readonly PlaylistPickerSheet playlistPicker = new();
    private readonly StoreWork resolverWork = new("music.resolver");
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private float clock;

    public MusicApp(RadioService radio, SongSearchService songSearch, SongLinkResolver songResolver,
        PlaybackHub playback, LibraryStore library, ArtworkCache artwork, AethernetApi aethernet,
        AethernetSession session, ReportService report, PhotoLibrary photoLibrary,
        WallpaperImageCache wallpaperImages, ConfirmService confirm, Configuration configuration,
        RemoteImageCache images, LodestoneService lodestone, GameData gameData, RadioLauncher launcher,
        SocialNotificationService socialNotifications, RolladeckService rolladeck, PcMediaSource pcMedia,
        JamSession jam, JamLauncher jamLauncher, ContactBook contacts,
        RadioRoomSession room, DownloadStore downloads,
        LyricsService lyrics, WindowsMediaSessions windowsMedia, ListeningPresence listening)
    {
        this.radio = radio;
        this.songSearch = songSearch;
        this.songResolver = songResolver;
        this.playback = playback;
        this.library = library;
        this.artwork = artwork;
        this.aethernet = aethernet;
        this.report = report;
        this.photoLibrary = photoLibrary;
        this.wallpaperImages = wallpaperImages;
        this.confirm = confirm;
        this.configuration = configuration;
        this.images = images;
        this.lodestone = lodestone;
        this.gameData = gameData;
        this.launcher = launcher;
        this.socialNotifications = socialNotifications;
        this.rolladeck = rolladeck;
        this.pcMedia = pcMedia;
        this.jam = jam;
        this.jamLauncher = jamLauncher;
        this.contacts = contacts;
        jamAccount = session;
        this.room = room;
        this.session = session;
        this.downloads = downloads;
        this.lyrics = lyrics;
        this.windowsMedia = windowsMedia;
        this.listening = listening;
        PastedLinkHandler = TryHandlePastedLink;
        community = new CommunityRadioService(aethernet, session);
        kit = new MusicKit(ui, images, playback, library) { Downloads = downloads };
        routers = CreateRouters();
        drawView = DrawView;
        popPage = PopPage;
    }

    public void OnOpened()
    {
        ResetTabs();
        ResetNowPlaying();
        pcMediaSheet.CloseImmediately();
        songMenu.Close();
        playlistPicker.Close();
        LoadFavoriteRadioStations();
        socialNotifications.MarkSeen(Id);
        rolladeck.EnsureFresh();
        OnLiveDjsOpened();
        ConsumeLaunchRequests();
    }

    public void OnResumed()
    {
        ResumeNowPlaying();
        LoadFavoriteRadioStations();
        socialNotifications.MarkSeen(Id);
        rolladeck.EnsureFresh();
        ConsumeLaunchRequests();
    }

    public void OnClosed()
    {
        LeaveStationRoom();
    }

    public void Draw(in PhoneContext context)
    {
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, MaxFrameSeconds);
        clock += delta;
        kit.Tick(delta);
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        var scale = UiScale.Current;
        var content = context.Content;
        var screen = SceneChrome.ScreenFrom(content, theme, scale);
        ui.Backdrop(screen);
        if (NeedsSetup)
        {
            TourHolds.Hold(Id);
            DrawSetupGate(screen, scale);
            return;
        }

        TourHolds.Release(Id);
        rolladeck.EnsureFresh();
        GateStationOverlays();
        DrawShell(content, screen, scale, delta);
        DrawStationOverlays(screen);
        TrackStationRoom();
    }

    public void Dispose()
    {
        DisposeNowPlaying();
        DisposeWorldRadio();
        DisposeSearch();
        DisposeLibrary();
        resolverWork.Dispose();
        community.Dispose();
    }
}
