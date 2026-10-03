using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Conduct;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Coin;

internal sealed partial class CoinApp : IPhoneApp, ITabRouteTarget
{
    private PendingTab pendingTab;

    public void OpenTab(string tab) => pendingTab.Request(tab);

    private const int TabWallet = 0;
    private const int TabShop = 1;
    private const int TabInventory = 2;
    private const int TabHistory = 3;

    public string Id => "coin";
    public string DisplayName => Loc.T(L.Apps.Coin);
    public string Glyph => "Ac";
    public int BadgeCount => 0;

    private readonly AethernetSession session;
    private readonly CoinStore store;
    private readonly CoinCatalogStore catalog;
    private readonly ConfirmService confirm;
    private readonly ConductGateService conduct;
    private readonly Core.Social.BadgeCatalogStore badgeCatalog;
    private readonly Core.Social.FrameCatalogStore frameCatalog;
    private readonly Core.Social.LoadoutStore inventory;
    private readonly Core.Lodestone.LodestoneService lodestone;
    private readonly Core.Media.RemoteImageCache images;
    private readonly Core.Casino.CasinoStore casino;
    private readonly AppSkin ui = new(AppPalettes.Coin);
    private readonly ViewRouter<CoinRoute> router;
    private readonly RouterDraw<CoinRoute> drawView;
    private readonly string[] tabOptions = new string[4];
    private readonly string[] filterOptions = new string[3];
    private readonly PullToRefresh walletRefresh = new();
    private readonly PullToRefresh historyRefresh = new();
    private readonly PullToRefresh shopRefresh = new();
    private readonly PullToRefresh browseRefresh = new();
    private readonly PullToRefresh inventoryRefresh = new();
    private readonly CoinFloat floats = new();

    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private int activeTab;
    private int historyFilter;

    public CoinApp(AethernetSession session, CoinStore store, CoinCatalogStore catalog, ConfirmService confirm,
        ConductGateService conduct, Core.Social.BadgeCatalogStore badgeCatalog, Core.Media.RemoteImageCache images,
        Core.Casino.CasinoStore casino, Core.Social.FrameCatalogStore frameCatalog,
        Core.Social.LoadoutStore inventory, Core.Lodestone.LodestoneService lodestone)
    {
        this.session = session;
        this.store = store;
        this.catalog = catalog;
        this.confirm = confirm;
        this.conduct = conduct;
        this.badgeCatalog = badgeCatalog;
        this.frameCatalog = frameCatalog;
        this.inventory = inventory;
        this.lodestone = lodestone;
        this.images = images;
        this.casino = casino;
        router = new ViewRouter<CoinRoute>(CoinRoute.Root);
        drawView = DrawView;
    }

    public void OnOpened()
    {
        router.Reset();
        activeTab = TabWallet;
        historyFilter = CoinLedgerList.FilterAll;
        store.RefreshNow();
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

        var scale = UiScale.Current;
        var screen = SceneChrome.ScreenFrom(context.Content, theme, scale);
        ui.Backdrop(screen);

        if (!session.IsSignedIn)
        {
            TourHolds.Hold(Id);
            ui.Body(context.Content);
            AppHeader.Draw(context, DisplayName, navigation.Back);
            var top = context.Content.Min.Y + AppHeader.Height * scale;
            var body = new Rect(new Vector2(context.Content.Min.X, top), context.Content.Max);
            EmptyState.Draw(body, ui, FontAwesomeIcon.UserLock, Loc.T(L.Coin.SignInTitle),
                Loc.T(L.Coin.SignInHint));
            return;
        }

        TourHolds.Release(Id);
        store.EnsureFresh();
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(CoinRoute route, Rect area, int depth)
    {
        ui.Body(area);
        if (route.Screen != CoinScreen.Root)
        {
            DrawShopBrowse(route, area);
            return;
        }

        if (pendingTab.Take("coin.tab.shop"))
        {
            EnterTab(TabShop);
        }

        var scale = UiScale.Current;
        AppHeader.Draw(new PhoneContext(area, theme, navigation), DisplayName, navigation.Back);
        var helpCenter = new Vector2(area.Max.X - 26f * scale, area.Min.Y + AppHeader.Height * scale * 0.5f);
        if (ui.IconButton(helpCenter, 16f * scale, IconGlyph.Of(FontAwesomeIcon.InfoCircle), ui.MutedInk,
                AppSkin.Transparent, 1.1f, Loc.T(L.Coin.HelpTitle), HoverLabelSide.Below))
        {
            confirm.Alert(Loc.T(L.Coin.HelpTitle), Loc.T(L.Coin.HelpBody), Loc.T(L.Onboarding.GotIt));
        }

        var rulesCenter = new Vector2(area.Max.X - 58f * scale, helpCenter.Y);
        if (ui.IconButton(rulesCenter, 16f * scale, IconGlyph.Of(FontAwesomeIcon.QuestionCircle), ui.MutedInk,
                AppSkin.Transparent, 1.1f, Loc.T(L.Conduct.Eyebrow), HoverLabelSide.Below))
        {
            conduct.ShowRules(ConductRules.Coin.AppId);
        }

        var top = area.Min.Y + AppHeader.Height * scale;
        var inset = 16f * scale;
        var segRow = new Rect(
            new Vector2(area.Min.X + inset, top + 4f * scale),
            new Vector2(area.Max.X - inset, top + 34f * scale));
        var segmentWidth = segRow.Width / tabOptions.Length;
        UiAnchors.Report("coin.tab.shop",
            new Rect(new Vector2(segRow.Min.X + segmentWidth * TabShop, segRow.Min.Y),
                new Vector2(segRow.Min.X + segmentWidth * (TabShop + 1), segRow.Max.Y)));
        tabOptions[TabWallet] = Loc.T(L.Coin.TabWallet);
        tabOptions[TabShop] = Loc.T(L.Coin.TabShop);
        tabOptions[TabInventory] = Loc.T(L.Coin.TabInventory);
        tabOptions[TabHistory] = Loc.T(L.Coin.TabHistory);
        var pickedTab = SegmentStrip.Draw("coin.tabs", segRow, tabOptions, activeTab, ui.Palette);
        if (pickedTab != activeTab)
        {
            EnterTab(pickedTab);
        }

        var body = new Rect(new Vector2(area.Min.X, segRow.Max.Y + 10f * scale), area.Max);
        switch (activeTab)
        {
            case TabShop:
                DrawShop(body);
                break;
            case TabInventory:
                DrawInventory(body);
                break;
            case TabHistory:
                DrawHistory(body);
                break;
            default:
                DrawWallet(body);
                break;
        }

        floats.Draw(ImGui.GetWindowDrawList(), ui.Palette.Accent, ui.MutedInk, ImGui.GetIO().DeltaTime);
    }

    private void EnterTab(int tab)
    {
        activeTab = tab;
        if (tab == TabShop)
        {
            catalog.RefreshOnEnter();
            badgeCatalog.EnsureFresh();
            frameCatalog.EnsureFresh();
            return;
        }

        if (tab == TabInventory)
        {
            inventory.RefreshOnEnter();
            frameCatalog.EnsureFresh();
        }
    }

    private void RefreshShop()
    {
        catalog.RefreshNow();
        badgeCatalog.RefreshNow();
        frameCatalog.RefreshNow();
    }

    private void RefreshInventory()
    {
        inventory.RefreshNow();
        badgeCatalog.RefreshNow();
        frameCatalog.RefreshNow();
    }

    public void Dispose()
    {
    }
}
