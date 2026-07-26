using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Conduct;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Report;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Core.YellowPages;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.YellowPages;

internal sealed partial class YellowPagesApp : IPhoneApp
{
    private const float CopiedSeconds = 1.6f;

    public string Id => "yellowpages";
    public string DisplayName => Loc.T(L.Apps.YellowPages);
    public string Glyph => "Yp";
    public int BadgeCount => socialNotifications.UnseenCount(Id);

    private readonly YellowPagesStore store;
    private readonly AdInquiryStore inquiries;
    private readonly YellowPagesLauncher launcher;
    private readonly SocialNotificationService socialNotifications;
    private readonly GramDmLauncher gramDmLauncher;
    private readonly MusterStore musters;
    private readonly AethernetApi api;
    private readonly GameData gameData;
    private readonly RemoteImageCache images;
    private readonly LodestoneService lodestone;
    private readonly PhotoLibrary library;
    private readonly WallpaperImageCache wallpaperImages;
    private readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private readonly ReportService report;
    private readonly ConductGateService conduct;
    private readonly EncryptionInfoPane encryptionPane;
    private readonly PhotoViewerOverlay photoViewer = new();
    private readonly BottomTabBar bottomNav = new();
    private readonly NavTab[] navTabs = new NavTab[5];
    private readonly DropdownMenu scopeMenu = new();
    private readonly DropdownMenu.Item[] scopeItems = new DropdownMenu.Item[4];
    private readonly AppSkin ui = new(AppPalettes.YellowPages);
    private readonly ViewRouter<YellowPagesRoute> router;
    private readonly RouterDraw<YellowPagesRoute> drawView;
    private readonly Action back;
    private readonly Action backToBrowse;
    private readonly Action backFromThread;
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private YellowPagesTab activeTab = YellowPagesTab.Browse;
    private float copiedTimer;
    private string copiedKey = string.Empty;
    private bool lifestreamAvailable;

    public YellowPagesApp(YellowPagesStore store, AdInquiryStore inquiries, YellowPagesLauncher launcher,
        SocialNotificationService socialNotifications, GramDmLauncher gramDmLauncher, MusterStore musters,
        AethernetApi api, GameData gameData, RemoteImageCache images, LodestoneService lodestone,
        PhotoLibrary library, WallpaperImageCache wallpaperImages, Configuration configuration,
        ConfirmService confirm, ReportService report, ConductGateService conduct)
    {
        this.store = store;
        this.inquiries = inquiries;
        this.launcher = launcher;
        this.socialNotifications = socialNotifications;
        this.gramDmLauncher = gramDmLauncher;
        this.musters = musters;
        this.api = api;
        this.gameData = gameData;
        this.images = images;
        this.lodestone = lodestone;
        this.library = library;
        this.wallpaperImages = wallpaperImages;
        this.configuration = configuration;
        this.confirm = confirm;
        this.report = report;
        this.conduct = conduct;
        encryptionPane = new EncryptionInfoPane(inquiries.Vault, confirm);
        router = new ViewRouter<YellowPagesRoute>(YellowPagesRoute.Browse);
        drawView = DrawView;
        back = () => router.Pop();
        backFromThread = () =>
        {
            router.Pop();
            inquiries.Close();
            inquiries.Refresh();
        };
        backToBrowse = () =>
        {
            router.Pop();
            RefreshBrowse();
        };
    }

    public void OnOpened()
    {
        router.Reset();
        activeTab = YellowPagesTab.Browse;
        socialNotifications.MarkSeen(Id);
        lifestreamAvailable = LifestreamBridge.IsAvailable();
        if (launcher.TryConsumeDetail(out var adId))
        {
            ResetDetailState();
            router.Push(YellowPagesRoute.Detail(adId), false);
        }

        store.SyncNow();
        RefreshBrowse();
    }

    public void OnClosed()
    {
        router.Reset();
        ResetDetailState();
        ResetComposeForm();
        scopeMenu.Close();
        copiedTimer = 0f;
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        var scale = ImGuiHelpers.GlobalScale;
        var screen = SceneChrome.ScreenFrom(context.Content, theme, scale);
        ui.Backdrop(screen);
        if (!store.IsSignedIn)
        {
            var rowCenterY = context.Content.Min.Y + AppHeader.Height * scale * 0.5f;
            Typography.DrawCentered(new Vector2(context.Content.Center.X, rowCenterY), DisplayName,
                AppPalettes.YellowPages.TitleInk, 1.3f, FontWeight.Bold);
            var body = new Rect(new Vector2(context.Content.Min.X, context.Content.Min.Y + AppHeader.Height * scale),
                context.Content.Max);
            Typography.DrawCentered(body.Center, Loc.T(L.YellowPages.SetUpAccount),
                AppPalettes.YellowPages.MutedInk);
            return;
        }

        if (copiedTimer > 0f)
        {
            copiedTimer -= ImGui.GetIO().DeltaTime;
        }

        var picked = Interlocked.Exchange(ref pendingPickedPath, null);
        if (picked is not null)
        {
            AddComposePhoto(picked);
        }

        if (photoViewer.Active)
        {
            photoViewer.Draw(screen, theme);
            return;
        }

        scopeMenu.Gate();
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        DrawScopeMenu(screen);
    }

