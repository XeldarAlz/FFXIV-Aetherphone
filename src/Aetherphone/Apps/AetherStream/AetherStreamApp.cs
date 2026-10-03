using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Video;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.AetherStream;

internal enum StreamScreen : byte
{
    Home,
    Screen,
    Settings,
    Info,
    PartySettings,
    FindFriend,
    Playlist,
}

internal readonly record struct StreamRoute(StreamScreen Screen, string Id = "")
{
    public static readonly StreamRoute Home = new(StreamScreen.Home);
}

internal enum StreamTab : byte
{
    Watch,
    Party,
    Library,
}

internal sealed partial class AetherStreamApp : IPhoneApp
{
    private const int TabCount = 3;

    private static readonly SocialInk Ink = new(AppPalettes.AetherStream);
    private static readonly TextStyle WordmarkStyle = new(1.4f, FontWeight.Bold);
    private static readonly TextStyle ScreenTitleStyle = new(1.13f, FontWeight.Bold);

    private readonly VideoSuite suite;
    private readonly VideoPlayer video;
    private readonly ScreenController screen;
    private readonly AetherStreamQueue queue;
    private readonly VideoLibrary library;
    private readonly WatchAlongSession watchAlong;
    private readonly StreamSuggestionNotifier suggestionNotifier;
    private readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private readonly RemoteImageCache remoteImages;
    private readonly HttpService http;
    private readonly LodestoneService lodestone;
    private readonly AethernetSession session;
    private readonly AetherStreamLauncher launcher;
    private readonly AetherStreamScreenWindow screenWindow;
    private readonly AccountClient joinAccount;
    private readonly StoreWork joinWork = new("aetherstream.join");
    private readonly StoreWork dependencyWork = new("aetherstream.dependencies");
    private readonly AppSkin ui = new(AppPalettes.AetherStream);
    private readonly ViewRouter<StreamRoute> router = new(StreamRoute.Home);
    private readonly RouterDraw<StreamRoute> drawView;
    private readonly Action back;
    private readonly TabBar tabBar = new();
    private readonly TabItem[] tabs = new TabItem[TabCount];

    private PhoneTheme theme = PhoneTheme.Default;
    private PhoneTheme accentedTheme = PhoneTheme.Default;
    private Rect screenRect;
    private StreamTab activeTab = StreamTab.Watch;

    internal AetherStreamApp(VideoSuite suite, Configuration configuration, ConfirmService confirm,
        RemoteImageCache remoteImages, HttpService http, AethernetSession aethernetSession,
        LodestoneService lodestone, AetherStreamLauncher launcher, AetherStreamScreenWindow screenWindow)
    {
        this.suite = suite;
        video = suite.Player;
        screen = suite.Screen;
        queue = suite.Queue;
        library = suite.Library;
        watchAlong = suite.WatchAlong;
        suggestionNotifier = suite.Suggestions;
        this.configuration = configuration;
        this.confirm = confirm;
        this.remoteImages = remoteImages;
        this.http = http;
        this.lodestone = lodestone;
        session = aethernetSession;
        this.launcher = launcher;
        this.screenWindow = screenWindow;
        joinAccount = new AethernetApi(http, aethernetSession, "aetherstream").Account;
        drawView = DrawView;
        back = () => router.Pop();
        drawAddContent = DrawAddContent;
        drawTracksContent = DrawTracksContent;
    }

    public string Id => "aetherstream";
    public string DisplayName => Loc.T(L.Apps.AetherStream);
    public string Glyph => "V";
    public Vector4 Accent => AppAccents.For(Id);
    public int BadgeCount => watchAlong.PendingRequests.Count + watchAlong.PendingQueueSuggestions.Count;
    public bool HasBadge => true;

    private VideoQueueEntry? CurrentEntry => watchAlong.IsViewing ? watchAlong.ViewingEntry : queue.Current;

    private bool SheetsCapturePointer => addSheet.CapturesPointer || tracksSheet.CapturesPointer;

    public void OnOpened()
    {
        watchAlong.RequestNearbyStreams();
        if (!launcher.TryConsumeUpNext())
        {
            return;
        }

        ExitTheater();
        router.Reset();
        CloseSheets();
        activeTab = StreamTab.Library;
        librarySegment = LibrarySegment.UpNext;
    }

    public void OnClosed()
    {
        FlushScreenSave();
        ExitTheater();
        router.Reset();
        CloseSheets();
    }

