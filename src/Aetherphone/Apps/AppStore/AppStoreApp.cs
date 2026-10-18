using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Shell.Home;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.AppStore;

internal enum StoreTab : byte
{
    Today,
    Apps,
    Search,
}

internal enum StoreViewKind : byte
{
    Root,
    Category,
    Detail,
    Updates,
}

internal readonly record struct StoreView(StoreViewKind Kind, string AppId, StoreCategory Category, int Serial)
{
    public static StoreView Root() => new(StoreViewKind.Root, string.Empty, StoreCategory.Social, 0);

    public static StoreView ForApp(string appId, int serial) =>
        new(StoreViewKind.Detail, appId, StoreCategory.Social, serial);

    public static StoreView ForCategory(StoreCategory category, int serial) =>
        new(StoreViewKind.Category, string.Empty, category, serial);

    public static StoreView ForUpdates(int serial) => new(StoreViewKind.Updates, string.Empty, StoreCategory.Social, serial);
}

internal sealed partial class AppStoreApp : IResumableApp, ISpotlightStoreApps
{
    private const string StoreAppId = "appstore";
    private const float InstallSeconds = 0.9f;
    private static readonly StoreTab[] TabOrder = { StoreTab.Today, StoreTab.Apps, StoreTab.Search };
    private static readonly IReadOnlyList<IHomeWidget> NoWidgets = Array.Empty<IHomeWidget>();

    private readonly AppInstaller installer;
    private readonly ConfirmService confirm;
    private readonly IReadOnlyList<IPhoneApp> apps;
    private readonly StoreIndex index;
    private readonly StoreText texts = new();
    private readonly AppSkin ui = new(AppPalettes.AppStore);
    private readonly TabBar tabBar = new();
    private readonly TabItem[] tabItems = new TabItem[TabOrder.Length];
    private readonly ViewRouter<StoreView> router;
    private readonly RouterDraw<StoreView> drawView;
    private readonly Action back;
    private readonly Action confirmRemove;
    private readonly Dictionary<string, float> installing = new(StringComparer.Ordinal);
    private readonly List<string> finished = new();
    private readonly Dictionary<string, List<IHomeWidget>> widgetsByApp = new(StringComparer.Ordinal);
    private WidgetRegistry? widgetRegistry;
    private WidgetHost? widgetHost;
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator? navigation;
    private StoreTab tab = StoreTab.Today;
    private string search = string.Empty;
    private string pendingAppId = string.Empty;
    private string removalAppId = string.Empty;
    private bool resetScroll;
    private int viewSerial;
    private bool rowAnchorTaken;

    public AppStoreApp(AppInstaller installer, ConfirmService confirm, IReadOnlyList<IPhoneApp> apps)
    {
        this.installer = installer;
        this.confirm = confirm;
        this.apps = apps;
        index = new StoreIndex(apps, installer);
        router = new ViewRouter<StoreView>(StoreView.Root());
        drawView = DrawView;
        back = () => router.Pop();
        confirmRemove = RemovePending;
    }

    public string Id => StoreAppId;
    public Vector4 Accent => AppAccents.For(Id);
    public string DisplayName => Loc.T(L.Apps.AppStore);
    public string Glyph => "A";
    public int BadgeCount => 0;

    public void AttachWidgets(WidgetRegistry registry, WidgetHost host)
    {
        widgetRegistry = registry;
        widgetHost = host;
        widgetsByApp.Clear();
        var all = registry.All;
        for (var widgetIndex = 0; widgetIndex < all.Count; widgetIndex++)
        {
            var widget = all[widgetIndex];
            if (!widgetsByApp.TryGetValue(widget.AppId, out var list))
            {
                list = new List<IHomeWidget>();
                widgetsByApp[widget.AppId] = list;
            }

            list.Add(widget);
        }
    }

    public void RequestApp(string appId) => pendingAppId = appId;

    public void RequestStoreApp(string appId) => RequestApp(appId);

    public void OnOpened()
    {
        router.Reset();
        tab = StoreTab.Today;
        search = string.Empty;
        resetScroll = true;
        index.Invalidate();
    }

    public void OnResumed() => index.Invalidate();

    public void OnClosed() => FinishInstalls();

    public void Dispose() => index.Dispose();

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        rowAnchorTaken = false;
        index.Refresh();
        if (pendingAppId.Length > 0)
        {
            tab = StoreTab.Apps;
            router.Reset();
            router.Push(StoreView.ForApp(pendingAppId, NextSerial()), false);
            pendingAppId = string.Empty;
        }

        var delta = ImGui.GetIO().DeltaTime;
        AdvanceInstalls(delta);
        var scale = UiScale.Current;
        ui.Backdrop(SceneChrome.ScreenFrom(context.Content, theme, scale));
        using (TabBar.ReserveContent(scale))
        {
            router.Draw(context.Content, AppSkin.Transparent, delta, drawView);
        }

