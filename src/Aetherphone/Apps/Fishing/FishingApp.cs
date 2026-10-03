using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Fishing;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Fishing;

internal sealed partial class FishingApp : IPhoneApp
{
    private const float BottomPad = 24f;
    private const int TabCount = 2;

    public string Id => FishingCatalog.AppId;
    public string DisplayName => Loc.T(L.Apps.Fishing);
    public string Glyph => "F";
    public int BadgeCount => 0;

    private readonly FishingCatalog catalog;
    private readonly FishingAlerts alerts;
    private readonly ITextureProvider textures;
    private readonly OceanVoyageText voyageText;
    private readonly AppSkin ui = new(AppPalettes.Fishing);
    private readonly ViewRouter<FishingRoute> router;
    private readonly RouterDraw<FishingRoute> drawView;
    private readonly Action back;
    private readonly TabBar tabBar = new();
    private readonly TabItem[] tabItems = new TabItem[TabCount];
    private readonly NavBarButton[] navButtons = new NavBarButton[1];
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private FishingTab activeTab;
    private float deltaSeconds;

    public FishingApp(FishingCatalog catalog, FishingAlerts alerts, ITextureProvider textures)
    {
        this.catalog = catalog;
        this.alerts = alerts;
        this.textures = textures;
        voyageText = new OceanVoyageText(catalog);
        router = new ViewRouter<FishingRoute>(FishingRoute.Root);
        drawView = DrawView;
        back = () => router.Pop();
    }

    public void OnOpened()
    {
        router.Reset();
        RefreshVoyages();
        catalog.EnsureLoaded();
        fishListDirty = true;
    }

    public void OnClosed()
    {
        router.Reset();
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var scale = UiScale.Current;
        ui.Backdrop(SceneChrome.ScreenFrom(context.Content, theme, scale));
        TickVoyages();
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        UpdateTourHold();
    }

    private void DrawView(FishingRoute route, Rect area, int depth)
    {
        ui.Body(area);
        switch (route.Screen)
        {
            case FishingScreen.Voyage:
                DrawVoyageDetail(area, route);
                break;
            case FishingScreen.Fish:
                DrawFishDetail(area, route.ItemId);
                break;
            default:
                DrawRoot(area);
                break;
        }
    }

    private void DrawRoot(Rect area)
    {
        var scale = UiScale.Current;
        var context = new PhoneContext(area, theme, navigation);
        using (TabBar.ReserveContent(scale))
        {
            var navBar = AppHeader.BeginLargeTitle(context, false);
            string title;
            if (activeTab == FishingTab.Fish)
            {
                title = Loc.T(L.Fishing.FishTab);
                DrawFishList(navBar.Body, scale);
            }
            else
            {
                title = Loc.T(L.Fishing.OceanTitle);
                DrawVoyages(navBar.Body, scale);
            }

            AppHeader.EndLargeTitle(in navBar, context, "fishing.nav", title, NavBarStyle.From(ui),
                ReadOnlySpan<NavBarButton>.Empty);
        }

        DrawTabBar(area);
    }

    private void DrawTabBar(Rect area)
    {
        tabItems[(int)FishingTab.Voyages] = new TabItem(Loc.T(L.Fishing.VoyagesTab), IconGlyph.Of(FontAwesomeIcon.Anchor),
            AnchorKey: "fishing.tab.voyages");
        tabItems[(int)FishingTab.Fish] = new TabItem(Loc.T(L.Fishing.FishTab), IconGlyph.Of(FontAwesomeIcon.Fish),
            AnchorKey: "fishing.tab.fish");
        var result = tabBar.Draw(area, ui, tabItems, (int)activeTab);
        if (result.Tapped < 0 || result.Tapped == (int)activeTab)
        {
            return;
        }

        activeTab = (FishingTab)result.Tapped;
        UiFeedback.Play(UiSound.Tap);
        if (activeTab == FishingTab.Fish)
        {
            catalog.EnsureLoaded();
            fishListDirty = true;
        }
    }

    private bool BellButton(Rect rect, bool active, string tooltip)
    {
        var glyph = active ? PhoneIcons.BellFilled : PhoneIcons.Bell;
        var ink = active ? ui.Accent : ui.TitleInk;
        var pressed = ui.IconButton(rect.Center, rect.Width * 0.5f, glyph, ink, ui.Palette.FieldSurface,
            1f, tooltip);
        return pressed;
    }

    private void UpdateTourHold()
    {
        if (router.Depth == 1 && activeTab == FishingTab.Voyages)
        {
            TourHolds.Release(Id);
            return;
        }

        TourHolds.Hold(Id);
    }

    private static void BottomSpacer(float scale) => ImGui.Dummy(new Vector2(0f, BottomPad * scale));

    public void Dispose()
    {
    }
}
