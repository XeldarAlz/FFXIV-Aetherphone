using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Campaigns;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Apps.LoveIsland;

internal sealed partial class LoveIslandApp : IResumableApp
{
    private const int TabCount = 5;
    private const string SettingsAppId = "settings";
    private const string AethergramAppId = "aethergram";
    private const string CoinAppId = "coin";
    private const float MinAccentContrast = 3f;

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    public string Id => "loveisland";
    public string DisplayName => store.DisplayName;
    public string Glyph => "Le";
    public Vector4 Accent => AppAccents.For(Id);
    public int BadgeCount => store.UnreadCount;
    public bool HasBadge => true;
    public bool IsAvailable => store.IsAvailable(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    public int TileEffectLevel => store.TileEffectLevel(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    public HomeTileEffect TileEffect => (HomeTileEffect)TileEffectLevel;

    private readonly CampaignStore store;
    private readonly GalleryLane gallery;
    private readonly CampaignLauncher launcher;
    private readonly SocialLauncher socialLauncher;
    private readonly RemoteImageCache images;
    private readonly LodestoneService lodestone;
    private readonly BadgeCatalogStore badgeCatalog;
    private readonly FrameCatalogStore frameCatalog;
    private readonly AethernetSession session;
    private readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private readonly AppSkin ui = new(AppPalettes.LoveIsland);
    private readonly TabBar tabBar = new();
    private readonly TabItem[] tabItems = new TabItem[TabCount];
    private readonly NavBarButton[] rootButtons = new NavBarButton[1];
    private readonly ViewRouter<LoveIslandRoute> router;
    private readonly RouterDraw<LoveIslandRoute> drawView;
    private readonly Action back;
    private readonly ScreenToast toast = new();
    private readonly PhotoViewerOverlay photoViewer = new();
    private readonly Func<int, IDalamudTextureWrap?> photoPage;
    private readonly LoveIslandLabels labels = new();
    private readonly PostLabels postLabels = new();
    private readonly CardEntrance entrance = new();

    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private Rect screenRect;
    private LoveIslandTab activeTab = LoveIslandTab.Tonight;
    private string[] viewerUrls = Array.Empty<string>();
    private string? paletteAccentSource;
    private CampaignDto? frameCampaign;
    private long nowUnix;
    private double nowPrecise;
    private float rootScroll;
    private PhoneTheme? toggleTheme;
    private PhoneTheme? toggleThemeSource;
    private Vector4 toggleThemeAccent;
    private float fontKey;
    private bool scrollTopPending;

    public LoveIslandApp(AethernetSession session, AethernetApi api, NotificationService notifications,
        Configuration configuration, CampaignLauncher launcher, RealtimeSignalBus signals, RemoteImageCache images,
        LodestoneService lodestone, BadgeCatalogStore badgeCatalog, FrameCatalogStore frameCatalog,
        SocialLauncher socialLauncher, ConfirmService confirm)
    {
        var definition = CampaignDefinition.LoveIsland;
        var icons = new CampaignIconSync(definition, configuration, images,
            Plugin.PluginInterface.ConfigDirectory.FullName);
        store = new CampaignStore(definition, session, api.Campaigns, notifications, configuration, signals, icons);
        gallery = new GalleryLane(api.Grams, session);
        this.session = session;
        this.configuration = configuration;
        this.launcher = launcher;
        this.images = images;
        this.lodestone = lodestone;
        this.badgeCatalog = badgeCatalog;
        this.frameCatalog = frameCatalog;
        this.socialLauncher = socialLauncher;
        this.confirm = confirm;
        router = new ViewRouter<LoveIslandRoute>(LoveIslandRoute.Home);
        drawView = DrawView;
        back = () => router.Pop();
        photoPage = index => index >= 0 && index < viewerUrls.Length ? images.Get(viewerUrls[index]) : null;
    }

    private CampaignLedger Ledger => store.Ledger;

    private CampaignTexts Texts => store.Texts;

    public void OnOpened()
    {
        router.Reset();
        activeTab = LoveIslandTab.Tonight;
        scrollTopPending = true;
        entrance.Restart();
        OnResumed();
    }

    public void OnResumed()
    {
        store.AppVisible = true;
        store.Refresh();
        ConsumeLaunch();
    }

    public void OnClosed()
    {
        store.AppVisible = false;
        sheet.CloseImmediately();
        photoViewer.Close();
    }

    private void ConsumeLaunch()
    {
        if (!launcher.TryConsume(out var launch))
        {
            return;
        }

        router.Reset();
        entrance.Restart();
        switch (launch.Kind)
        {
            case CampaignLaunchKind.Post:
                activeTab = LoveIslandTab.Tonight;
                Ledger.MarkPostsSeen();
                router.Push(LoveIslandRoute.Post(launch.Id), false);
                break;
            case CampaignLaunchKind.Experience:
                activeTab = LoveIslandTab.Experiences;
                router.Push(LoveIslandRoute.Experience(launch.Id), false);
                break;
            case CampaignLaunchKind.Perks:
                activeTab = LoveIslandTab.Perks;
                store.RefreshMe();
                break;
            case CampaignLaunchKind.Syncshells:
                activeTab = LoveIslandTab.Tonight;
                router.Push(LoveIslandRoute.Syncshells, false);
                break;
        }
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        SyncPalette();
        fontKey = Typography.LineHeight(TextStyles.Body);
        nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        nowPrecise = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        var scale = UiScale.Current;
        var screen = SceneChrome.ScreenFrom(context.Content, theme, scale);
        screenRect = screen;
        ui.Backdrop(screen);
        var appArea = SceneChrome.AppAreaFrom(context.Content, theme, scale);
        PaintSkyStrips(screen, appArea, scale);
        if (!store.IsSignedIn)
        {
            TourHolds.Hold(Id);
            DrawSignedOut(context);
            return;
        }

        TourHolds.Release(Id);
        frameCampaign = store.Campaign;
        labels.Sync(frameCampaign, Texts, Ledger.Hashtag);
        postLabels.Sync(store.Posts);
        Ledger.MarkOpened();
        if (photoViewer.Active)
        {
            photoViewer.Draw(screen, theme);
            return;
        }

        using (InputShield.Engage(sheet.CapturesPointer))
        {
            router.Draw(appArea, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        }

        DrawSheet(screen);
        toast.Draw(screen, ScreenToastStyle.From(ui));
    }

    private void PaintSkyStrips(Rect screen, Rect appArea, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = theme.ScreenRounding * scale;
        var drift = router.Depth <= 1 ? rootScroll : 0f;
        var moon = ShowsMoon(router.Current);
        var top = new Rect(screen.Min, new Vector2(screen.Max.X, MathF.Floor(appArea.Min.Y)));
        var bottom = new Rect(new Vector2(screen.Min.X, MathF.Ceiling(appArea.Max.Y)), screen.Max);
        EclipseSky.Paint(drawList, top, screen, rounding, ui.Accent, drift, moon);
        EclipseSky.Paint(drawList, bottom, screen, rounding, ui.Accent, drift, moon);
    }

    private bool ShowsMoon(LoveIslandRoute route) =>
        route.Screen != LoveIslandScreen.Home || activeTab != LoveIslandTab.Tonight;

    private PhoneTheme ToggleTheme()
    {
        var accent = ui.Accent;
        if (toggleTheme is not null && ReferenceEquals(toggleThemeSource, theme) && toggleThemeAccent == accent)
        {
            return toggleTheme;
        }

        toggleThemeSource = theme;
        toggleThemeAccent = accent;
        toggleTheme = PhoneTheme.WithAccent(theme, accent);
        return toggleTheme;
    }

    private void SyncPalette()
    {
        var hex = store.Campaign?.AccentHex;
        if (ReferenceEquals(hex, paletteAccentSource))
        {
            return;
        }

        paletteAccentSource = hex;
        if (LoveIslandKit.TryParseHex(hex, out var accent) && Palette.ContrastRatio(accent, White) >= MinAccentContrast)
        {
            ui.Palette = AppPalettes.LoveIsland with { Accent = accent };
            return;
        }

        ui.Palette = AppPalettes.LoveIsland;
    }

    private void DrawSignedOut(in PhoneContext context)
    {
        ui.Body(context.Content);
        EclipseSky.Paint(ImGui.GetWindowDrawList(), context.Content, screenRect,
            theme.ScreenRounding * UiScale.Current, ui.Accent, 0f, true);
        var navBar = AppHeader.BeginLargeTitle(context, false);
        if (EmptyState.Draw(navBar.Body, ui, FontAwesomeIcon.UserLock, DisplayName, Loc.T(L.LoveIsland.SignInBody),
                Loc.T(L.Announcements.OpenSettings)))
        {
            navigation.Open(SettingsAppId);
        }

        AppHeader.EndLargeTitle(in navBar, context, "loveisland.nav.signedOut", string.Empty, NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty);
    }

    private void DrawView(LoveIslandRoute route, Rect area, int depth)
    {
        entrance.BeginFrame();
        ui.Body(area);
        EclipseSky.Paint(ImGui.GetWindowDrawList(), area, screenRect, theme.ScreenRounding * UiScale.Current,
            ui.Accent, depth == 0 ? rootScroll : 0f, ShowsMoon(route));
        switch (route.Screen)
        {
            case LoveIslandScreen.Experience:
                DrawExperienceDetail(area, route.Id);
                break;
            case LoveIslandScreen.Posts:
                DrawPostsList(area);
                break;
            case LoveIslandScreen.Post:
                DrawPostDetail(area, route.Id, depth);
                break;
            case LoveIslandScreen.Syncshells:
                DrawSyncshells(area);
                break;
            default:
                DrawHome(area);
                break;
        }
    }

    private void DrawHome(Rect area)
    {
        var scale = UiScale.Current;
        using (TabBar.ReserveContent(scale))
        {
            switch (activeTab)
            {
                case LoveIslandTab.Djs:
                    DrawDjsTab(area);
                    break;
                case LoveIslandTab.Experiences:
                    DrawExperiencesTab(area);
                    break;
                case LoveIslandTab.Gallery:
                    DrawGalleryTab(area);
                    break;
                case LoveIslandTab.Perks:
                    DrawPerksTab(area);
                    break;
                default:
                    DrawTonightTab(area);
                    break;
            }
        }

        DrawTabBar(area);
    }

    private void DrawTabBar(Rect area)
    {
        var texts = Texts;
        var unread = activeTab == LoveIslandTab.Tonight ? 0 : store.UnreadCount;
        tabItems[(int)LoveIslandTab.Tonight] = new TabItem(texts.T(CampaignTextKeys.TabTonight), PhoneIcons.Moon,
            PhoneIcons.MoonStars, unread, "loveisland.tab.tonight");
        tabItems[(int)LoveIslandTab.Djs] = new TabItem(texts.T(CampaignTextKeys.TabDjs), PhoneIcons.Music,
            PhoneIcons.Music, 0, "loveisland.tab.djs");
        tabItems[(int)LoveIslandTab.Experiences] = new TabItem(texts.T(CampaignTextKeys.TabExperiences),
            PhoneIcons.Calendar, PhoneIcons.CalendarFilled, 0, "loveisland.tab.experiences");
        tabItems[(int)LoveIslandTab.Gallery] = new TabItem(texts.T(CampaignTextKeys.TabGallery), PhoneIcons.Photo,
            PhoneIcons.PhotoFilled, 0, "loveisland.tab.gallery");
        tabItems[(int)LoveIslandTab.Perks] = new TabItem(texts.T(CampaignTextKeys.TabPerks), PhoneIcons.Star,
            PhoneIcons.StarFilled, 0, "loveisland.tab.perks");
        var result = tabBar.Draw(area, ui, tabItems, (int)activeTab);
        if (result.Tapped < 0)
        {
            return;
        }

        SelectTab((LoveIslandTab)result.Tapped);
    }

    private void SelectTab(LoveIslandTab tab)
    {
        if (tab == activeTab)
        {
            return;
        }

        activeTab = tab;
        scrollTopPending = true;
        entrance.Restart();
        if (tab == LoveIslandTab.Gallery)
        {
            gallery.EnsureFresh(Ledger.Hashtag);
        }
        else if (tab == LoveIslandTab.Perks)
        {
            store.RefreshMe();
        }
    }

    private NavBarFrame BeginRoot(Rect area, out PhoneContext context)
    {
        context = new PhoneContext(area, theme, navigation);
        return AppHeader.BeginLargeTitle(context, false);
    }

    private void EndRoot(in NavBarFrame frame, in PhoneContext context, string id)
    {
        rootButtons[0] = new NavBarButton(PhoneIcons.InfoCircle, Texts.T(CampaignTextKeys.About));
        if (AppHeader.EndLargeTitle(in frame, context, id, string.Empty, NavBarStyle.From(ui), rootButtons) == 0)
        {
            OpenAbout();
        }
    }

    private NavBarFrame BeginPushed(Rect area, out PhoneContext headerContext, out Rect body)
    {
        var scale = UiScale.Current;
        var side = theme.SidePadding * scale;
        var inset = new Rect(new Vector2(area.Min.X + side, area.Min.Y), new Vector2(area.Max.X - side, area.Max.Y));
        headerContext = new PhoneContext(inset, theme, navigation);
        var frame = AppHeader.BeginLargeTitle(headerContext);
        body = new Rect(new Vector2(area.Min.X, frame.Body.Min.Y), area.Max);
        return frame;
    }

    private NavBarFrame BeginPushedInline(Rect area, out PhoneContext headerContext, out Rect body)
    {
        var scale = UiScale.Current;
        headerContext = new PhoneContext(area, theme, navigation);
        var inlineHeight = NavBarMetrics.InlineHeight * scale;
        body = new Rect(new Vector2(area.Min.X, area.Min.Y + inlineHeight), area.Max);
        AppSurface.ArmNavBar(body.Min.Y, inlineHeight);
        return new NavBarFrame(area, body, scale, inlineHeight);
    }

    private void EndPushed(in NavBarFrame frame, in PhoneContext headerContext, string id, string title,
        string backTitle)
    {
        AppHeader.EndLargeTitle(in frame, headerContext, id, title, NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty, backTitle, back);
    }

    private void PrepareRootSurface(in AppSurface.SurfaceScope surface)
    {
        rootScroll = ImGui.GetScrollY();
        if (!scrollTopPending)
        {
            return;
        }

        surface.JumpToTop();
        scrollTopPending = false;
    }

    private bool DrawLoadState(Rect body, out float bottom)
    {
        bottom = ImGui.GetCursorScreenPos().Y;
        if (store.LoadedOnce)
        {
            return false;
        }

        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var state = new Rect(new Vector2(body.Min.X, origin.Y), body.Max);
        if (!store.Failed)
        {
            LoadingPulse.Draw(new Vector2(state.Center.X, origin.Y + LoadingOffset * scale), LoadingRadius * scale,
                ui.Accent, ui.MutedInk, Loc.T(L.Common.Loading), 1f, 0.80f, ImGui.GetWindowDrawList());
            bottom = origin.Y + (LoadingOffset + LoadingRadius * 4f) * scale;
            return true;
        }

        if (EmptyState.Draw(state, ui, FontAwesomeIcon.Moon, Loc.T(L.LoveIsland.Unavailable),
                Loc.T(L.LoveIsland.UnavailableHint), Loc.T(L.Common.Retry)))
        {
            store.Refresh();
        }

        bottom = state.Max.Y;
        return true;
    }

    private void OpenExperience(string experienceId)
    {
        UiFeedback.Play(UiSound.Tap);
        router.Push(LoveIslandRoute.Experience(experienceId));
    }

    private void OpenPost(string postId)
    {
        UiFeedback.Play(UiSound.Tap);
        router.Push(LoveIslandRoute.Post(postId));
    }

    private void OpenPosts()
    {
        UiFeedback.Play(UiSound.Tap);
        postsVisitSeenUnix = configuration.LoveIslandPostsSeenUnix;
        Ledger.MarkPostsSeen();
        router.Push(LoveIslandRoute.Posts);
    }

    private void OpenSyncshells()
    {
        UiFeedback.Play(UiSound.Tap);
        router.Push(LoveIslandRoute.Syncshells);
    }

    private void PostWithHashtag()
    {
        var hashtag = string.Concat("#", Ledger.Hashtag);
        ImGui.SetClipboardText(hashtag);
        UiFeedback.Play(UiSound.Success);
        ShellToast.Show(Loc.T(L.LoveIsland.HashtagCopied, Ledger.Hashtag));
        navigation.Open(AethergramAppId);
    }

    private void CopyText(string text)
    {
        ImGui.SetClipboardText(text);
        UiFeedback.Play(UiSound.Success);
        ShellToast.Show();
    }

    private void OpenPhotos(string[] urls, int start)
    {
        if (urls.Length == 0)
        {
            return;
        }

        viewerUrls = urls;
        photoViewer.Open(this, urls.Length, start, photoPage);
    }

    private float LeftOf(float originX) => originX + LoveIslandKit.PadX * UiScale.Current;

    private float InnerWidth(float width) => MathF.Max(1f, width - LoveIslandKit.PadX * 2f * UiScale.Current);

    public void Dispose()
    {
        store.Dispose();
        gallery.Dispose();
    }
}
