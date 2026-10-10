using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Conduct;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Coin;

internal sealed partial class CoinApp : IPhoneApp, ITabRouteTarget
{
    private const string WalletRoute = "coin.tab.wallet";
    private const string ShopRoute = "coin.tab.shop";
    private const string ItemsRoute = "coin.tab.items";
    private const string HistoryRoute = "coin.tab.history";
    private const string SettingsAppId = "settings";
    private const int TabCount = 4;

    private enum CoinTab : byte
    {
        Wallet,
        Shop,
        Items,
        History,
    }

    public string Id => "coin";
    public string DisplayName => Loc.T(L.Apps.Coin);
    public string Glyph => "Ac";
    public int BadgeCount => 0;

    private readonly AethernetSession session;
    private readonly CoinStore store;
    private readonly CoinQuestStore quests;
    private readonly CoinCatalogStore catalog;
    private readonly ConfirmService confirm;
    private readonly ConductGateService conduct;
    private readonly Core.Social.BadgeCatalogStore badgeCatalog;
    private readonly Core.Social.FrameCatalogStore frameCatalog;
    private readonly Core.Social.LoadoutStore inventory;
    private readonly Core.Lodestone.LodestoneService lodestone;
    private readonly Core.Media.RemoteImageCache images;
    private readonly Core.Casino.CasinoStore casino;
    private readonly Configuration configuration;
    private readonly AppSkin ui = new(AppPalettes.Coin);
    private readonly ViewRouter<CoinRoute> router;
    private readonly RouterDraw<CoinRoute> drawView;
    private readonly Action back;
    private readonly TabBar tabBar = new();
    private readonly TabItem[] tabItems = new TabItem[TabCount];
    private readonly NavBarButton[] navButtons = new NavBarButton[2];
    private readonly string[] filterOptions = new string[3];
    private readonly PullToRefresh walletRefresh = new();
    private readonly PullToRefresh historyRefresh = new();
    private readonly PullToRefresh shopRefresh = new();
    private readonly PullToRefresh browseRefresh = new();
    private readonly PullToRefresh inventoryRefresh = new();
    private readonly CoinFloat floats = new();
    private readonly CoinLedgerText ledgerText = new();
    private readonly CoinStreakCard streakCard = new();
    private readonly CoinTreatCard treatCard = new();
    private readonly CoinTextCache texts = new();

    private PendingTab pendingTab;
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private CoinTab activeTab;
    private int historyFilter;

