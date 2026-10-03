using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Conduct;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Game;
using Aetherphone.Core.Geography;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Net;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Report;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Translation;
using Aetherphone.Core.Venues;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Core.YellowPages;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.YellowPages;

internal sealed partial class YellowPagesApp : IPhoneApp
{
    private const float CopiedSeconds = 1.6f;
    private const int TabCount = 4;
    private const float CellPadX = SocialChrome.CellPadX;
    private const float HeaderIconSize = 24f;
    private const float EmptyStateTop = 72f;
    private const float EmptyBodyGap = 8f;

    private static readonly SocialInk Ink = YellowPagesInk.Shared;
    private static readonly TextStyle ScreenTitleStyle = new(1.05f, FontWeight.SemiBold);
    private static readonly TextStyle WordmarkStyle = new(1.4f, FontWeight.Bold);
    private static readonly TextStyle EmptyTitleStyle = TextStyles.Title2;
    private static readonly TextStyle EmptyBodyStyle = TextStyles.Callout;

    public string Id => "yellowpages";
    public string DisplayName => Loc.T(L.Apps.YellowPages);
    public string Glyph => "Yp";
    public int BadgeCount => inquiries.UnreadCount + socialNotifications.UnseenCount(Id, SocialActivity.TypeAdInquiry);
    public bool HasBadge => true;
    public Vector4 Accent => AppAccents.For(Id);

    private readonly YellowPagesStore store;
    private readonly AdInquiryStore inquiries;
    private readonly YellowPagesLauncher launcher;
    private readonly SocialNotificationService socialNotifications;
    private readonly MusterStore musters;
    private readonly AethernetApi api;
    private readonly GameData gameData;
    private readonly RemoteImageCache images;
    private readonly LodestoneService lodestone;
    private readonly PhotoLibrary library;
    private readonly WallpaperImageCache wallpaperImages;
    private readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private readonly TranslationService translation;
    private readonly ReportService report;
    private readonly ConductGateService conduct;
    private readonly EncryptionHelpService encryptionHelp;
    private readonly HttpService http;
    private readonly AppSkin ui = new(AppPalettes.YellowPages);
    private readonly TabBar tabBar = new();
    private readonly TabItem[] tabItems = new TabItem[TabCount];
    private readonly ViewRouter<YellowPagesRoute> router;
    private readonly GeoScopeScreen scopeScreen;
    private readonly RouterDraw<YellowPagesRoute> drawView;
    private readonly ThreadView threadView;
    private readonly PhotoComposeSession composeSession;
    private readonly PhotoViewerOverlay photoViewer = new();
    private readonly DropdownMenu optionsMenu = new();
    private readonly DropdownMenu.Item[] optionItems = new DropdownMenu.Item[6];
    private readonly ActionSheet adSheet = new();
    private readonly ActionSheet inboxSheet = new();
    private readonly Action back;
    private readonly Action backToBrowse;
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private Rect screenRect;
    private YellowPagesTab activeTab = YellowPagesTab.Browse;
    private float copiedTimer;
    private string copiedKey = string.Empty;
    private bool lifestreamAvailable;

    public YellowPagesApp(YellowPagesStore store, AdInquiryStore inquiries, YellowPagesLauncher launcher,
        SocialNotificationService socialNotifications, MusterStore musters, AethernetApi api, GameData gameData,
        RemoteImageCache images, LodestoneService lodestone, PhotoLibrary library,
        WallpaperImageCache wallpaperImages, Configuration configuration, ConfirmService confirm,
        TranslationService translation, ReportService report, ConductGateService conduct,
        EncryptionHelpService encryptionHelp, HttpService http)
    {
        this.store = store;
        this.inquiries = inquiries;
        this.launcher = launcher;
        this.socialNotifications = socialNotifications;
        this.musters = musters;
        this.api = api;
        this.gameData = gameData;
        this.images = images;
        this.lodestone = lodestone;
        this.library = library;
        this.wallpaperImages = wallpaperImages;
        this.configuration = configuration;
        this.confirm = confirm;
        this.translation = translation;
        this.report = report;
        this.conduct = conduct;
        this.encryptionHelp = encryptionHelp;
        this.http = http;
        composeSession = new PhotoComposeSession(library, wallpaperImages);
        router = new ViewRouter<YellowPagesRoute>(YellowPagesRoute.Root);
        drawView = DrawView;
        back = () => router.Pop();
        backToBrowse = () =>
        {
            router.Pop();
            RefreshBrowse();
        };
        stepEveryWeeksBack = StepEveryWeeksBack;
        stepEveryWeeksForward = StepEveryWeeksForward;
        stepFirstWeekBack = StepFirstWeekBack;
        stepFirstWeekForward = StepFirstWeekForward;
        threadView = new ThreadView(this);
        scopeScreen = new GeoScopeScreen(Ink, ui, ScreenTitleStyle);
        MigrateScope();
    }