        DrawTabBar(context.Content);
    }

    private void DrawView(StoreView view, Rect area, int depth)
    {
        ui.Body(area);
        var viewContext = new PhoneContext(area, theme, navigation!);
        switch (view.Kind)
        {
            case StoreViewKind.Detail:
                DrawDetail(viewContext, view, depth);
                return;
            case StoreViewKind.Category:
                DrawCategoryView(viewContext, view, depth);
                return;
            case StoreViewKind.Updates:
                DrawUpdatesView(viewContext, view, depth);
                return;
        }

        switch (tab)
        {
            case StoreTab.Apps:
                DrawAppsTab(viewContext);
                break;
            case StoreTab.Search:
                DrawSearchTab(viewContext);
                break;
            default:
                DrawTodayTab(viewContext);
                break;
        }
    }

    private void DrawTabBar(Rect area)
    {
        for (var tabIndex = 0; tabIndex < TabOrder.Length; tabIndex++)
        {
            var value = TabOrder[tabIndex];
            tabItems[tabIndex] = new TabItem(Loc.T(TabLabel(value)), IconGlyph.Of(TabIcon(value)),
                AnchorKey: TabAnchor(value));
        }

        var result = tabBar.Draw(area, ui, tabItems, (int)tab);
        if (result.Tapped < 0)
        {
            return;
        }

        var tapped = TabOrder[result.Tapped];
        if (tapped == tab && router.Depth <= 1)
        {
            resetScroll = true;
            return;
        }

        tab = tapped;
        router.Reset();
    }

    private static string TabAnchor(StoreTab value) => value switch
    {
        StoreTab.Apps => "appstore.tab.apps",
        StoreTab.Search => "appstore.tab.search",
        _ => "appstore.tab.today",
    };

    private static FontAwesomeIcon TabIcon(StoreTab value) => value switch
    {
        StoreTab.Apps => FontAwesomeIcon.LayerGroup,
        StoreTab.Search => FontAwesomeIcon.Search,
        _ => FontAwesomeIcon.Newspaper,
    };

    private static LocString TabLabel(StoreTab value) => value switch
    {
        StoreTab.Apps => L.Store.Apps,
        StoreTab.Search => L.Store.Search,
        _ => L.Store.Today,
    };

    private string BackTitle(int depth)
    {
        if (!router.TryGetView(depth - 2, out var previous))
        {
            return Loc.T(TabLabel(tab));
        }

        return previous.Kind switch
        {
            StoreViewKind.Category => Loc.T(AppStoreCatalog.Name(previous.Category)),
            StoreViewKind.Updates => texts.UpdatedIn(index.LatestVersion),
            StoreViewKind.Detail => Find(previous.AppId)?.DisplayName ?? Loc.T(TabLabel(tab)),
            _ => Loc.T(TabLabel(tab)),
        };
    }

    private int NextSerial() => ++viewSerial;

    private void OpenDetail(string appId) => router.Push(StoreView.ForApp(appId, NextSerial()));

    private void OpenCategory(StoreCategory category) => router.Push(StoreView.ForCategory(category, NextSerial()));

    private void TakeScrollReset(in AppSurface.SurfaceScope surface)
    {
        if (!resetScroll)
        {
            return;
        }

        surface.JumpToTop();
        resetScroll = false;
    }

    private IReadOnlyList<IHomeWidget> WidgetsFor(string appId) =>
        widgetsByApp.TryGetValue(appId, out var list) ? list : NoWidgets;

    private IHomeWidget? FirstLiveWidget(string appId)
    {
        if (widgetRegistry is null || widgetHost is null)
        {
            return null;
        }

        var widgets = WidgetsFor(appId);
        for (var widgetIndex = 0; widgetIndex < widgets.Count; widgetIndex++)
        {
            if (widgetRegistry.IsAvailable(widgets[widgetIndex]))
            {
                return widgets[widgetIndex];
            }
        }

        return null;
    }

    private void AdvanceInstalls(float delta)
    {
        if (installing.Count == 0)
        {
            return;
        }

        finished.Clear();
        foreach (var pair in installing)
        {
            var next = pair.Value + delta / InstallSeconds;
            if (next >= 1f)
            {
                finished.Add(pair.Key);
                continue;
            }

            installing[pair.Key] = next;
        }

        for (var finishedIndex = 0; finishedIndex < finished.Count; finishedIndex++)
        {
            installing.Remove(finished[finishedIndex]);
            if (installer.Install(finished[finishedIndex]))
            {
                UiFeedback.Play(UiSound.Success);
            }
        }
    }

    private void FinishInstalls()
    {
        if (installing.Count == 0)
        {
            return;
        }

        finished.Clear();
        foreach (var pair in installing)
        {
            finished.Add(pair.Key);
        }

        installing.Clear();
        for (var finishedIndex = 0; finishedIndex < finished.Count; finishedIndex++)
        {
            installer.Install(finished[finishedIndex]);
        }
    }

    private void BeginInstall(string appId)
    {
        if (installer.IsInstalled(appId) || installing.ContainsKey(appId))
        {
            return;
        }

        installing[appId] = 0f;
    }

    private void AskRemove(IPhoneApp app)
    {
        if (!AppInstaller.CanUninstall(app.Id) || !installer.IsInstalled(app.Id))
        {
            return;
        }

        removalAppId = app.Id;
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Home.RemoveConfirm, app.DisplayName),
            ConfirmLabel = Loc.T(L.Home.Remove),
            CancelLabel = Loc.T(L.Common.Cancel),
            Sheet = true,
            Confirm = confirmRemove,
        });
    }

    private void RemovePending()
    {
        if (removalAppId.Length == 0)
        {
            return;
        }

        installer.Uninstall(removalAppId);
        removalAppId = string.Empty;
    }

    private IPhoneApp? Find(string appId)
    {
        for (var appIndex = 0; appIndex < apps.Count; appIndex++)
        {
            if (string.Equals(apps[appIndex].Id, appId, StringComparison.Ordinal))
            {
                return apps[appIndex];
            }
        }

        return null;
    }

    private void OpenApp(string appId)
    {
        if (string.Equals(appId, StoreAppId, StringComparison.Ordinal))
        {
            return;
        }

        navigation?.Open(appId);
    }
}