    public CoinApp(AethernetSession session, CoinStore store, CoinCatalogStore catalog, ConfirmService confirm,
        ConductGateService conduct, Core.Social.BadgeCatalogStore badgeCatalog, Core.Media.RemoteImageCache images,
        Core.Casino.CasinoStore casino, Core.Social.FrameCatalogStore frameCatalog,
        Core.Social.LoadoutStore inventory, Core.Lodestone.LodestoneService lodestone, Configuration configuration,
        CoinQuestStore quests)
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
        this.configuration = configuration;
        this.quests = quests;
        router = new ViewRouter<CoinRoute>(CoinRoute.Root);
        drawView = DrawView;
        back = () => router.Pop();
    }

    public void OpenTab(string tab) => pendingTab.Request(tab);

    public void OnOpened()
    {
        router.Reset();
        activeTab = CoinTab.Wallet;
        historyFilter = CoinLedgerText.FilterAll;
        PrimeWallet();
        store.RefreshNow();
        quests.Watch();
    }

    public void OnClosed()
    {
        router.Reset();
        quests.Unwatch();
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        var scale = UiScale.Current;
        ui.Backdrop(SceneChrome.ScreenFrom(context.Content, theme, scale));
        if (!session.IsSignedIn)
        {
            TourHolds.Hold(Id);
            DrawSignedOut(context);
            return;
        }

        TourHolds.Release(Id);
        store.EnsureFresh();
        ConsumePurchaseResult();
        ConsumePendingTab();
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawSignedOut(in PhoneContext context)
    {
        ui.Body(context.Content);
        var navBar = AppHeader.BeginLargeTitle(context, false);
        if (CoinArt.StateScreen(ImGui.GetWindowDrawList(), ui, navBar.Body, FontAwesomeIcon.UserLock,
                Loc.T(L.Coin.SignInTitle), Loc.T(L.Coin.SignInHint), Loc.T(L.Coin.OpenSettings), ImGui.GetID("coin.signin"),
                UiScale.Current)
            && navigation.IsAvailable(SettingsAppId))
        {
            UiFeedback.Play(UiSound.Tap);
            navigation.Open(SettingsAppId);
        }

        AppHeader.EndLargeTitle(in navBar, context, "coin.signedout.nav", DisplayName, NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty);
    }

    private void ConsumePendingTab()
    {
        if (pendingTab.Take(ShopRoute))
        {
            OpenRootTab(CoinTab.Shop);
        }
        else if (pendingTab.Take(HistoryRoute))
        {
            OpenRootTab(CoinTab.History);
        }
        else if (pendingTab.Take(ItemsRoute))
        {
            OpenRootTab(CoinTab.Items);
        }
        else if (pendingTab.Take(WalletRoute))
        {
            OpenRootTab(CoinTab.Wallet);
        }
    }

    private void OpenRootTab(CoinTab tab)
    {
        router.Reset();
        SelectTab(tab);
    }

    private void DrawView(CoinRoute route, Rect area, int depth)
    {
        ui.Body(area);
        var context = new PhoneContext(area, theme, navigation);
        switch (route.Screen)
        {
            case CoinScreen.ShopFolder:
            case CoinScreen.ShopShelf:
                DrawShopBrowse(context, route, depth);
                return;
            case CoinScreen.Product:
                DrawProduct(context, route, depth);
                return;
            case CoinScreen.Entry:
                DrawEntry(context, route, depth);
                return;
            default:
                DrawRoot(context, area);
                return;
        }
    }

    private void DrawRoot(in PhoneContext context, Rect area)
    {
        var scale = UiScale.Current;
        using (TabBar.ReserveContent(scale))
        {
            switch (activeTab)
            {
                case CoinTab.Shop:
                    DrawShop(context);
                    break;
                case CoinTab.Items:
                    DrawInventory(context);
                    break;
                case CoinTab.History:
                    DrawHistory(context);
                    break;
                default:
                    DrawWallet(context);
                    break;
            }
        }

        DrawTabBar(area);
        floats.Draw(ImGui.GetWindowDrawList(), ui.Palette.Accent, ui.MutedInk, ImGui.GetIO().DeltaTime);
    }

    private void DrawTabBar(Rect area)
    {
        tabItems[(int)CoinTab.Wallet] = new TabItem(Loc.T(L.Coin.TabWallet), IconGlyph.Of(FontAwesomeIcon.Coins),
            AnchorKey: WalletRoute);
        tabItems[(int)CoinTab.Shop] = new TabItem(Loc.T(L.Coin.TabShop), IconGlyph.Of(FontAwesomeIcon.ShoppingBag),
            AnchorKey: ShopRoute);
        tabItems[(int)CoinTab.Items] = new TabItem(Loc.T(L.Coin.TabInventory), IconGlyph.Of(FontAwesomeIcon.Gem),
            AnchorKey: ItemsRoute);
        tabItems[(int)CoinTab.History] = new TabItem(Loc.T(L.Coin.TabHistory),
            IconGlyph.Of(FontAwesomeIcon.Receipt), AnchorKey: HistoryRoute);
        var result = tabBar.Draw(area, ui, tabItems, (int)activeTab);
        if (result.Tapped < 0 || result.Tapped == (int)activeTab)
        {
            return;
        }

        SelectTab((CoinTab)result.Tapped);
    }

    private void SelectTab(CoinTab tab)
    {
        if (tab == activeTab)
        {
            return;
        }

        activeTab = tab;
        if (tab == CoinTab.Shop)
        {
            catalog.RefreshOnEnter();
            badgeCatalog.EnsureFresh();
            frameCatalog.EnsureFresh();
            return;
        }

        if (tab == CoinTab.Items)
        {
            inventory.RefreshOnEnter();
            badgeCatalog.EnsureFresh();
            frameCatalog.EnsureFresh();
        }
    }

    private string TabTitle(CoinTab tab) => tab switch
    {
        CoinTab.Shop => Loc.T(L.Coin.TabShop),
        CoinTab.Items => Loc.T(L.Coin.TabInventory),
        CoinTab.History => Loc.T(L.Coin.TabHistory),
        _ => DisplayName,
    };

    private string BackTitle(int depth)
    {
        if (!router.TryGetView(depth - 2, out var previous))
        {
            return TabTitle(activeTab);
        }

        return previous.Screen switch
        {
            CoinScreen.ShopFolder or CoinScreen.ShopShelf => CategoryTitle(catalog.Category(previous.CategoryId)),
            CoinScreen.Product => FindSku(previous.CategoryId, previous.ItemId)?.Name ?? Loc.T(L.Coin.TabShop),
            CoinScreen.Entry => Loc.T(L.Coin.TabHistory),
            _ => TabTitle(activeTab),
        };
    }

    private int NavButton(int count, string glyph, string tooltip)
    {
        navButtons[count] = new NavBarButton(glyph, tooltip);
        return count + 1;
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