    public void OnOpened()
    {
        router.Reset();
        activeTab = YellowPagesTab.Browse;
        socialNotifications.MarkSeen(Id);
        lifestreamAvailable = LifestreamBridge.IsAvailable();
        if (launcher.TryConsumeInquiry(out var inquiryId))
        {
            activeTab = YellowPagesTab.Inbox;
            OpenThread(inquiryId, false);
        }
        else if (launcher.TryConsumeDetail(out var adId))
        {
            ResetDetailState();
            router.Push(YellowPagesRoute.Detail(adId), false);
        }

        store.SyncNow();
        RefreshBrowse();
        inquiries.RefreshThreads();
    }

    public void OnClosed()
    {
        router.Reset();
        ResetDetailState();
        ResetComposeForm();
        optionsMenu.Close();
        adSheet.Close();
        inboxSheet.Close();
        threadView.OnAppClosed();
        copiedTimer = 0f;
        inboxFilterDirty = true;
    }

    public void Draw(in PhoneContext context)
    {
        store.NoteWatched();
        inquiries.NoteInboxWatched();
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        var scale = UiScale.Current;
        var screen = SceneChrome.ScreenFrom(context.Content, theme, scale);
        screenRect = screen;
        ui.Backdrop(screen);
        if (!store.IsSignedIn)
        {
            TourHolds.Hold(Id);
            DrawEmptyState(context.Content, DisplayName, Loc.T(L.YellowPages.SetUpAccount));
            return;
        }

        TourHolds.Release(Id);
        if (copiedTimer > 0f)
        {
            copiedTimer -= ImGui.GetIO().DeltaTime;
        }

        if (photoViewer.Active)
        {
            photoViewer.Draw(screen, theme);
            return;
        }

        threadView.GateMenus();
        optionsMenu.Gate();
        adSheet.Gate();
        inboxSheet.Gate();
        var appArea = SceneChrome.AppAreaFrom(context.Content, theme, scale);
        router.Draw(appArea, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        DrawOptionsMenu(screen);
        DrawAdSheet(screen);
        DrawInboxSheet(screen);
    }

    private void DrawView(YellowPagesRoute route, Rect area, int depth)
    {
        ui.Body(area);
        switch (route.Screen)
        {
            case YellowPagesScreen.Category:
                DrawCategory(area, route.Intent);
                break;
            case YellowPagesScreen.Detail:
                DrawDetail(area, route.Id!);
                break;
            case YellowPagesScreen.Compose:
                DrawCompose(area);
                break;
            case YellowPagesScreen.Thread:
                threadView.Draw(ChatArea(area), route.Id!, depth == router.Depth);
                break;
            case YellowPagesScreen.NewInquiry:
                DrawNewInquiry(area, route.Id!);
                break;
            case YellowPagesScreen.ChatImage:
                threadView.DrawImagePicker(ChatArea(area), route.Id!);
                break;
            case YellowPagesScreen.ImageView:
                threadView.DrawImageViewer(ChatArea(area), route.Id!);
                break;
            case YellowPagesScreen.Reactions:
                threadView.DrawReactions(ChatArea(area), route.Id!);
                break;
            case YellowPagesScreen.Encryption:
                threadView.DrawEncryptionScreen(ChatArea(area));
                break;
            case YellowPagesScreen.InboxArchived:
                DrawInboxArchived(area);
                break;
            case YellowPagesScreen.Scope:
                DrawScopeScreen(area);
                break;
            default:
                DrawRoot(area);
                break;
        }
    }

    private Rect ChatArea(Rect area)
    {
        var sidePadding = theme.SidePadding * UiScale.Current;
        return new Rect(new Vector2(area.Min.X + sidePadding, area.Min.Y),
            new Vector2(area.Max.X - sidePadding, area.Max.Y));
    }

    private void DrawRoot(Rect area)
    {
        var scale = UiScale.Current;
        using (TabBar.ReserveContent(scale))
        {
            switch (activeTab)
            {
                case YellowPagesTab.Saved:
                    DrawSaved(area);
                    break;
                case YellowPagesTab.Inbox:
                    DrawInbox(area);
                    break;
                case YellowPagesTab.Mine:
                    DrawMine(area);
                    break;
                default:
                    DrawBrowse(area);
                    break;
            }
        }

        DrawTabBar(area);
    }

    private void DrawTabBar(Rect area)
    {
        tabItems[(int)YellowPagesTab.Browse] = new TabItem(Loc.T(L.YellowPages.BrowseTab), PhoneIcons.Compass);
        tabItems[(int)YellowPagesTab.Saved] = new TabItem(Loc.T(L.YellowPages.SavedTab), PhoneIcons.Bookmark,
            PhoneIcons.BookmarkFilled);
        tabItems[(int)YellowPagesTab.Inbox] = new TabItem(Loc.T(L.YellowPages.InboxTab), PhoneIcons.MessageCircle,
            PhoneIcons.MessageCircleFilled, inquiries.UnreadCount, "yellowpages.tab.inquiries");
        tabItems[(int)YellowPagesTab.Mine] = new TabItem(Loc.T(L.YellowPages.MineTab), PhoneIcons.LayoutList);
        var post = new TabBarAction(PhoneIcons.Plus, Loc.T(L.YellowPages.PostAd), AnchorKey: "yellowpages.tab.post");
        var result = tabBar.Draw(area, ui, tabItems, (int)activeTab, post);
        if (result.ActionTapped)
        {
            StartCompose();
            return;
        }

        if (result.Tapped < 0)
        {
            return;
        }

        SelectTab((YellowPagesTab)result.Tapped);
    }

    private void SelectTab(YellowPagesTab tab)
    {
        if (activeTab == tab)
        {
            return;
        }

        activeTab = tab;
        rootScrollTopPending = true;
        switch (tab)
        {
            case YellowPagesTab.Saved:
                store.RefreshSaved();
                break;
            case YellowPagesTab.Inbox:
                inquiries.RefreshThreads();
                break;
            case YellowPagesTab.Mine:
                store.SyncNow();
                inquiries.RefreshThreads();
                break;
            default:
                RefreshBrowse();
                break;
        }
    }

    private void DrawScopeScreen(Rect area)
    {
        var choice = scopeScreen.Draw(area, Loc.T(L.YellowPages.ScopeTitle), back, HomeWorldName(),
            configuration.YellowPagesScopeKind, configuration.YellowPagesScopeValue, false);
        if (!choice.Picked)
        {
            return;
        }

        router.Pop();
        if (choice.Kind == configuration.YellowPagesScopeKind &&
            string.Equals(choice.Value, configuration.YellowPagesScopeValue, StringComparison.Ordinal))
        {
            return;
        }

        configuration.YellowPagesScopeKind = choice.Kind;
        configuration.YellowPagesScopeValue = choice.Value;
        configuration.Save();
        RefreshCurrentList();
    }

    private void MigrateScope()
    {
        if (configuration.YellowPagesScope == AdScopes.Region)
        {
            return;
        }

        configuration.YellowPagesScopeKind = configuration.YellowPagesScope == AdScopes.DataCenter
            ? GeoScopeKind.MyDataCenter
            : GeoScopeKind.Everywhere;
        configuration.YellowPagesScope = AdScopes.Region;
        configuration.Save();
    }

    private string HomeWorldName() => gameData.WorldName(gameData.LocalCurrentWorldId);

    private void DrawOptionsMenu(Rect screen)
    {
        var sort = configuration.YellowPagesSort;
        optionItems[0] = new DropdownMenu.Item(Loc.T(L.YellowPages.SortNewest), Selected: sort == AdSorts.Newest);
        optionItems[1] = new DropdownMenu.Item(Loc.T(L.YellowPages.SortOpenFirst), Selected: sort == AdSorts.OpenFirst);
        optionItems[2] = new DropdownMenu.Item(Loc.T(L.YellowPages.SortEndingSoon), Selected: sort == AdSorts.EndingSoon);
        optionItems[3] = new DropdownMenu.Item(Loc.T(L.YellowPages.LayoutCompact),
            Selected: configuration.YellowPagesCompactCards);
        optionItems[4] = new DropdownMenu.Item(Loc.T(L.YellowPages.AfterDarkConfirmYes),
            Selected: configuration.YellowPagesAfterDark);
        optionItems[5] = new DropdownMenu.Item(Loc.T(L.Conduct.Eyebrow));
        var picked = optionsMenu.Draw(screen, theme, optionItems);
        switch (picked)
        {
            case 0:
            case 1:
            case 2:
                if (configuration.YellowPagesSort != picked)
                {
                    configuration.YellowPagesSort = picked;
                    configuration.Save();
                    lastDirectory = Array.Empty<Core.Aethernet.Contracts.AdDto>();
                }

                break;
            case 3:
                configuration.YellowPagesCompactCards = !configuration.YellowPagesCompactCards;
                configuration.Save();
                break;
            case 4:
                ToggleAfterDark();
                break;
            case 5:
                conduct.ShowRules(Id);
                break;
        }
    }

    private void RefreshCurrentList()
    {
        rootScrollTopPending = true;
        var route = router.Current;
        if (route.Screen == YellowPagesScreen.Category)
        {
            RefreshIntent(route.Intent);
            return;
        }

        RefreshBrowse();
    }

    private void OpenDetail(string adId)
    {
        ResetDetailState();
        router.Push(YellowPagesRoute.Detail(adId));
    }

    private float DrawScreenHeader(Rect area, string title, int trailingSlots = 0, bool showBack = true,
        bool centered = true, string subtitle = "") =>
        SocialChrome.DrawScreenHeader(area, title, Ink, back, ScreenTitleStyle,
            SocialChrome.HeaderReserve(trailingSlots), subtitle, showBack, centered);

    private static bool DrawHeaderIcon(ImDrawListPtr drawList, Vector2 center, string glyph, string tooltip,
        bool highlighted = false, int badge = 0, float iconSize = HeaderIconSize) =>
        SocialChrome.DrawHeaderIcon(drawList, center, SocialChrome.HeaderIconRadius * UiScale.Current, glyph,
            iconSize, tooltip, Ink, Ink.MutedInk, highlighted, badge);

    private static void DrawHairline(ImDrawListPtr drawList, float left, float right, float y) =>
        FeedCell.Hairline(drawList, left, right, y, Ink.Hairline);

    private void PaintBarBackdrop(ImDrawListPtr drawList, Rect bar) =>
        SocialChrome.PaintBarBackdrop(ui, drawList, bar, screenRect);

    private static void DrawEmptyState(Rect area, string title, string body)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var maxWidth = MathF.Max(1f, area.Width - CellPadX * 2f * scale);
        var top = area.Min.Y + EmptyStateTop * scale;
        var titleBottom = Typography.DrawWrappedCentered(drawList, title, EmptyTitleStyle, Ink.TitleInk,
            new Vector2(area.Center.X, top), maxWidth);
        if (body.Length == 0)
        {
            return;
        }

        Typography.DrawWrappedCentered(drawList, body, EmptyBodyStyle, Ink.MutedInk,
            new Vector2(area.Center.X, titleBottom + EmptyBodyGap * scale), maxWidth);
    }