    private void CloseSheets()
    {
        addSheet.Close();
        tracksSheet.Close();
        actions.Close();
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        ui.Theme = context.Theme;
        accentedTheme = AccentedTheme(context.Theme);
        var scale = UiScale.Current;
        screenRect = SceneChrome.ScreenFrom(context.Content, context.Theme, scale);
        ui.Backdrop(screenRect);
        if (TheaterActive && !video.HasMedia && CurrentEntry is null)
        {
            ExitTheater();
        }

        if (TheaterActive && screenRect.IsLandscape())
        {
            DrawTheater(screenRect, scale);
            return;
        }

        actions.Gate();
        SaveWhenDue();
        ConsumePickedFiles();
        AnnounceImports();
        var area = SceneChrome.AppAreaFrom(context.Content, context.Theme, scale);
        router.Draw(area, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        DrawActions(screenRect);
    }

    private void DrawView(StreamRoute route, Rect area, int depth)
    {
        ui.Body(area);
        var scale = UiScale.Current;
        switch (route.Screen)
        {
            case StreamScreen.Screen:
                DrawScreenEditor(area, scale);
                return;
            case StreamScreen.Settings:
                DrawSettings(area, scale);
                return;
            case StreamScreen.Info:
                DrawInfo(area, scale);
                return;
            case StreamScreen.PartySettings:
                DrawPartySettings(area, scale);
                return;
            case StreamScreen.FindFriend:
                DrawFindFriend(area, scale);
                return;
            case StreamScreen.Playlist:
                DrawPlaylist(area, route.Id, scale);
                return;
            default:
                DrawHome(area, scale);
                return;
        }
    }

    private void DrawHome(Rect area, float scale)
    {
        if (NeedsSetup)
        {
            TourHolds.Hold(Id);
            DrawSetupGate(area, scale);
            return;
        }

        TourHolds.Release(Id);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);

        using (InputShield.Engage(SheetsCapturePointer))
        {
            using (TabBar.ReserveContent(scale))
            using (ImRaii.PushId((int)activeTab))
            {
                switch (activeTab)
                {
                    case StreamTab.Party:
                        DrawPartyTab(body, scale);
                        break;
                    case StreamTab.Library:
                        DrawLibraryTab(body, scale);
                        break;
                    default:
                        DrawWatchTab(body, scale);
                        break;
                }
            }

            DrawTopBar(area, scale);
            DrawTabBar(area);
        }

        DrawAddSheet(area, scale);
        DrawTracksSheet(area, scale);
    }

    private string TabTitle() => activeTab switch
    {
        StreamTab.Party => Loc.T(L.AetherStream.Party),
        StreamTab.Library => Loc.T(L.AetherStream.Library),
        _ => DisplayName,
    };

    private void DrawTopBar(Rect area, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var header = new Rect(area.Min, new Vector2(area.Max.X, area.Min.Y + AppHeader.Height * scale));
        ui.PaintGradient(drawList, header, screenRect, 0f);

        var settingsCenter = SocialChrome.HeaderSlot(area, 0);
        var screenCenter = SocialChrome.HeaderSlot(area, 1);
        var radius = SocialChrome.HeaderIconRadius * scale;
        var titleLeft = area.Min.X + PadX * scale;
        var titleLimit = MathF.Max(1f, screenCenter.X - radius - Metrics.Space.Md * scale - titleLeft);
        var title = Typography.FitText(TabTitle(), titleLimit, WordmarkStyle);
        var titleHeight = Typography.LineHeight(WordmarkStyle);
        Typography.Draw(drawList, new Vector2(titleLeft, header.Center.Y - titleHeight * 0.5f), title, Ink.TitleInk,
            WordmarkStyle);

        var delta = ImGui.GetIO().DeltaTime;
        UiAnchors.Report("aetherstream.screen",
            new Rect(screenCenter - new Vector2(radius, radius), screenCenter + new Vector2(radius, radius)));
        if (HoverButton.Circle(drawList, "aetherstream.header.settings", settingsCenter, radius * 0.82f,
                FontAwesomeIcon.Cog, AppSkin.Transparent, Ink.TitleInk, delta, 1f, true,
                Loc.T(L.AetherStream.SettingsTitle)))
        {
            router.Push(new StreamRoute(StreamScreen.Settings));
        }

        if (HoverButton.Circle(drawList, "aetherstream.header.screen", screenCenter, radius * 0.82f,
                FontAwesomeIcon.Tv, AppSkin.Transparent, Ink.TitleInk, delta, 1f, true,
                Loc.T(L.AetherStream.Screen)))
        {
            router.Push(new StreamRoute(StreamScreen.Screen));
        }
    }

    private void DrawTabBar(Rect area)
    {
        tabs[0] = new TabItem(Loc.T(L.AetherStream.TabWatch), IconGlyph.Of(FontAwesomeIcon.Play));
        tabs[1] = new TabItem(Loc.T(L.AetherStream.Party), IconGlyph.Of(FontAwesomeIcon.UserFriends),
            Badge: watchAlong.PendingRequests.Count, AnchorKey: "aetherstream.tab.party");
        tabs[2] = new TabItem(Loc.T(L.AetherStream.Library), IconGlyph.Of(FontAwesomeIcon.ListUl),
            Badge: watchAlong.IsHosting ? watchAlong.PendingQueueSuggestions.Count : 0,
            AnchorKey: "aetherstream.tab.library");
        var result = tabBar.Draw(area, ui, tabs, (int)activeTab);
        if (result.Tapped < 0)
        {
            return;
        }

        activeTab = (StreamTab)result.Tapped;
    }

    private static PhoneTheme AccentedTheme(PhoneTheme baseTheme) =>
        PhoneTheme.WithAccent(baseTheme, AppAccents.For("aetherstream"));

    public void Dispose()
    {
        joinWork.Dispose();
        dependencyWork.Dispose();
    }
}
