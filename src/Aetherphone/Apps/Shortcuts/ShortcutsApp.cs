using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Shortcuts;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Shortcuts;

internal sealed partial class ShortcutsApp : IResumableApp
{
    private const int TabCount = 3;

    public string Id => "shortcuts";
    public string DisplayName => Loc.T(L.Apps.Shortcuts);
    public string Glyph => "S";
    public int BadgeCount => 0;

    private readonly ShortcutStore store;
    private readonly ShortcutRunner runner;
    private readonly PluginCatalog catalog;
    private readonly ConfirmService confirm;
    private readonly ImagePickCrop iconPicker;
    private readonly AppSkin ui = new(AppPalettes.Shortcuts);
    private readonly ViewRouter<ShortcutsView> router;
    private readonly RouterDraw<ShortcutsView> drawView;
    private readonly Action back;
    private readonly TabBar tabBar = new();
    private readonly TabItem[] tabItems = new TabItem[TabCount];

    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private ShortcutsTab activeTab;
    private LanguageInfo? labelLanguage;

    public ShortcutsApp(ShortcutStore store, ShortcutRunner runner, ConfirmService confirm, PhotoLibrary photoLibrary,
        WallpaperImageCache wallpaperImages)
    {
        this.store = store;
        this.runner = runner;
        this.confirm = confirm;
        catalog = store.Catalog;
        iconPicker = new ImagePickCrop(photoLibrary, wallpaperImages);
        router = new ViewRouter<ShortcutsView>(ShortcutsView.Root);
        drawView = DrawView;
        back = GoBack;
        openPluginDetail = OpenPluginDetail;
        pickStepPlugin = PickStepPlugin;
        pickIconPlugin = UsePluginIcon;
        leaveEditor = LeaveEditor;
    }

    public void OnOpened()
    {
        router.Reset();
        ClearDraft();
        activeTab = ShortcutsTab.Library;
        catalog.Invalidate();
        pluginQuery = string.Empty;
        libraryQuery = string.Empty;
        CloseOverlays();
    }

    public void OnResumed() => catalog.Invalidate();

    public void OnClosed() => CloseOverlays();

    private void ClearDraft()
    {
        DiscardUnsavedIcon();
        draft = null;
        draftBaseline = null;
        previewEntry = null;
    }

    private void CloseOverlays()
    {
        actionSheet.CloseImmediately();
        menu.Close();
        menuItems.Clear();
        dragIndex = -1;
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = context.Theme;
        SyncLabelLanguage();
        ConsumeBakedIcon();
        var scale = UiScale.Current;
        var screen = SceneChrome.ScreenFrom(context.Content, context.Theme, scale);
        ui.Backdrop(screen);
        menu.Gate();
        using (InputShield.Engage(actionSheet.CapturesPointer))
        {
            router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        }

        DrawActionSheet(screen);
        DrawMenu(screen);
    }

    private void SyncLabelLanguage()
    {
        if (ReferenceEquals(labelLanguage, Loc.Current))
        {
            return;
        }

        labelLanguage = Loc.Current;
        runStatus.Reset();
        previewSteps.Reset();
        authorLabel.Reset();
        stepLimitLabel.Reset();
        commandCountLabels.Clear();
        for (var index = 0; index < waitLabels.Length; index++)
        {
            waitLabels[index].Reset();
        }
    }

    private void DrawView(ShortcutsView view, Rect area, int depth)
    {
        ui.Body(area);
        var context = new PhoneContext(area, theme, navigation);
        switch (view.Route)
        {
            case ShortcutsRoute.Editor:
                DrawEditor(context, view);
                return;
            case ShortcutsRoute.Appearance:
                DrawAppearance(context, view);
                return;
            case ShortcutsRoute.Plugin:
                DrawPluginDetail(context, view);
                return;
            case ShortcutsRoute.PluginPicker:
                DrawPluginPicker(context, view);
                return;
            case ShortcutsRoute.Preview:
                DrawPreview(context, view);
                return;
            case ShortcutsRoute.CustomIcon:
                DrawCustomIconPicker(area);
                return;
            default:
                DrawRoot(context);
                return;
        }
    }

    private void DrawRoot(in PhoneContext context)
    {
        var scale = UiScale.Current;
        using (TabBar.ReserveContent(scale))
        using (UiScope.PushId((int)activeTab))
        {
            switch (activeTab)
            {
                case ShortcutsTab.Gallery:
                    DrawGallery(context);
                    break;
                case ShortcutsTab.Plugins:
                    DrawPluginsTab(context);
                    break;
                default:
                    DrawLibrary(context);
                    break;
            }
        }

        DrawTabBar(context.Content);
    }

    private void DrawTabBar(Rect area)
    {
        tabItems[(int)ShortcutsTab.Library] = new TabItem(Loc.T(L.Shortcuts.TabShortcuts), PhoneIcons.LayoutList,
            AnchorKey: "shortcuts.tab.library");
        tabItems[(int)ShortcutsTab.Gallery] = new TabItem(Loc.T(L.Shortcuts.TabGallery), PhoneIcons.Sparkles,
            AnchorKey: "shortcuts.tab.gallery");
        tabItems[(int)ShortcutsTab.Plugins] = new TabItem(Loc.T(L.Shortcuts.TabPlugins), PhoneIcons.SquareRoundedPlus,
            AnchorKey: "shortcuts.tab.plugins");
        var result = tabBar.Draw(area, ui, tabItems, (int)activeTab);
        if (result.Tapped < 0 || result.Tapped == (int)activeTab)
        {
            return;
        }

        activeTab = (ShortcutsTab)result.Tapped;
        menu.Close();
    }

    private string RootTitle() => activeTab switch
    {
        ShortcutsTab.Gallery => Loc.T(L.Shortcuts.TabGallery),
        ShortcutsTab.Plugins => Loc.T(L.Shortcuts.TabPlugins),
        _ => DisplayName,
    };

    private void Push(ShortcutsRoute route, string backTitle)
    {
        menu.Close();
        router.Push(new ShortcutsView(route, backTitle));
    }

    private void GoBack() => router.Pop();

    private static string Seconds(float value) => value.ToString("0.#", Loc.Culture);

    public void Dispose() => DiscardUnsavedIcon();
}