    private void DrawSectionLabel(string label, float topPad = 0f)
    {
        var scale = UiScale.Current;
        if (topPad > 0f)
        {
            ImGui.Dummy(new Vector2(0f, topPad * scale));
        }

        SocialChrome.DrawSectionLabel(label, Ink, TextStyles.FootnoteEmphasized);
    }

    private void Copy(string key, string text)
    {
        ImGui.SetClipboardText(text);
        copiedKey = key;
        copiedTimer = CopiedSeconds;
        ShellToast.Show();
    }

    private bool JustCopied(string key) =>
        copiedTimer > 0f && string.Equals(copiedKey, key, StringComparison.Ordinal);

    private void SubmitReport(string adId, string? reason, Action<bool> done)
    {
        _ = Task.Run(async () =>
        {
            var ok = false;
            try
            {
                ok = await api.Safety.ReportAsync("ad", adId, reason, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "[YellowPages] report failed");
            }

            done(ok);
        });
    }

    private static long NowUnix() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static int TrimmedLength(string value)
    {
        var start = 0;
        var end = value.Length - 1;
        while (start <= end && char.IsWhiteSpace(value[start]))
        {
            start++;
        }

        while (end >= start && char.IsWhiteSpace(value[end]))
        {
            end--;
        }

        return end - start + 1;
    }

    public void Dispose()
    {
        threadView.Dispose();
        composeSession.Dispose();
    }
}
