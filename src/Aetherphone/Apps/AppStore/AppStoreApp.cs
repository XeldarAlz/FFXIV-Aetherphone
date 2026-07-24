using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.AppStore;

internal enum StoreTab
{
    Today,
    Apps,
    Search,
}

internal enum StoreViewKind
{
    Root,
    Category,
    Detail,
}

internal readonly record struct StoreView(StoreViewKind Kind, string AppId, StoreCategory Category)
{
    public static StoreView Root() => new(StoreViewKind.Root, string.Empty, StoreCategory.Social);

    public static StoreView ForApp(string appId) => new(StoreViewKind.Detail, appId, StoreCategory.Social);

    public static StoreView ForCategory(StoreCategory category) =>
        new(StoreViewKind.Category, string.Empty, category);
}

internal sealed partial class AppStoreApp : IPhoneApp
{
    private const float TabBarHeight = 62f;
    private const float HeaderHeight = 82f;
    private const float RowHeight = 68f;
    private const float RowIconSize = 50f;
    private const float SearchHeight = 50f;
    private const float InstallSeconds = 0.9f;
    private static readonly Vector4 GlyphInk = new(1f, 1f, 1f, 0.96f);

    private readonly AppInstaller installer;
    private readonly IReadOnlyList<IPhoneApp> apps;
    private readonly AppSkin ui = new(AppPalettes.AppStore);
    private readonly ViewRouter<StoreView> router;
    private readonly RouterDraw<StoreView> drawView;
    private readonly Dictionary<string, float> installing = new(StringComparer.Ordinal);
    private readonly List<IPhoneApp> scratch = new();
    private readonly List<string> finished = new();
    private INavigator? frameNavigation;
    private StoreTab tab = StoreTab.Today;
    private string search = string.Empty;
    private string lastSearch = string.Empty;
    private bool resetScroll;

    public AppStoreApp(AppInstaller installer, IReadOnlyList<IPhoneApp> apps)
    {
        this.installer = installer;
        this.apps = apps;
        router = new ViewRouter<StoreView>(StoreView.Root());
        drawView = DrawView;
    }

    public string Id => "appstore";
    public Vector4 Accent => AppAccents.For(Id);
    public string DisplayName => Loc.T(L.Apps.AppStore);
    public string Glyph => "A";
    public int BadgeCount => 0;

    public void OnOpened()
    {
        router.Reset();
        tab = StoreTab.Today;
        search = string.Empty;
        lastSearch = string.Empty;
        resetScroll = true;
    }

    public void OnClosed()
    {
        installing.Clear();
        router.Reset();
    }

    public void Dispose()
    {
    }

    public void Draw(in PhoneContext context)
    {
        frameNavigation = context.Navigation;
        ui.Theme = context.Theme;
        var delta = ImGui.GetIO().DeltaTime;
        AdvanceInstalls(delta);
        var scale = ImGuiHelpers.GlobalScale;
        var screen = SceneChrome.ScreenFrom(context.Content, context.Theme, scale);
        ui.Backdrop(screen);
        router.Draw(context.Content, AppSkin.Transparent, delta, drawView);
        var content = context.Content;
        DrawTabBar(new Rect(new Vector2(content.Min.X, content.Max.Y - TabBarHeight * scale), content.Max), scale);
    }

    private void DrawView(StoreView view, Rect area, int depth)
    {
        ui.Body(area);
        var scale = ImGuiHelpers.GlobalScale;
        var body = new Rect(area.Min, new Vector2(area.Max.X, area.Max.Y - TabBarHeight * scale));
        switch (view.Kind)
        {
            case StoreViewKind.Detail:
                DrawDetail(body, view.AppId);
                return;
            case StoreViewKind.Category:
                DrawCategoryView(body, view.Category);
                return;
        }

        switch (tab)
        {
            case StoreTab.Apps:
                DrawCatalogTab(body);
                break;
            case StoreTab.Search:
                DrawSearchTab(body);
                break;
            default:
                DrawTodayTab(body);
                break;
        }
    }

    private void DrawTabBar(Rect area, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(area.Min, new Vector2(area.Max.X, area.Min.Y),
            ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, 0.08f)), 1f);
        Span<StoreTab> order = stackalloc StoreTab[] { StoreTab.Today, StoreTab.Apps, StoreTab.Search };
        var cellWidth = area.Width / order.Length;
        for (var index = 0; index < order.Length; index++)
        {
            var cellMin = new Vector2(area.Min.X + index * cellWidth, area.Min.Y);
            var cellMax = new Vector2(cellMin.X + cellWidth, area.Max.Y);
            var active = order[index] == tab;
            var hovered = UiInteract.Hover(cellMin, cellMax);
            var ink = active ? ui.Accent : hovered ? ui.TitleInk : ui.MutedInk;
            var center = new Vector2((cellMin.X + cellMax.X) * 0.5f, cellMin.Y + 22f * scale);
            AppSkin.Icon(center, TabIcon(order[index]).ToIconString(), ink, active ? 1.02f : 0.94f);
            Typography.DrawCentered(new Vector2(center.X, center.Y + 20f * scale), Loc.T(TabLabel(order[index])), ink,
                TextStyles.Caption1);
            if (!hovered)
            {
                continue;
            }

            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                continue;
            }

            tab = order[index];
            resetScroll = true;
            router.Reset();
        }
    }

    private static FontAwesomeIcon TabIcon(StoreTab value) => value switch
    {
        StoreTab.Apps => FontAwesomeIcon.ThLarge,
        StoreTab.Search => FontAwesomeIcon.Search,
        _ => FontAwesomeIcon.Newspaper,
    };

    private static LocString TabLabel(StoreTab value) => value switch
    {
        StoreTab.Apps => L.Store.Apps,
        StoreTab.Search => L.Store.Search,
        _ => L.Store.Today,
    };

    private void DrawLargeTitle(Rect area, string title, string? eyebrow)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var left = area.Min.X + Metrics.Space.Lg * scale;
        var top = area.Min.Y + 18f * scale;
        if (eyebrow is not null)
        {
            Typography.Draw(new Vector2(left, top), eyebrow, ui.HeaderInk, TextStyles.FootnoteEmphasized);
            top += 20f * scale;
        }

        Typography.Draw(new Vector2(left, top), title, ui.TitleInk, TextStyles.LargeTitle);
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

        for (var index = 0; index < finished.Count; index++)
        {
            installing.Remove(finished[index]);
            installer.Install(finished[index]);
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

    private List<IPhoneApp> Collect(Func<IPhoneApp, bool> predicate)
    {
        scratch.Clear();
        for (var index = 0; index < apps.Count; index++)
        {
            var app = apps[index];
            if (app.IsAvailable && predicate(app))
            {
                scratch.Add(app);
            }
        }

        scratch.Sort((first, second) =>
            string.Compare(first.DisplayName, second.DisplayName, StringComparison.CurrentCultureIgnoreCase));
        return scratch;
    }

    private IPhoneApp? Find(string appId)
    {
        for (var index = 0; index < apps.Count; index++)
        {
            if (string.Equals(apps[index].Id, appId, StringComparison.Ordinal))
            {
                return apps[index];
            }
        }

        return null;
    }

    private void OpenApp(string appId)
    {
        frameNavigation?.Open(appId);
    }
}