    private void DrawView(YellowPagesRoute route, Rect area, int depth)
    {
        ui.Body(area);
        switch (route.Screen)
        {
            case YellowPagesScreen.Intent:
                DrawIntent(area, route.Intent);
                break;
            case YellowPagesScreen.Detail:
                DrawDetail(area, route.AdId!);
                break;
            case YellowPagesScreen.Thread:
                DrawInquiryThread(area, route.AdId!);
                break;
            case YellowPagesScreen.NewInquiry:
                DrawNewInquiry(area, route.AdId!);
                break;
            case YellowPagesScreen.Encryption:
                DrawEncryptionInfo(area);
                break;
            case YellowPagesScreen.Compose:
                DrawCompose(area);
                break;
            default:
                DrawRoot(area);
                break;
        }
    }

    private void DrawRoot(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var navRect = new Rect(new Vector2(area.Min.X, area.Max.Y - BottomTabBar.Height * scale), area.Max);
        var tabArea = new Rect(area.Min, new Vector2(area.Max.X, navRect.Min.Y));
        switch (activeTab)
        {
            case YellowPagesTab.Saved:
                DrawSaved(tabArea);
                break;
            case YellowPagesTab.Inquiries:
                DrawInquiries(tabArea);
                break;
            case YellowPagesTab.Mine:
                DrawMine(tabArea);
                break;
            default:
                DrawBrowse(tabArea);
                break;
        }

        DrawBottomNav(navRect);
    }

    private void DrawBottomNav(Rect bar)
    {
        navTabs[0] = new NavTab(FontAwesomeIcon.ThLarge, Loc.T(L.YellowPages.BrowseTab));
        navTabs[1] = new NavTab(FontAwesomeIcon.Heart, Loc.T(L.YellowPages.SavedTab));
        navTabs[2] = new NavTab(FontAwesomeIcon.Plus, Loc.T(L.YellowPages.PostAd), 0, true);
        navTabs[3] = new NavTab(FontAwesomeIcon.Comments, Loc.T(L.YellowPages.InquiriesTitle),
            inquiries.UnreadTotal);
        navTabs[4] = new NavTab(FontAwesomeIcon.Bullhorn, Loc.T(L.YellowPages.MineTab), BadgeCount);
        var tapped = bottomNav.Draw(bar, ui, theme, navTabs, ActiveNavSlot());
        switch (tapped)
        {
            case 0:
                SelectTab(YellowPagesTab.Browse);
                break;
            case 1:
                SelectTab(YellowPagesTab.Saved);
                break;
            case 2:
                ResetComposeForm();
                router.Push(YellowPagesRoute.Compose);
                break;
            case 3:
                inquiryAdFilter = null;
                SelectTab(YellowPagesTab.Inquiries);
                break;
            case 4:
                SelectTab(YellowPagesTab.Mine);
                break;
        }
    }

    private int ActiveNavSlot() =>
        activeTab switch
        {
            YellowPagesTab.Saved => 1,
            YellowPagesTab.Inquiries => 3,
            YellowPagesTab.Mine => 4,
            _ => 0,
        };

    private void SelectTab(YellowPagesTab tab)
    {
        if (activeTab == tab)
        {
            return;
        }

        activeTab = tab;
        switch (tab)
        {
            case YellowPagesTab.Saved:
                store.RefreshSaved();
                break;
            case YellowPagesTab.Inquiries:
                inquiries.Refresh();
                break;
            case YellowPagesTab.Mine:
                store.SyncNow();
                break;
            default:
                RefreshBrowse();
                break;
        }
    }

    private void DrawScopeMenu(Rect screen)
    {
        var scope = configuration.YellowPagesScope;
        scopeItems[0] = new DropdownMenu.Item(Loc.T(L.YellowPages.ScopeRegion),
            Selected: scope == AdScopes.Region);
        scopeItems[1] = new DropdownMenu.Item(Loc.T(L.YellowPages.ScopeMyDc),
            Selected: scope == AdScopes.DataCenter);
        scopeItems[2] = new DropdownMenu.Item(Loc.T(L.YellowPages.ScopeEverywhere),
            Selected: scope == AdScopes.Everywhere);
        scopeItems[3] = new DropdownMenu.Item(Loc.T(L.YellowPages.AfterDarkConfirmYes),
            Selected: configuration.YellowPagesAfterDark);
        var picked = scopeMenu.Draw(screen, theme, scopeItems);
        if (picked < 0)
        {
            return;
        }

        if (picked == 3)
        {
            ToggleAfterDark();
            return;
        }

        var next = picked switch
        {
            1 => AdScopes.DataCenter,
            2 => AdScopes.Everywhere,
            _ => AdScopes.Region,
        };
        if (next == scope)
        {
            return;
        }

        configuration.YellowPagesScope = next;
        configuration.Save();
        RefreshCurrentList();
    }

    private void RefreshCurrentList()
    {
        var route = router.Current;
        if (route.Screen == YellowPagesScreen.Intent)
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

    private void Copy(string key, string text)
    {
        ImGui.SetClipboardText(text);
        copiedKey = key;
        copiedTimer = CopiedSeconds;
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
                AepLog.Warning($"[YellowPages] report failed: {exception.Message}");
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
        encryptionPane.Dispose();
    }
}
